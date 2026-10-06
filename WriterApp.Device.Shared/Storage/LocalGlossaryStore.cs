using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Shared.Quality;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalGlossaryCache(int Version, string Scope, Guid LocalDocumentId,
    DeviceGlossarySnapshot Snapshot, DateTimeOffset CheckedAt);

/// <summary>Additive read-only cache. Never modifies authored documents or sync payloads.</summary>
public sealed class LocalGlossaryStore(string root)
{
    private const int MaximumCacheBytes = DeviceGlossarySnapshot.MaximumBytes + 2048;
    private readonly AtomicDocumentWriter _writer = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private string PathFor(string scope, Guid id, Guid serverId)
    {
        if (scope.Length != 64 || !scope.All(Uri.IsHexDigit) || id == Guid.Empty || serverId == Guid.Empty)
            throw new InvalidDataException("Invalid glossary cache identity.");
        return Path.Combine(root, scope, id.ToString("N"), serverId.ToString("N") + ".json");
    }
    public async Task<LocalGlossaryCache?> ReadAsync(string scope, LocalDocument document, CancellationToken ct = default)
    {
        var path = PathFor(scope, document.DocumentId, document.ServerDocumentId ?? Guid.Empty);
        if (!File.Exists(path)) return null;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
            4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > MaximumCacheBytes) throw new InvalidDataException("Glossary cache exceeds the supported size.");
        var cache = await JsonSerializer.DeserializeAsync<LocalGlossaryCache>(stream, Json, ct)
            ?? throw new InvalidDataException("Invalid glossary cache.");
        if (cache.Version != 1 || cache.Scope != scope || cache.LocalDocumentId != document.DocumentId
            || cache.Snapshot is null || cache.CheckedAt == default)
            throw new InvalidDataException("Invalid or unsupported glossary cache. Existing file is preserved.");
        cache.Snapshot.Validate(document.ServerDocumentId ?? Guid.Empty);
        return cache;
    }
    public async Task SaveAsync(LocalGlossaryCache cache, CancellationToken ct = default)
    {
        cache.Snapshot.Validate(cache.Snapshot.DocumentId);
        if (cache.Version != 1 || cache.CheckedAt == default) throw new InvalidDataException("Invalid glossary cache.");
        var path = PathFor(cache.Scope, cache.LocalDocumentId, cache.Snapshot.DocumentId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cache, Json);
        if (bytes.Length > MaximumCacheBytes) throw new InvalidDataException("Glossary cache exceeds the supported size.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await _writer.WriteAsync(path, bytes, ct);
    }
}
