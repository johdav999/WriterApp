using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Shared.Canon;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalBibleCache(int Version, string Scope, Guid LocalDocumentId, Guid ServerDocumentId,
    CanonKind Kind, DeviceBibleSnapshot? Snapshot, DateTimeOffset CheckedAt, string RefreshState);

/// <summary>Read-only cloud canon cache, separate from authored writing and sync payloads.</summary>
public sealed class LocalBibleStore(string root)
{
    private readonly AtomicDocumentWriter _writer = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string ScopeKey(Uri backend, string accountId) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new[] { backend.AbsoluteUri.TrimEnd('/'), accountId })));
    private string PathFor(string scope, Guid localId, CanonKind kind)
    {
        if (scope.Length != 64 || !scope.All(Uri.IsHexDigit) || localId == Guid.Empty || !Enum.IsDefined(kind))
            throw new InvalidDataException("Invalid canon cache identity.");
        return Path.Combine(root, scope, localId.ToString("N"), kind + ".json");
    }
    public async Task<LocalBibleCache?> ReadAsync(string scope, LocalDocument document, CanonKind kind, CancellationToken ct = default)
    {
        string path = PathFor(scope, document.DocumentId, kind);
        if (!File.Exists(path)) return null;
        if (new FileInfo(path).Length > 8_000_000) throw new InvalidDataException("Canon cache exceeds the supported size. Existing file is preserved.");
        // An atomic replacement may run while cached context is being read. Retain this complete
        // file snapshot without denying the writer's rename on Windows.
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > 8_000_000) throw new InvalidDataException("Canon cache exceeds the supported size. Existing file is preserved.");
        var value = await JsonSerializer.DeserializeAsync<LocalBibleCache>(stream, Json, ct)
            ?? throw new InvalidDataException("Invalid canon cache.");
        Validate(value);
        if (value.Scope != scope || value.LocalDocumentId != document.DocumentId || value.ServerDocumentId != document.ServerDocumentId || value.Kind != kind)
            throw new InvalidDataException("Canon cache identity changed. Existing file is preserved.");
        return value;
    }
    public async Task SaveAsync(LocalBibleCache value, CancellationToken ct = default)
    {
        Validate(value); string path = PathFor(value.Scope, value.LocalDocumentId, value.Kind);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > 8_000_000) throw new InvalidDataException("Canon cache exceeds the supported size.");
        await _writer.WriteAsync(path, bytes, ct);
    }
    private static void Validate(LocalBibleCache value)
    {
        if (value.Version != 1 || value.ServerDocumentId == Guid.Empty || value.RefreshState is not ("Ready" or "Refreshing" or "Interrupted"))
            throw new InvalidDataException("Invalid or unsupported canon cache. Existing file is preserved.");
        if (value.Snapshot is { } snapshot)
        {
            if (snapshot.ContractVersion != 1 || snapshot.DocumentId != value.ServerDocumentId || snapshot.Kind != value.Kind
                || string.IsNullOrWhiteSpace(snapshot.SnapshotVersion) || string.IsNullOrWhiteSpace(snapshot.CheckedDocumentVersion)
                || snapshot.SnapshotVersion.Length > 200 || snapshot.CheckedDocumentVersion.Length > 200
                || snapshot.ChangedSections < 0 || (snapshot.Exists && (snapshot.RefreshedAt is null || snapshot.SourceHash is not { Length: 64 } || !snapshot.SourceHash.All(Uri.IsHexDigit)))
                || (snapshot.SourceDocumentVersion is not null && snapshot.SourceDocumentVersion != snapshot.CheckedDocumentVersion)
                || (!snapshot.Exists && (snapshot.SnapshotVersion != "missing" || snapshot.SourceHash != "" || snapshot.SourceDocumentVersion is not null || snapshot.RefreshedAt is not null)))
                throw new InvalidDataException("Canon snapshot has invalid identity or revision evidence.");
            var content = CanonContent.Parse(snapshot.Kind, snapshot.ContentJson);
            if (!snapshot.Exists && content.Entries.Count != 0) throw new InvalidDataException("Missing canon cannot contain entries.");
        }
    }
}
