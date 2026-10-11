using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Sync;

namespace WriterApp.Application.AI;

/// <summary>Read-only owned manuscript adapter. No provider call, save, canon refresh or structure command.</summary>
public sealed class BrainstormingStorySourceReader(AppDbContext db, string owner)
{
    public async Task<BrainstormingStorySnapshot> ReadAsync(Guid documentId, Guid? expectedProject = null, CancellationToken ct = default)
    {
        var checkedSources = new WebAiSourceService(db, owner);
        var lease = await checkedSources.Capture(documentId, null, null, null, ct);
        var document = await db.Documents.AsNoTracking().SingleAsync(d => d.Id == documentId, ct);
        if (expectedProject is not null && document.ProjectId != expectedProject) throw new BrainstormingAccessException();
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == document.ProjectId && p.OwnerUserId == owner, ct);
        var sections = await db.Sections.AsNoTracking().Where(s => s.DocumentId == documentId).OrderBy(s => s.OrderIndex).ThenBy(s => s.Id).Take(1001).ToArrayAsync(ct);
        var pages = await db.Pages.AsNoTracking().Where(p => p.DocumentId == documentId && db.Sections.Any(s => s.Id == p.SectionId && s.DocumentId == documentId)).OrderBy(p => p.OrderIndex).ThenBy(p => p.Id).Take(1001).ToArrayAsync(ct);
        var nodes = await db.ProjectNodes.AsNoTracking().Where(n => n.DocumentId == documentId && n.ProjectId == document.ProjectId && n.SyncDeletionId == null).Take(1001).ToArrayAsync(ct);
        var outline = WritingOutline.Create(documentId, project.SyncEnabled || nodes.Length != 0 ? document.ProjectId : null,
            lease.DocumentVersion, sections.Select(s => new WritingOutlineSection(s.Id, s.OrderIndex, s.Title)),
            nodes.Select(n => new WritingOutlineNode(n.Id, n.ParentId, n.OrderIndex, n.NodeType.ToString().ToLowerInvariant(), n.Title, n.LinkedSectionId)));
        var storyPages = pages.Select(p => new BrainstormingStoryPage(p.Id, p.SectionId, p.OrderIndex, p.Title, p.Content, "Html", lease.DocumentVersion, BrainstormingFreshness.Saved)).ToArray();
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
