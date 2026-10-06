using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Application.Documents;
using WriterApp.Shared;

namespace WriterApp.Application.AI;

public sealed class WebAiSourceService(AppDbContext db, string owner)
{
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    public async Task<WebAiSource> Capture(Guid id, Guid? section, Guid? page, Guid? scene, CancellationToken ct)
    {
        var doc = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && d.OwnerUserId == owner && d.DeletedAtUtc == null && !d.IsArchived, ct)
            ?? throw new DocumentSyncException(404, "ai.source_missing", "Owned active manuscript not found.");
        if (await db.DeletedUserIdentities.AnyAsync(x => x.UserId == owner, ct)) throw new DocumentSyncException(404, "ai.owner_missing", "Account unavailable.");
        var project = await db.Projects.AsNoTracking().Where(p => p.Id == doc.ProjectId && p.OwnerUserId == owner)
            .Select(p => new { p.Id, p.Title, p.Subtitle, p.AuthorName, p.Language, p.Genre, p.CoverImageUrl }).SingleOrDefaultAsync(ct);
        if (doc.ProjectId != Guid.Empty && project is null) throw new DocumentSyncException(404, "ai.project_missing", "Owned project unavailable.");
        var sync = await db.DocumentSyncRecords.AsNoTracking().SingleOrDefaultAsync(r => r.DocumentId == id && r.OwnerUserId == owner && !r.IsDeleted && !r.IsTrashed, ct)
            ?? throw new DocumentSyncException(409, "ai.capability_missing", "Save/synchronize before requesting checked AI.");
        if(await db.Pages.Where(p=>p.DocumentId==id).SumAsync(p=>(long)p.Content.Length,ct)>2_000_000
            || await db.SceneContents.Where(s=>s.SceneNode!.DocumentId==id).SumAsync(s=>(long)s.ContentJson.Length,ct)>2_000_000
            || await db.BibleSnapshots.Where(b=>b.DocumentId==id).SumAsync(b=>(long)b.ContentJson.Length,ct)>250_000)
            throw new InvalidDataException("Checked source is oversized. Use a smaller manuscript or canon.");
        if(await db.SectionNotes.Where(n=>n.Section!.DocumentId==id).SumAsync(n=>(long)n.NotesText.Length,ct)>2_000_000
            || await db.PageNotes.Where(n=>n.Page!.DocumentId==id).SumAsync(n=>(long)n.Notes.Length,ct)>2_000_000
            || await db.SceneNotes.Where(n=>n.SceneNode!.DocumentId==id).SumAsync(n=>(long)n.NotesText.Length,ct)>2_000_000)
            throw new InvalidDataException("Checked notes exceed the supported source size.");
        var sections = await db.Sections.AsNoTracking().Where(s => s.DocumentId == id).OrderBy(s => s.Id).Take(1001)
            .Select(s => new { s.Id, s.Title, s.NarrativePurpose, s.OrderIndex, s.LanguageCode, s.TranslationGroupId }).ToArrayAsync(ct);
        var pages = await db.Pages.AsNoTracking().Where(p => p.DocumentId == id).OrderBy(p => p.Id).Take(1001)
            .Select(p => new { p.Id, p.SectionId, p.Title, p.OrderIndex, p.Content }).ToArrayAsync(ct);
        var nodes = await db.ProjectNodes.AsNoTracking().Where(n => n.DocumentId == id && n.SyncDeletionId == null).OrderBy(n => n.Id).Take(1001)
            .Select(n => new { n.Id, n.ProjectId, n.ParentId, n.NodeType, n.Title, n.OrderIndex, n.LinkedSectionId, n.MetadataJson }).ToArrayAsync(ct);
        if (section is { } sid && !sections.Any(s => s.Id == sid) || page is { } pid && !pages.Any(p => p.Id == pid && (section == null || p.SectionId == section))
            || scene is { } nid && !nodes.Any(n => n.Id == nid && n.ProjectId == doc.ProjectId && (section == null || n.LinkedSectionId == section)))
            throw new DocumentSyncException(404, "ai.foreign_target", "Choose a current target from this manuscript.");
        if (sections.Length > 1000 || pages.Length > 1000 || nodes.Length > 1000 || nodes.Any(n => n.ProjectId != doc.ProjectId)) throw new InvalidDataException("Checked AI supports up to 1,000 source entities per kind.");
        var ids = nodes.Select(n => n.Id).ToArray();
        var cards = await db.SceneCards.AsNoTracking().Where(c => ids.Contains(c.SceneNodeId)).OrderBy(c => c.SceneNodeId)
            .Select(c => new { c.SceneNodeId, c.NarrativePurpose, c.NarrativeRole, c.NarrativeIntent, c.Summary, c.Status, c.EmotionalBeat, c.KeyEvents, c.OpenQuestions, c.PovCharacterId, c.PlaceId, c.TimelineEventId, c.TimeRef, c.TagsJson, c.SubplotTagsJson, c.ReferencesJson }).ToArrayAsync(ct);
        var synopsis = await db.DocumentSynopses.AsNoTracking().Where(s => s.DocumentId == id)
            .Select(s => new { s.Logline, s.Premise, s.Theme, s.ProtagonistArc, s.CentralConflict, s.Stakes, s.Setting, s.EndingIntent, s.OpenQuestions, s.Notes }).SingleOrDefaultAsync(ct);
        var canon = await db.BibleSnapshots.AsNoTracking().Where(b => b.DocumentId == id).OrderBy(b => b.BibleType)
            .Select(b => new { b.BibleType, b.ContentJson, b.LastRefreshSourceHash }).ToArrayAsync(ct);
        var sectionIds=sections.Select(s=>s.Id).ToArray();
        var sceneContent=await db.SceneContents.AsNoTracking().Where(s=>ids.Contains(s.SceneNodeId)).OrderBy(s=>s.SceneNodeId).Select(s=>new { s.SceneNodeId,s.ContentJson,s.LanguageCode }).ToArrayAsync(ct);
        var sceneNotes=await db.SceneNotes.AsNoTracking().Where(s=>ids.Contains(s.SceneNodeId)).OrderBy(s=>s.SceneNodeId).Select(s=>new { s.SceneNodeId,s.NotesText }).ToArrayAsync(ct);
        var sectionCards=await db.SectionSceneCards.AsNoTracking().Where(c=>sectionIds.Contains(c.SectionId)).OrderBy(c=>c.SectionId)
            .Select(c=>new { c.SectionId,c.NarrativePurpose,c.NarrativeRole,c.NarrativeIntent,c.Summary,c.Status,c.EmotionalBeat,c.KeyEvents,c.OpenQuestions,c.PovCharacterId,c.PlaceId,c.TimelineEventId,c.TimeRef,c.TagsJson,c.SubplotTagsJson,c.ReferencesJson }).ToArrayAsync(ct);
        var outline=await db.DocumentOutlineNodes.AsNoTracking().Where(n=>n.DocumentId==id).OrderBy(n=>n.Id).Take(1001)
            .Select(n=>new {n.Id,n.ParentId,n.Order,n.Title,n.Notes,n.MetadataJson,n.LinkedSectionId}).ToArrayAsync(ct);
        if(outline.Length>1000)throw new InvalidDataException("Checked source supports up to 1,000 outline nodes.");
        var sectionNotes=await db.SectionNotes.AsNoTracking().Where(n=>sectionIds.Contains(n.SectionId)).OrderBy(n=>n.SectionId).Select(n=>new{n.SectionId,n.NotesText}).ToArrayAsync(ct);
        var pageIds=pages.Select(p=>p.Id).ToArray();
        var pageNotes=await db.PageNotes.AsNoTracking().Where(n=>pageIds.Contains(n.PageId)).OrderBy(n=>n.PageId).Select(n=>new{n.PageId,n.Notes}).ToArrayAsync(ct);
        var source = new { doc.Id, doc.ProjectId, doc.Title, doc.LanguageCode, doc.TranslationGroupId, project, sections, pages, nodes, cards, sectionCards, sceneContent, sceneNotes, sectionNotes, pageNotes, outline, synopsis, canon };
        if (JsonSerializer.SerializeToUtf8Bytes(source).Length > 4_000_000) throw new InvalidDataException("Checked source exceeds 4 MB. Use a smaller manuscript.");
        return new(1, Hash(owner), id, doc.ProjectId, sync.Version, Hash(source), section, page, scene);
    }
    public async Task Require(WebAiSource source, CancellationToken ct)
    {
        WebAiSources.RequireCurrent(await Capture(source.DocumentId, source.SectionId, source.PageId, source.SceneId, ct), source);
    }
}
