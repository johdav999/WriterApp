using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Sync;
using WriterApp.Application.Collaboration;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.AI;

/// <summary>Read-only checked manuscript adapter. Shared prose comes from committed projections.</summary>
public sealed class BrainstormingStorySourceReader(AppDbContext db, string owner, CollaborationAccessService? access = null)
{
    public Task<BrainstormingStorySnapshot> ReadAsync(Guid documentId, Guid? expectedProject = null, CancellationToken ct = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct) : null;
            var result = await ReadCore(documentId, expectedProject, ct);
            if (tx is not null) await tx.CommitAsync(ct);
            return result;
        });
    private async Task<BrainstormingStorySnapshot> ReadCore(Guid documentId, Guid? expectedProject, CancellationToken ct)
    {
        var checkedSources = new WebAiSourceService(db, owner, access);
        var lease = await checkedSources.Capture(documentId, null, null, null, ct);
        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == documentId, ct);
        if (expectedProject is not null && document.ProjectId != expectedProject) throw new BrainstormingAccessException();
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == document.ProjectId && p.OwnerUserId == document.OwnerUserId, ct);
        var shared = document.CollaborationMode == CollaborationMode.Collaborative
            ? await CollaborationProjectionService.Read(db, owner, documentId, new(), ct, access, CollaborationOperation.PrepareAi) : null;
        var sectionQuery = db.Sections.AsNoTracking().Where(s => s.DocumentId == documentId);
        var pageQuery = db.Pages.AsNoTracking().Where(p => p.DocumentId == documentId && db.Sections.Any(s => s.Id == p.SectionId && s.DocumentId == documentId));
        if (shared is not null) {
            var liveSections = shared.Sections.Select(s => s.SectionId).ToArray(); var livePages = shared.Pages.Select(p => p.PageId).ToArray();
            sectionQuery = sectionQuery.Where(s => liveSections.Contains(s.Id)); pageQuery = pageQuery.Where(p => livePages.Contains(p.Id));
        }
        var sections = await sectionQuery.OrderBy(s => s.OrderIndex).ThenBy(s => s.Id).Take(1001).ToArrayAsync(ct);
        var pages = await pageQuery.OrderBy(p => p.OrderIndex).ThenBy(p => p.Id).Take(1001).ToArrayAsync(ct);
        var nodes = await db.ProjectNodes.AsNoTracking().Where(n => n.DocumentId == documentId && n.ProjectId == document.ProjectId && n.SyncDeletionId == null).Take(1001).ToArrayAsync(ct);
        if (nodes.Length > 1000) throw new InvalidDataException("Story outline exceeds the supported source size.");
        if (shared is not null) {
            var excluded = nodes.Where(n => n.LinkedSectionId is { } section && !sections.Any(s => s.Id == section)).Select(n => n.Id).ToHashSet();
            while (nodes.Any(n => !excluded.Contains(n.Id) && n.ParentId is { } parent && excluded.Contains(parent)))
                foreach (var node in nodes.Where(n => n.ParentId is { } parent && excluded.Contains(parent))) excluded.Add(node.Id);
            nodes = nodes.Where(n => !excluded.Contains(n.Id)).ToArray();
        }
        var outline = WritingOutline.Create(documentId, shared is not null || project.SyncEnabled || nodes.Length != 0 ? document.ProjectId : null,
            lease.DocumentVersion, sections.Select(s => new WritingOutlineSection(s.Id, s.OrderIndex, s.Title)),
            nodes.Select(n => new WritingOutlineNode(n.Id, n.ParentId, n.OrderIndex, n.NodeType.ToString().ToLowerInvariant(), n.Title, n.LinkedSectionId)));
        var storyPages = pages.Select(p => new BrainstormingStoryPage(p.Id, p.SectionId, p.OrderIndex, p.Title,
            shared is null ? p.Content : shared.Pages.Single(active => active.PageId == p.Id).Html, "Html", lease.DocumentVersion, BrainstormingFreshness.Saved)).ToArray();
        var refs = new List<BrainstormingStoryReference>();
        void Add(Guid id, BrainstormingSourceKind kind, string title, object text, string revision, Guid? node = null) => refs.Add(new(id, documentId, kind, title[..Math.Min(200, title.Length)],
            text is string str ? str : JsonSerializer.Serialize(text, BrainstormingValidation.Json), revision, BrainstormingFreshness.Saved, BrainstormingSourceNature.PlanningIntent, NodeId: node));
        var synopsis = await db.DocumentSynopses.AsNoTracking().SingleOrDefaultAsync(s => s.DocumentId == documentId, ct);
        if (synopsis is not null) Add(BrainstormingStorySources.Identity(documentId, "synopsis"), BrainstormingSourceKind.Synopsis, "Synopsis",
            new SyncSynopsis(synopsis.Logline, synopsis.Premise, synopsis.Theme, synopsis.ProtagonistArc, synopsis.CentralConflict, synopsis.Stakes, synopsis.Setting, synopsis.EndingIntent, synopsis.OpenQuestions, synopsis.Notes), synopsis.UpdatedAt.ToString("O"));
        else refs.Add(new(BrainstormingStorySources.Identity(documentId,"synopsis"),documentId,BrainstormingSourceKind.Synopsis,"Synopsis","",lease.DocumentVersion,BrainstormingFreshness.Saved,BrainstormingSourceNature.PlanningIntent,BrainstormingSourceAvailability.Missing));
        var nodeIds = nodes.Select(n => n.Id).ToArray();
        foreach (var card in await db.SceneCards.AsNoTracking().Where(c => nodeIds.Contains(c.SceneNodeId)).Take(1001).ToArrayAsync(ct))
            Add(BrainstormingStorySources.Identity(documentId, "card:" + card.SceneNodeId), BrainstormingSourceKind.Scene, "Scene: " + nodes.Single(n => n.Id == card.SceneNodeId).Title,
                new SyncSceneCard(card.NarrativePurpose, card.NarrativeRole, card.NarrativeIntent, card.EmotionalBeat, card.KeyEvents, card.OpenQuestions, card.Summary, card.Status,
                    card.PovCharacterId, card.PlaceId, card.TimelineEventId, card.TimeRef, card.TagsJson, card.SubplotTagsJson, card.ReferencesJson), card.UpdatedAtUtc.ToString("O"), card.SceneNodeId);
        foreach (var note in await db.SceneNotes.AsNoTracking().Where(n => nodeIds.Contains(n.SceneNodeId)).Take(1001).ToArrayAsync(ct))
            Add(BrainstormingStorySources.Identity(documentId, "note:" + note.SceneNodeId), BrainstormingSourceKind.StoryNote, "Scene notes", note.NotesText, note.UpdatedAtUtc.ToString("O"), note.SceneNodeId);
        var sectionIds = sections.Select(s => s.Id).ToArray(); var pageIds = pages.Select(p => p.Id).ToArray();
        foreach (var note in await db.SectionNotes.AsNoTracking().Where(n => sectionIds.Contains(n.SectionId)).Take(1001).ToArrayAsync(ct))
            Add(BrainstormingStorySources.Identity(documentId, "note:" + note.SectionId), BrainstormingSourceKind.StoryNote, "Section notes: " + sections.Single(s => s.Id == note.SectionId).Title, note.NotesText, note.UpdatedAtUtc.ToString("O"));
        foreach (var note in await db.PageNotes.AsNoTracking().Where(n => pageIds.Contains(n.PageId)).Take(1001).ToArrayAsync(ct))
            Add(BrainstormingStorySources.Identity(documentId, "note:" + note.PageId), BrainstormingSourceKind.StoryNote, "Page notes: " + pages.Single(p => p.Id == note.PageId).Title, note.Notes, note.UpdatedAt.ToString("O"));
        foreach (var kind in new[] { BrainstormingSourceKind.Character, BrainstormingSourceKind.Place, BrainstormingSourceKind.Timeline })
            refs.Add(new(BrainstormingStorySources.Identity(documentId, kind.ToString()), documentId, kind, kind + " canon", "", lease.DocumentVersion,
                BrainstormingFreshness.Cached, BrainstormingSourceNature.ExtractedCanon, BrainstormingSourceAvailability.NotLoaded));
        var result = new BrainstormingStorySnapshot(1, outline, document.Title, BrainstormingStorySources.Stamp(lease.DocumentVersion, outline, new { Pages = storyPages, References = refs }), storyPages, refs, true, "");
        BrainstormingStorySources.Validate(result); await checkedSources.Require(lease, ct); ct.ThrowIfCancellationRequested(); return result;
    }
}
