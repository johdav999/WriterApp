using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Sync;

namespace WriterApp.Device.Shared.Services;

public static class LocalBrainstormingPlanning
{
    // Standalone manuscripts retain the same authored synopsis model in the lossless extension envelope.
    // Sync v5 transfers this authored value explicitly; local receipts remain separate from server saves.
    private const string StandaloneKey="authoredSynopsis";
    public static SyncSynopsis Synopsis(LocalDocument d)=>d.Project?.Synopsis??(d.ExtensionData?.TryGetValue(StandaloneKey,out var value)==true?value.Deserialize<SyncSynopsis>(BrainstormingValidation.Json):null)??new();
    public static BrainstormingPlanningSnapshot Capture(LocalDocument d,BrainstormingScope scope) {
        if(d.DocumentId!=scope.DocumentId || d.Project?.ProjectId!=scope.ProjectId || d.DeletedAtUtc!=null || d.SyncState==LocalSyncState.Conflict)throw new InvalidOperationException("Select the original active manuscript and resolve its conflict.");
        var fingerprint=BrainstormingPersistence.Hash(new {Writing=LocalBrainstormingWriting.Capture(d),Synopsis=Synopsis(d),Nodes=d.Project?.Nodes});
        return new(1,scope,new(d.LocalRevision.ToString(),d.LocalRevision.ToString(),fingerprint),Synopsis(d),
            (d.Project?.Nodes??[]).Where(n=>n.NodeType=="chapter" && n.DeletionId==null).Select(n=>new BrainstormingPlanningDestination(n.NodeId,n.Title,n.NodeType,n.ParentId,n.OrderIndex,
                d.Project!.Nodes.Where(c=>c.ParentId==n.NodeId && c.DeletionId==null).Select(c=>c.OrderIndex).DefaultIfEmpty(-1).Max()+1)).ToArray());
    }
    public static LocalDocument Apply(LocalDocument d,BrainstormingPlanningReview r,bool undo=false) {
        BrainstormingPlanningContract.Validate(r);var capture=Capture(d,r.Scope);
        if(!undo && capture.Source!=r.Source)throw new InvalidOperationException("The planning source changed. Refresh and review again.");
        if(r.Kind==BrainstormingPlanningKind.SceneIdea) {
            var project=d.Project??throw new InvalidOperationException("Scene ideas require an owned project chapter.");
            if(!project.Nodes.Any(n=>n.NodeId==r.ParentId && n.NodeType=="chapter" && n.DeletionId==null))throw new InvalidOperationException("The chapter is unavailable.");
            var node=new LocalProjectNode {NodeId=r.NewNodeId,ParentId=r.ParentId,NodeType="scene",Title=r.Fields.Single(f=>f.Field=="Title").After,
                OrderIndex=r.NewNodeOrder,
                MetadataJson=BrainstormingPlanningContract.SceneIdeaMetadata,Notes=r.Fields.SingleOrDefault(f=>f.Field=="Notes")?.After,Card=r.Fields.SingleOrDefault(f=>f.Field=="Summary") is { } summary?LocalPlanning.EmptyCard with {Summary=summary.After}:null};
            if(!undo) {
                if(project.Nodes.Count>=1000 || project.Nodes.Any(n=>n.NodeId==r.NewNodeId || n.ParentId==r.ParentId && n.DeletionId==null && n.OrderIndex==r.NewNodeOrder))throw new InvalidOperationException("Scene identity or position occupied, or outline limit reached.");
                return LocalPlanning.SceneIdea(d,node);
            }
            var actual=project.Nodes.SingleOrDefault(n=>n.NodeId==r.NewNodeId);
            if(actual is null || actual.DeletionId!=null || actual.SectionId!=null || actual.ParentId!=node.ParentId || actual.OrderIndex!=r.NewNodeOrder || actual.Title!=node.Title || actual.Notes!=node.Notes
                || actual.Card!=node.Card || actual.MetadataJson!=node.MetadataJson || actual.Annotations.Count!=0 || actual.ExtensionData is {Count:>0} || project.Nodes.Any(n=>n.ParentId==r.NewNodeId))
                throw new InvalidOperationException("Scene has later writing or planning. Keep it and copy recovery fields.");
            return d with {Project=project with {Nodes=project.Nodes.Where(n=>n.NodeId!=r.NewNodeId).ToArray()}};
        }
        var synopsis=capture.Synopsis;
        foreach(var field in r.Fields) {
            if(BrainstormingPlanningContract.Value(synopsis,field.Field)!=(undo?field.After:field.Before))throw new InvalidOperationException("An approved field changed. Keep later planning and copy its original value.");
            synopsis=BrainstormingPlanningContract.Set(synopsis,field.Field,undo?field.Before:field.After);
        }
        return LocalPlanning.Synopsis(d,synopsis);
    }
    internal static string MarkerKey(BrainstormingPlanningReview r)=>"planningSave_"+BrainstormingPersistence.Hash(new {r.Scope,r.OperationId});
    internal static LocalDocument Mark(LocalDocument d,BrainstormingPlanningReview r,BrainstormingApplicationState state) {
        var extensions=d.ExtensionData is null?new Dictionary<string,JsonElement>():new(d.ExtensionData);
        if(!extensions.ContainsKey(MarkerKey(r)) && extensions.Keys.Count(k=>k.StartsWith("planningSave_",StringComparison.Ordinal))>=500)throw new InvalidOperationException("Planning receipt capacity reached; retain journals for review.");
        extensions[MarkerKey(r)]=JsonSerializer.SerializeToElement(new {Hash=BrainstormingPlanningContract.Hash(r),State=state},BrainstormingValidation.Json);return d with {ExtensionData=extensions};
    }
    internal static BrainstormingApplicationState? State(LocalDocument d,BrainstormingPlanningReview r) {
        if(d.ExtensionData?.TryGetValue(MarkerKey(r),out var marker)!=true)return null;
        if(marker.GetProperty("hash").GetString()!=BrainstormingPlanningContract.Hash(r))throw new InvalidDataException("Planning approval identity changed.");
        var state=(BrainstormingApplicationState)marker.GetProperty("state").GetInt32();if(state is not (BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone))throw new InvalidDataException("Invalid atomic planning receipt.");return state;
    }
}

