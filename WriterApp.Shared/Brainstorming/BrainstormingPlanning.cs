using WriterApp.Shared.Sync;

namespace WriterApp.Shared.Brainstorming;

public sealed record BrainstormingPlanningDestination(Guid Id, string Title, string Kind, Guid? ParentId, int Order,int NextChildOrder=0);
public sealed record BrainstormingPlanningSnapshot(int Version, BrainstormingScope Scope, BrainstormingSourceStamp Source,
    SyncSynopsis Synopsis, IReadOnlyList<BrainstormingPlanningDestination> Chapters);
public sealed record BrainstormingPlanningField(string Field, string Before, string After);
public sealed record BrainstormingPlanningReview(int Version, Guid OperationId, Guid CandidateId, BrainstormingScope Scope,
    Guid TurnId, Guid ContextId, Guid? DraftId, BrainstormingPlanningKind Kind, Guid? ParentId, Guid NewNodeId,
    BrainstormingSourceStamp Source, IReadOnlyList<BrainstormingPlanningField> Fields, DateTimeOffset ReviewedAt,int NewNodeOrder=0);
public sealed record BrainstormingPlanningReceipt(int Version, Guid OperationId, BrainstormingScope Scope, string ReviewHash,
    BrainstormingApplicationState State, DateTimeOffset UpdatedAt, string Message);
public sealed record BrainstormingPlanningJournal(BrainstormingPlanningReview Review, BrainstormingPlanningReceipt? Receipt);

public static class BrainstormingPlanningContract
{
    public const string SceneIdeaMetadata="{\"brainstormingSceneIdea\":true}";
    public static bool IsSceneIdea(string? metadata) {
        try {using var json=System.Text.Json.JsonDocument.Parse(metadata??"{}");return json.RootElement.TryGetProperty("brainstormingSceneIdea",out var flag)&&flag.ValueKind==System.Text.Json.JsonValueKind.True;}
        catch(System.Text.Json.JsonException){return false;}
    }
    public static readonly string[] SynopsisFields = ["Logline", "Premise", "Theme", "ProtagonistArc", "CentralConflict", "Stakes", "Setting", "EndingIntent", "OpenQuestions", "Notes"];
    public static string Value(SyncSynopsis synopsis, string field) => field switch {
        "Logline"=>synopsis.Logline,"Premise"=>synopsis.Premise,"Theme"=>synopsis.Theme,"ProtagonistArc"=>synopsis.ProtagonistArc,
        "CentralConflict"=>synopsis.CentralConflict,"Stakes"=>synopsis.Stakes,"Setting"=>synopsis.Setting,"EndingIntent"=>synopsis.EndingIntent,
        "OpenQuestions"=>synopsis.OpenQuestions,"Notes"=>synopsis.Notes,_=>throw new InvalidDataException("Unknown authored planning field.") };
    public static SyncSynopsis Set(SyncSynopsis s,string field,string value)=>field switch {
        "Logline"=>s with {Logline=value},"Premise"=>s with {Premise=value},"Theme"=>s with {Theme=value},"ProtagonistArc"=>s with {ProtagonistArc=value},
        "CentralConflict"=>s with {CentralConflict=value},"Stakes"=>s with {Stakes=value},"Setting"=>s with {Setting=value},"EndingIntent"=>s with {EndingIntent=value},
        "OpenQuestions"=>s with {OpenQuestions=value},"Notes"=>s with {Notes=value},_=>throw new InvalidDataException("Unknown authored planning field.") };
    public static void Validate(BrainstormingPlanningReview r) {
        if(r is null || r.Version!=1 || r.OperationId==Guid.Empty || r.CandidateId==Guid.Empty || r.TurnId==Guid.Empty || r.ContextId==Guid.Empty
            || r.NewNodeId==Guid.Empty || r.ParentId==Guid.Empty || r.DraftId==Guid.Empty || r.NewNodeOrder<0 || !Enum.IsDefined(r.Kind) || r.ReviewedAt< DateTimeOffset.UnixEpoch
            || r.ReviewedAt>DateTimeOffset.UtcNow.AddMinutes(1) || r.Fields is null || r.Fields.Count is <1 or >10 || r.Source is null)
            throw new InvalidDataException("Invalid planning approval.");
        BrainstormingValidation.Validate(r.Scope);
        if(string.IsNullOrEmpty(r.Source.ContentRevision) || r.Source.ContentRevision.Length>200 || string.IsNullOrEmpty(r.Source.StructureRevision)
            || r.Source.StructureRevision.Length>200 || r.Source.Fingerprint is not {Length:64} || !r.Source.Fingerprint.All(Uri.IsHexDigit)
            || r.Fields.Select(f=>f?.Field).Distinct().Count()!=r.Fields.Count)throw new InvalidDataException("Invalid planning source or fields.");
        foreach(var f in r.Fields) {
            if(f is null || f.Before is null || f.After is null || f.Before.Length>100_000 || f.After.Length>100_000 || string.IsNullOrWhiteSpace(f.After) || f.Before==f.After
                || !(r.Kind==BrainstormingPlanningKind.SceneIdea ? f.Field is "Title" or "Summary" or "Notes" : r.Kind==BrainstormingPlanningKind.SynopsisField ? SynopsisFields.Contains(f.Field) : f.Field=="Notes"))
                throw new InvalidDataException("Approve only changed, supported authored planning fields.");
        }
        if(r.Kind==BrainstormingPlanningKind.SceneIdea && (r.Scope.ProjectId is null || r.ParentId is null || !r.Fields.Any(f=>f.Field=="Title")
            || r.Fields.Single(f=>f.Field=="Title").After.Length>200 || r.Fields.Any(f=>f.Before.Length!=0)))throw new InvalidDataException("Choose an owned chapter and title for the new scene idea.");
        if(r.Kind!=BrainstormingPlanningKind.SceneIdea && r.ParentId is not null)throw new InvalidDataException("Synopsis notes belong to the selected manuscript.");
        if(BrainstormingPersistence.Json(r.Fields.Select(f=>new {f.Field,f.After})).Length>BrainstormingValidation.MaxTextCharacters
            || BrainstormingPersistence.Json(r.Fields.Select(f=>new {f.Field,f.Before})).Length>BrainstormingValidation.MaxTextCharacters)
            throw new InvalidDataException("Selected planning fields exceed the bounded provenance record; review a smaller group.");
    }
    public static void Provenance(BrainstormingSession s,BrainstormingPlanningReview r) {
        Validate(r);BrainstormingValidation.RequireScope(s.Scope,r.Scope);
        var turn=s.Turns.SingleOrDefault(t=>t.Id==r.TurnId);
        if(s.State is BrainstormingConversationState.Deleted or BrainstormingConversationState.Conflict || turn?.State!=BrainstormingTurnState.Complete
            || turn.ContextId!=r.ContextId || !s.Contexts.Any(c=>c.Id==r.ContextId) || r.DraftId is { } draft && !s.Drafts.Any(d=>d.Id==draft && d.ContextId==r.ContextId)
            || s.Requests.Any(q=>q.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming or BrainstormingGenerationState.Unknown))
            throw new InvalidDataException("Choose a complete retained message and settle generation before planning approval.");
    }
    public static string Hash(BrainstormingPlanningReview r)=>BrainstormingPersistence.Hash(r);
    public static void RequireReceipt(BrainstormingPlanningReview r,BrainstormingPlanningReceipt receipt) {
        Validate(r);
        if(receipt.Version!=1 || receipt.OperationId!=r.OperationId || receipt.Scope!=r.Scope || receipt.ReviewHash!=Hash(r)
            || !Enum.IsDefined(receipt.State) || receipt.UpdatedAt==default || receipt.Message is null || receipt.Message.Length>2000)throw new InvalidDataException("Unconfirmed planning save; inspect the retained operation.");
    }
}

