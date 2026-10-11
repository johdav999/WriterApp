using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Device.Shared.Services;

public static class LocalBrainstormingWriting
{
    public static BrainstormingWritingSnapshot Capture(LocalDocument d) => new(
        WritingOutline.Create(d.DocumentId,d.Project?.ProjectId,d.LocalRevision.ToString(),d.Sections.Select(s=>new WritingOutlineSection(s.SectionId,s.OrderIndex,s.Title)),
            (d.Project?.Nodes??[]).Where(n=>n.DeletionId is null).Select(n=>new WritingOutlineNode(n.NodeId,n.ParentId,n.OrderIndex,n.NodeType,n.Title,n.SectionId))),
        d.Sections.OrderBy(s=>s.OrderIndex).SelectMany(s=>s.Pages.OrderBy(p=>p.OrderIndex).Select(p=>new BrainstormingStoryPage(p.PageId,s.SectionId,p.OrderIndex,p.Title,p.Content,p.ContentFormat.ToString(),d.LocalRevision.ToString(),BrainstormingFreshness.Saved))).ToArray());
    public static LocalDocument Apply(LocalDocument before,BrainstormingKeepReview review)
    {
        BrainstormingKeepContract.Validate(review);
        if(before.DeletedAtUtc is not null || before.SyncState==LocalSyncState.Conflict || BrainstormingKeepContract.WritingHash(Capture(before))!=BrainstormingKeepContract.WritingHash(review.Before))
            throw new InvalidOperationException("The approved manuscript changed. Review a fresh proposal; nothing was applied.");
        var destination=BrainstormingPlacements.Destination(review);
        if(review.Kind!=BrainstormingTargetKind.CreateSection)return before with {Sections=before.Sections.Select(s=>s with {Pages=s.Pages.Select(p=>p.PageId==destination.PageId?p with {Content=review.AfterHtml,ContentFormat=LocalContentFormat.Html}:p).ToArray()}).ToArray()};
        var outline=BrainstormingKeepContract.AfterOutline(review);var now=review.ReviewedAt;
        var sections=outline.Sections.Select(s=>before.Sections.SingleOrDefault(b=>b.SectionId==s.Id) is { } old?old with {OrderIndex=s.Order}:
            new LocalSection{SectionId=s.Id,Title=s.Title,OrderIndex=s.Order,CreatedAtUtc=now,UpdatedAtUtc=now,Pages=[new LocalPage{PageId=review.NewPageId,Title="Page 1",OrderIndex=0,Content=review.AfterHtml,ContentFormat=LocalContentFormat.Html,CreatedAtUtc=now,UpdatedAtUtc=now}]}).ToArray();
        var nodes=outline.Nodes.Select(n=>before.Project!.Nodes.SingleOrDefault(b=>b.NodeId==n.Id) is { } old?old with {OrderIndex=n.Order}:
            new LocalProjectNode{NodeId=n.Id,ParentId=n.ParentId,NodeType=n.Type,Title=n.Title,OrderIndex=n.Order,SectionId=n.SectionId}).ToArray();
        var result=before with {Sections=sections,Project=before.Project is null?null:before.Project with {Nodes=nodes.Concat(before.Project.Nodes.Where(n=>n.DeletionId is not null)).ToArray()}};
        LocalProjectStructure.Validate(result);return result;
    }
    public static void ValidateEntry(LocalAiHistory e)
    {
        var r=e.Brainstorming??throw new InvalidDataException("Missing approved brainstorming recovery evidence.");
        if(e.Version!=6 || e.Id!=r.OperationId || e.DocumentId!=r.Scope.DocumentId || e.Action!="brainstorming.keep" || e.Target!="Brainstorming:manuscript" || e.After is null || e.Proposed!=r.Candidate.PlainText
            || !LocalDocumentCodec.Encode(Apply(e.Before,r)).SequenceEqual(LocalDocumentCodec.Encode(e.After)))throw new InvalidDataException("Invalid retained brainstorming operation.");
    }
    public static LocalDocument Change(LocalDocument current,LocalAiHistory e,bool redo)
    {
        ValidateEntry(e);
        if(e.Status is "Undoing" or "Redoing") {
            try{return Change(current,e with {Status=redo?"Undone":"Applied"},redo);}
            catch(InvalidOperationException){_=Change(current,e with {Status=redo?"Applied":"Undone"},!redo);return current;}
        }
        if(current.DeletedAtUtc is not null || current.SyncState==LocalSyncState.Conflict || (redo?e.Status is not ("Undone" or "Redoing"):e.Status is not ("Applied" or "Undoing")))throw new InvalidOperationException("Inspect this application before undo or redo.");
        var from=redo?e.Before:e.After!;var to=redo?e.After!:e.Before;var r=e.Brainstorming!;var destination=BrainstormingPlacements.Destination(r);
        if(r.Kind!=BrainstormingTargetKind.CreateSection) {
            var oldSection=from.Sections.Single(s=>s.SectionId==destination.SectionId);var section=current.Sections.SingleOrDefault(s=>s.SectionId==oldSection.SectionId);
            var old=oldSection.Pages.Single(p=>p.PageId==destination.PageId);var page=section?.Pages.SingleOrDefault(p=>p.PageId==old.PageId);
            var desired=to.Sections.Single(s=>s.SectionId==oldSection.SectionId).Pages.Single(p=>p.PageId==old.PageId);
            if(section is null || page is null || section.OrderIndex!=oldSection.OrderIndex || page.OrderIndex!=old.OrderIndex || page.ContentFormat!=old.ContentFormat || page.Content!=old.Content)
                throw new InvalidOperationException("Affected writing or order changed. Use a recovery copy to keep later edits.");
            return current with {Sections=current.Sections.Select(s=>s with {Pages=s.Pages.Select(p=>p.PageId==old.PageId?p with {Content=desired.Content,ContentFormat=desired.ContentFormat}:p).ToArray()}).ToArray()};
        }
        var added=e.After!.Sections.Single(s=>s.SectionId==r.NewSectionId);var actual=current.Sections.SingleOrDefault(s=>s.SectionId==r.NewSectionId);
        var addedNode=e.After.Project?.Nodes.SingleOrDefault(n=>n.NodeId==r.NewNodeId);var actualNode=current.Project?.Nodes.SingleOrDefault(n=>n.NodeId==r.NewNodeId);
        if(redo ? actual is not null || actualNode is not null : actual is null || (addedNode is null ? actualNode is not null : actualNode is null || BrainstormingPersistence.Json(actualNode with {ServerNodeId=null})!=BrainstormingPersistence.Json(addedNode))
            || BrainstormingPersistence.Json(actual with {ServerSectionId=null,CreatedAtUtc=added.CreatedAtUtc,UpdatedAtUtc=added.UpdatedAtUtc,Pages=actual.Pages.Select(p=>p with {ServerPageId=null,CreatedAtUtc=added.Pages[0].CreatedAtUtc,UpdatedAtUtc=added.Pages[0].UpdatedAtUtc}).ToArray()})!=BrainstormingPersistence.Json(added)
            )
            throw new InvalidOperationException("The new section has later writing or planning changes. Use a recovery copy.");
        if(current.Sections.Count!=from.Sections.Count || (current.Project?.Nodes.Count??0)!=(from.Project?.Nodes.Count??0))throw new InvalidOperationException("Outline structure changed. Recover the original as a copy.");
        var sections=current.Sections.Where(s=>s.SectionId!=r.NewSectionId).ToList();var nodes=(current.Project?.Nodes??[]).Where(n=>n.NodeId!=r.NewNodeId).ToList();
        foreach(var expected in from.Sections.Where(s=>s.SectionId!=r.NewSectionId)) {
            var desired=to.Sections.Single(s=>s.SectionId==expected.SectionId);var index=sections.FindIndex(s=>s.SectionId==expected.SectionId);
            if(index<0 || sections[index].OrderIndex!=expected.OrderIndex)throw new InvalidOperationException("Section order changed. Recover the original as a copy.");
            sections[index]=sections[index] with {OrderIndex=desired.OrderIndex};
        }
        foreach(var expected in (from.Project?.Nodes??[]).Where(n=>n.NodeId!=r.NewNodeId)) {
            var desired=to.Project!.Nodes.Single(n=>n.NodeId==expected.NodeId);var index=nodes.FindIndex(n=>n.NodeId==expected.NodeId);
            if(index<0 || nodes[index].ParentId!=expected.ParentId || nodes[index].OrderIndex!=expected.OrderIndex)throw new InvalidOperationException("Outline order changed. Recover the original as a copy.");
            nodes[index]=nodes[index] with {OrderIndex=desired.OrderIndex};
        }
        if(redo){sections.Add(added);if(addedNode is not null)nodes.Add(addedNode);}
        if(sections.GroupBy(s=>s.OrderIndex).Any(g=>g.Count()>1))throw new InvalidOperationException("The insertion position is occupied. Use a recovery copy.");
        var next=current with {Sections=sections,Project=current.Project is null?null:current.Project with {Nodes=nodes}};LocalProjectStructure.Validate(next);return next;
    }
}

