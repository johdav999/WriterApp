using System.Text.Json;

namespace WriterApp.Shared.Brainstorming;

public sealed record BrainstormingSourceSelection(Guid DocumentId, BrainstormingSourceKind Kind, string? EntityKey);
public sealed record BrainstormingWrite(int Version, Guid OperationId, long ExpectedRevision, BrainstormingSession Session);
public sealed record BrainstormingReceipt(int Version, Guid OperationId, BrainstormingScope Scope, long Revision, string PayloadHash, DateTimeOffset SavedAt);
public sealed record BrainstormingSummary(BrainstormingScope Scope, long Revision, BrainstormingConversationState State, DateTimeOffset UpdatedAt);
public sealed record BrainstormingSessionPage(int Version, IReadOnlyList<BrainstormingSummary> Items, Guid? NextAfter);
public sealed record BrainstormingTurnPage(int Version, BrainstormingScope Scope, long Revision, IReadOnlyList<BrainstormingTurn> Items, long? NextAfter);
public sealed record BrainstormingConflict(Guid OperationId, BrainstormingSession Local, BrainstormingSession Server, DateTimeOffset CapturedAt);
public sealed record BrainstormingCache(int Version, long CacheRevision, BrainstormingSession Session, string Composer,
    BrainstormingMapping? Mapping, BrainstormingWrite? Pending, BrainstormingReceipt? Acknowledgement,
    IReadOnlyList<BrainstormingConflict> Conflicts, IReadOnlyList<BrainstormingLocalReceipt> LocalReceipts, bool DeletionAcknowledged = false,
    BrainstormingSend? PendingGeneration = null, BrainstormingGeneration? LastGeneration = null,
    BrainstormingDraftReview? DraftReview = null, BrainstormingDraftEdit? DraftEdit = null,
    IReadOnlyList<BrainstormingKeepJournal>? Keeps = null,
    IReadOnlyList<BrainstormingPlanningJournal>? PlanningOperations = null,
    BrainstormingRecoveryBundle? Recovery = null,
    IReadOnlyList<BrainstormingDictationJournal>? Dictations = null);
public sealed record BrainstormingLocalReceipt(Guid OperationId, long Revision, string PayloadHash);
public sealed record BrainstormingRejectedWrite(int Version, Guid OperationId, long ExpectedRevision, BrainstormingCache Proposed, DateTimeOffset CapturedAt);
public sealed class BrainstormingConcurrencyException(BrainstormingSession? current = null) : Exception("The conversation changed. Both versions are retained for review.")
{ public BrainstormingSession? Current { get; } = current; }

