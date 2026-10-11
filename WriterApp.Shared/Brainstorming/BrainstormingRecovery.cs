namespace WriterApp.Shared.Brainstorming;

// Original namespace and receipt origin are retained. A client copy is never server commit evidence.
public sealed record BrainstormingRecoveryCopy(int Version,string Id,BrainstormingMapping Mapping,
    IReadOnlyList<BrainstormingKeepJournal> Keeps,IReadOnlyList<BrainstormingPlanningJournal> Planning,BrainstormingDraftEdit? DraftEdit);
public sealed record BrainstormingRecoveryBundle(int Version,BrainstormingScope Scope,
    IReadOnlyList<BrainstormingRecoveryCopy> Copies,IReadOnlyList<BrainstormingKeepJournal> ServerKeeps,
    IReadOnlyList<BrainstormingPlanningJournal> ServerPlanning);
public interface IBrainstormingRecoveryRemote {
    Task<BrainstormingRecoveryBundle> ExchangeRecoveryAsync(BrainstormingScope scope,BrainstormingRecoveryCopy? copy,CancellationToken ct=default);
}
public static class BrainstormingRecovery {
    public const int MaxBytes=6_000_000,MaxCopies=100;
    private static string Id(BrainstormingRecoveryCopy c)=>BrainstormingPersistence.Hash(c with {Id="",Mapping=c.Mapping with {Revision=1,UpdatedAt=DateTimeOffset.UnixEpoch}});
    public static BrainstormingRecoveryCopy? Copy(BrainstormingCache cache) {
        if(cache.Mapping is null || (cache.Keeps?.Count??0)+(cache.PlanningOperations?.Count??0)==0 && cache.DraftEdit is null)return null;
        var copy=new BrainstormingRecoveryCopy(1,"",cache.Mapping,cache.Keeps??[],cache.PlanningOperations??[],cache.DraftEdit);
        return copy with {Id=Id(copy)};
    }
    public static void Validate(BrainstormingRecoveryCopy c,BrainstormingScope server) {
        if(c is null || c.Version!=1 || c.Mapping is null || c.Keeps is null || c.Planning is null || c.Keeps.Count>500 || c.Planning.Count>500 || c.Id!=Id(c))throw new InvalidDataException("Invalid recovery copy.");
        BrainstormingValidation.Validate(c.Mapping);BrainstormingValidation.RequireScope(c.Mapping.Server,server);
        if(c.Mapping.Local.AccountKey is null)throw new InvalidDataException("Accountless recovery cannot attach to an authenticated session.");
        foreach(var j in c.Keeps) {BrainstormingKeepContract.Validate(j.Review);if(j.Review.Scope!=server && j.Review.Scope!=c.Mapping.Local)throw new InvalidDataException("Recovery ownership changed.");if(j.Receipt is { } r)BrainstormingKeepContract.RequireReceipt(j.Review,r);}
        foreach(var j in c.Planning) {BrainstormingPlanningContract.Validate(j.Review);if(j.Review.Scope!=server && j.Review.Scope!=c.Mapping.Local)throw new InvalidDataException("Recovery ownership changed.");if(j.Receipt is { } r)BrainstormingPlanningContract.RequireReceipt(j.Review,r);}
        if(c.DraftEdit is { } edit && (edit.Version!=1 || edit.CandidateId==Guid.Empty || edit.BaseFingerprint is not {Length:64} || edit.Html is null || edit.PlainText is null || edit.Html.Length>BrainstormingValidation.MaxTextCharacters || edit.PlainText.Length>BrainstormingValidation.MaxTextCharacters))throw new InvalidDataException("Invalid retained proposal edit.");
        if(System.Text.Encoding.UTF8.GetByteCount(BrainstormingPersistence.Json(c))>MaxBytes)throw new InvalidDataException("Recovery copy exceeds its storage bound; original device journal retained.");
    }
    public static void Validate(BrainstormingRecoveryBundle b,BrainstormingScope server) {
        if(b is null || b.Version!=1 || b.Copies is null || b.ServerKeeps is null || b.ServerPlanning is null || b.Copies.Count>MaxCopies || b.ServerKeeps.Count>500 || b.ServerPlanning.Count>500 || b.Copies.Select(c=>c.Id).Distinct().Count()!=b.Copies.Count)throw new InvalidDataException("Invalid recovery history.");
        BrainstormingValidation.RequireScope(b.Scope,server);foreach(var c in b.Copies)Validate(c,server);
        foreach(var j in b.ServerKeeps){BrainstormingKeepContract.Validate(j.Review);BrainstormingValidation.RequireScope(j.Review.Scope,server);if(j.Receipt is { } r)BrainstormingKeepContract.RequireReceipt(j.Review,r);}
        foreach(var j in b.ServerPlanning){BrainstormingPlanningContract.Validate(j.Review);BrainstormingValidation.RequireScope(j.Review.Scope,server);if(j.Receipt is { } r)BrainstormingPlanningContract.RequireReceipt(j.Review,r);}
        if(System.Text.Encoding.UTF8.GetByteCount(BrainstormingPersistence.Json(b))>MaxBytes)throw new InvalidDataException("Recovery history exceeds its bounded response; original journals are retained.");
    }
    public static async Task<BrainstormingCache> ExchangeAsync(IBrainstormingCacheStore store,IBrainstormingRemote remote,BrainstormingScope local,Action check,CancellationToken ct) {
        var current=await store.ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");
        if(remote is not IBrainstormingRecoveryRemote recovery || current.Mapping is null)return current;
        check();var bundle=await recovery.ExchangeRecoveryAsync(current.Mapping.Server,Copy(current),ct);check();Validate(bundle,current.Mapping.Server);
        var latest=await store.ReadAsync(local,ct)??throw new InvalidDataException("Conversation unavailable.");if(latest.Mapping?.Server!=bundle.Scope)throw new OperationCanceledException("Recovery mapping changed.");
        return await store.WriteAsync(latest with {Recovery=bundle},latest.CacheRevision,Guid.NewGuid(),ct);
    }
}
