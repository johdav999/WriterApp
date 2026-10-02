using WriterApp.Device.Shared.Storage;
using WriterApp.UI.Shared;

namespace WriterApp.Device.Shared.Services;

public enum LocalSearchScope { Writing, Planning, All }

public sealed record LocalSearchResult(Guid DocumentId, Guid? SectionId, Guid? PageId, long Revision,
    string DocumentTitle, string SectionTitle, string PageTitle, string Snippet, bool TitleMatch, Guid? ProjectId = null, Guid? PlanningNodeId = null, string? PlanningView = null);
public sealed record LocalSearchResults(IReadOnlyList<LocalSearchResult> Items, bool Limited, int UnavailableDocuments);

public sealed class LocalDocumentSearch(ILocalDocumentStore store)
{
    public async Task<LocalSearchResults> SearchAsync(string query, CancellationToken ct = default, LocalSearchScope scope = LocalSearchScope.Writing)
    {
        query = query.Trim();
        if (query.Length is 0 or > 200) return new([], false, 0);
        var catalog = await store.ReadSearchDocumentsAsync(ct);
        return await Task.Run(() => Query(catalog, query, ct, scope), ct);
    }

    internal static LocalSearchResults Query(LocalSearchDocuments catalog, string query, CancellationToken ct, LocalSearchScope scope = LocalSearchScope.Writing)
    {
        const int maxResults = 50, textBudget = 2_000_000;
        var results = new List<LocalSearchResult>(); int scanned = 0; bool limited = catalog.Limited;
        foreach (var doc in catalog.Documents)
        {
            ct.ThrowIfCancellationRequested();
            if (doc.DeletedAtUtc is not null) continue;
            if (scope != LocalSearchScope.Writing && doc.Project is { } project)
            {
                foreach (var item in PlanningEntries(doc))
                {
                    if (results.Count >= maxResults) return new(results, true, catalog.Issues.Count);
                    if (item.Text.Length > textBudget - scanned) { limited = true; continue; }
                    scanned += item.Text.Length;
                    var match = TextSearch.Find(item.Text, query, 1, ct).FirstOrDefault();
                    if (match is null) continue;
                    int start = Math.Max(0, match.Start - 50), end = Math.Min(item.Text.Length, match.Start + match.Length + 90);
                    results.Add(new(doc.DocumentId, null, null, doc.LocalRevision, doc.Title, project.Title, item.Title, item.Text[start..end], false, project.ProjectId, item.Id, item.View));
                }
            }
            if (scope == LocalSearchScope.Planning) continue;
            var first = doc.Sections.OrderBy(s => s.OrderIndex).FirstOrDefault();
            var firstPage = first?.Pages.OrderBy(p => p.OrderIndex).FirstOrDefault();
            if (doc.Title.Length > textBudget - scanned) { limited = true; continue; }
            scanned += doc.Title.Length;
            if (TextSearch.Find(doc.Title, query, 1, ct).Count != 0)
                results.Add(new(doc.DocumentId, first?.SectionId, firstPage?.PageId, doc.LocalRevision, doc.Title, first?.Title ?? "", firstPage?.Title ?? "", doc.Title, true));
            foreach (var section in doc.Sections.OrderBy(s => s.OrderIndex))
            foreach (var page in section.Pages.OrderBy(p => p.OrderIndex))
            {
                ct.ThrowIfCancellationRequested();
                if (results.Count >= maxResults) return new(results.Take(maxResults).ToArray(), true, catalog.Issues.Count);
                if (page.Content.Length > textBudget - scanned) { limited = true; continue; }
                scanned += page.Content.Length;
                string text = LocalDocumentPreview.PlainText(page);
                var match = TextSearch.Find(text, query, 1, ct).FirstOrDefault();
                if (match is null) continue;
                int start = Math.Max(0, match.Start - 50), end = Math.Min(text.Length, match.Start + match.Length + 90);
                results.Add(new(doc.DocumentId, section.SectionId, page.PageId, doc.LocalRevision,
                    doc.Title, section.Title, page.Title, text[start..end], false));
            }
        }
        ct.ThrowIfCancellationRequested();
        return new(results.Take(maxResults).ToArray(), limited || results.Count > maxResults, catalog.Issues.Count);
    }

    private static IEnumerable<(Guid? Id, string Title, string View, string Text)> PlanningEntries(LocalDocument doc)
    {
        if (doc.Project is not { } p) yield break;
        if (p.Synopsis is { } s) yield return (null, "Synopsis", "Synopsis", string.Join("\n", s.Logline, s.Premise, s.Theme, s.ProtagonistArc, s.CentralConflict, s.Stakes, s.Setting, s.EndingIntent, s.OpenQuestions, s.Notes));
        foreach(var n in p.Nodes.Where(n => n.DeletionId is null && n.NodeType == "scene"))
        {
            yield return (n.NodeId, n.Title, "Story", string.Join("\n", n.Title, n.Notes, n.Card?.Summary, n.Card?.NarrativePurpose, n.Card?.NarrativeRole, n.Card?.NarrativeIntent,
                n.Card?.EmotionalBeat, n.Card?.KeyEvents, n.Card?.OpenQuestions, n.Card?.PovCharacterId, n.Card?.SubplotTagsJson));
            yield return (n.NodeId, n.Title, "Notes & Tasks", string.Join("\n", n.Annotations.Select(a => a.Value.Content + " " + a.Value.AnchorText)));
        }
    }

    // Revalidate the target after saving pending edits: moves retain page identity, deletions never navigate.
    public async Task<LocalSearchResult?> ResolveAsync(LocalSearchResult result, string query, CancellationToken ct = default)
    {
        var doc = await store.GetAsync(result.DocumentId, ct);
        if (doc is null || doc.DeletedAtUtc is not null) return null;
        if (result.ProjectId is { } projectId)
        {
            if (doc.Project?.ProjectId != projectId) return null;
            var entry = PlanningEntries(doc).FirstOrDefault(x => x.Id == result.PlanningNodeId && x.View == result.PlanningView);
            return entry.Text is not null && TextSearch.Find(entry.Text, query, 1).Count > 0
                ? result with { Revision = doc.LocalRevision, PageTitle = entry.Title, SectionTitle = doc.Project.Title } : null;
        }
        var section = doc.Sections.FirstOrDefault(s => s.Pages.Any(p => p.PageId == result.PageId));
        if (result.TitleMatch)
        {
            if (TextSearch.Find(doc.Title, query, 1).Count == 0) return null;
            section ??= doc.Sections.OrderBy(s => s.OrderIndex).FirstOrDefault();
            return result with { Revision = doc.LocalRevision, DocumentTitle = doc.Title, SectionId = section?.SectionId,
                PageId = section?.Pages.FirstOrDefault(p => p.PageId == result.PageId)?.PageId ?? section?.Pages.OrderBy(p => p.OrderIndex).FirstOrDefault()?.PageId };
        }
        var page = section?.Pages.Single(p => p.PageId == result.PageId);
        if (page is null || TextSearch.Find(LocalDocumentPreview.PlainText(page), query, 1).Count == 0) return null;
        return result with { Revision = doc.LocalRevision, DocumentTitle = doc.Title, SectionId = section!.SectionId, SectionTitle = section.Title, PageTitle = page.Title };
    }
}
