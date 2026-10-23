using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Device.Shared.Storage;

/// <summary>A checkpoint and its immutable outbox share one durable atomic replacement.</summary>
public sealed class DeviceCollaborationJournal
{
    private readonly string _root;
    private readonly AtomicDocumentWriter _writer;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private sealed record FileRecord(string Payload, string Sha256);
    public DeviceCollaborationJournal(string root) : this(root, new AtomicDocumentWriter()) { }
    internal DeviceCollaborationJournal(string root, AtomicDocumentWriter writer) { _root = Path.GetFullPath(root); _writer = writer; }

    public static CollaborationJournalScope Normalize(CollaborationJournalScope scope)
    {
        if (!Uri.TryCreate(scope.Backend, UriKind.Absolute, out var url) || (url.Scheme != "https" && !(url.Scheme == "http" && url.IsLoopback))
            || url.UserInfo.Length != 0 || url.Query.Length != 0 || url.Fragment.Length != 0
            || string.IsNullOrWhiteSpace(scope.OwnerId) || scope.OwnerId.Length > 256 || scope.OwnerId.Any(char.IsControl)
            || scope.Environment.Length is < 1 or > 64 || !scope.Environment.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_')
            || scope.MembershipRevision is < 0 or > 9007199254740991
            || new[] { scope.DocumentId, scope.PageId, scope.Generation }.Contains(Guid.Empty)
            || scope.Support is null || scope.Support.ProtocolVersion != 1 || scope.Support.ContentContractVersion != 2
            || !HashShape(scope.Support.SchemaFingerprint)) throw new InvalidDataException("Unsupported collaboration identity. Keep its recovery data.");
        return scope with { Backend = url.AbsoluteUri.TrimEnd('/') + "/" };
    }
    public static string Key(CollaborationJournalScope scope)
    {
        scope = Normalize(scope);
        string identity = string.Join('\n', scope.Backend, scope.OwnerId, scope.Environment,
            scope.DocumentId.ToString("D"), scope.PageId.ToString("D"), scope.Generation.ToString("D"));
        return Hash(Encoding.UTF8.GetBytes(identity + (scope.MembershipRevision == 0 ? "" : "\n" + scope.MembershipRevision.ToString(System.Globalization.CultureInfo.InvariantCulture))));
    }
    private string PathFor(CollaborationJournalScope scope) => Path.Combine(_root, Key(scope) + ".json");
    public Task<bool> AccountBindingAsync(CollaborationJournalScope scope, string accountId, bool create, CancellationToken ct = default) => Locked(async () =>
    {
        scope = Normalize(scope);
        if (string.IsNullOrWhiteSpace(accountId) || accountId.Length > 512 || accountId.Any(char.IsControl)) throw new InvalidDataException("Missing device account identity.");
        string path = Path.Combine(_root, Hash(Encoding.UTF8.GetBytes(scope.Backend + "\n" + scope.Environment + "\n" + accountId)) + ".account");
        if (File.Exists(path)) return await File.ReadAllTextAsync(path, ct) == scope.OwnerId;
        if (!create) return false;
        await _writer.WriteAsync(path, Encoding.UTF8.GetBytes(scope.OwnerId), ct); return true;
    }, ct);
    public Task<CollaborationJournalPage?> LoadAsync(CollaborationJournalScope scope, CancellationToken ct = default) => Locked(async () => await Read(scope, ct), ct);
    public Task<CollaborationJournalPage> InitializeAsync(CollaborationJournalScope scope, string state, string hash, CancellationToken ct = default) => Locked(async () =>
    {
        scope = Normalize(scope);
        var existing = await Read(scope, ct); if (existing is not null) return existing; // Existing generations are never reseeded.
        VerifyBytes(state, hash, 16 * 1024 * 1024);
        string installationPath = Path.Combine(_root, "installation.json");
        Guid installation;
        if (File.Exists(installationPath))
        { if (!Guid.TryParse(await File.ReadAllTextAsync(installationPath, ct), out installation) || installation == Guid.Empty) throw new InvalidDataException("Invalid installation identity. Its file is preserved."); }
        else { installation = Guid.NewGuid(); await _writer.WriteAsync(installationPath, Encoding.UTF8.GetBytes(installation.ToString("D")), ct); }
        var page = new CollaborationJournalPage(1, scope, installation, 0, state, hash, [], []);
        await Write(page, ct); return page;
    }, ct);

