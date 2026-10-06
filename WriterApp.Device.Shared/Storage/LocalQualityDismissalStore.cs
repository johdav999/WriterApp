using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Shared.Quality;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalQualityDecision(string Id, string SourceHash, string IssueKey, string RuleId, string Message,
    string? Anchor, bool Dismissed, bool Pending, string Status, Guid? ServerDocumentId, Guid? ServerPageId);
public sealed record LocalQualityDecisions(int Version, string Scope, Guid DocumentId, Guid PageId, IReadOnlyList<LocalQualityDecision> Items);

/// <summary>Separate, additive decision journal. Anonymous decisions are never adopted by a signed-in account.</summary>
public sealed class LocalQualityDismissalStore
{
    private readonly string _root;
    public LocalQualityDismissalStore(string root) : this(root, null) { }
    internal LocalQualityDismissalStore(string root, Action<string>? beforeCommit)
    { _root = root; _writer = new(beforeCommit); }
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    private readonly AtomicDocumentWriter _writer;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string DecisionId(string source, string issue, Guid? serverDocument, Guid? serverPage) =>
        QualityDismissalIdentity.Key(source, QualityDismissalIdentity.Hash($"{issue}:{serverDocument:D}:{serverPage:D}"));
    private string PathFor(string scope, Guid document, Guid page)
    {
        if (scope.Length != 64 || !scope.All(Uri.IsHexDigit) || document == Guid.Empty || page == Guid.Empty)
            throw new InvalidDataException("Invalid quality decision identity.");
        return Path.Combine(_root, scope, document.ToString("N"), page.ToString("N") + ".json");
    }
    public async Task<LocalQualityDecisions> ReadAsync(string scope, Guid document, Guid page, CancellationToken ct = default)
    {
        var path = PathFor(scope, document, page);
        if (!File.Exists(path)) return new(1, scope, document, page, []);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        if (stream.Length > 2_000_000) throw new InvalidDataException("Quality decision journal is too large. Its file is preserved.");
        var value = await JsonSerializer.DeserializeAsync<LocalQualityDecisions>(stream, Json, ct)
            ?? throw new InvalidDataException("Invalid quality decision journal.");
        Validate(value);
        if (value.Scope != scope || value.DocumentId != document || value.PageId != page)
            throw new InvalidDataException("Quality decision journal identity mismatch. Its file is preserved.");
        return value;
    }
    public async Task SaveAsync(LocalQualityDecisions value, CancellationToken ct = default)
    {
        Validate(value);
        var path = PathFor(value.Scope, value.DocumentId, value.PageId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);
        if (bytes.Length > 2_000_000) throw new InvalidDataException("Quality decision journal is full. Restore old decisions before dismissing more findings.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await _writer.WriteAsync(path, bytes, ct);
    }
    private static void Validate(LocalQualityDecisions value)
    {
        if (value.Version != 1 || value.Items is null || value.Items.Count > 1000
            || value.Items.Any(i => i is null)
            || value.Items.Select(i => i.Id).Distinct().Count() != value.Items.Count
            || value.Items.Any(i => i is null || !QualityDismissalIdentity.IsHash(i.SourceHash) || !QualityDismissalIdentity.IsHash(i.IssueKey)
                || i.Id != DecisionId(i.SourceHash, i.IssueKey, i.ServerDocumentId, i.ServerPageId) || i.RuleId is null || i.RuleId.Length > 100 || i.Message is null || i.Message.Length > 2000
                || i.ServerDocumentId == Guid.Empty || i.ServerPageId == Guid.Empty
                || i.Anchor?.Length > 1000 || i.Status is not ("Pending" or "Synced" or "Local" or "Unmapped" or "Conflict" or "Failed")))
            throw new InvalidDataException("Invalid or unsupported quality decision journal. Its file is preserved.");
    }
}
