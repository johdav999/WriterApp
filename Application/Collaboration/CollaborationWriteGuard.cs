using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

/// <summary>Early tracked-write backstop. Database fences provide the concurrent/raw-SQL guarantee.</summary>
public static class CollaborationWriteGuard
{
    public static async Task RequireLegacyAsync(AppDbContext db, Guid documentId, CancellationToken ct)
    {
        var mode = await db.Documents.AsNoTracking().Where(x => x.Id == documentId).Select(x => x.CollaborationMode).SingleOrDefaultAsync(ct);
        if (mode != CollaborationMode.Legacy) throw new CollaborationWriteException(CollaborationWriteException.ForMode(mode));
    }

    private static IQueryable<DocumentRecord>? AffectedDocuments(AppDbContext db)
    {
        var documents = new HashSet<Guid>(); var projects = new HashSet<Guid>();
        var pages = new HashSet<Guid>(); var sections = new HashSet<Guid>(); var scenes = new HashSet<Guid>();
        foreach (EntityEntry entry in db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray())
        {
            if (entry.Metadata.ClrType.Namespace == "WriterApp.Data.Collaboration") continue;
            // Actor-owned proposals, usage accounting and receipts do not write manuscript content.
            if (entry.Entity is WriterApp.Data.AI.AiActionHistoryEntryRecord or WriterApp.Data.AI.WebAiHistoryOperationRecord
                or WriterApp.Data.AI.BrainstormingSessionRecord or WriterApp.Data.Usage.UsageEvent or WebTranslationOperation) continue;
            if (entry.Entity is DocumentRecord doc)
            {
                // Mode-only transitions are reserved for future internal migration code. No HTTP endpoint exposes them.
                if (entry.State == EntityState.Modified && entry.Properties.Where(p => p.IsModified).All(p => p.Metadata.Name is nameof(DocumentRecord.CollaborationMode) or nameof(DocumentRecord.CollaborationMigrationState)))
                {
                    if (doc.CollaborationMode < (CollaborationMode)entry.Property(nameof(DocumentRecord.CollaborationMode)).OriginalValue!)
                        throw new CollaborationWriteException(CollaborationWriteException.ForMode(CollaborationMode.Collaborative));
                    continue;
                }
                if (entry.State != EntityState.Added) documents.Add(doc.Id);
            }
            if (entry.Entity is ProjectRecord project && entry.State != EntityState.Added) projects.Add(project.Id);
            foreach (var (name, set) in new[] { ("DocumentId", documents), ("ProjectId", projects), ("PageId", pages), ("SectionId", sections), ("SceneNodeId", scenes) })
            {
                var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == name);
                if (property?.CurrentValue is Guid id) set.Add(id);
                if (entry.State != EntityState.Added && property?.OriginalValue is Guid oldId) set.Add(oldId);
            }
        }
        if (documents.Count + projects.Count + pages.Count + sections.Count + scenes.Count == 0) return null;
        return db.Documents.AsNoTracking().Where(d => d.CollaborationMode != CollaborationMode.Legacy &&
            (documents.Contains(d.Id) || projects.Contains(d.ProjectId)
            || db.Pages.Any(p => pages.Contains(p.Id) && p.DocumentId == d.Id)
            || db.Sections.Any(s => sections.Contains(s.Id) && s.DocumentId == d.Id)
            || db.ProjectNodes.IgnoreQueryFilters().Any(n => scenes.Contains(n.Id) && n.ProjectId == d.ProjectId)));
    }

    public static void Validate(AppDbContext db)
    {
        var affected = AffectedDocuments(db);
        if (affected is null) return;
        var mode = affected.Select(d => (CollaborationMode?)d.CollaborationMode).FirstOrDefault();
        if (mode.HasValue) throw new CollaborationWriteException(CollaborationWriteException.ForMode(mode.Value));
    }

    public static async Task ValidateAsync(AppDbContext db, CancellationToken ct)
    {
        var affected = AffectedDocuments(db);
        if (affected is null) return;
        var mode = await affected.Select(d => (CollaborationMode?)d.CollaborationMode).FirstOrDefaultAsync(ct);
        if (mode.HasValue) throw new CollaborationWriteException(CollaborationWriteException.ForMode(mode.Value));
    }

}
