using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data.Documents;
using WriterApp.Shared.Sync;

namespace WriterApp.Application.Documents;

public sealed partial class DocumentSyncService
{
    private static DocumentSyncException MultipleDocumentCapability() => new(426, "multi_document_sync_required",
        "This project requires multi-document sync v4. Upgrade the client; existing writing is preserved.");

    private static DocumentSyncException ProjectCapability() => new(426, "project_sync_required",
        "This manuscript has project structure. Use project-aware sync v2; document-only requests cannot replace it.");

    private async Task<ProjectRecord> MoveStandaloneToProjectAsync(DocumentRecord document, ProjectRecord source,
        SyncProject incoming, string owner, bool multipleDocuments, CancellationToken ct)
    {
        if (!multipleDocuments || incoming.Version < 3) throw MultipleDocumentCapability();
        // Only an active standalone document may change its implicit container. Existing project members stay scoped.
        if (document.DeletedAtUtc is not null || document.IsArchived || source.SyncEnabled || source.PlanningSyncEnabled
            || await db.ProjectNodes.IgnoreQueryFilters().AnyAsync(n => n.ProjectId == source.Id, ct)
            || await db.Documents.AnyAsync(d => d.ProjectId == source.Id && d.Id != document.Id, ct))
            throw Invalid("Only an active standalone document can be moved into a project.");
        var target = await db.Projects.SingleOrDefaultAsync(p => p.Id == incoming.Id, ct);
        if (target is not null)
        {
            if (target.OwnerUserId != owner)
                throw new DocumentSyncException(404, "project_not_found", "Project not found.");
            if (await db.Documents.AnyAsync(d => d.ProjectId == target.Id, ct)
                && !await db.Documents.AnyAsync(d => d.ProjectId == target.Id && d.DeletedAtUtc == null && !d.IsArchived, ct))
                throw Invalid("Restore the destination project before moving documents into it.");
        }
        else
        {
            var now = DateTimeOffset.UtcNow;
            target = new() { Id = incoming.Id, OwnerUserId = owner, Title = incoming.Title, CreatedUtc = now, UpdatedUtc = now };
            db.Projects.Add(target);
        }
        if (source.PrimaryDocumentId == document.Id) source.PrimaryDocumentId = null;
        document.ProjectId = target.Id;
        return target;
    }

    private async Task<SyncProject?> ReadProjectAsync(DocumentRecord document, CancellationToken ct, bool planning = false, bool multipleDocuments = false)
    {
        var project = await db.Projects.SingleAsync(p => p.Id == document.ProjectId, ct);
        var nodes = await db.ProjectNodes.IgnoreQueryFilters().AsNoTracking().Where(n => n.ProjectId == project.Id && (n.DocumentId == document.Id || n.DocumentId == null)).ToListAsync(ct);
        if (!project.SyncEnabled && !project.PlanningSyncEnabled && nodes.Count == 0 && !await db.DocumentSynopses.AnyAsync(s => s.DocumentId == document.Id, ct)) return null;
        await ManuscriptOwnership.ResolvePrimaryAsync(db, project, ct);
        nodes = await db.ProjectNodes.IgnoreQueryFilters().AsNoTracking().Where(n => n.ProjectId == project.Id && n.DocumentId == document.Id).ToListAsync(ct);
        if (!multipleDocuments && await db.Documents.AnyAsync(d => d.ProjectId == project.Id && d.Id != document.Id, ct))
            throw MultipleDocumentCapability();
        var ids = nodes.Select(n => n.Id).ToArray();
        var notes = await db.SceneNotes.AsNoTracking().Where(n => ids.Contains(n.SceneNodeId)).ToDictionaryAsync(n => n.SceneNodeId, ct);
        var cards = await db.SceneCards.AsNoTracking().Where(n => ids.Contains(n.SceneNodeId)).ToDictionaryAsync(n => n.SceneNodeId, ct);
        var result = new SyncProject(project.Id, project.Title, nodes.OrderBy(n => n.Id).Select(n => new SyncProjectNode(n.Id, n.ParentId,
            n.NodeType.ToString().ToLowerInvariant(), n.Title, n.OrderIndex, n.LinkedSectionId, n.SyncDeletionId, n.MetadataJson,
            notes.GetValueOrDefault(n.Id)?.NotesText, cards.TryGetValue(n.Id, out var card) ? ToCard(card) : null)).ToArray(),
            project.Subtitle, project.AuthorName, project.Language, project.Genre, project.DefaultExportSettingsJson, project.CoverImageUrl);
        try { ValidateProject(result, ToDto(document).Sections); }
        catch (DocumentSyncException) { throw new DocumentSyncException(422, "project_shape_unsupported", "The web project has unsupported or unlinked structure. Link its scenes to this manuscript before synchronizing. Originals are preserved."); }
        result = await ReadPlanningAsync(document, project, result, planning, ct);
        return multipleDocuments ? result with { Version = 3, PrimaryDocumentId = project.PrimaryDocumentId, MetadataRevision = project.MetadataRevision } : result;
    }