public sealed class BrainstormingPlanningWorkflow(IBrainstormingCacheStore store)
{
    public async Task<BrainstormingCache> RememberAsync(BrainstormingScope local,BrainstormingPlanningReview review,BrainstormingPlanningReceipt? receipt=null,CancellationToken ct=default) {
        BrainstormingPlanningContract.Validate(review);
        var cache=await store.ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");
        if(review.Scope!=local && review.Scope!=cache.Mapping?.Server)throw new InvalidDataException("Planning ownership changed.");
        var old=(cache.PlanningOperations??[]).SingleOrDefault(p=>p.Review.OperationId==review.OperationId || p.Review.CandidateId==review.CandidateId);
        if(old is not null && BrainstormingPlanningContract.Hash(old.Review)!=BrainstormingPlanningContract.Hash(review))throw new InvalidDataException("Inspect the original immutable approval.");
        if(receipt is not null)BrainstormingPlanningContract.RequireReceipt(review,receipt);
        return await store.WriteAsync(cache with {PlanningOperations=[..(cache.PlanningOperations??[]).Where(p=>p.Review.OperationId!=review.OperationId),new(review,receipt??old?.Receipt)]},cache.CacheRevision,Guid.NewGuid(),ct);
    }
    public async Task<BrainstormingCache> ApplyAsync(BrainstormingScope local,BrainstormingPlanningReview r,Func<BrainstormingPlanningReview,Task<BrainstormingPlanningReceipt>> apply,CancellationToken ct=default) {
        var cache=await RememberAsync(local,r,ct:ct);var state=cache.PlanningOperations!.Single(p=>p.Review.OperationId==r.OperationId).Receipt?.State;
        if(state is BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone)return cache;
        if(state==BrainstormingApplicationState.Unknown)throw new InvalidOperationException("Inspect the uncertain planning operation before retrying.");
        await RememberAsync(local,r,new(1,r.OperationId,r.Scope,BrainstormingPlanningContract.Hash(r),BrainstormingApplicationState.Unknown,DateTimeOffset.UtcNow,"Approved planning save awaiting inspection."),ct);
        return await RememberAsync(local,r,await apply(r),ct);
    }
}
