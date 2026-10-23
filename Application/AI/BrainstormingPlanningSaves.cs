using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Commands;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Sync;
using WriterApp.Application.Collaboration;

namespace WriterApp.Application.AI;

public sealed class BrainstormingPlanningSaves(AppDbContext db,string owner,string backend,CollaborationAccessService? access=null)
{
    private bool shared;
    private CollaborationBrainstormingWrites Writes=>new(db,owner,access,null);
    private static T Read<T>(string json)=>JsonSerializer.Deserialize<T>(json,BrainstormingValidation.Json)!;
    private async Task<BrainstormingSession> Session(BrainstormingScope scope,CancellationToken ct) {
        var session=await new BrainstormingSessions(db,owner,backend,access).ReadAsync(scope.DocumentId,scope.SessionId,ct)??throw new BrainstormingAccessException();
        BrainstormingValidation.RequireScope(session.Scope,scope);if(session.State==BrainstormingConversationState.Deleted)throw new BrainstormingAccessException();return session;
    }
    private async Task<T> Transaction<T>(BrainstormingPlanningReview review,Func<Task<T>> work,CancellationToken ct)=>await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
        db.ChangeTracker.Clear();await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
        shared=await Writes.Begin(review.Scope.DocumentId,review.OperationId,ct);
        var result=await work();await db.SaveChangesAsync(ct);if(shared)await Writes.End(review.Scope.DocumentId,review.OperationId,ct);await tx.CommitAsync(ct);return result;
    });
    private static SyncSynopsis Synopsis(DocumentSynopsisRecord? s)=>s is null?new():new(s.Logline,s.Premise,s.Theme,s.ProtagonistArc,s.CentralConflict,s.Stakes,s.Setting,s.EndingIntent,s.OpenQuestions,s.Notes);
    public async Task<BrainstormingPlanningSnapshot> CaptureAsync(BrainstormingScope scope,CancellationToken ct=default) {
        await Session(scope,ct);var source=await new BrainstormingStorySourceReader(db,owner,access).ReadAsync(scope.DocumentId,scope.ProjectId,ct);
        var synopsis=Synopsis(await db.DocumentSynopses.AsNoTracking().SingleOrDefaultAsync(s=>s.DocumentId==scope.DocumentId,ct));
        var confirmed=await new BrainstormingStorySourceReader(db,owner,access).ReadAsync(scope.DocumentId,scope.ProjectId,ct);
        if(confirmed.Stamp!=source.Stamp)throw new InvalidOperationException("Planning changed during capture. Refresh the review.");
        return new(1,scope,source.Stamp,synopsis,source.Outline.Nodes.Where(n=>n.Type=="chapter").Select(n=>new BrainstormingPlanningDestination(n.Id,n.Title,n.Type,n.ParentId,n.Order,
            source.Outline.Nodes.Where(c=>c.ParentId==n.Id).Select(c=>c.Order).DefaultIfEmpty(-1).Max()+1)).ToArray());
    }
    public async Task<BrainstormingPlanningReceipt> InspectAsync(BrainstormingPlanningReview r,CancellationToken ct=default) {
        BrainstormingPlanningContract.Validate(r);await Session(r.Scope,ct);
        var row=await db.BrainstormingPlanningSaves.AsNoTracking().SingleOrDefaultAsync(p=>p.SessionId==r.Scope.SessionId && (p.OperationId==r.OperationId || p.CandidateId==r.CandidateId),ct);
        if(row is null)return Receipt(r,BrainstormingApplicationState.Failed,"No save record in this owned session. Explicit retry may use the same approval.");
        if(row.ReviewJson!=BrainstormingPersistence.Json(r))throw new InvalidDataException("Inspect the original planning approval.");return Read<BrainstormingPlanningReceipt>(row.ReceiptJson);
    }
    private static BrainstormingPlanningReceipt Receipt(BrainstormingPlanningReview r,BrainstormingApplicationState state,string message)=>new(1,r.OperationId,r.Scope,BrainstormingPlanningContract.Hash(r),state,DateTimeOffset.UtcNow,message);
    public Task<BrainstormingPlanningReceipt> ApplyAsync(BrainstormingPlanningReview r,CancellationToken ct=default)=>Transaction(r,async()=>{
        BrainstormingPlanningContract.Validate(r);var session=await Session(r.Scope,ct);
        var prior=await db.BrainstormingPlanningSaves.SingleOrDefaultAsync(p=>p.SessionId==r.Scope.SessionId && (p.OperationId==r.OperationId || p.CandidateId==r.CandidateId),ct);
        if(prior is not null){if(prior.ReviewJson!=BrainstormingPersistence.Json(r))throw new InvalidDataException("Planning approval already has an immutable identity.");return Read<BrainstormingPlanningReceipt>(prior.ReceiptJson);}
        BrainstormingPlanningContract.Provenance(session,r);var source=await CaptureAsync(r.Scope,ct);
        if(source.Source!=r.Source)throw new InvalidOperationException("Planning or writing changed. Refresh and review again; no fields were saved.");
        if(r.Kind==BrainstormingPlanningKind.SceneIdea) {
            var parent=await db.ProjectNodes.SingleOrDefaultAsync(n=>n.Id==r.ParentId && n.DocumentId==r.Scope.DocumentId && n.ProjectId==r.Scope.ProjectId && n.NodeType==ProjectNodeType.Chapter && n.SyncDeletionId==null,ct)??throw new InvalidOperationException("The selected chapter is unavailable.");
            if(source.Chapters.Single(c=>c.Id==r.ParentId).NextChildOrder!=r.NewNodeOrder || await db.ProjectNodes.CountAsync(n=>n.DocumentId==r.Scope.DocumentId,ct)>=1000 || await db.ProjectNodes.AnyAsync(n=>n.Id==r.NewNodeId,ct))throw new InvalidOperationException("Scene identity or position changed, or outline limit reached.");
            var order=r.NewNodeOrder;
            if(shared) {
                await Writes.AddNode(r.NewNodeId,r.Scope.DocumentId,r.Scope.ProjectId!.Value,parent.Id,r.Fields.Single(f=>f.Field=="Title").After,order,null,BrainstormingPlanningContract.SceneIdeaMetadata,ct);
                var now=DateTimeOffset.UtcNow;
                if(r.Fields.SingleOrDefault(f=>f.Field=="Summary") is {} sharedSummary)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SceneCards(SceneNodeId,Summary,Status,UpdatedAtUtc) VALUES({r.NewNodeId},{sharedSummary.After},{"Draft"},{now})",ct);
                if(r.Fields.SingleOrDefault(f=>f.Field=="Notes") is {} sharedNotes)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SceneNotes(SceneNodeId,NotesText,UpdatedAtUtc) VALUES({r.NewNodeId},{sharedNotes.After},{now})",ct);
            } else {
            db.ProjectNodes.Add(new(){Id=r.NewNodeId,ProjectId=r.Scope.ProjectId!.Value,DocumentId=r.Scope.DocumentId,ParentId=parent.Id,NodeType=ProjectNodeType.Scene,Title=r.Fields.Single(f=>f.Field=="Title").After,MetadataJson=BrainstormingPlanningContract.SceneIdeaMetadata,OrderIndex=order,UpdatedUtc=DateTimeOffset.UtcNow});
            if(r.Fields.SingleOrDefault(f=>f.Field=="Summary") is { } summary)db.SceneCards.Add(new(){SceneNodeId=r.NewNodeId,Summary=summary.After,Status="Draft",UpdatedAtUtc=DateTimeOffset.UtcNow});
            if(r.Fields.SingleOrDefault(f=>f.Field=="Notes") is { } notes)db.SceneNotes.Add(new(){SceneNodeId=r.NewNodeId,NotesText=notes.After,UpdatedAtUtc=DateTimeOffset.UtcNow});
            }
        } else await ApplyFields(r,false,ct);
        var receipt=Receipt(r,BrainstormingApplicationState.Committed,"Planning saved. Earlier context remains pinned; refresh story context before another request.");
        db.BrainstormingPlanningSaves.Add(new(){SessionId=r.Scope.SessionId,OperationId=r.OperationId,CandidateId=r.CandidateId,ReviewJson=BrainstormingPersistence.Json(r),ReceiptJson=BrainstormingPersistence.Json(receipt)});
        await Record(session,r,receipt,ct);return receipt;
    },ct);
    private async Task ApplyFields(BrainstormingPlanningReview r,bool undo,CancellationToken ct) {
        var record=await db.DocumentSynopses.SingleOrDefaultAsync(s=>s.DocumentId==r.Scope.DocumentId,ct);
        var current=Synopsis(record);foreach(var f in r.Fields)if(BrainstormingPlanningContract.Value(current,f.Field)!=(undo?f.After:f.Before))throw new InvalidOperationException("An affected planning field changed. Retain later planning and copy the original from recovery.");
        var model=new WriterApp.Domain.Documents.Synopsis {Logline=current.Logline,Premise=current.Premise,Theme=current.Theme,ProtagonistArc=current.ProtagonistArc,CentralConflict=current.CentralConflict,Stakes=current.Stakes,Setting=current.Setting,EndingIntent=current.EndingIntent,OpenQuestions=current.OpenQuestions,Notes=current.Notes};
        var document=new WriterApp.Domain.Documents.Document {Synopsis=model};
        foreach(var f in r.Fields)new UpdateSynopsisFieldManualCommand(Guid.Empty,Key(f.Field),undo?f.Before:f.After,undo?f.After:f.Before).Execute(document);
        if(shared) {
            var now=DateTimeOffset.UtcNow;
            if(record is null)await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO DocumentSynopses(DocumentId,Logline,Premise,Theme,ProtagonistArc,CentralConflict,Stakes,Setting,EndingIntent,OpenQuestions,Notes,UpdatedAt) VALUES({r.Scope.DocumentId},{model.Logline},{model.Premise},{model.Theme},{model.ProtagonistArc},{model.CentralConflict},{model.Stakes},{model.Setting},{model.EndingIntent},{model.OpenQuestions},{model.Notes},{now})",ct);
            else await db.DocumentSynopses.Where(s=>s.DocumentId==r.Scope.DocumentId).ExecuteUpdateAsync(s=>s
                .SetProperty(x=>x.Logline,model.Logline).SetProperty(x=>x.Premise,model.Premise).SetProperty(x=>x.Theme,model.Theme)
                .SetProperty(x=>x.ProtagonistArc,model.ProtagonistArc).SetProperty(x=>x.CentralConflict,model.CentralConflict).SetProperty(x=>x.Stakes,model.Stakes)
                .SetProperty(x=>x.Setting,model.Setting).SetProperty(x=>x.EndingIntent,model.EndingIntent).SetProperty(x=>x.OpenQuestions,model.OpenQuestions).SetProperty(x=>x.Notes,model.Notes).SetProperty(x=>x.UpdatedAt,now),ct);
            return;
        }
        if(record is null){record=new(){DocumentId=r.Scope.DocumentId};db.DocumentSynopses.Add(record);}
        foreach(var f in r.Fields)typeof(DocumentSynopsisRecord).GetProperty(f.Field)!.SetValue(record,typeof(WriterApp.Domain.Documents.Synopsis).GetProperty(f.Field)!.GetValue(model));
        record.UpdatedAt=DateTimeOffset.UtcNow;
    }
    private static string Key(string field)=>field switch {"ProtagonistArc"=>"protagonist_arc","CentralConflict"=>"central_conflict","EndingIntent"=>"ending_intent","OpenQuestions"=>"open_questions",_=>field.ToLowerInvariant()};
    public Task<BrainstormingPlanningReceipt> UndoAsync(BrainstormingPlanningReview r,CancellationToken ct=default)=>Transaction(r,async()=>{
        await Session(r.Scope,ct);var receipt=await InspectAsync(r,ct);if(receipt.State==BrainstormingApplicationState.Undone)return receipt;
        if(receipt.State!=BrainstormingApplicationState.Committed)throw new InvalidOperationException("Inspect and confirm the planning save before Undo.");
        if(r.Kind==BrainstormingPlanningKind.SceneIdea) {
            var node=await db.ProjectNodes.SingleOrDefaultAsync(n=>n.Id==r.NewNodeId && n.DocumentId==r.Scope.DocumentId,ct);
            var card=await db.SceneCards.SingleOrDefaultAsync(n=>n.SceneNodeId==r.NewNodeId,ct);var notes=await db.SceneNotes.SingleOrDefaultAsync(n=>n.SceneNodeId==r.NewNodeId,ct);
            if(node is null || node.NodeType!=ProjectNodeType.Scene || node.ParentId!=r.ParentId || node.OrderIndex!=r.NewNodeOrder || node.SyncDeletionId!=null || node.LinkedSectionId!=null || node.Title!=r.Fields.Single(f=>f.Field=="Title").After || node.MetadataJson!=BrainstormingPlanningContract.SceneIdeaMetadata
                || !await db.ProjectNodes.AnyAsync(n=>n.Id==r.ParentId && n.DocumentId==r.Scope.DocumentId && n.SyncDeletionId==null,ct)
                || await db.SceneContents.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct) || await db.SceneAnnotations.AnyAsync(n=>n.SceneNodeId==r.NewNodeId,ct) || await db.ProjectNodes.AnyAsync(n=>n.ParentId==r.NewNodeId,ct)
                || (notes?.NotesText??"")!=(r.Fields.SingleOrDefault(f=>f.Field=="Notes")?.After??"") || (card is not null && (card.Summary!=(r.Fields.SingleOrDefault(f=>f.Field=="Summary")?.After) || card.Status!="Draft"
                    || card.TagsJson!=null || card.SubplotTagsJson!=null || card.ReferencesJson!=null || card.PovCharacterId!=null || card.PlaceId!=null || card.TimelineEventId!=null || card.TimeRef!=null
                    || !string.IsNullOrEmpty(card.NarrativePurpose) || !string.IsNullOrEmpty(card.NarrativeRole) || !string.IsNullOrEmpty(card.NarrativeIntent) || !string.IsNullOrEmpty(card.EmotionalBeat) || !string.IsNullOrEmpty(card.KeyEvents) || !string.IsNullOrEmpty(card.OpenQuestions))))
                throw new InvalidOperationException("Scene idea has later writing or planning. Preserve it and copy the original recovery fields.");
            if(shared) {
                await db.SceneCards.Where(n=>n.SceneNodeId==r.NewNodeId).ExecuteDeleteAsync(ct);await db.SceneNotes.Where(n=>n.SceneNodeId==r.NewNodeId).ExecuteDeleteAsync(ct);
                await db.ProjectNodes.Where(n=>n.Id==r.NewNodeId && n.DocumentId==r.Scope.DocumentId).ExecuteDeleteAsync(ct);
            } else {if(card is not null)db.SceneCards.Remove(card);if(notes is not null)db.SceneNotes.Remove(notes);db.ProjectNodes.Remove(node);}
        } else await ApplyFields(r,true,ct);
        receipt=Receipt(r,BrainstormingApplicationState.Undone,"Approved planning fields restored; unrelated planning retained.");
        (await db.BrainstormingPlanningSaves.SingleAsync(p=>p.SessionId==r.Scope.SessionId && p.OperationId==r.OperationId,ct)).ReceiptJson=BrainstormingPersistence.Json(receipt);
        await Record((await Session(r.Scope,ct)),r,receipt,ct);return receipt;
    },ct);
    private async Task Record(BrainstormingSession session,BrainstormingPlanningReview r,BrainstormingPlanningReceipt receipt,CancellationToken ct) {
        if(!shared)(await db.Documents.SingleAsync(d=>d.Id==r.Scope.DocumentId,ct)).UpdatedAt=DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);var stamp=(await new BrainstormingStorySourceReader(db,owner,access).ReadAsync(r.Scope.DocumentId,r.Scope.ProjectId,ct)).Stamp;
        var proposal=session.Planning.SingleOrDefault(p=>p.Id==r.CandidateId)??new(1,r.CandidateId,r.Scope,r.TurnId,r.ContextId,r.Kind,r.Kind==BrainstormingPlanningKind.SceneIdea?r.NewNodeId.ToString():r.Scope.DocumentId.ToString(),r.Source,string.Join(", ",r.Fields.Select(f=>f.Field)),BrainstormingPersistence.Json(r.Fields.Select(f=>new {f.Field,f.Before})),BrainstormingPersistence.Json(r.Fields.Select(f=>new {f.Field,f.After})),1,r.ReviewedAt,r.Kind==BrainstormingPlanningKind.CharacterNote);
        var approval=new BrainstormingApproval(1,r.OperationId,r.Scope,r.OperationId,r.CandidateId,1,BrainstormingValidation.Fingerprint(proposal.Proposed),r.ReviewedAt);
        var app=new BrainstormingApplication(1,r.OperationId,r.Scope,approval.Id,receipt.State,r.OperationId,r.OperationId,stamp,receipt.Message,receipt.UpdatedAt);
        var next=session with {Revision=session.Revision+1,UpdatedAt=receipt.UpdatedAt,Planning=[..session.Planning.Where(p=>p.Id!=proposal.Id),proposal],Approvals=[..session.Approvals.Where(a=>a.Id!=approval.Id),approval],Applications=[..session.Applications.Where(a=>a.OperationId!=app.OperationId),app]};
        BrainstormingValidation.Validate(next);var row=await db.BrainstormingSessions.SingleAsync(s=>s.Id==r.Scope.SessionId,ct);row.Revision=next.Revision;row.SnapshotJson=BrainstormingPersistence.Json(next);row.UpdatedUnixMilliseconds=receipt.UpdatedAt.ToUnixTimeMilliseconds();
    }
}