public sealed class LocalBrainstormingPlanningSaves(LocalDocumentRepository documents,DeviceBrainstormingSessions sessions)
{
    private readonly SemaphoreSlim gate=new(1,1);
    public async Task<BrainstormingPlanningSnapshot> CaptureAsync(BrainstormingScope scope) {
        var check=await sessions.RequireCurrentAsync(scope);var d=await documents.LoadAsync(scope.DocumentId)??throw new IOException("Manuscript unavailable.");check();return LocalBrainstormingPlanning.Capture(d,scope);
    }
    private static BrainstormingPlanningReceipt Receipt(BrainstormingPlanningReview r,BrainstormingApplicationState state)=>new(1,r.OperationId,r.Scope,BrainstormingPlanningContract.Hash(r),state,DateTimeOffset.UtcNow,
        state==BrainstormingApplicationState.Failed?"No atomic local receipt exists. Explicit retry requires the unchanged approved source.":state==BrainstormingApplicationState.Undone?"Approved local planning restored.":"Confirmed atomic local planning save. Refresh context; server writing receipt remains separate.");
    public async Task<BrainstormingPlanningReceipt> InspectAsync(BrainstormingPlanningReview r) {
        BrainstormingPlanningContract.Validate(r);var check=await sessions.RequireCurrentAsync(r.Scope);var d=await documents.LoadAsync(r.Scope.DocumentId)??throw new IOException("Original manuscript unavailable.");check();
        if(d.DeletedAtUtc!=null || d.Project?.ProjectId!=r.Scope.ProjectId)throw new InvalidOperationException("Original planning destination unavailable.");
        return Receipt(r,LocalBrainstormingPlanning.State(d,r)??BrainstormingApplicationState.Failed);
    }
    public async Task<BrainstormingPlanningReceipt> ApplyAsync(BrainstormingPlanningReview r) {
        await gate.WaitAsync();try {
            var check=await sessions.RequireCurrentAsync(r.Scope);var cache=await sessions.ReadAsync(r.Scope)??throw new InvalidDataException("Conversation unavailable.");
            BrainstormingPlanningContract.Provenance(cache.Session,r);
            if(cache.PendingGeneration!=null || cache.DraftEdit!=null || cache.Pending!=null || cache.Conflicts.Count!=0)throw new InvalidOperationException("Settle conversation recovery before planning approval.");
            var d=await documents.LoadAsync(r.Scope.DocumentId)??throw new IOException("Manuscript unavailable.");check();
            if(LocalBrainstormingPlanning.State(d,r) is { } state)return Receipt(r,state);
            var intent=(cache.PlanningOperations??[]).SingleOrDefault(p=>p.Review.OperationId==r.OperationId);
            if(intent is null || BrainstormingPlanningContract.Hash(intent.Review)!=BrainstormingPlanningContract.Hash(r))throw new InvalidDataException("Persist the exact approval before local mutation.");
            var after=LocalBrainstormingPlanning.Mark(LocalBrainstormingPlanning.Apply(d,r),r,BrainstormingApplicationState.Committed);check();
            await documents.SaveAsync(after);check();return Receipt(r,BrainstormingApplicationState.Committed);
        }finally{gate.Release();}
    }
    public async Task<BrainstormingPlanningReceipt> UndoAsync(BrainstormingPlanningReview r) {
        await gate.WaitAsync();try {
            var check=await sessions.RequireCurrentAsync(r.Scope);var d=await documents.LoadAsync(r.Scope.DocumentId)??throw new IOException("Manuscript unavailable.");check();
            var state=LocalBrainstormingPlanning.State(d,r);if(state==BrainstormingApplicationState.Undone)return Receipt(r,state.Value);
            if(state!=BrainstormingApplicationState.Committed)throw new InvalidOperationException("Inspect and confirm this planning save before Undo.");
            var after=LocalBrainstormingPlanning.Mark(LocalBrainstormingPlanning.Apply(d,r,true),r,BrainstormingApplicationState.Undone);check();await documents.SaveAsync(after);check();return Receipt(r,BrainstormingApplicationState.Undone);
        }finally{gate.Release();}
    }
}
