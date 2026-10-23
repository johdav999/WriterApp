using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;
using WriterApp.Application.Collaboration;

namespace WriterApp.Application.AI;

public sealed partial class BrainstormingKeeps(AppDbContext db,string owner,string backend,CollaborationAccessService? access=null,ICollaborationCodec? codec=null)
{
    private bool shared;
    private CollaborationBrainstormingWrites Writes=>new(db,owner,access,codec);
    private T Read<T>(string json)=>JsonSerializer.Deserialize<T>(json,BrainstormingValidation.Json)!;
    private async Task<BrainstormingSession> Session(BrainstormingScope scope,CancellationToken ct)
    {
        var s=await new BrainstormingSessions(db,owner,backend,access).ReadAsync(scope.DocumentId,scope.SessionId,ct)??throw new BrainstormingAccessException();
        BrainstormingValidation.RequireScope(s.Scope,scope);if(s.State==BrainstormingConversationState.Deleted)throw new BrainstormingAccessException();return s;
    }
    private async Task<T> Transaction<T>(Guid document,Guid operation,Func<Task<T>> work,CancellationToken ct)=>await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
        db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
        shared=await Writes.Begin(document,operation,ct);
        var result=await work();await db.SaveChangesAsync(ct);if(shared)await Writes.End(document,operation,ct);await tx.CommitAsync(ct);return result;
    });
    private async Task Fresh(BrainstormingKeepReview r,CancellationToken ct)
    {
        var current=await new BrainstormingStorySourceReader(db,owner,access).ReadAsync(r.Scope.DocumentId,r.Scope.ProjectId,ct);
        if(current.Stamp!=r.Target.Source || BrainstormingKeepContract.WritingHash(BrainstormingKeepContract.Writing(current))!=BrainstormingKeepContract.WritingHash(r.Before))
            throw new InvalidOperationException("Source writing, planning, document revision or outline changed. Refresh and review a new proposal.");
    }
    public Task<BrainstormingKeepReceipt> PrepareAsync(BrainstormingKeepReview r,CancellationToken ct=default)=>Transaction(r.Scope.DocumentId,r.OperationId,async()=>{
        BrainstormingKeepContract.Validate(r);var session=await Session(r.Scope,ct);
        var old=await db.BrainstormingKeeps.SingleOrDefaultAsync(k=>k.SessionId==r.Scope.SessionId && (k.OperationId==r.OperationId || k.CandidateId==r.Candidate.Id),ct);
        if(old is not null){if(old.ReviewJson!=BrainstormingPersistence.Json(r))throw new InvalidDataException("Candidate already has an immutable application identity. Inspect its original operation.");return Read<BrainstormingKeepReceipt>(old.ReceiptJson);}
        if(!session.Drafts.Any(d=>BrainstormingPersistence.Json(d)==BrainstormingPersistence.Json(r.Candidate)) || !session.Targets.Any(t=>t==r.Target)
            || session.Requests.Any(q=>q.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming or BrainstormingGenerationState.Unknown))throw new InvalidDataException("Review the complete retained candidate before applying.");
        await Fresh(r,ct);var receipt=new BrainstormingKeepReceipt(1,r.OperationId,r.Scope,r.Candidate.Id,BrainstormingKeepContract.Hash(r),BrainstormingApplicationState.Approved,r.OperationId,0,"Approval and source snapshots saved; manuscript unchanged.",DateTimeOffset.UtcNow);
        db.BrainstormingKeeps.Add(new(){SessionId=r.Scope.SessionId,OperationId=r.OperationId,CandidateId=r.Candidate.Id,ReviewJson=BrainstormingPersistence.Json(r),ReceiptJson=BrainstormingPersistence.Json(receipt)});return receipt;
    },ct);
    public async Task<BrainstormingKeepReceipt?> InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)
    {
        await Session(scope,ct);var row=await db.BrainstormingKeeps.AsNoTracking().SingleOrDefaultAsync(k=>k.SessionId==scope.SessionId && k.OperationId==operation,ct);
        return row is null?null:Read<BrainstormingKeepReceipt>(row.ReceiptJson);
    }
    public Task<BrainstormingKeepReceipt> CommitAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)=>Transaction(scope.DocumentId,operation,async()=>{
        var session=await Session(scope,ct);var row=await db.BrainstormingKeeps.SingleAsync(k=>k.SessionId==scope.SessionId && k.OperationId==operation,ct);
        var receipt=Read<BrainstormingKeepReceipt>(row.ReceiptJson);if(receipt.State is BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone)return receipt;
        var r=Read<BrainstormingKeepReview>(row.ReviewJson);await Fresh(r,ct);var destination=BrainstormingPlacements.Destination(r);
        var webSource=await new WebAiSourceService(db,owner,access).Capture(scope.DocumentId,destination.SectionId,destination.PageId,null,ct);
        if(shared)await ApplyShared(r,operation,false,ct);
        else {
        if(r.Kind==BrainstormingTargetKind.CreateSection)await Create(r,ct);else {
            await RequireUnannotatedPage(r,ct);
            var page=await db.Pages.SingleAsync(p=>p.Id==destination.PageId && p.DocumentId==scope.DocumentId && p.SectionId==destination.SectionId,ct);
            page.Content=r.AfterHtml;page.UpdatedAt=DateTimeOffset.UtcNow;
        }
        (await db.Documents.SingleAsync(d=>d.Id==scope.DocumentId,ct)).UpdatedAt=DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);await WriterApp.Application.Documents.ManuscriptSceneMirror.UpdateAsync(db,destination.SectionId??r.NewSectionId,ct);
        }
        receipt=receipt with {State=BrainstormingApplicationState.Committed,Sequence=1,Message="Confirmed manuscript and structure transaction.",UpdatedAt=DateTimeOffset.UtcNow};row.ReceiptJson=BrainstormingPersistence.Json(receipt);
        db.AiActionHistoryEntries.Add(new(){Id=r.OperationId,OwnerUserId=owner,DocumentId=scope.DocumentId,SectionId=destination.SectionId??r.NewSectionId,PageId=destination.PageId??r.NewPageId,ActionKey="brainstorming.keep",RequestJson=row.ReviewJson,
            ResultJson=JsonSerializer.Serialize(new AiActionExecuteResponseDto(r.OperationId,r.ReplacedText,r.Candidate.PlainText,"Approved manuscript version. Guarded undo/redo and original-copy recovery are available in its brainstorming conversation.",receipt.UpdatedAt,"brainstorming.keep",WebSource:webSource),WebAiHistoryContracts.Json),CreatedAt=receipt.UpdatedAt});
        var intent=new WebAiHistoryIntent(1,r.OperationId,r.OperationId,r.OperationId,1,null,"Applied",webSource,"Aggregate",null,null,r.OperationId);
        db.WebAiHistoryOperations.Add(new(){OwnerUserId=owner,OperationId=r.OperationId,ApplicationId=r.OperationId,ProposalId=r.OperationId,DocumentId=scope.DocumentId,Sequence=1,Outcome="Applied",RequestHash=WebAiHistoryContracts.Hash(intent),IntentJson=JsonSerializer.Serialize(intent,WebAiHistoryContracts.Json),SavedResponseJson=row.ReceiptJson,CommittedAt=receipt.UpdatedAt,ReportedAt=receipt.UpdatedAt});
        await UpdateSession(session,r,receipt,ct);return receipt;
    },ct);
    private async Task Create(BrainstormingKeepReview r,CancellationToken ct)
    {
        var outline=BrainstormingKeepContract.AfterOutline(r);var now=DateTimeOffset.UtcNow;
        foreach(var s in await db.Sections.Where(s=>s.DocumentId==r.Scope.DocumentId).ToArrayAsync(ct))s.OrderIndex=outline.Sections.Single(n=>n.Id==s.Id).Order;
        foreach(var n in await db.ProjectNodes.Where(n=>n.DocumentId==r.Scope.DocumentId && n.SyncDeletionId==null).ToArrayAsync(ct))n.OrderIndex=outline.Nodes.Single(x=>x.Id==n.Id).Order;
        db.Sections.Add(new(){Id=r.NewSectionId,DocumentId=r.Scope.DocumentId,Title=r.SectionTitle,OrderIndex=outline.Sections.Single(s=>s.Id==r.NewSectionId).Order,CreatedAt=now,UpdatedAt=now});
        db.Pages.Add(new(){Id=r.NewPageId,DocumentId=r.Scope.DocumentId,SectionId=r.NewSectionId,Title="Page 1",OrderIndex=0,Content=r.AfterHtml,CreatedAt=now,UpdatedAt=now});
        if(outline.Nodes.SingleOrDefault(n=>n.Id==r.NewNodeId) is { } added)
            db.ProjectNodes.Add(new(){Id=r.NewNodeId,DocumentId=r.Scope.DocumentId,ProjectId=r.Scope.ProjectId!.Value,ParentId=added.ParentId,NodeType=ProjectNodeType.Scene,Title=r.SectionTitle,LinkedSectionId=r.NewSectionId,OrderIndex=added.Order,UpdatedUtc=now});
    }
    private async Task UpdateSession(BrainstormingSession session,BrainstormingKeepReview r,BrainstormingKeepReceipt receipt,CancellationToken ct)
    {
        var row=await db.BrainstormingSessions.SingleAsync(s=>s.Id==session.Scope.SessionId,ct);var now=DateTimeOffset.UtcNow;
        var approval=session.Approvals.SingleOrDefault(a=>a.OperationId==r.OperationId)??new(1,r.OperationId,session.Scope,r.OperationId,r.Candidate.Id,r.Candidate.Revision,BrainstormingValidation.CandidateFingerprint(r.Candidate),r.ReviewedAt);
        await db.SaveChangesAsync(ct);
        var committed=await new BrainstormingStorySourceReader(db,owner,access).ReadAsync(session.Scope.DocumentId,session.Scope.ProjectId,ct);
        var app=new BrainstormingApplication(1,r.OperationId,session.Scope,approval.Id,receipt.State,r.OperationId,r.OperationId,committed.Stamp,receipt.Message,receipt.UpdatedAt);
        var next=session with {Revision=session.Revision+1,UpdatedAt=now,Approvals=session.Approvals.Any(a=>a.Id==approval.Id)?session.Approvals:[..session.Approvals,approval],Applications=[..session.Applications.Where(a=>a.OperationId!=r.OperationId),app]};
        BrainstormingValidation.Validate(next);row.Revision=next.Revision;row.SnapshotJson=BrainstormingPersistence.Json(next);row.UpdatedUnixMilliseconds=now.ToUnixTimeMilliseconds();
    }
    public Task<BrainstormingKeepReceipt> MoveAsync(BrainstormingScope scope,Guid operation,BrainstormingKeepMove move,CancellationToken ct=default)=>Transaction(scope.DocumentId,move.OperationId,async()=>{
        var session=await Session(scope,ct);var row=await db.BrainstormingKeeps.SingleAsync(k=>k.SessionId==scope.SessionId && k.OperationId==operation,ct);
        var receipt=Read<BrainstormingKeepReceipt>(row.ReceiptJson);var moves=Read<List<BrainstormingKeepMove>>(row.MovesJson);
        var prior=moves.SingleOrDefault(m=>m.OperationId==move.OperationId);if(prior is not null){if(prior!=move)throw new InvalidDataException("Undo/redo identity changed.");return receipt;}
        if(move.OperationId==Guid.Empty || moves.Count>=500 || move.ExpectedSequence!=receipt.Sequence || (move.Redo?receipt.State!=BrainstormingApplicationState.Undone:receipt.State!=BrainstormingApplicationState.Committed))throw new InvalidOperationException("Inspect the application before undo/redo.");
        var r=Read<BrainstormingKeepReview>(row.ReviewJson);
        if(shared)await MoveShared(r,move,ct);else await Restore(r,move.Redo,ct);
        receipt=receipt with {State=move.Redo?BrainstormingApplicationState.Committed:BrainstormingApplicationState.Undone,Sequence=receipt.Sequence+1,UpdatedAt=DateTimeOffset.UtcNow,Message=move.Redo?"Confirmed guarded redo.":"Confirmed guarded undo."};
        row.ReceiptJson=BrainstormingPersistence.Json(receipt);row.MovesJson=BrainstormingPersistence.Json(moves.Append(move).ToArray());
        var previous=await db.WebAiHistoryOperations.Where(h=>h.OwnerUserId==owner && h.ApplicationId==operation).OrderByDescending(h=>h.Sequence).FirstAsync(ct);
        var intent=WebAiHistoryOperations.Intent(previous) with {OperationId=move.OperationId,Sequence=(int)receipt.Sequence,PreviousOperationId=previous.OperationId,Outcome=move.Redo?"Redone":"Undone"};
        db.WebAiHistoryOperations.Add(new(){OwnerUserId=owner,OperationId=move.OperationId,ApplicationId=operation,ProposalId=operation,DocumentId=scope.DocumentId,Sequence=intent.Sequence,Outcome=intent.Outcome,RequestHash=WebAiHistoryContracts.Hash(intent),IntentJson=JsonSerializer.Serialize(intent,WebAiHistoryContracts.Json),SavedResponseJson=row.ReceiptJson,CommittedAt=receipt.UpdatedAt,ReportedAt=receipt.UpdatedAt});
        await UpdateSession(session,r,receipt,ct);return receipt;
    },ct);
    private async Task Restore(BrainstormingKeepReview r,bool redo,CancellationToken ct)
    {
        var destination=BrainstormingPlacements.Destination(r);
        var sections=await db.Sections.Where(s=>s.DocumentId==r.Scope.DocumentId).ToListAsync(ct);var pages=await db.Pages.Where(p=>p.DocumentId==r.Scope.DocumentId).ToListAsync(ct);
        var nodes=await db.ProjectNodes.Where(n=>n.DocumentId==r.Scope.DocumentId && n.SyncDeletionId==null).ToListAsync(ct);
        if(r.Kind!=BrainstormingTargetKind.CreateSection) {
            await RequireUnannotatedPage(r,ct);
            var old=r.Before.Pages.Single(p=>p.Id==destination.PageId);var current=pages.SingleOrDefault(p=>p.Id==old.Id && p.SectionId==old.SectionId);
            if(current is null || current.OrderIndex!=old.Order || !sections.Any(s=>s.Id==old.SectionId && s.OrderIndex==r.Before.Outline.Sections.Single(s=>s.Id==old.SectionId).Order)
                || current.Content!=(redo?old.Content:r.AfterHtml))throw new InvalidOperationException("Affected writing or order changed. Use original-copy recovery; later writing is retained.");
            current.Content=redo?r.AfterHtml:old.Content;current.UpdatedAt=DateTimeOffset.UtcNow;
        } else {
            var before=r.Before.Outline;var after=BrainstormingKeepContract.AfterOutline(r);var expected=redo?before:after;var desired=redo?after:before;
            var added=sections.SingleOrDefault(s=>s.Id==r.NewSectionId);var page=pages.SingleOrDefault(p=>p.Id==r.NewPageId);var node=nodes.SingleOrDefault(n=>n.Id==r.NewNodeId);
            var expectedNode=after.Nodes.SingleOrDefault(n=>n.Id==r.NewNodeId);
            if(redo ? added is not null || page is not null || node is not null : added is null || page is null || (expectedNode is null ? node is not null : node is null || node.Title!=r.SectionTitle || node.MetadataJson is not null || node.ParentId!=expectedNode.ParentId || node.LinkedSectionId!=r.NewSectionId || node.OrderIndex!=expectedNode.Order || node.NodeType!=ProjectNodeType.Scene) || added.Title!=r.SectionTitle || added.NarrativePurpose is not null || added.LanguageCode is not null
                || added.OrderIndex!=after.Sections.Single(s=>s.Id==r.NewSectionId).Order || page.SectionId!=r.NewSectionId
                || page.Content!=r.AfterHtml || page.Title!="Page 1" || page.OrderIndex!=0 || pages.Count(p=>p.SectionId==r.NewSectionId)!=1
                || await db.SectionNotes.AnyAsync(n=>n.SectionId==r.NewSectionId,ct) || await db.PageNotes.AnyAsync(n=>n.PageId==r.NewPageId,ct) || await db.PageAnnotations.AnyAsync(n=>n.PageId==r.NewPageId,ct)
                || await db.SceneCards.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct) || await db.SceneNotes.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct) || await db.SceneContents.AnyAsync(n=>n.SceneNodeId==r.NewNodeId && (n.ContentJson!=r.AfterHtml || n.LanguageCode!=null),ct) || await db.SceneAnnotations.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct))throw new InvalidOperationException("The added section has later writing/planning. Use original-copy recovery.");
            if(sections.Count!=expected.Sections.Count || nodes.Count!=expected.Nodes.Count)throw new InvalidOperationException("Outline structure changed. Use original-copy recovery; added author structure is retained.");
            foreach(var s in expected.Sections.Where(s=>s.Id!=r.NewSectionId)) {
                var current=sections.SingleOrDefault(n=>n.Id==s.Id);if(current is null || current.OrderIndex!=s.Order)throw new InvalidOperationException("Section order changed. Use original-copy recovery.");current.OrderIndex=desired.Sections.Single(n=>n.Id==s.Id).Order;
            }
            foreach(var n in expected.Nodes.Where(n=>n.Id!=r.NewNodeId)) {
                var current=nodes.SingleOrDefault(x=>x.Id==n.Id);if(current is null || current.ParentId!=n.ParentId || current.OrderIndex!=n.Order)throw new InvalidOperationException("Outline order changed. Use original-copy recovery.");current.OrderIndex=desired.Nodes.Single(x=>x.Id==n.Id).Order;
            }
            if(redo)await Create(r,ct);else {db.Pages.Remove(page!);if(node is not null)db.ProjectNodes.Remove(node);db.Sections.Remove(added!);}
        }
        (await db.Documents.SingleAsync(d=>d.Id==r.Scope.DocumentId,ct)).UpdatedAt=DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);if(r.Kind!=BrainstormingTargetKind.CreateSection || redo)await WriterApp.Application.Documents.ManuscriptSceneMirror.UpdateAsync(db,destination.SectionId??r.NewSectionId,ct);
    }
    private async Task RequireUnannotatedPage(BrainstormingKeepReview r,CancellationToken ct) {
        var destination=BrainstormingPlacements.Destination(r);
        if(await db.PageAnnotations.AnyAsync(a=>a.PageId==destination.PageId,ct) || await db.SceneAnnotations.AnyAsync(a=>a.SceneNode!.DocumentId==r.Scope.DocumentId && a.SceneNode.LinkedSectionId==destination.SectionId,ct))
            throw new InvalidOperationException("This destination has anchored annotations. Keep them intact: review insertion into a new section or apply the passage through the annotation-aware manuscript editor.");
    }
    public Task<Guid> RecoverAsync(BrainstormingScope scope,Guid operation,CancellationToken ct=default)=>Transaction(scope.DocumentId,operation,async()=>{
        await Session(scope,ct);var row=await db.BrainstormingKeeps.SingleAsync(k=>k.SessionId==scope.SessionId && k.OperationId==operation,ct);var r=Read<BrainstormingKeepReview>(row.ReviewJson);
        Guid Identity(string suffix)=>new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(operation+suffix))[..16]);
        var id=Identity("recovered-writing");var project=Identity("recovered-project");
        var existing=await db.Documents.SingleOrDefaultAsync(d=>d.Id==id,ct);if(existing is not null){if(existing.OwnerUserId!=owner)throw new BrainstormingAccessException();return id;}
        var original=await db.Documents.AsNoTracking().SingleAsync(d=>d.Id==scope.DocumentId,ct);var now=DateTimeOffset.UtcNow;
        db.Projects.Add(new(){Id=project,OwnerUserId=owner,Title="Recovered writing",PrimaryDocumentId=id,CreatedUtc=now,UpdatedUtc=now});
        db.Documents.Add(new(){Id=id,ProjectId=project,OwnerUserId=owner,Title=(original.Title.Length>170?original.Title[..170]:original.Title)+" (recovered original)",CreatedAt=now,UpdatedAt=now,DocumentKind=original.DocumentKind});
        foreach(var section in r.Before.Outline.Sections) {
            var sectionId=Identity("section"+section.Id);db.Sections.Add(new(){Id=sectionId,DocumentId=id,Title=section.Title,OrderIndex=section.Order,CreatedAt=now,UpdatedAt=now});
            foreach(var page in r.Before.Pages.Where(p=>p.SectionId==section.Id))db.Pages.Add(new(){Id=Identity("page"+page.Id),DocumentId=id,SectionId=sectionId,Title=page.Title,OrderIndex=page.Order,Content=page.Content,CreatedAt=now,UpdatedAt=now});
        }
        foreach(var node in r.Before.Outline.Nodes)db.ProjectNodes.Add(new(){Id=Identity("node"+node.Id),DocumentId=id,ProjectId=project,ParentId=node.ParentId is { } parent?Identity("node"+parent):null,NodeType=Enum.Parse<ProjectNodeType>(node.Type,true),Title=node.Title,OrderIndex=node.Order,LinkedSectionId=node.SectionId is { } section?Identity("section"+section):null,UpdatedUtc=now});
        return id;
    },ct);
}
