using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.State;
using WriterApp.Application.AI;
using WriterApp.Data.Documents;
using WriterApp.Shared;

namespace WriterApp.Controllers;
public sealed partial class AiActionsController
{
    // A current receipt must never bless older client-provided writing or storyboard context.
    private async Task<AiActionExecuteRequestDto> PrepareWebInput(string key,AiActionExecuteRequestDto request,Guid section,CancellationToken ct)
    {
        var source=request.WebSource!;
        var pages=await _dbContext.Pages.AsNoTracking().Where(p=>p.DocumentId==source.DocumentId && p.SectionId==section)
            .OrderBy(p=>p.OrderIndex).ThenBy(p=>p.Id).ToArrayAsync(ct);
        string text=source.SceneId is { } scene
            ? PlainTextMapper.ToPlainText(await _dbContext.SceneContents.AsNoTracking().Where(s=>s.SceneNodeId==scene).Select(s=>s.ContentJson).SingleOrDefaultAsync(ct) ?? "")
            : request.PageId is { } page && request.SelectionStart is not null
                ? PlainTextMapper.ToPlainText(pages.Single(p=>p.Id==page).Content)
                : string.Join("\n\n",pages.Select(p=>PlainTextMapper.ToPlainText(p.Content)));
        if(text.Length>100_000)throw new InvalidDataException("This checked action supports up to 100,000 characters. Choose a smaller manuscript section.");
        if(request.SelectionStart is not null && (string.IsNullOrWhiteSpace(request.OriginalText) || !text.Contains(request.OriginalText,StringComparison.Ordinal)))
            throw new InvalidDataException("The selected text differs from saved writing. Save and select the current text again.");
        // Structured writing/translation already carry exact saved run mappings and their own bounds.
        if(request.Parameters?.ContainsKey(WritingActions.Parameter)!=true && request.Parameters?.ContainsKey(TranslationStructures.Parameter)!=true)
            request=request with { SurroundingText=text };
        var parameters=request.Parameters is null ? new Dictionary<string,object?>() : new(request.Parameters);
        if (parameters.TryGetValue(StyleQualityReview.Parameter, out var goal)) {
            _ = StyleQualityReview.Goal(goal?.ToString() ?? "");
            if (key != "custom_transform" || request.PageId is null || request.SelectionStart is not { } start || request.SelectionEnd is not { } end
                || start < 0 || end <= start || end > text.Length || text[start..end] != request.OriginalText
                || parameters.GetValueOrDefault("scope")?.ToString() != "selection")
                throw new InvalidDataException("Style review needs an exact saved page or selection range.");
        }
        if(key.StartsWith("storyboard.",StringComparison.Ordinal)) {
            var nodes=await _dbContext.ProjectNodes.AsNoTracking().Where(n=>n.DocumentId==source.DocumentId && n.SyncDeletionId==null)
                .OrderBy(n=>n.OrderIndex).ThenBy(n=>n.Id).ToArrayAsync(ct);
            var ids=nodes.Select(n=>n.Id).ToArray();
            var cards=await _dbContext.SceneCards.AsNoTracking().Where(c=>ids.Contains(c.SceneNodeId)).ToDictionaryAsync(c=>c.SceneNodeId,ct);
            var project=await _dbContext.Projects.AsNoTracking().Where(p=>p.Id==source.ProjectId).Select(p=>p.Title).SingleAsync(ct);
            if (key == "storyboard.suggest-next-scene") {
                var anchor = nodes.SingleOrDefault(n => n.NodeType == ProjectNodeType.Scene && n.LinkedSectionId == section);
                parameters["selected_scene_title"] = anchor?.Title ?? "";
                parameters["preferred_chapter_title"] = nodes.SingleOrDefault(n => n.Id == anchor?.ParentId)?.Title ?? "";
            }
            parameters["storyboard_context"]=StoryboardAnalysis.Build(project,nodes.Select(n=> {
                var c=cards.GetValueOrDefault(n.Id);
                var card=c is null ? null : new WriterApp.Shared.Sync.SyncSceneCard(c.NarrativePurpose,c.NarrativeRole,c.NarrativeIntent,c.EmotionalBeat,
                    c.KeyEvents,c.OpenQuestions,c.Summary,c.Status,c.PovCharacterId,c.PlaceId,c.TimelineEventId,c.TimeRef,c.TagsJson,c.SubplotTagsJson,c.ReferencesJson);
                return new StoryboardAnalysisNode(n.Id,n.ParentId,n.OrderIndex,n.NodeType.ToString().ToLowerInvariant(),n.Title,card,n.MetadataJson);
            }));
        }
        if(key.StartsWith("scene.",StringComparison.Ordinal) || key=="continuity.check_section") {
            if(key.StartsWith("scene.",StringComparison.Ordinal)) {
                var card=source.SceneId is { } sceneId
                    ? await _dbContext.SceneCards.AsNoTracking().Where(c=>c.SceneNodeId==sceneId).Select(c=>new {c.NarrativePurpose,c.EmotionalBeat,c.KeyEvents,c.OpenQuestions,c.PovCharacterId,c.PlaceId,c.TimelineEventId,c.TimeRef,c.TagsJson,c.ReferencesJson,c.Summary,c.Status,c.SubplotTagsJson,c.NarrativeRole,c.NarrativeIntent}).SingleOrDefaultAsync(ct)
                    : await _dbContext.SectionSceneCards.AsNoTracking().Where(c=>c.SectionId==section).Select(c=>new {c.NarrativePurpose,c.EmotionalBeat,c.KeyEvents,c.OpenQuestions,c.PovCharacterId,c.PlaceId,c.TimelineEventId,c.TimeRef,c.TagsJson,c.ReferencesJson,c.Summary,c.Status,c.SubplotTagsJson,c.NarrativeRole,c.NarrativeIntent}).SingleOrDefaultAsync(ct);
                string saved="{}";
                if(card is not null) saved=JsonSerializer.Serialize(new WriterApp.Application.Documents.SectionSceneCardProposalDto(card.NarrativePurpose,card.EmotionalBeat,card.KeyEvents,card.OpenQuestions,
                    card.PovCharacterId,card.PlaceId,card.TimelineEventId,card.TimeRef,JsonSerializer.Deserialize<string[]>(card.TagsJson ?? "[]"),JsonSerializer.Deserialize<WriterApp.Application.Documents.SceneCardReferenceDto[]>(card.ReferencesJson ?? "[]"),
                    card.Summary,card.Status,JsonSerializer.Deserialize<string[]>(card.SubplotTagsJson ?? "[]"),card.NarrativeRole,card.NarrativeIntent),new JsonSerializerOptions(JsonSerializerDefaults.Web));
                parameters["current_scene_card"]=saved;request=request with { OriginalText=saved };
            }
            // Entity identities are owned server data, never an arbitrary client-supplied allow list.
            var entities=new List<WriterApp.Application.Documents.SceneEntity>();
            foreach(var bible in await _dbContext.BibleSnapshots.AsNoTracking().Where(b=>b.DocumentId==source.DocumentId).ToArrayAsync(ct)) {
                if(!Enum.TryParse<WriterApp.Shared.Canon.CanonKind>(bible.BibleType,true,out var kind))continue;
                var canon=WriterApp.Shared.Canon.CanonContent.Parse(kind,bible.ContentJson);
                entities.AddRange(canon.Entries.Where(e=>e.Id is not null).Select(e=>new WriterApp.Application.Documents.SceneEntity(
                    kind switch {WriterApp.Shared.Canon.CanonKind.Character=>WriterApp.Application.Documents.SceneEntityKind.Character,WriterApp.Shared.Canon.CanonKind.Place=>WriterApp.Application.Documents.SceneEntityKind.Place,_=>WriterApp.Application.Documents.SceneEntityKind.Timeline},e.Id!,e.Name)));
            }
            foreach(var s in await _dbContext.Sections.AsNoTracking().Where(s=>s.DocumentId==source.DocumentId).ToArrayAsync(ct))
                entities.Add(new(WriterApp.Application.Documents.SceneEntityKind.Section,s.Id.ToString(),s.Title));
            foreach(var n in await _dbContext.ProjectNodes.AsNoTracking().Where(n=>n.DocumentId==source.DocumentId && n.SyncDeletionId==null).ToArrayAsync(ct))
                entities.Add(new(n.NodeType switch {ProjectNodeType.Scene=>WriterApp.Application.Documents.SceneEntityKind.Scene,ProjectNodeType.Chapter=>WriterApp.Application.Documents.SceneEntityKind.Chapter,_=>WriterApp.Application.Documents.SceneEntityKind.Part},n.Id.ToString(),n.Title));
            if(key.StartsWith("scene.",StringComparison.Ordinal))parameters["scene_entities_json"]=JsonSerializer.Serialize(entities,new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        return request with { Parameters=parameters,ExpectedDocumentVersion=request.ExpectedDocumentVersion ?? source.DocumentVersion };
    }
}