    public Task<CollaborationJournalPage> SaveAsync(CollaborationJournalScope scope, CollaborationJournalMutation request, CancellationToken ct = default) => Locked(async () =>
    {
        var page = await Read(scope, ct) ?? throw new InvalidDataException("No compatible local checkpoint. Recover the original generation.");
        if (request.Envelope is { } envelope)
        {
            ValidateEnvelope(page, envelope);
            var prior = page.Pending.FirstOrDefault(e => e.UpdateId == envelope.UpdateId);
            var receipt = page.Receipts.FirstOrDefault(e => e.UpdateId == envelope.UpdateId);
            if (prior is not null || receipt is not null)
            {
                if (prior is not null && prior != envelope || receipt is not null && receipt.Sha256 != envelope.Sha256) throw new InvalidDataException("An immutable update identity was reused.");
                return page;
            }
        }
        if (request.Revision != page.Revision) throw new InvalidOperationException("collaboration_journal_revision_changed");
        VerifyBytes(request.State, request.StateSha256, 16 * 1024 * 1024);
        var next = page with { Revision = checked(page.Revision + 1), State = request.State, StateSha256 = request.StateSha256,
            Pending = request.Envelope is null ? page.Pending : [..page.Pending, request.Envelope] };
        await Write(next, ct); return next;
    }, ct);

    public Task<CollaborationJournalPage> AcknowledgeAsync(CollaborationJournalScope scope, CollaborationDurableReceipt receipt, CancellationToken ct = default) => Locked(async () =>
    {
        var page = await Read(scope, ct) ?? throw new InvalidDataException("Missing collaboration journal.");
        ValidateReceipt(page, receipt);
        var prior = page.Receipts.FirstOrDefault(r => r.UpdateId == receipt.UpdateId);
        if (prior is not null) { if (prior != receipt) throw new InvalidDataException("Receipt identity changed."); return page; }
        var pending = page.Pending.FirstOrDefault(e => e.UpdateId == receipt.UpdateId);
        if (pending is null || pending.Sha256 != receipt.Sha256) throw new InvalidDataException("Receipt does not match a pending immutable envelope.");
        var next = page with { Revision = checked(page.Revision + 1), Pending = page.Pending.Where(e => e.UpdateId != receipt.UpdateId).ToList(), Receipts = [..page.Receipts, receipt] };
        await Write(next, ct); return next;
    }, ct);

    public Task<CollaborationJournalPage> RecoveryAsync(CollaborationJournalScope scope, string? reason, CancellationToken ct = default) => Locked(async () =>
    {
        if (reason is not null && reason is not ("access_unavailable" or "generation_changed" or "schema_changed")) throw new InvalidDataException("Unknown recovery reason.");
        var page = await Read(scope, ct) ?? throw new InvalidDataException("Missing collaboration journal.");
        var next = page with { Revision = checked(page.Revision + 1), RecoveryReason = reason }; await Write(next, ct); return next;
    }, ct);

    // Recovery enumeration is restricted to this authenticated account/backend/environment, including old generations.
    public Task<IReadOnlyList<CollaborationJournalScope>> ListRecoveryAsync(CollaborationJournalScope account, CancellationToken ct = default) => Locked(async () =>
    {
        account = Normalize(account); var result = new List<CollaborationJournalScope>();
        foreach (var file in Directory.EnumerateFiles(_root, "*.json"))
        {
            if (Path.GetFileName(file) == "installation.json") continue;
            var wrapped = JsonSerializer.Deserialize<FileRecord>(await File.ReadAllBytesAsync(file, ct), Json) ?? throw new InvalidDataException("Invalid journal. Its source is preserved.");
            if (Hash(Encoding.UTF8.GetBytes(wrapped.Payload)) != wrapped.Sha256) throw new InvalidDataException("Journal checksum failed. Its source is preserved.");
            var page = JsonSerializer.Deserialize<CollaborationJournalPage>(wrapped.Payload, Json) ?? throw new InvalidDataException("Invalid journal.");
            if (page.Scope.Backend == account.Backend && page.Scope.OwnerId == account.OwnerId && page.Scope.Environment == account.Environment)
            { Validate(page); if (page.Pending.Count != 0 || page.RecoveryReason is not null) result.Add(page.Scope); }
        }
        return (IReadOnlyList<CollaborationJournalScope>)result;
    }, ct);

