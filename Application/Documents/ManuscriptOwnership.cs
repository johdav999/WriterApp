using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents;

public static class ManuscriptOwnership
{
    // Existing projects used one tree. Adopt that tree exactly once, preserving IDs and links.
    public static async Task<Guid?> ResolvePrimaryAsync(AppDbContext db, ProjectRecord project, CancellationToken ct)
    {
        var manuscripts = await db.Documents.Where(d => d.ProjectId == project.Id
            && d.OwnerUserId == project.OwnerUserId && d.DocumentKind == DocumentKind.Manuscript)
            .OrderBy(d => d.CreatedAtUnixSeconds).ThenBy(d => d.Id).ToListAsync(ct);
        var primary = manuscripts.FirstOrDefault(d => d.Id == project.PrimaryDocumentId && d.DeletedAtUtc == null)
            ?? manuscripts.FirstOrDefault(d => d.DeletedAtUtc == null);
        if (primary is null) return null;
        project.PrimaryDocumentId = primary.Id;
        var legacy = await db.ProjectNodes.IgnoreQueryFilters().Where(n => n.ProjectId == project.Id && n.DocumentId == null).ToListAsync(ct);
        foreach (var node in legacy) node.DocumentId = primary.Id;
        await db.SaveChangesAsync(ct);
        return primary.Id;
    }
}
