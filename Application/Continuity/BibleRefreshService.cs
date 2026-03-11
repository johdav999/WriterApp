using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.Documents;
using WriterApp.Application.Subscriptions;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Continuity
{
    public sealed class BibleRefreshService
    {
        private const int MaxDiagnosticPayloadLength = 4000;
        private readonly IAiOrchestrator _aiOrchestrator;
        private readonly IEntitlementService _entitlementService;
        private readonly IBibleStore _bibleStore;
        private readonly BiblePatchApplier _patchApplier;
        private readonly ILogger<BibleRefreshService> _logger;

        public BibleRefreshService(
            IAiOrchestrator aiOrchestrator,
            IEntitlementService entitlementService,
            IBibleStore bibleStore,
            BiblePatchApplier patchApplier,
            ILogger<BibleRefreshService> logger)
        {
            _aiOrchestrator = aiOrchestrator ?? throw new ArgumentNullException(nameof(aiOrchestrator));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _bibleStore = bibleStore ?? throw new ArgumentNullException(nameof(bibleStore));
            _patchApplier = patchApplier ?? throw new ArgumentNullException(nameof(patchApplier));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<BibleSnapshotState> RefreshAsync(
            Document document,
            string userId,
            Guid activeSectionId,
            BibleType bibleType,
            bool fullRebuild,
            CancellationToken ct)
        {
            const string featureKey = "ai.bibles.refresh";
            UserEntitlements entitlements = await _entitlementService.GetEntitlementsAsync(userId);
            bool aiEnabled = await _entitlementService.HasAsync(userId, "ai.enabled");
            if (!aiEnabled)
            {
                _logger.LogInformation(
                    "[BIBLE] Entitlement denied. FeatureKey={FeatureKey} UserId={UserId} PlanKey={PlanKey} BibleType={BibleType}",
                    featureKey,
                    userId,
                    entitlements.PlanKey,
                    bibleType);
                throw new EntitlementDeniedException(
                    featureKey,
                    entitlements.PlanKey,
                    "Upgrade to enable Bible refresh.");
            }

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

            string payloadForDiagnostics = aiResult.Proposal.ProposedText!;
            bool repairAttempted = false;
            if (!_patchApplier.TryApply(
                bibleType,
                existingJson,
                payloadForDiagnostics,
                out BiblePatchApplyResult patchResult,
                out string failureReason))
            {
                string preview = CreatePreview(payloadForDiagnostics);
                IReadOnlyList<SectionDeltaPayload> fallbackSections = changedSections.Count > 0
                    ? changedSections
                    : ResolveAllSections(document);

                if (ShouldAttemptCharacterRepair(bibleType, failureReason))
                {
                    repairAttempted = true;
                    _logger.LogWarning(
                        "[BIBLE] Character refresh payload parse failed; attempting JSON repair. BibleType={BibleType} DocumentId={DocumentId} ActionId={ActionId} Reason={Reason} Preview={Preview}",
                        bibleType,
                        document.DocumentId,
                        actionId,
                        failureReason,
                        preview);

                    CharacterRepairAttemptResult repairResult = await TryRepairCharacterPayloadAsync(
                        actionId,
                        input,
                        existingJson,
                        payloadForDiagnostics,
                        failureReason,
                        ct);

                    if (repairResult.Succeeded)
                    {
                        patchResult = repairResult.PatchResult;
                        payloadForDiagnostics = repairResult.Payload;
                    }
                    else
                    {
                        failureReason = repairResult.FailureReason;
                        preview = CreatePreview(repairResult.Payload);
                        payloadForDiagnostics = repairResult.Payload;

                        _logger.LogWarning(
                            "[BIBLE] Invalid refresh payload after repair attempt. BibleType={BibleType} DocumentId={DocumentId} ActionId={ActionId} Reason={Reason} Preview={Preview}",
                            bibleType,
                            document.DocumentId,
                            actionId,
                            failureReason,
                            preview);
                        throw new BibleRefreshInvalidPayloadException(
                            bibleType,
                            document.DocumentId,
                            actionId,
                            failureReason,
                            preview,
                            CreateDiagnosticPayload(payloadForDiagnostics),
                            repairAttempted);
                    }
                }
                else if (bibleType == BibleType.Timeline
                    && TryApplyTimelineFallbackPatch(existingJson, fallbackSections, out patchResult))
                {
                    _logger.LogWarning(
                        "[BIBLE] Timeline patch validation failed; applied deterministic fallback patch with {SectionCount} sections. preview={Preview}",
                        fallbackSections.Count,
                        preview);
                }
                else
                {
                    _logger.LogWarning(
                        "[BIBLE] Invalid refresh payload. BibleType={BibleType} DocumentId={DocumentId} ActionId={ActionId} Reason={Reason} Preview={Preview}",
                        bibleType,
                        document.DocumentId,
                        actionId,
                        failureReason,
                        preview);
                    throw new BibleRefreshInvalidPayloadException(
                        bibleType,
                        document.DocumentId,
                        actionId,
                        failureReason,
                        preview,
                        CreateDiagnosticPayload(payloadForDiagnostics),
                        repairAttempted);
                }
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
                payloadForDiagnostics.Length,
                saved.ContentJson.Length);

            return saved;
        }

        private async Task<CharacterRepairAttemptResult> TryRepairCharacterPayloadAsync(
            string actionId,
            AiActionInput originalInput,
            string existingJson,
            string invalidPayload,
            string failureReason,
            CancellationToken ct)
        {
            Dictionary<string, object?> repairOptions = originalInput.Options is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(originalInput.Options);

            repairOptions["repair_invalid_json"] = true;
            repairOptions["invalid_json_payload"] = invalidPayload;
            repairOptions["invalid_json_failure_reason"] = failureReason;

            AiActionInput repairInput = originalInput with
            {
                Instruction = "Re-emit the prior character bible response as one valid JSON object only.",
                Options = repairOptions
            };

            AiExecutionResult repairResult = await _aiOrchestrator.ExecuteActionAsync(actionId, repairInput, ct);
            if (!repairResult.Succeeded || repairResult.Proposal is null || string.IsNullOrWhiteSpace(repairResult.Proposal.ProposedText))
            {
                return new CharacterRepairAttemptResult(
                    false,
                    new BiblePatchApplyResult(existingJson, BibleJson.EmptyStats()),
                    invalidPayload,
                    repairResult.ErrorMessage ?? "AI repair attempt did not return structured output.");
            }

            string repairedPayload = repairResult.Proposal.ProposedText!;
            bool ok = _patchApplier.TryApply(
                BibleType.Character,
                existingJson,
                repairedPayload,
                out BiblePatchApplyResult patchResult,
                out string repairFailureReason);
            return new CharacterRepairAttemptResult(ok, patchResult, repairedPayload, repairFailureReason);
        }

        private static bool ShouldAttemptCharacterRepair(BibleType bibleType, string failureReason)
        {
            if (bibleType != BibleType.Character || string.IsNullOrWhiteSpace(failureReason))
            {
                return false;
            }

            return failureReason.Contains("JSON could not be parsed into an object", StringComparison.OrdinalIgnoreCase)
                || failureReason.Contains("Patch application failed while reading JSON", StringComparison.OrdinalIgnoreCase);
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

        private static List<SectionDeltaPayload> ResolveAllSections(Document document)
        {
            return document.Chapters
                .SelectMany(chapter => chapter.Sections)
                .OrderBy(section => section.Order)
                .Select(section =>
                {
                    string plain = State.PlainTextMapper.ToPlainText(section.Content.Value ?? string.Empty);
                    return new SectionDeltaPayload(
                        section.SectionId,
                        section.Title ?? string.Empty,
                        section.Order,
                        plain,
                        true);
                })
                .ToList();
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

        private static string CreatePreview(string value, int maxLength = 360)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "<empty>";
            }

            string normalized = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength) + "...";
        }

        private static string CreateDiagnosticPayload(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim();
            return normalized.Length <= MaxDiagnosticPayloadLength
                ? normalized
                : normalized[..MaxDiagnosticPayloadLength];
        }

        private bool TryApplyTimelineFallbackPatch(
            string existingJson,
            IReadOnlyList<SectionDeltaPayload> changedSections,
            out BiblePatchApplyResult result)
        {
            result = new BiblePatchApplyResult(existingJson, BibleJson.EmptyStats());
            if (changedSections.Count == 0)
            {
                return false;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            JsonArray ops = new();

            foreach (SectionDeltaPayload section in changedSections.OrderBy(item => item.Order))
            {
                string summary = BuildTimelineSummary(section.Content);
                JsonObject data = new()
                {
                    ["id"] = section.SectionId.ToString(),
                    ["title"] = string.IsNullOrWhiteSpace(section.Title) ? "Untitled Scene" : section.Title.Trim(),
                    ["timeRef"] = string.Empty,
                    ["order"] = section.Order,
                    ["locationId"] = string.Empty,
                    ["participants"] = new JsonArray(),
                    ["summary"] = summary,
                    ["constraints"] = new JsonArray(),
                    ["lastUpdatedUtc"] = now.ToString("O"),
                    ["evidence"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["sectionId"] = section.SectionId.ToString(),
                            ["quote"] = summary
                        }
                    }
                };

                ops.Add(new JsonObject
                {
                    ["op"] = "upsertTimelineEvent",
                    ["id"] = section.SectionId.ToString(),
                    ["data"] = data
                });
            }

            JsonObject patch = new()
            {
                ["bibleType"] = "Timeline",
                ["schemaVersion"] = 1,
                ["ops"] = ops,
                ["stats"] = new JsonObject
                {
                    ["updatedEntries"] = 0,
                    ["newEntries"] = changedSections.Count,
                    ["flags"] = 0
                }
            };

            return _patchApplier.TryApply(BibleType.Timeline, existingJson, patch.ToJsonString(BibleJson.JsonOptions), out result);
        }

        private static string BuildTimelineSummary(string content, int maxLength = 280)
        {
            string normalized = (content ?? string.Empty)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

            if (normalized.Length <= maxLength)
            {
                return normalized;
            }

            return normalized.Substring(0, maxLength).TrimEnd() + "...";
        }

        private sealed record SectionDeltaPayload(
            Guid SectionId,
            string Title,
            int Order,
            string Content,
            bool IsNew);

        private sealed record CharacterRepairAttemptResult(
            bool Succeeded,
            BiblePatchApplyResult PatchResult,
            string Payload,
            string FailureReason);
    }
}