public interface IBrainstormingCacheStore
{
    Task<BrainstormingCache?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default);
    Task<BrainstormingCache> WriteAsync(BrainstormingCache value, long expectedRevision, Guid operationId, CancellationToken ct = default);
    Task<IReadOnlyList<BrainstormingSummary>> ListAsync(BrainstormingScope documentScope, Guid? after = null, int limit = 25, CancellationToken ct = default);
    Task<IReadOnlyList<BrainstormingRejectedWrite>> ReadRejectedAsync(BrainstormingScope scope, CancellationToken ct = default);
}
public interface IBrainstormingRemote
{
    Task<BrainstormingSession?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default);
    Task<BrainstormingReceipt> WriteAsync(BrainstormingWrite value, CancellationToken ct = default);
    Task<BrainstormingSessionPage> ListAsync(Guid documentId, Guid? after = null, int limit = 25, CancellationToken ct = default);
    Task<BrainstormingReceipt?> InspectAsync(BrainstormingScope scope, Guid operationId, CancellationToken ct = default);
}
public static class BrainstormingPersistence
{
    public const int MaxCacheBytes = 8_000_000, MaxLocalReceipts = 10_000;
    public static string Json<T>(T value) => JsonSerializer.Serialize(value, BrainstormingValidation.Json);
    public static string Hash<T>(T value) => BrainstormingValidation.Fingerprint(Json(value));
    public static BrainstormingSession Empty(BrainstormingScope scope)
    {
        BrainstormingValidation.Validate(scope); var now = DateTimeOffset.UtcNow;
        return new(1, scope, 1, BrainstormingConversationState.LocalOnly, now, now, [], [], [], [], [], [], [], []);
    }
    public static string Partition(BrainstormingScope scope)
    {
        BrainstormingValidation.Validate(scope);
        return Hash(new { scope.BackendKey, scope.AccountKey, scope.LocalOwnerId, scope.Space });
    }
    public static void Validate(BrainstormingWrite write)
    {
        if (write is null || write.Version != 1 || write.OperationId == Guid.Empty || write.ExpectedRevision < 0)
            throw new InvalidDataException("Invalid session write.");
        BrainstormingValidation.Validate(write.Session);
        if (write.Session.Scope.Space != BrainstormingIdentitySpace.Server || write.Session.Revision != write.ExpectedRevision + 1
            || write.Session.State is not (BrainstormingConversationState.Saved or BrainstormingConversationState.Deleted))
            throw new InvalidDataException("Session write needs the next server revision and an explicit saved/tombstone state.");
    }
    public static void RequireReceipt(BrainstormingWrite write, BrainstormingReceipt receipt)
    {
        Validate(write);
        if (receipt is null || receipt.Version != 1 || receipt.OperationId != write.OperationId || receipt.Scope != write.Session.Scope
            || receipt.Revision != write.Session.Revision || receipt.PayloadHash != Hash(write) || receipt.SavedAt == default)
            throw new InvalidDataException("Unconfirmed conversation save. Retain the operation and inspect its receipt before retrying.");
    }
    public static void Validate(BrainstormingCache value)
    {
        if (value is null || value.Version != 1 || value.CacheRevision < 1 || value.Composer is null || value.Composer.Length > 8_000
            || value.Conflicts is null || value.Conflicts.Count > 10 || value.LocalReceipts is null || value.LocalReceipts.Count > MaxLocalReceipts
            || value.LocalReceipts.Any(r => r is null || r.OperationId == Guid.Empty || r.Revision < 1 || r.PayloadHash is not {Length:64})
            || value.LocalReceipts.Select(r => r.OperationId).Distinct().Count() != value.LocalReceipts.Count)
            throw new InvalidDataException("Invalid or unsupported conversation cache. Its original file is preserved.");
        BrainstormingValidation.Validate(value.Session);
        if(value.Dictations is { } dictations) {
            if(dictations.Count>100 || dictations.Select(d=>d.OperationId).Distinct().Count()!=dictations.Count)throw new InvalidDataException("Dictation history is full or invalid; preserve it and start a new conversation.");
            foreach(var d in dictations){if(d.OperationId==Guid.Empty)throw new InvalidDataException("Missing dictation identity.");BrainstormingSpeech.RequireLanguage(d.Language);if(d.Result is { } r){BrainstormingSpeech.Validate(r,value.Mapping?.Server??throw new InvalidDataException("Dictation mapping unavailable."),d.OperationId);if(r.Language!=d.Language || d.Adopted && r.State!=BrainstormingSpeechState.Completed)throw new InvalidDataException("Invalid adopted dictation.");}else if(d.Adopted)throw new InvalidDataException("Missing dictated text.");}
        }
        BrainstormingDrafts.ValidateReview(value);
        if(value.Recovery is { } recovery)BrainstormingRecovery.Validate(recovery,value.Mapping?.Server??throw new InvalidDataException("Recovery mapping unavailable."));
        if(value.PlanningOperations is { } planning) {
            if(planning.Count>500 || planning.Select(p=>p.Review.OperationId).Distinct().Count()!=planning.Count || planning.Select(p=>p.Review.CandidateId).Distinct().Count()!=planning.Count)
                throw new InvalidDataException("Duplicate or oversized planning journal.");
            foreach(var item in planning) {
                BrainstormingPlanningContract.Validate(item.Review);
                if(item.Review.Scope!=value.Session.Scope && item.Review.Scope!=value.Mapping?.Server)throw new InvalidDataException("Planning ownership changed.");
                if(item.Receipt is { } receipt)BrainstormingPlanningContract.RequireReceipt(item.Review,receipt);
            }
        }
        if(value.Keeps is { } keeps) {
            if(keeps.Count>500 || keeps.Select(k=>k.Review.OperationId).Distinct().Count()!=keeps.Count
                || keeps.Select(k=>k.Review.Candidate.Id).Distinct().Count()!=keeps.Count)throw new InvalidDataException("Duplicate or oversized application journal.");
            foreach(var keep in keeps) {
                BrainstormingKeepContract.Validate(keep.Review);
                if(keep.Review.Scope!=value.Session.Scope && keep.Review.Scope!=value.Mapping?.Server)throw new InvalidDataException("Application ownership changed.");
                if(keep.Receipt is { } receipt)BrainstormingKeepContract.RequireReceipt(keep.Review,receipt);
            }
        }
        if(value.PendingGeneration is { } send) {
            BrainstormingConversationContract.Validate(send);
            if(value.Mapping?.Server!=send.Scope)throw new InvalidDataException("Pending generation mapping changed.");
        }
        if(value.LastGeneration is { } generation) {
            BrainstormingConversationContract.Validate(generation.Send);
            if(value.Mapping?.Server!=generation.Send.Scope || generation.InspectionText.Length>BrainstormingConversationContract.MaxOutput || generation.Events.Count>BrainstormingConversationContract.MaxEvents)
                throw new InvalidDataException("Invalid cached generation.");
        }
        if (value.DeletionAcknowledged && (value.Session.State != BrainstormingConversationState.Deleted || value.Acknowledgement is null))
            throw new InvalidDataException("Deletion acknowledgement requires a retained tombstone and receipt.");
        if (value.Mapping is { } mapping)
        {
            BrainstormingValidation.Validate(mapping); BrainstormingValidation.RequireScope(mapping.Local, value.Session.Scope);
            if (value.Acknowledgement is { } ack && (ack.Version != 1 || ack.Scope != mapping.Server || ack.Revision < 1 || ack.PayloadHash is not {Length:64}))
                throw new InvalidDataException("Invalid mapped acknowledgement.");
        }
        else if (value.Pending is not null || value.Acknowledgement is not null) throw new InvalidDataException("Unmapped cache has cloud operations.");
        if (value.Pending is { } pending)
        { Validate(pending); BrainstormingValidation.RequireScope(pending.Session.Scope, value.Mapping!.Server); }
        foreach (var conflict in value.Conflicts)
        {
            if (conflict is null || conflict.OperationId == Guid.Empty || conflict.CapturedAt == default) throw new InvalidDataException("Invalid conflict.");
            BrainstormingValidation.Validate(conflict.Local); BrainstormingValidation.Validate(conflict.Server);
            BrainstormingValidation.RequireScope(conflict.Local.Scope, value.Session.Scope);
            if (value.Mapping is null) throw new InvalidDataException("Conflict has no mapping.");
            BrainstormingValidation.RequireScope(conflict.Server.Scope, value.Mapping.Server);
        }
        if (System.Text.Encoding.UTF8.GetByteCount(Json(value)) > MaxCacheBytes) throw new InvalidDataException("Conversation recovery cache is full. Archive explicitly; nothing was removed.");
    }
    // Same operation/payload deduplicates even after later writes. A new operation uses CAS.
    public static BrainstormingCache PrepareWrite(BrainstormingCache? current, BrainstormingCache incoming, long expected, Guid operationId)
    {
        Validate(incoming); if (operationId == Guid.Empty) throw new InvalidDataException("Missing local operation identity.");
        if (current is not null) BrainstormingValidation.RequireScope(current.Session.Scope, incoming.Session.Scope);
        var hash = Hash(incoming with { CacheRevision = expected + 1, LocalReceipts = [] });
        var receipt = current?.LocalReceipts.SingleOrDefault(r => r.OperationId == operationId);
        if (receipt is not null)
        { if (receipt.PayloadHash != hash) throw new InvalidDataException("An operation cannot change its payload."); return current!; }
        if ((current?.CacheRevision ?? 0) != expected) throw new BrainstormingConcurrencyException(current?.Session);
        if (current?.Mapping is { } mapped && incoming.Mapping?.Server != mapped.Server)
            throw new InvalidDataException("An established session mapping cannot be replaced.");
        if (current?.Session.State == BrainstormingConversationState.Deleted && incoming.Session.State != BrainstormingConversationState.Deleted)
            throw new InvalidDataException("Deleted sessions retain their tombstone. Fork explicitly.");
        if (current?.Pending is { } outbox) {
            if (incoming.Pending is { } replacement && Json(outbox) != Json(replacement))
                throw new InvalidDataException("Pending operation payload is immutable until acknowledged.");
            if (incoming.Pending is null) RequireReceipt(outbox, incoming.Acknowledgement
                ?? throw new InvalidDataException("A pending operation requires its exact acknowledgement."));
        }
        if(current?.PendingGeneration is { } generation && incoming.PendingGeneration is { } replacementGeneration && generation!=replacementGeneration)
            throw new InvalidDataException("Generation identity is immutable until its outcome is inspected.");
        foreach(var keep in current?.Keeps??[])if(!(incoming.Keeps??[]).Any(k=>BrainstormingKeepContract.Hash(k.Review)==BrainstormingKeepContract.Hash(keep.Review)))
            throw new InvalidDataException("Approved application journal and recovery source cannot be removed or changed.");
        foreach(var planning in current?.PlanningOperations??[])if(!(incoming.PlanningOperations??[]).Any(p=>BrainstormingPlanningContract.Hash(p.Review)==BrainstormingPlanningContract.Hash(planning.Review)))
            throw new InvalidDataException("Approved planning journal and recovery source cannot be removed or changed.");
        foreach(var copy in current?.Recovery?.Copies??[])if(!(incoming.Recovery?.Copies??[]).Any(c=>c.Id==copy.Id))throw new InvalidDataException("Retained recovery copies cannot be removed by a stale response.");
        foreach(var d in current?.Dictations??[]) {
            var next=(incoming.Dictations??[]).SingleOrDefault(n=>n.OperationId==d.OperationId);
            if(next is null || next.Language!=d.Language || d.Adopted && (!next.Adopted || BrainstormingPersistence.Json(next.Result)!=BrainstormingPersistence.Json(d.Result)))throw new InvalidDataException("Retained dictation identity/adopted transcript cannot be removed by a stale write.");
        }
        if (current?.Pending is { } queued) foreach (var turn in queued.Session.Turns.Where(t => t.Role == BrainstormingRole.User))
            if (!incoming.Session.Turns.Any(t => t.Id == turn.Id && t.OperationId == turn.OperationId && t.Text == turn.Text && t.Sequence == turn.Sequence))
                throw new InvalidDataException("Queued message text/order is immutable. Retain or branch the operation before changing it.");
        if (current?.LocalReceipts.Count == MaxLocalReceipts) throw new InvalidDataException("Conversation operation ledger is full. Archive explicitly; receipts were retained.");
        if (current?.Mapping is not null && incoming.Session.State == BrainstormingConversationState.Saved
            && incoming.Acknowledgement == current.Acknowledgement && Json(incoming.Session) != Json(current.Session))
            incoming = incoming with { Session = incoming.Session with { State = BrainstormingConversationState.PendingSave } };
        var result = incoming with { CacheRevision = expected + 1, LocalReceipts = [..current?.LocalReceipts ?? [], new(operationId, expected + 1, hash)] };
        Validate(result); return result;
    }
    public static BrainstormingSession ReScope(BrainstormingSession s, BrainstormingScope scope) => s with
    {
        Scope = scope, Turns = s.Turns.Select(x => x with { Scope = scope }).ToArray(),
        Contexts = s.Contexts.Select(x => x with { Scope = scope }).ToArray(), Requests = s.Requests.Select(x => x with { Scope = scope }).ToArray(),
        Drafts = s.Drafts.Select(x => x with { Scope = scope }).ToArray(), Planning = s.Planning.Select(x => x with { Scope = scope }).ToArray(),
        Approvals = s.Approvals.Select(x => x with { Scope = scope }).ToArray(), Applications = s.Applications.Select(x => x with { Scope = scope }).ToArray()
    };
}
