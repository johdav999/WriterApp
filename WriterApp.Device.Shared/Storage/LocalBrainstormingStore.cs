using System.Text.Json;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Device.Shared.Storage;

public sealed class LocalBrainstormingStore : IBrainstormingCacheStore
{
    private readonly string root;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly AtomicDocumentWriter writer;
    public LocalBrainstormingStore(string root) : this(root, new AtomicDocumentWriter()) { }
    internal LocalBrainstormingStore(string root, AtomicDocumentWriter writer) { this.root = Path.GetFullPath(root); this.writer = writer; }
    private string PathFor(BrainstormingScope scope) => Path.Combine(root, BrainstormingPersistence.Partition(scope), scope.DocumentId.ToString("N"), scope.SessionId.ToString("N") + ".json");
    private string IdentityPath(string backend,string native) {
        if(backend.Length!=64 || native.Length!=64 || !backend.All(Uri.IsHexDigit) || !native.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid account cache key.");
        return Path.Combine(root,"backend-accounts",backend+"-"+native+".txt");
    }
    internal Task<string?> ReadBackendAccountAsync(string backend,string native,CancellationToken ct)=>Locked<string?>(async()=>{
        var path=IdentityPath(backend,native);if(!File.Exists(path))return null;
        if(new FileInfo(path).Length!=64)throw new InvalidDataException("Invalid backend account cache; retained.");
        var value=await File.ReadAllTextAsync(path,ct);if(!value.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid backend account cache; retained.");return value;
    },ct);
    internal Task RememberBackendAccountAsync(string backend,string native,string canonical,CancellationToken ct)=>Locked(async()=>{
        if(canonical.Length!=64 || !canonical.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid canonical account key.");
        var path=IdentityPath(backend,native);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await writer.WriteAsync(path,System.Text.Encoding.UTF8.GetBytes(canonical),ct);return true;
    },ct);
    internal Task CopyNativeDiscussionAsync(BrainstormingScope canonical,string native,Action check,CancellationToken ct)=>Locked(async()=>{
        check();var source=canonical with {AccountKey=native};var directory=Path.GetDirectoryName(PathFor(source))!;
        if(!Directory.Exists(directory))return false;
        var paths=Directory.EnumerateFiles(directory,"*.json").Take(1001).ToArray();if(paths.Length>1000)throw new InvalidDataException("Native conversation listing exceeds its limit; originals retained.");
        foreach(var path in paths) {
            check();if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out var id))throw new InvalidDataException("Unknown native conversation filename; retained.");
            var old=await ReadFile(source with{SessionId=id},ct,preserveProject:true);if(old is null || old.Session.State==BrainstormingConversationState.Deleted)continue;
            var next=old.Session.Scope with{AccountKey=canonical.AccountKey};var destination=PathFor(next);if(File.Exists(destination))continue;
            // Originals, receipts, mappings and pending operations are retained in their native namespace.
            var discussion=BrainstormingPersistence.ReScope(old.Session,next) with {Revision=1,State=BrainstormingConversationState.LocalOnly,Approvals=[],Applications=[],
                Requests=old.Session.Requests.Select(r=>r with{Scope=next,ExpectedSessionRevision=1,State=r.State is BrainstormingGenerationState.Accepted or BrainstormingGenerationState.Streaming or BrainstormingGenerationState.Unknown?BrainstormingGenerationState.Interrupted:r.State}).ToArray()};
            var copy=new BrainstormingCache(1,1,discussion,old.Composer,null,null,null,[],[],DraftReview:old.DraftReview,DraftEdit:old.DraftEdit);
            BrainstormingPersistence.Validate(copy);check();Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await writer.WriteAsync(destination,System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(copy,BrainstormingValidation.Json),ct);check();
        }
        return true;
    },ct);
    private async Task<T> Locked<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { Directory.CreateDirectory(root); await using var fileLock = new FileStream(Path.Combine(root, ".store.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            ct.ThrowIfCancellationRequested(); return await work(); }
        finally { gate.Release(); }
    }
    public Task<Guid> LocalOwnerAsync(CancellationToken ct = default) => Locked(async () => {
        var path = Path.Combine(root, "local-owner.json");
        if (File.Exists(path)) {
            if (new FileInfo(path).Length > 100) throw new InvalidDataException("Local conversation owner file is invalid; preserved.");
            var existing = JsonSerializer.Deserialize<Guid>(await File.ReadAllTextAsync(path, ct));
            if (existing == Guid.Empty) throw new InvalidDataException("Local conversation owner is invalid; preserved."); return existing;
        }
        var id = Guid.NewGuid(); await writer.WriteAsync(path, JsonSerializer.SerializeToUtf8Bytes(id), ct); return id;
    }, ct);
    private async Task<BrainstormingCache?> ReadFile(BrainstormingScope scope, CancellationToken ct, bool preserveProject = false)
    {
        var path = PathFor(scope); if (!File.Exists(path)) return null;
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (file.Length > BrainstormingPersistence.MaxCacheBytes) throw new InvalidDataException("Conversation file is oversized; original retained.");
        var value = await JsonSerializer.DeserializeAsync<BrainstormingCache>(file, BrainstormingValidation.Json, ct)
            ?? throw new InvalidDataException("Missing conversation cache; original retained.");
        BrainstormingPersistence.Validate(value); BrainstormingValidation.RequireScope(value.Session.Scope, preserveProject ? scope with { ProjectId = value.Session.Scope.ProjectId } : scope); return value;
    }
    public Task<BrainstormingCache?> ReadAsync(BrainstormingScope scope, CancellationToken ct = default) => Locked(() => ReadFile(scope, ct), ct);
    public Task<BrainstormingCache> WriteAsync(BrainstormingCache value, long expectedRevision, Guid operationId, CancellationToken ct = default) => Locked(async () => {
        var current = await ReadFile(value.Session.Scope, ct); BrainstormingCache prepared;
        try { prepared = BrainstormingPersistence.PrepareWrite(current, value, expectedRevision, operationId); }
        catch (BrainstormingConcurrencyException) {
            var directory = Path.Combine(Path.GetDirectoryName(PathFor(value.Session.Scope))!, "conflicts", value.Session.Scope.SessionId.ToString("N"));
            Directory.CreateDirectory(directory); var conflictPath = Path.Combine(directory, operationId.ToString("N") + ".json");
            if (!File.Exists(conflictPath)) { if (Directory.EnumerateFiles(directory, "*.json").Take(11).Count() >= 10) throw new InvalidDataException("Local conflict archive is full; choose a new branch explicitly.");
                await writer.WriteAsync(conflictPath, JsonSerializer.SerializeToUtf8Bytes(new BrainstormingRejectedWrite(1, operationId, expectedRevision, value, DateTimeOffset.UtcNow), BrainstormingValidation.Json), ct); }
            throw;
        }
        if (ReferenceEquals(current, prepared)) return prepared;
        var path = PathFor(prepared.Session.Scope); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await writer.WriteAsync(path, JsonSerializer.SerializeToUtf8Bytes(prepared, BrainstormingValidation.Json), ct); return prepared;
    }, ct);
    public Task<IReadOnlyList<BrainstormingRejectedWrite>> ReadRejectedAsync(BrainstormingScope scope, CancellationToken ct = default) => Locked<IReadOnlyList<BrainstormingRejectedWrite>>(async () => {
        var directory = Path.Combine(Path.GetDirectoryName(PathFor(scope))!, "conflicts", scope.SessionId.ToString("N"));
        if (!Directory.Exists(directory)) return [];
        var result = new List<BrainstormingRejectedWrite>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").OrderBy(p => p).Take(10)) {
            if (new FileInfo(path).Length > BrainstormingPersistence.MaxCacheBytes + 1000) throw new InvalidDataException("Oversized retained conflict.");
            var item = JsonSerializer.Deserialize<BrainstormingRejectedWrite>(await File.ReadAllTextAsync(path, ct), BrainstormingValidation.Json)
                ?? throw new InvalidDataException("Invalid retained conflict.");
            if (item.Version != 1 || item.OperationId == Guid.Empty) throw new InvalidDataException("Unsupported retained conflict.");
            BrainstormingPersistence.Validate(item.Proposed); BrainstormingValidation.RequireScope(item.Proposed.Session.Scope, scope); result.Add(item);
        } return result;
    }, ct);
    public Task<IReadOnlyList<BrainstormingSummary>> ListAsync(BrainstormingScope scope, Guid? after = null, int limit = 25, CancellationToken ct = default) => Locked<IReadOnlyList<BrainstormingSummary>>(async () => {
        if (limit is < 1 or > 50) throw new InvalidDataException("Use a page size of 1–50.");
        var directory = Path.GetDirectoryName(PathFor(scope))!; if (!Directory.Exists(directory)) return [];
        var files = Directory.EnumerateFiles(directory, "*.json").Take(1001).ToArray(); if (files.Length > 1000) throw new InvalidDataException("Conversation directory exceeds its supported listing limit; files retained.");
        var ids = files.Select(p => Guid.TryParseExact(Path.GetFileNameWithoutExtension(p), "N", out var id) ? id : throw new InvalidDataException("Unknown conversation filename; retained."))
            .Where(id => after is null || id.CompareTo(after.Value) > 0).OrderBy(id => id).Take(limit);
        var summaries = new List<BrainstormingSummary>();
        foreach (var id in ids) { var value = await ReadFile(scope with { SessionId = id }, ct, preserveProject: true); if (value is not null) summaries.Add(new(value.Session.Scope, value.Session.Revision, value.Session.State, value.Session.UpdatedAt)); }
        return summaries;
    }, ct);
}
