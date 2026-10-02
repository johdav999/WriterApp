using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Documents;
using WriterApp.Shared.Sync;

namespace WriterApp.Application.Documents;

public sealed partial class DocumentSyncService
{
    private static DocumentSyncException PlanningCapability() => new(426, "planning_sync_required", "This project has planning data. Upgrade to planning sync v3; originals are preserved.");
    private static void ValidatePlanning(SyncProject project)
    {
        if (project.Version < 2 && (project.Synopsis is not null || project.Nodes.Any(n => n.Annotations is not null))) throw PlanningCapability();
        var ids = new HashSet<Guid>();
        foreach (var node in project.Nodes)
        foreach (var a in node.Annotations ?? [])
            if (a is null || a.Id == Guid.Empty || !ids.Add(a.Id) || node.NodeType != "scene" || a.Kind is not ("comment" or "todo" or "highlight")
                || a.Status is not ("open" or "resolved") || a.AnchorFrom < 0 || a.AnchorTo < a.AnchorFrom || a.CreatedAt == default
                || a.Content is null || a.Content.Length > 100_000 || a.AnchorText is null || a.AnchorText.Length > 100_000)
                throw Invalid("Invalid scene task or annotation.");
    }
    private async Task<SyncProject> ReadPlanningAsync(DocumentRecord document, ProjectRecord project, SyncProject snapshot, bool planning, CancellationToken ct)
    {
        var ids = snapshot.Nodes.Select(n => n.Id).ToArray();
        var annotations = await db.SceneAnnotations.AsNoTracking().Where(a => ids.Contains(a.SceneNodeId)).ToListAsync(ct);
        var synopsis = await db.DocumentSynopses.AsNoTracking().SingleOrDefaultAsync(s => s.DocumentId == document.Id, ct);
        if (!planning)
        {
            if (project.PlanningSyncEnabled || annotations.Count != 0 || synopsis is not null) throw PlanningCapability();
            return snapshot;
        }
        return snapshot with { Version = 2, Synopsis = synopsis is null ? null : new(synopsis.Logline, synopsis.Premise, synopsis.Theme,
            synopsis.ProtagonistArc, synopsis.CentralConflict, synopsis.Stakes, synopsis.Setting, synopsis.EndingIntent, synopsis.OpenQuestions, synopsis.Notes),
            Nodes = snapshot.Nodes.Select(n => n with { Annotations = annotations.Where(a => a.SceneNodeId == n.Id).OrderBy(a => a.Id)
                .Select(a => new SyncSceneAnnotation(a.Id, a.Kind, a.Status, a.AnchorFrom, a.AnchorTo, a.AnchorText, a.Content, a.AuthorUserId, a.CreatedAt, a.ResolvedAt, a.AnchorDetached)).ToArray() }).ToArray() };
    }
    private async Task ApplyAnnotationsAsync(SyncProjectNode node, string owner, CancellationToken ct)
    {
        var existing = await db.SceneAnnotations.Where(a => a.SceneNodeId == node.Id).ToDictionaryAsync(a => a.Id, ct);
        var ids = (node.Annotations ?? []).Select(a => a.Id).ToArray();
        if (existing.Keys.Except(ids).Any()) throw Invalid("Retain existing annotations; resolve tasks instead of omitting them.");
        if (await db.SceneAnnotations.AnyAsync(a => ids.Contains(a.Id) && a.SceneNodeId != node.Id, ct)) throw Invalid("Annotation identity belongs to another scene.");
        foreach (var a in node.Annotations ?? [])
        {
            if (!existing.TryGetValue(a.Id, out var target))
            { target = new() { Id = a.Id, SceneNodeId = node.Id, AuthorUserId = owner, CreatedAt = a.CreatedAt }; db.SceneAnnotations.Add(target); }
            target.Kind = a.Kind; target.Status = a.Status; target.Content = a.Content; target.AnchorText = a.AnchorText;
            target.AnchorDetached = a.AnchorDetached; target.AnchorFrom = a.AnchorDetached ? 0 : a.AnchorFrom; target.AnchorTo = a.AnchorDetached ? 0 : a.AnchorTo;
            target.ResolvedAt = a.Status == "resolved" ? a.ResolvedAt ?? DateTimeOffset.UtcNow : null;
        }
    }
    private async Task ApplySynopsisAsync(Guid documentId, SyncSynopsis? synopsis, CancellationToken ct)
    {
        var target = await db.DocumentSynopses.SingleOrDefaultAsync(s => s.DocumentId == documentId, ct);
        if (synopsis is null) { if (target is not null) throw Invalid("Retain the synopsis; empty individual fields explicitly."); return; }
        if (target is null) { target = new() { DocumentId = documentId }; db.DocumentSynopses.Add(target); }
        target.Logline = synopsis.Logline ?? ""; target.Premise = synopsis.Premise ?? ""; target.Theme = synopsis.Theme ?? "";
        target.ProtagonistArc = synopsis.ProtagonistArc ?? ""; target.CentralConflict = synopsis.CentralConflict ?? ""; target.Stakes = synopsis.Stakes ?? "";
        target.Setting = synopsis.Setting ?? ""; target.EndingIntent = synopsis.EndingIntent ?? ""; target.OpenQuestions = synopsis.OpenQuestions ?? "";
        target.Notes = synopsis.Notes ?? ""; target.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