    private async Task<CollaborationJournalPage?> Read(CollaborationJournalScope scope, CancellationToken ct)
    {
        scope = Normalize(scope); string path = PathFor(scope); if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 96 * 1024 * 1024) throw new InvalidDataException("Journal exceeds its recovery budget. Its source is preserved.");
        var file = JsonSerializer.Deserialize<FileRecord>(await File.ReadAllBytesAsync(path, ct), Json) ?? throw new InvalidDataException("Invalid journal.");
        if (Hash(Encoding.UTF8.GetBytes(file.Payload)) != file.Sha256) throw new InvalidDataException("Journal checksum failed. Its source is preserved.");
        var page = JsonSerializer.Deserialize<CollaborationJournalPage>(file.Payload, Json) ?? throw new InvalidDataException("Invalid journal.");
        Validate(page); if (page.Scope != scope) throw new InvalidDataException("Incompatible local schema or identity. Its checkpoint is preserved.");
        return page;
    }
    private async Task Write(CollaborationJournalPage page, CancellationToken ct)
    {
        Validate(page); string payload = JsonSerializer.Serialize(page, Json);
        if (Encoding.UTF8.GetByteCount(payload) > 64 * 1024 * 1024) throw new IOException("Collaboration journal is full. Keep the recovery copy.");
        await _writer.WriteAsync(PathFor(page.Scope), JsonSerializer.SerializeToUtf8Bytes(new FileRecord(payload, Hash(Encoding.UTF8.GetBytes(payload))), Json), ct);
    }
    private async Task<T> Locked<T>(Func<Task<T>> work, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        // Cross-instance/process exclusion; no in-memory cache can overwrite another journal revision.
        FileStream? lease = null;
        for (int attempt = 0; lease is null; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { lease = new FileStream(Path.Combine(_root, ".collaboration.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 100) { await Task.Delay(20, ct); }
        }
        using (lease) return await work();
    }
    private static void Validate(CollaborationJournalPage page)
    {
        if (page.Version != 1 || page.Scope is null || page.InstallationId == Guid.Empty || page.Revision is < 0 or > 9007199254740991 || page.Pending is null || page.Receipts is null
            || page.Pending.Any(e => e is null) || page.Receipts.Any(e => e is null)
            || page.Pending.Count > 4096 || page.Receipts.Count > 50000 || page.Pending.Sum(e => (long)e.Update.Length) > 44 * 1024 * 1024
            || page.Pending.Select(e => e.UpdateId).Distinct().Count() != page.Pending.Count || page.Receipts.Select(e => e.UpdateId).Distinct().Count() != page.Receipts.Count
            || page.Receipts.Select(e => e.CommitSequence).Distinct().Count() != page.Receipts.Count
            || page.Pending.Select(e => e.UpdateId).Intersect(page.Receipts.Select(r => r.UpdateId)).Any()
            || page.RecoveryReason is not null && page.RecoveryReason is not ("access_unavailable" or "generation_changed" or "schema_changed")) throw new InvalidDataException("Unsupported journal. Its source is preserved.");
        if (Normalize(page.Scope) != page.Scope) throw new InvalidDataException("Journal scope is not canonical.");
        VerifyBytes(page.State, page.StateSha256, 16 * 1024 * 1024);
        foreach (var envelope in page.Pending) ValidateEnvelope(page, envelope);
        foreach (var receipt in page.Receipts) ValidateReceipt(page, receipt);
    }
    private static void ValidateEnvelope(CollaborationJournalPage page, CollaborationPendingEnvelope e)
    {
        if (e.Type != "commitUpdate" || e.ProtocolVersion != 1 || e.DocumentId != page.Scope.DocumentId || e.PageId != page.Scope.PageId || e.Generation != page.Scope.Generation
            || e.InstallationId != page.InstallationId || e.UpdateId == Guid.Empty) throw new InvalidDataException("Update scope differs from its checkpoint.");
        VerifyBytes(e.Update, e.Sha256, 512 * 1024);
    }
    private static void ValidateReceipt(CollaborationJournalPage page, CollaborationDurableReceipt r)
    {
        if (r.PageId != page.Scope.PageId || r.Generation != page.Scope.Generation || r.InstallationId != page.InstallationId || r.UpdateId == Guid.Empty
            || r.CommitSequence is < 1 or > 9007199254740991 || r.CommittedAtUnixMilliseconds is < 1 or > 9007199254740991 || !HashShape(r.Sha256)) throw new InvalidDataException("Receipt scope differs from its checkpoint.");
    }
    private static bool HashShape(string? hash) => hash is { Length: 64 } && hash.All(c => char.IsAsciiDigit(c) || c is >= 'a' and <= 'f');
    private static void VerifyBytes(string encoded, string hash, int maximum)
    {
        if (encoded is null || encoded.Length > (maximum + 2L) / 3 * 4 || !HashShape(hash)) throw new InvalidDataException("Invalid journal bytes.");
        byte[] bytes; try { bytes = Convert.FromBase64String(encoded); } catch (FormatException) { throw new InvalidDataException("Invalid journal bytes."); }
        if (bytes.Length == 0 || bytes.Length > maximum || Convert.ToBase64String(bytes) != encoded || Hash(bytes) != hash) throw new InvalidDataException("Journal bytes failed integrity checks.");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
