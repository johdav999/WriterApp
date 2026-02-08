using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.Documents;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Continuity
{
    public sealed class BibleRefreshService
    {
        private readonly IAiOrchestrator _aiOrchestrator;
        private readonly IBibleStore _bibleStore;
        private readonly BiblePatchApplier _patchApplier;
        private readonly ILogger<BibleRefreshService> _logger;

        public BibleRefreshService(
            IAiOrchestrator aiOrchestrator,
            IBibleStore bibleStore,
            BiblePatchApplier patchApplier,
            ILogger<BibleRefreshService> logger)
        {
            _aiOrchestrator = aiOrchestrator ?? throw new ArgumentNullException(nameof(aiOrchestrator));
            _bibleStore = bibleStore ?? throw new ArgumentNullException(nameof(bibleStore));
            _patchApplier = patchApplier ?? throw new ArgumentNullException(nameof(patchApplier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BibleSnapshotState> RefreshAsync(
            Document document,
            Guid activeSectionId,
            BibleType bibleType,
            bool fullRebuild,
            CancellationToken ct)
        {
            BibleSnapshotState? existing = await _bibleStore.GetSnapshotAsync(document.DocumentId, bibleType, ct);
            BibleRefreshCursor cursor = existing?.Cursor ?? BibleJson.EmptyCursor();
            Dictionary<Guid, string> currentHashes = ComputeSectionHashes(document);
            List<SectionDeltaPayload> changedSections = ResolveChangedSections(document, cursor.SectionHashes, currentHashes);

            if (!fullRebuild && changedSections.Count == 0 && existing is not null)
            {
                _logger.LogInformation("[BIBLE] Skipping refresh for {BibleType}; no changed sections.", bibleType);
                return existing;
            }

            string actionId = ResolveRefreshActionId(bibleType, fullRebuild);
            string existingJson = existing?.ContentJson ?? BibleJson.EmptyBibleContent(bibleType);
            string deltaJson = JsonSerializer.Serialize(changedSections, BibleJson.JsonOptions);
            string sourceHash = BuildSourceHash(currentHashes);
            Dictionary<string, object?> options = new()
            {
                ["existing_bible_json"] = existingJson,
                ["delta_sections_json"] = deltaJson,
                ["full_rebuild"] = fullRebuild,
                ["bible_type"] = bibleType.ToString()
            };

            AiActionInput input = new(
                document,
                activeSectionId,
                new Commands.TextRange(0, 0),
                string.Empty,
                fullRebuild ? $"Rebuild {bibleType} bible" : $"Refresh {bibleType} bible",
                options);

            AiExecutionResult aiResult = await _aiOrchestrator.ExecuteActionAsync(actionId, input, ct);
            if (!aiResult.Succeeded || aiResult.Proposal is null || string.IsNullOrWhiteSpace(aiResult.Proposal.ProposedText))
            {
                throw new InvalidOperationException(aiResult.ErrorMessage ?? $"{bibleType} bible refresh failed.");
            }

            if (!_patchApplier.TryApply(bibleType, existingJson, aiResult.Proposal.ProposedText!, out BiblePatchApplyResult patchResult))
            {
                throw new InvalidOperationException($"{bibleType} bible patch validation failed.");
            }

            BibleRefreshStats stats = patchResult.Stats with
            {
                ChangedSections = changedSections.Count,
                NewSections = changedSections.Count(item => item.IsNew),
                DeletedSections = cursor.SectionHashes.Keys.Except(currentHashes.Keys).Count()
            };

            BibleRefreshCursor nextCursor = new(currentHashes, DateTimeOffset.UtcNow, "bySectionHash-v1");
            BibleSnapshotState saved = await _bibleStore.UpsertSnapshotAsync(
                document.DocumentId,
                bibleType,
                patchResult.ContentJson,
                sourceHash,
                nextCursor,
                stats,
                ct);

            _logger.LogInformation(
                "[BIBLE] Refreshed {BibleType} doc={DocumentId} delta={Delta} bytesIn={BytesIn} bytesOut={BytesOut}",
                bibleType,
                document.DocumentId,
                changedSections.Count,
                aiResult.Proposal.ProposedText!.Length,
                saved.ContentJson.Length);

            return saved;
        }

        private static string ResolveRefreshActionId(BibleType bibleType, bool fullRebuild)
        {
            if (fullRebuild)
            {
                return bibleType switch
                {
                    BibleType.Character => ExtractCharacterBibleAction.ActionIdValue,
                    BibleType.Place => ExtractPlaceBibleAction.ActionIdValue,
                    _ => ExtractTimelineBibleAction.ActionIdValue
                };
            }

            return bibleType switch
            {
                BibleType.Character => RefreshCharacterBibleAction.ActionIdValue,
                BibleType.Place => RefreshPlaceBibleAction.ActionIdValue,
                _ => RefreshTimelineBibleAction.ActionIdValue
            };
        }

        private static Dictionary<Guid, string> ComputeSectionHashes(Document document)
        {
            Dictionary<Guid, string> hashes = new();
            foreach (Section section in document.Chapters.SelectMany(chapter => chapter.Sections))
            {
                string plain = State.PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
                hashes[section.SectionId] = ComputeHash(plain);
            }

            return hashes;
        }

        private static List<SectionDeltaPayload> ResolveChangedSections(
            Document document,
            Dictionary<Guid, string> previousHashes,
            Dictionary<Guid, string> currentHashes)
        {
            List<SectionDeltaPayload> deltas = new();
            foreach (Section section in document.Chapters.SelectMany(chapter => chapter.Sections).OrderBy(section => section.Order))
            {
                string plain = State.PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
                bool isNew = !previousHashes.TryGetValue(section.SectionId, out string? oldHash);
                string newHash = currentHashes[section.SectionId];
                if (!isNew && string.Equals(oldHash, newHash, StringComparison.Ordinal))
                {
                    continue;
                }

                deltas.Add(new SectionDeltaPayload(section.SectionId, section.Title ?? string.Empty, section.Order, plain, isNew));
            }

            return deltas;
        }

        private static string BuildSourceHash(Dictionary<Guid, string> sectionHashes)
        {
            StringBuilder builder = new();
            foreach (KeyValuePair<Guid, string> item in sectionHashes.OrderBy(entry => entry.Key))
            {
                builder.Append(item.Key);
                builder.Append(':');
                builder.Append(item.Value);
                builder.Append('|');
            }

            return ComputeHash(builder.ToString());
        }

        private static string ComputeHash(string input)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input ?? string.Empty));
            return Convert.ToHexString(bytes);
        }

        private sealed record SectionDeltaPayload(
            Guid SectionId,
            string Title,
            int Order,
            string Content,
            bool IsNew);
    }
}