    private static void ValidateProject(SyncProject project, IReadOnlyList<SyncSection> sections)
    {
        if (project.Version is not (1 or 2 or 3) || project.Id == Guid.Empty || string.IsNullOrWhiteSpace(project.Title) || project.Title.Length > 200
            || project.Nodes is null || project.Nodes.Any(n => n is null) || project.Nodes.Count > 1000) throw Invalid("Invalid project version, identity or metadata.");
        ValidatePlanning(project);
        var ids = new HashSet<Guid>(); var links = new HashSet<Guid>();
        foreach (var node in project.Nodes)
        {
            if (node is null || node.Id == Guid.Empty || !ids.Add(node.Id) || node.DeletionId == Guid.Empty
                || node.NodeType is not ("part" or "chapter" or "scene") || string.IsNullOrWhiteSpace(node.Title) || node.Title.Length > 200
                || node.OrderIndex < 0 || node.MetadataJson?.Length > 100_000 || node.Notes?.Length > 100_000)
                throw Invalid("Invalid project node metadata.");
            if (node.NodeType == "scene")
            {
                if (node.SectionId is not { } section || !links.Add(section) || !sections.Any(s => s.Id == section)) throw Invalid("Scenes require unique canonical manuscript sections.");
            }
            else if (node.SectionId is not null || node.Notes is not null || node.Card is not null) throw Invalid("Only scenes carry scene metadata.");
            if (node.MetadataJson is not null) { try { using var json = JsonDocument.Parse(node.MetadataJson); } catch (JsonException) { throw Invalid("Invalid node metadata JSON."); } }
            if (node.Card is { } card && (card.Status?.Length > 16 || card.TimeRef?.Length > 120))
                throw Invalid("Scene card status or time reference exceeds the supported length.");
        }
        foreach (var node in project.Nodes)
        {
            var parent = project.Nodes.SingleOrDefault(n => n.Id == node.ParentId);
            if ((node.ParentId is not null && parent is null) || !ProjectNodeHierarchyRules.IsPlacementAllowed(node.NodeType, parent?.NodeType)
                || (node.DeletionId is null && parent?.DeletionId is not null)) throw Invalid("Missing parent, cycle or invalid node placement.");
        }
        if (project.Nodes.Where(n => n.DeletionId is null).GroupBy(n => (n.ParentId, n.OrderIndex)).Any(g => g.Count() != 1)) throw Invalid("Sibling ordering must be unique.");
    }

