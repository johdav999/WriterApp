using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public enum LocalLibraryFilter { All, Projects, Documents, Trash }

public sealed record LocalLibraryRow(LocalDocument Document, bool IsProject = false, bool IsChild = false, int DocumentCount = 1)
{
    public Guid Id => IsProject ? Document.Project!.ProjectId : Document.DocumentId;
    public string Key => $"{(IsProject ? "project" : "document")}-{Id:D}";
    public string Title => IsProject ? Document.Project!.Title : Document.Title;
    public string Context => IsProject ? $"{DocumentCount} {(DocumentCount == 1 ? "document" : "documents")}" : Document.Project is { } project ? $"In {project.Title}" : "Standalone document";
    public string OpenUrl => $"/documents/{Document.DocumentId:D}";
    public string? ProjectDetailsUrl => Document.Project is { } project ? $"/projects/{project.ProjectId:D}?documentId={Document.DocumentId:D}" : null;
}

/// <summary>One folder per project; expanded rows expose its documents without copying writing.</summary>
public static class LocalLibraryView
{
    public static IReadOnlyList<LocalLibraryRow> Rows(IEnumerable<LocalDocument> documents, LocalLibraryFilter filter,
        string search, string sort, IReadOnlySet<Guid> expanded, IReadOnlySet<Guid>? collapsedSearchProjects = null)
    {
        string query = search.Trim();
        bool Matches(string value) => value.Contains(query, StringComparison.CurrentCultureIgnoreCase);
        var available = documents.Where(d => (filter == LocalLibraryFilter.Trash) == (d.DeletedAtUtc is not null)).ToArray();
        var matches = available.Where(d => Matches(d.Title) || d.Project is { } project && Matches(project.Title)).ToArray();
        var roots = filter == LocalLibraryFilter.Documents ? matches.Select(d => new LocalLibraryRow(d))
            : matches.Where(d => d.Project is null && filter != LocalLibraryFilter.Projects).Select(d => new LocalLibraryRow(d))
                .Concat(matches.Where(d => d.Project is not null).GroupBy(d => d.Project!.ProjectId).Select(group =>
                {
                    var members = available.Where(d => d.Project?.ProjectId == group.Key).ToArray();
                    var primary = members.FirstOrDefault(d => d.DocumentId == d.Project!.PrimaryDocumentId)
                        ?? members.OrderBy(d => d.Kind != "manuscript").ThenBy(d => d.CreatedAtUtc).First();
                    return new LocalLibraryRow(primary, IsProject: true, DocumentCount: members.Length);
                }));
        DateTimeOffset Date(LocalLibraryRow row) => filter == LocalLibraryFilter.Trash
            ? row.Document.DeletedAtUtc ?? row.Document.UpdatedAtUtc : row.Document.UpdatedAtUtc;
        var ordered = sort switch
        {
            "title" => roots.OrderBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase).ThenByDescending(Date),
            "oldest" => roots.OrderBy(Date).ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase),
            _ => roots.OrderByDescending(Date).ThenBy(r => r.Title, StringComparer.CurrentCultureIgnoreCase)
        };
        List<LocalLibraryRow> rows = [];
        foreach (var row in ordered)
        {
            rows.Add(row);
            // Search reveals the matching manuscript even when its project was previously collapsed.
            if (filter == LocalLibraryFilter.All && row.IsProject && (query.Length > 0
                ? collapsedSearchProjects?.Contains(row.Id) != true : expanded.Contains(row.Id)))
                rows.AddRange(matches.Where(d => d.Project?.ProjectId == row.Id)
                    .OrderBy(d => d.DocumentId != row.Document.Project!.PrimaryDocumentId).ThenBy(d => d.Title)
                    .Select(d => new LocalLibraryRow(d, IsChild: true)));
        }
        return rows;
    }
}