public sealed class LocalBrainstormingKeeps(LocalDocumentRepository documents,LocalAiStore history,DeviceBrainstormingSessions sessions,DeviceAccountService account,DeviceHostOptions host)
{
    private readonly SemaphoreSlim gate=new(1,1);
    public async Task<BrainstormingKeepReceipt> ApplyAsync(BrainstormingKeepReview r,CancellationToken ct=default)
    {
        await gate.WaitAsync(ct);try {
            var check=await sessions.RequireCurrentAsync(r.Scope,ct);var entries=await history.HistoryAsync(r.Scope.DocumentId,ct);check();
            var cache=await sessions.Sessions.ReadAsync(r.Scope,ct)??throw new InvalidDataException("Conversation unavailable.");
            if(cache.PendingGeneration is not null || cache.DraftEdit is not null || cache.Conflicts.Count!=0 || cache.Session.State==BrainstormingConversationState.Deleted
                || !cache.Session.Drafts.Any(d=>BrainstormingPersistence.Json(d)==BrainstormingPersistence.Json(r.Candidate)) || !cache.Session.Targets.Any(t=>t==r.Target))
                throw new InvalidDataException("Apply only the complete reviewed candidate in its original conversation.");
            var same=entries.SingleOrDefault(h=>h.Brainstorming?.Candidate.Id==r.Candidate.Id && h.Brainstorming.Scope==r.Scope);
            if(same is not null){if(BrainstormingKeepContract.Hash(same.Brainstorming!)!=BrainstormingKeepContract.Hash(r))throw new InvalidOperationException("Inspect this candidate's original application.");return await InspectCore(same,check,ct);}
            var source=await documents.LoadAsync(r.Scope.DocumentId,ct)??throw new IOException("Manuscript unavailable.");check();var after=LocalBrainstormingWriting.Apply(source,r);
            var entry=new LocalAiHistory(6,r.OperationId,source.DocumentId,"brainstorming.keep","Brainstorming:manuscript",source.LocalRevision,source.ServerVersion,r.ReviewedAt,"Applying",source,r.Candidate.PlainText,After:after,Brainstorming:r);
            await history.SaveHistoryAsync(entry,ct);check();await documents.SaveAsync(after,ct);
            await history.SaveHistoryAsync(entry with {Status="Applied"},CancellationToken.None);return Receipt(entry with {Status="Applied"});
        }finally{gate.Release();}
    }
    public async Task<BrainstormingKeepReceipt> InspectAsync(BrainstormingKeepReview r,CancellationToken ct=default)
    {
        var check=await sessions.RequireCurrentAsync(r.Scope,ct);var e=(await history.HistoryAsync(r.Scope.DocumentId,ct)).SingleOrDefault(h=>h.Id==r.OperationId);check();
        if(e is not null && BrainstormingKeepContract.Hash(e.Brainstorming!)!=BrainstormingKeepContract.Hash(r))throw new InvalidDataException("Inspect the original approved application identity.");
        return e is null?new(1,r.OperationId,r.Scope,r.Candidate.Id,BrainstormingKeepContract.Hash(r),BrainstormingApplicationState.Failed,r.OperationId,0,"No local writing operation was prepared. Explicit retry uses this same identity.",DateTimeOffset.UtcNow):await InspectCore(e,check,ct);
    }
    private async Task<BrainstormingKeepReceipt> InspectCore(LocalAiHistory e,Action check,CancellationToken ct)
    {
        LocalBrainstormingWriting.ValidateEntry(e);var current=await documents.LoadAsync(e.DocumentId,ct)??throw new IOException("Manuscript unavailable.");check();
        if(e.Status=="Applying") {
            if(LocalTranslation.Signature(current)==LocalTranslation.Signature(LocalPlanning.Reconcile(e.Before,e.After!))){e=e with {Status="Applied"};await history.SaveHistoryAsync(e,ct);}
            else if(LocalTranslation.Signature(current)==LocalTranslation.Signature(e.Before))return Receipt(e) with {State=BrainstormingApplicationState.Approved,Message="Approved operation was not saved. Explicit retry can finish the same operation."};
            else return Receipt(e) with {State=BrainstormingApplicationState.Unknown,Message="Writing diverged after interruption. Keep current writing and use a recovery copy."};
        }
        if(e.Status is "Undoing" or "Redoing") {
            bool redo=e.Status=="Redoing";string previous=redo?"Undone":"Applied",completed=redo?"Applied":"Undone";
            try{_=LocalBrainstormingWriting.Change(current,e with {Status=previous},redo);e=e with {Status=previous};}
            catch(InvalidOperationException){try{_=LocalBrainstormingWriting.Change(current,e with {Status=completed},!redo);e=e with {Status=completed};}catch(InvalidOperationException){return Receipt(e) with {State=BrainstormingApplicationState.Unknown,Message="Interrupted undo/redo diverged. Preserve current writing and recover the original as a copy."};}}
            // Inspection reconciles only history metadata after proving the saved state. It never replays writing.
            check();await history.SaveHistoryAsync(e,ct);
        }
        return Receipt(e);
    }
    public async Task<BrainstormingKeepReceipt> ResumeAsync(BrainstormingKeepReview r,CancellationToken ct=default)
    {
        var receipt=await InspectAsync(r,ct);if(receipt.State!=BrainstormingApplicationState.Approved)return await ApplyAsync(r,ct);
        var check=await sessions.RequireCurrentAsync(r.Scope,ct);var e=(await history.HistoryAsync(r.Scope.DocumentId,ct)).Single(h=>h.Id==r.OperationId);
        var current=await documents.LoadAsync(e.DocumentId,ct)??throw new IOException("Manuscript unavailable.");
        if(current.LocalRevision!=e.Before.LocalRevision || LocalTranslation.Signature(current)!=LocalTranslation.Signature(e.Before))throw new InvalidOperationException("Approved source changed. Recover a copy.");check();
        await documents.SaveAsync(e.After!,ct);await history.SaveHistoryAsync(e with {Status="Applied"},CancellationToken.None);return Receipt(e with {Status="Applied"});
    }
    public async Task<BrainstormingKeepReceipt> MoveAsync(BrainstormingKeepReview r,bool redo,CancellationToken ct=default)
    {
        var check=await sessions.RequireCurrentAsync(r.Scope,ct);check();await new LocalAiHistoryActions(documents,history,account,host).ChangeAsync(r.Scope.DocumentId,r.OperationId,redo,ct);check();return await InspectAsync(r,ct);
    }
    public async Task RecoverAsync(BrainstormingKeepReview r,CancellationToken ct=default)
    {
        var check=await sessions.RequireCurrentAsync(r.Scope,ct);var e=(await history.HistoryAsync(r.Scope.DocumentId,ct)).Single(h=>h.Id==r.OperationId);check();await documents.RecoverSnapshotAsync(e.Before,ct);
    }
    private static BrainstormingKeepReceipt Receipt(LocalAiHistory e)=>new(1,e.Id,e.Brainstorming!.Scope,e.Brainstorming.Candidate.Id,BrainstormingKeepContract.Hash(e.Brainstorming),e.Status=="Undone"?BrainstormingApplicationState.Undone:e.Status=="Applied"?BrainstormingApplicationState.Committed:BrainstormingApplicationState.Unknown,e.Id,e.Status=="Undone"?2:1,e.Status=="Applied"?"Confirmed atomic local writing save; server writing is a separate synchronization operation.":e.Status,DateTimeOffset.UtcNow);
}