    private async Task ApplyProjectAsync(DocumentRecord document, ProjectRecord record, SyncProject project, CancellationToken ct)
    {
        var existing = await db.ProjectNodes.IgnoreQueryFilters().Where(n => n.ProjectId == record.Id && (n.DocumentId == document.Id || n.DocumentId == null)).ToDictionaryAsync(n => n.Id, ct);
        var ids = project.Nodes.Select(n => n.Id).ToArray();
        if (existing.Keys.Except(ids).Any()) throw Invalid("Retain every known node; send deletion tokens rather than omitting nodes.");
        if (project.Nodes.Any(n => existing.TryGetValue(n.Id, out var old) && !string.Equals(old.NodeType.ToString(), n.NodeType, StringComparison.OrdinalIgnoreCase)))
            throw Invalid("A node's type cannot change. Create a new node and retain the old node with a deletion token.");
        if (await db.ProjectNodes.IgnoreQueryFilters().AnyAsync(n => ids.Contains(n.Id) && (n.ProjectId != record.Id || (n.DocumentId != null && n.DocumentId != document.Id)), ct)) throw Invalid("Node identity belongs to another project.");
        bool metadataChanged = record.Title != project.Title || record.Subtitle != project.Subtitle || record.AuthorName != project.AuthorName
            || record.Language != project.Language || record.Genre != project.Genre || record.DefaultExportSettingsJson != project.DefaultExportSettingsJson
            || record.CoverImageUrl != project.CoverImageUrl || (project.PrimaryDocumentId is not null && record.PrimaryDocumentId != project.PrimaryDocumentId);
        if (project.Version >= 3 && metadataChanged && project.MetadataRevision != record.MetadataRevision)
            throw new DocumentSyncException(409, "project_metadata_conflict", "Project metadata changed. Preserve local writing and download the current project.");
        if (project.PrimaryDocumentId is { } primary)
        {
            if (!await db.Documents.AnyAsync(d => d.Id == primary && d.ProjectId == record.Id && d.OwnerUserId == record.OwnerUserId
                && d.DocumentKind == DocumentKind.Manuscript && d.DeletedAtUtc == null, ct)) throw Invalid("Primary manuscript must belong to this project.");
            record.PrimaryDocumentId = primary;
        }
        else if (document.DocumentKind == DocumentKind.Manuscript) record.PrimaryDocumentId ??= document.Id;
        if (metadataChanged) record.MetadataRevision++;
        if (project.Version >= 2) record.PlanningSyncEnabled = true;
        record.SyncEnabled = true; record.Title = project.Title; record.Subtitle = project.Subtitle; record.AuthorName = project.AuthorName;
        record.Language = project.Language; record.Genre = project.Genre; record.DefaultExportSettingsJson = project.DefaultExportSettingsJson;
        record.CoverImageUrl = project.CoverImageUrl; record.UpdatedUtc = DateTimeOffset.UtcNow;
        foreach (var node in project.Nodes.OrderBy(n => n.NodeType == "part" ? 0 : n.NodeType == "chapter" ? 1 : 2))
        {
            if (!existing.TryGetValue(node.Id, out var target))
            { target = new() { Id = node.Id, ProjectId = record.Id, DocumentId = document.Id }; db.ProjectNodes.Add(target); existing.Add(node.Id, target); }
            target.DocumentId = document.Id;
            target.NodeType = Enum.Parse<ProjectNodeType>(node.NodeType, true); target.Title = node.Title; target.OrderIndex = node.OrderIndex;
            target.ParentId = node.ParentId; target.Parent = node.ParentId is { } parent ? existing[parent] : null;
            target.LinkedSectionId = node.SectionId; target.SyncDeletionId = node.DeletionId; target.MetadataJson = node.MetadataJson; target.UpdatedUtc = DateTimeOffset.UtcNow;
            if (node.NodeType != "scene") continue;
            var section = document.Sections.Single(s => s.Id == node.SectionId);
            var content = await db.SceneContents.SingleOrDefaultAsync(n => n.SceneNodeId == node.Id, ct);
            if (content is null) { content = new() { SceneNodeId = node.Id }; db.SceneContents.Add(content); }
            content.ContentJson = string.Join("\n\n", section.Pages.OrderBy(p => p.OrderIndex).ThenBy(p => p.Id).Select(p => p.Content));
            content.LanguageCode = section.LanguageCode; content.UpdatedAtUtc = DateTimeOffset.UtcNow;
            var note = await db.SceneNotes.SingleOrDefaultAsync(n => n.SceneNodeId == node.Id, ct);
            if (node.Notes is not null) { if (note is null) { note = new() { SceneNodeId = node.Id }; db.SceneNotes.Add(note); } note.NotesText = node.Notes; note.UpdatedAtUtc = DateTimeOffset.UtcNow; }
            else if (note is not null) db.SceneNotes.Remove(note);
            var card = await db.SceneCards.SingleOrDefaultAsync(n => n.SceneNodeId == node.Id, ct);
            if (node.Card is { } c) { if (card is null) { card = new() { SceneNodeId = node.Id }; db.SceneCards.Add(card); } ApplyCard(card, c); }
            else if (card is not null) db.SceneCards.Remove(card);
            if (project.Version >= 2) await ApplyAnnotationsAsync(node, record.OwnerUserId, ct);
        }
        if (project.Version >= 2) await ApplySynopsisAsync(document.Id, project.Synopsis, ct);
    }

    private static SyncSceneCard ToCard(SceneCardRecord c) => new(c.NarrativePurpose, c.NarrativeRole, c.NarrativeIntent, c.EmotionalBeat,
        c.KeyEvents, c.OpenQuestions, c.Summary, c.Status, c.PovCharacterId, c.PlaceId, c.TimelineEventId, c.TimeRef, c.TagsJson, c.SubplotTagsJson, c.ReferencesJson);
    private static void ApplyCard(SceneCardRecord c, SyncSceneCard s)
    {
        c.NarrativePurpose=s.NarrativePurpose; c.NarrativeRole=s.NarrativeRole; c.NarrativeIntent=s.NarrativeIntent; c.EmotionalBeat=s.EmotionalBeat;
        c.KeyEvents=s.KeyEvents; c.OpenQuestions=s.OpenQuestions; c.Summary=s.Summary; c.Status=s.Status; c.PovCharacterId=s.PovCharacterId;
        c.PlaceId=s.PlaceId; c.TimelineEventId=s.TimelineEventId; c.TimeRef=s.TimeRef; c.TagsJson=s.TagsJson; c.SubplotTagsJson=s.SubplotTagsJson;
        c.ReferencesJson=s.ReferencesJson; c.UpdatedAtUtc=DateTimeOffset.UtcNow;
    }
}
