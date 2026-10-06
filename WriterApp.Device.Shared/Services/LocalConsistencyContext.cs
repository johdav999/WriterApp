using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Canon;

namespace WriterApp.Device.Shared.Services;

public sealed record ConsistencyPageText(Guid PageId, string PlainText);
public sealed record ConsistencyPassage(Guid PageId, string PlainText, int Start, string Quote, Guid RequestId, Guid DocumentId);

public static class LocalConsistencyContext
{
    public static AdvancedAiPrepared Prepare(AdvancedAiPrepared prepared, DeviceCanonContext canon,
        IReadOnlyList<ConsistencyPageText> pages, bool allowReduced)
    {
        var doc = prepared.Source;
        if (prepared.Request.Key != "continuity.check_section" || canon.DocumentId != doc.ServerDocumentId
            || canon.DocumentVersion != doc.ServerVersion || doc.SyncState != LocalSyncState.Synced)
            throw new InvalidDataException("Canon and manuscript revisions do not match. Synchronize and load canon again.");
        if (!allowReduced && canon.Snapshots.Count != 3)
            throw new InvalidOperationException("Story references are incomplete. Prepare the available references before checking.");
        var section = doc.Sections.Single(s => s.SectionId == prepared.SectionId);
        if (pages.Count != section.Pages.Count || pages.Select(p => p.PageId).Distinct().Count() != pages.Count
            || section.Pages.Any(p => !pages.Any(t => t.PageId == p.PageId)))
            throw new InvalidDataException("Every saved page must be included exactly once.");
        string writing = string.Join("\n\n", section.Pages.OrderBy(p => p.OrderIndex).Select(p => pages.Single(t => t.PageId == p.PageId).PlainText));
        if (string.IsNullOrWhiteSpace(writing) || writing.Length > 100_000) throw new InvalidDataException("Check a section with 1–100,000 characters of writing.");
        var parameters = new Dictionary<string, object?>(prepared.Request.Request.Parameters ?? []);
        var versions = new Dictionary<CanonKind, string>();
        foreach (var kind in Enum.GetValues<CanonKind>())
        {
            string json = "{}";
            if (canon.Snapshots.TryGetValue(kind, out var snapshot))
            {
                if (snapshot.ContractVersion != 1 || snapshot.Kind != kind || !snapshot.Exists
                    || snapshot.DocumentId != canon.DocumentId || snapshot.SourceDocumentVersion != canon.DocumentVersion
                    || snapshot.CheckedDocumentVersion != canon.DocumentVersion || string.IsNullOrWhiteSpace(snapshot.SnapshotVersion))
                    throw new InvalidDataException("Canon has the wrong source or identity. Load canon again.");
                _ = CanonContent.Parse(kind, snapshot.ContentJson);
                json = snapshot.ContentJson; versions.Add(kind, snapshot.SnapshotVersion);
            }
            parameters[kind.ToString().ToLowerInvariant() + "_bible_json"] = json;
        }
        if (parameters.Values.Sum(v => v?.ToString()?.Length ?? 0) > 250_000)
            throw new InvalidDataException("Canon exceeds the supported context size. Use a smaller manuscript; canon was not truncated.");
        string outline = System.Text.Json.JsonSerializer.Serialize(new { synopsis = doc.Project?.Synopsis,
            scenes = doc.Project?.Nodes.Where(n => n.DeletionId is null).Select(n => new { n.Title, n.Card }) });
        if (outline.Length > 100_000) throw new InvalidDataException("Story planning exceeds the supported context size.");
        return prepared with { Request = prepared.Request with { Request = prepared.Request.Request with {
            SurroundingText = writing, OutlineText = outline, Parameters = parameters, ExpectedCanonVersions = versions } } };
    }

    public static void RequireSameCanon(DeviceCanonContext expected, DeviceCanonContext current)
    {
        if (expected.DocumentId != current.DocumentId || expected.DocumentVersion != current.DocumentVersion
            || expected.Snapshots.Count != current.Snapshots.Count || expected.Snapshots.Any(p =>
                !current.Snapshots.TryGetValue(p.Key, out var next) || next.SnapshotVersion != p.Value.SnapshotVersion || next.ContentJson != p.Value.ContentJson))
            throw new InvalidOperationException("Canon changed after analysis. Run the consistency check again.");
    }
}
