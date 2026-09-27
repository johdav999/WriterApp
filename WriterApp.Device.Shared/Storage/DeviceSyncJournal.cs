using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WriterApp.Shared.Sync;

namespace WriterApp.Device.Shared.Storage;

public sealed class DeviceSyncJournal(string root)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal string AccountKey(string backend, string owner) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(backend + "\n" + owner)));
    internal IDisposable Acquire()
    {
        Directory.CreateDirectory(root);
        return new FileStream(Path.Combine(root, ".sync.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }
    internal async Task<SyncJournal> LoadAsync(string key, CancellationToken ct)
    {
        string path = Path.Combine(root, key + ".json");
        if (!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<SyncJournal>(await File.ReadAllBytesAsync(path, ct), Json);
        if (value is null || value.SchemaVersion != 1 || value.Entries is null) throw new JsonException("Unsupported sync journal. Its file was preserved.");
        if (value.Entries.Any(e => e is null || e.LocalId == Guid.Empty || e.ServerId == Guid.Empty
            || e.Pending is { Request: null } || e.Pending?.Request.OperationId == Guid.Empty
            || e.Conflict is { Remote: null })
            || value.Entries.Select(e => e.LocalId).Distinct().Count() != value.Entries.Count
            || value.Entries.Select(e => e.ServerId).Distinct().Count() != value.Entries.Count)
            throw new JsonException("Invalid sync journal. Its file was preserved.");
        return value;
    }
    internal Task SaveAsync(string key, SyncJournal value, CancellationToken ct) =>
        new AtomicDocumentWriter().WriteAsync(Path.Combine(root, key + ".json"), JsonSerializer.SerializeToUtf8Bytes(value, Json), ct);
    internal async Task BindAsync(Guid localId, string key, CancellationToken ct)
    {
        string path = Path.Combine(root, localId.ToString("N") + ".owner");
        if (File.Exists(path))
        {
            if (await File.ReadAllTextAsync(path, ct) != key)
                throw new InvalidOperationException("This local document belongs to another sync account. Duplicate it to upload a separate copy.");
            return;
        }
        await new AtomicDocumentWriter().WriteAsync(path, Encoding.UTF8.GetBytes(key), ct);
    }
}

internal sealed class SyncJournal
{
    public int SchemaVersion { get; set; } = 1;
    public string? Cursor { get; set; }
    public List<SyncEntry> Entries { get; set; } = [];
}
internal sealed class SyncEntry
{
    public Guid LocalId { get; set; }
    public Guid ServerId { get; set; }
    public string? Version { get; set; }
    public bool ServerTrashed { get; set; }
    public bool Deleted { get; set; }
    public bool DeleteRequested { get; set; }
    public string? BaseFingerprint { get; set; }
    public string? BaseContentFingerprint { get; set; }
    public DateTimeOffset? LastSynced { get; set; }
    public string? Error { get; set; }
    public bool Rejected { get; set; }
    public bool TransientError { get; set; }
    public SyncPending? Pending { get; set; }
    public SyncConflict? Conflict { get; set; }
}
internal sealed record SyncPending(SyncMutation Request, string Fingerprint, string ContentFingerprint);
internal sealed record SyncConflict(SyncSnapshot Remote, Guid RemoteCopyId, Guid LocalCopyId);
