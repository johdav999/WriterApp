using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed record SceneContentBackfillResult(
        int TotalScenes,
        int ExistingSceneContent,
        int CreatedSceneContent,
        int CreatedSceneNotes,
        int CreatedSceneCards,
        int FailedScenes);

    public interface ISceneContentBackfillService
    {
        Task<SceneContentBackfillResult> BackfillAsync(CancellationToken ct);
    }

    public sealed class SceneContentBackfillService : ISceneContentBackfillService
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<SceneContentBackfillService> _logger;

        public SceneContentBackfillService(
            AppDbContext dbContext,
            ILogger<SceneContentBackfillService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<SceneContentBackfillResult> BackfillAsync(CancellationToken ct)
        {
            List<ProjectNodeRecord> scenes = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.NodeType == ProjectNodeType.Scene)
                .OrderBy(node => node.ProjectId)
                .ThenBy(node => node.ParentId)
                .ThenBy(node => node.OrderIndex)
                .ThenBy(node => node.Id)
                .ToListAsync(ct);

            HashSet<Guid> existingSceneContent = (await _dbContext.SceneContents
                .AsNoTracking()
                .Select(item => item.SceneNodeId)
                .ToListAsync(ct))
                .ToHashSet();
            HashSet<Guid> existingSceneNotes = (await _dbContext.SceneNotes
                .AsNoTracking()
                .Select(item => item.SceneNodeId)
                .ToListAsync(ct))
                .ToHashSet();
            HashSet<Guid> existingSceneCards = (await _dbContext.SceneCards
                .AsNoTracking()
                .Select(item => item.SceneNodeId)
                .ToListAsync(ct))
                .ToHashSet();

            List<Guid> linkedSectionIds = scenes
                .Where(scene => scene.LinkedSectionId.HasValue)
                .Select(scene => scene.LinkedSectionId!.Value)
                .Distinct()
                .ToList();

            Dictionary<Guid, SectionRecord> sectionsById = linkedSectionIds.Count == 0
                ? new Dictionary<Guid, SectionRecord>()
                : await _dbContext.Sections
                    .AsNoTracking()
                    .Where(section => linkedSectionIds.Contains(section.Id))
                    .ToDictionaryAsync(section => section.Id, ct);

            Dictionary<Guid, List<PageRecord>> pagesBySection = linkedSectionIds.Count == 0
                ? new Dictionary<Guid, List<PageRecord>>()
                : (await _dbContext.Pages
                    .AsNoTracking()
                    .Where(page => linkedSectionIds.Contains(page.SectionId))
                    .OrderBy(page => page.SectionId)
                    .ThenBy(page => page.OrderIndex)
                    .ThenBy(page => page.Id)
                    .ToListAsync(ct))
                    .GroupBy(page => page.SectionId)
                    .ToDictionary(group => group.Key, group => group.ToList());

            Dictionary<Guid, SectionNoteRecord> sectionNotesById = linkedSectionIds.Count == 0
                ? new Dictionary<Guid, SectionNoteRecord>()
                : await _dbContext.SectionNotes
                    .AsNoTracking()
                    .Where(note => linkedSectionIds.Contains(note.SectionId))
                    .ToDictionaryAsync(note => note.SectionId, ct);

            Dictionary<Guid, SectionSceneCardRecord> sectionCardsById = linkedSectionIds.Count == 0
                ? new Dictionary<Guid, SectionSceneCardRecord>()
                : await _dbContext.SectionSceneCards
                    .AsNoTracking()
                    .Where(card => linkedSectionIds.Contains(card.SectionId))
                    .ToDictionaryAsync(card => card.SectionId, ct);

            HashSet<Guid> sceneIds = scenes.Select(scene => scene.Id).ToHashSet();
            int existingCount = existingSceneContent.Count(sceneIds.Contains);
            int createdContent = 0;
            int createdNotes = 0;
            int createdCards = 0;
            int failed = 0;

            foreach (ProjectNodeRecord scene in scenes)
            {
                ct.ThrowIfCancellationRequested();

                if (existingSceneContent.Contains(scene.Id))
                {
                    continue;
                }

                try
                {
                    DateTimeOffset now = DateTimeOffset.UtcNow;
                    string contentJson = string.Empty;
                    string? languageCode = null;
                    Guid? linkedSectionId = scene.LinkedSectionId;

                    if (linkedSectionId.HasValue && sectionsById.TryGetValue(linkedSectionId.Value, out SectionRecord? section))
                    {
                        languageCode = section.LanguageCode;
                        if (pagesBySection.TryGetValue(section.Id, out List<PageRecord>? pages) && pages.Count > 0)
                        {
                            contentJson = string.Join(
                                "\n\n",
                                pages.Select(page => page.Content ?? string.Empty));
                        }

                        if (!existingSceneNotes.Contains(scene.Id)
                            && sectionNotesById.TryGetValue(section.Id, out SectionNoteRecord? sectionNote))
                        {
                            _dbContext.SceneNotes.Add(new SceneNoteRecord
                            {
                                SceneNodeId = scene.Id,
                                NotesText = sectionNote.NotesText ?? string.Empty,
                                UpdatedAtUtc = sectionNote.UpdatedAtUtc
                            });
                            createdNotes++;
                            existingSceneNotes.Add(scene.Id);
                        }

                        if (!existingSceneCards.Contains(scene.Id)
                            && sectionCardsById.TryGetValue(section.Id, out SectionSceneCardRecord? sectionCard))
                        {
                            string? narrativeRole = SceneNarrativeRoleCatalog.NormalizeOptional(sectionCard.NarrativeRole);
                            string? narrativeIntent = SceneNarrativeRoleCatalog.NormalizeOptional(sectionCard.NarrativeIntent);
                            if (narrativeRole is null && narrativeIntent is null)
                            {
                                if (SceneNarrativeRoleCatalog.TryNormalize(sectionCard.NarrativePurpose, out string? normalizedRole))
                                {
                                    narrativeRole = normalizedRole;
                                }
                                else
                                {
                                    narrativeIntent = SceneNarrativeRoleCatalog.NormalizeOptional(sectionCard.NarrativePurpose);
                                }
                            }

                            _dbContext.SceneCards.Add(new SceneCardRecord
                            {
                                SceneNodeId = scene.Id,
                                NarrativePurpose = SceneNarrativeRoleCatalog.ToLegacyPurpose(narrativeRole, narrativeIntent),
                                NarrativeRole = narrativeRole,
                                NarrativeIntent = narrativeIntent,
                                EmotionalBeat = sectionCard.EmotionalBeat,
                                KeyEvents = sectionCard.KeyEvents,
                                OpenQuestions = sectionCard.OpenQuestions,
                                Summary = sectionCard.Summary,
                                Status = sectionCard.Status,
                                PovCharacterId = sectionCard.PovCharacterId,
                                PlaceId = sectionCard.PlaceId,
                                TimelineEventId = sectionCard.TimelineEventId,
                                TimeRef = sectionCard.TimeRef,
                                TagsJson = sectionCard.TagsJson,
                                SubplotTagsJson = sectionCard.SubplotTagsJson,
                                ReferencesJson = sectionCard.ReferencesJson,
                                UpdatedAtUtc = sectionCard.UpdatedUtc
                            });
                            createdCards++;
                            existingSceneCards.Add(scene.Id);
                        }
                    }

                    _dbContext.SceneContents.Add(new SceneContentRecord
                    {
                        SceneNodeId = scene.Id,
                        ContentJson = contentJson,
                        LanguageCode = languageCode,
                        UpdatedAtUtc = now
                    });

                    await _dbContext.SaveChangesAsync(ct);
                    createdContent++;
                    existingSceneContent.Add(scene.Id);
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(
                        ex,
                        "Scene content backfill failed for SceneNodeId={SceneNodeId}.",
                        scene.Id);
                    _dbContext.ChangeTracker.Clear();
                }
            }

            _logger.LogInformation(
                "Scene content backfill completed. TotalScenes={TotalScenes}, ExistingSceneContent={ExistingSceneContent}, CreatedSceneContent={CreatedSceneContent}, CreatedSceneNotes={CreatedSceneNotes}, CreatedSceneCards={CreatedSceneCards}, FailedScenes={FailedScenes}.",
                scenes.Count,
                existingCount,
                createdContent,
                createdNotes,
                createdCards,
                failed);

            return new SceneContentBackfillResult(
                scenes.Count,
                existingCount,
                createdContent,
                createdNotes,
                createdCards,
                failed);
        }
    }
}
