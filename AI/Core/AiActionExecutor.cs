using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI.StoryCoach;
using WriterApp.Application.Commands;
using WriterApp.Application.Synopsis;

namespace WriterApp.AI.Core
{
    public sealed class AiActionExecutor : IAiActionExecutor
    {
        private readonly IAiRouter _router;
        private readonly IArtifactStore _artifactStore;
        private readonly ILogger<AiActionExecutor> _logger;

        public AiActionExecutor(IAiRouter router, IArtifactStore artifactStore, ILogger<AiActionExecutor> logger)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AiExecutionOutcome> ExecuteAsync(IAiAction action, AiActionInput input, CancellationToken ct)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AiRequest request = action.BuildRequest(input);
            if (string.Equals(action.ActionId, ApplyContinuityFixAction.ActionIdValue, StringComparison.Ordinal))
            {
                return BuildApplyContinuityFixOutcome(action, input, request);
            }

            AiProviderSelection selection = _router.Route(request);
            AiResult result = await selection.Provider.ExecuteAsync(request, ct);
            return BuildOutcome(action, input, request, result, selection.SelectedProviderId);
        }

        private AiExecutionOutcome BuildOutcome(
            IAiAction action,
            AiActionInput input,
            AiRequest request,
            AiResult result,
            string providerId)
        {
            List<ProposedOperation> operations = new();
            List<Guid> artifactIds = new();
            string summaryLabel = string.IsNullOrWhiteSpace(input.Instruction) ? action.DisplayName : input.Instruction;
            string? originalText = null;
            string? proposedText = null;

            if (string.Equals(action.ActionId, RewriteSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = input.SelectedText;
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
            }
            else if (string.Equals(action.ActionId, TranslateSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.SelectionText ?? request.Context.OriginalText ?? input.SelectedText;
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
            }
            else if (string.Equals(action.ActionId, TranslateSectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, TranslateDocumentAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.OriginalText ?? input.SelectedText;
            }
            else if (string.Equals(action.ActionId, CustomTransformAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.SelectionText ?? request.Context.OriginalText ?? input.SelectedText;

                if (input.SelectionRange.Length > 0 && !string.IsNullOrWhiteSpace(input.SelectedText))
                {
                    operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
                }
                else
                {
                    TextRange sectionRange = new(0, (originalText ?? string.Empty).Length);
                    operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, sectionRange, proposedText));
                }
            }
            else if (IsReviseSelectionAction(action.ActionId))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.SelectionText ?? request.Context.OriginalText ?? input.SelectedText;
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
            }
            else if (IsReviseSectionAction(action.ActionId))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.OriginalText ?? string.Empty;
                TextRange sectionRange = new(0, (originalText ?? string.Empty).Length);
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, sectionRange, proposedText));
            }
            else if (string.Equals(action.ActionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = GetInputValue(request, "existing_value");
                string fieldKey = GetInputValue(request, "focus_field_key");

                if (string.IsNullOrWhiteSpace(fieldKey))
                {
                    _logger.LogWarning("Story Coach output rejected: missing target field key.");
                    return AiExecutionOutcome.Rejected(
                        result,
                        providerId,
                        "ai.story_coach_rejected",
                        "Story Coach output rejected: missing target field key.");
                }

                if (!StoryCoachOutputValidator.TryValidate(proposedText, fieldKey, originalText ?? string.Empty, out string reason))
                {
                    _logger.LogWarning(
                        "Story Coach output rejected: fieldKey={FieldKey} reason={Reason}",
                        fieldKey,
                        reason);

                    return AiExecutionOutcome.Rejected(
                        result,
                        providerId,
                        "ai.story_coach_rejected",
                        $"Story Coach output rejected: {reason}");
                }

                proposedText = proposedText.Trim();
                operations.Add(new ReplaceSynopsisFieldOperation(fieldKey, proposedText));
            }
            else if (string.Equals(action.ActionId, SynopsisEvaluateAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, SynopsisQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = null;
            }
            else if (string.Equals(action.ActionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
            }
            else if (string.Equals(action.ActionId, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                string rawJson = textArtifact?.TextContent ?? string.Empty;
                if (!OutlineDraftParser.TryParse(rawJson, out OutlineDraft? outline))
                {
                    _logger.LogWarning("Synopsis outline output rejected: invalid JSON contract.");
                    return AiExecutionOutcome.Rejected(
                        result,
                        providerId,
                        "ai.outline_parse_failed",
                        "Outline response was not valid JSON.");
                }

                string canonicalJson = OutlineDraftParser.ToCanonicalJson(outline!);
                originalText = input.Document.Synopsis?.OutlineDraft ?? string.Empty;
                proposedText = canonicalJson;
                operations.Add(new ReplaceSynopsisFieldOperation("outline_draft", canonicalJson));
            }
            else if (string.Equals(action.ActionId, ExtractCharacterBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, ExtractPlaceBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, ExtractTimelineBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, RefreshCharacterBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, RefreshPlaceBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, RefreshTimelineBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, ContinuityCheckAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
            }
            else if (string.Equals(action.ActionId, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
            }
            else if (string.Equals(action.ActionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, SceneRefineAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
            }
            else if (string.Equals(action.ActionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? imageArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Image);
                if (imageArtifact is not null)
                {
                    _artifactStore.Store(imageArtifact);
                    artifactIds.Add(imageArtifact.ArtifactId);
                    operations.Add(new AttachImageOperation(input.ActiveSectionId, imageArtifact.ArtifactId, "cover"));
                }
            }

            string? proposalReason = IsReviseAction(action.ActionId)
                ? BuildReviseReason(action.ActionId)
                : input.Instruction;

            AiProposal proposal = new(
                Guid.NewGuid(),
                input.ActiveSectionId,
                summaryLabel,
                action.ActionId,
                providerId,
                request.RequestId,
                DateTime.UtcNow,
                proposalReason,
                operations,
                artifactIds,
                BuildUserSummary(action.ActionId, input.Instruction, input.Options),
                BuildTargetScope(action.ActionId),
                input.Instruction,
                originalText,
                proposedText);

            return AiExecutionOutcome.Success(proposal, result, providerId);
        }

        private static string BuildTargetScope(string actionId)
        {
            if (string.Equals(actionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, TranslateDocumentAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Document";
            }

            if (string.Equals(actionId, TranslateSectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, CustomTransformAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (IsReviseSectionAction(actionId))
            {
                return "Section";
            }

            if (IsReviseSelectionAction(actionId))
            {
                return "Selection";
            }

            if (string.Equals(actionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis";
            }

            if (string.Equals(actionId, SynopsisEvaluateAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, SynopsisQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis";
            }

            if (string.Equals(actionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Document";
            }

            if (string.Equals(actionId, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis";
            }

            if (string.Equals(actionId, ExtractCharacterBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ExtractPlaceBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ExtractTimelineBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, RefreshCharacterBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, RefreshPlaceBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, RefreshTimelineBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Document";
            }

            if (string.Equals(actionId, ContinuityCheckAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, ApplyContinuityFixAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, SceneRefineAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            return "Selection";
        }

        private static string BuildUserSummary(string actionId, string? instruction, Dictionary<string, object?>? options)
        {
            if (string.Equals(actionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Generate cover image";
            }

            if (string.Equals(actionId, TranslateSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Translate selection";
            }

            if (string.Equals(actionId, TranslateSectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Translate section";
            }

            if (string.Equals(actionId, TranslateDocumentAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Translate document";
            }

            if (string.Equals(actionId, CustomTransformAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Run custom prompt";
            }

            if (string.Equals(actionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Story Coach suggestion";
            }

            if (string.Equals(actionId, SynopsisEvaluateAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis evaluation";
            }

            if (string.Equals(actionId, SynopsisQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis questions";
            }

            if (string.Equals(actionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Generate outline";
            }

            if (string.Equals(actionId, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Generate synopsis outline";
            }

            if (string.Equals(actionId, ExtractCharacterBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Build character bible";
            }

            if (string.Equals(actionId, ExtractPlaceBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Build place bible";
            }

            if (string.Equals(actionId, ExtractTimelineBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Build timeline bible";
            }

            if (string.Equals(actionId, RefreshCharacterBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Refresh character bible";
            }

            if (string.Equals(actionId, RefreshPlaceBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Refresh place bible";
            }

            if (string.Equals(actionId, RefreshTimelineBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Refresh timeline bible";
            }

            if (string.Equals(actionId, ContinuityCheckAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Check continuity";
            }

            if (string.Equals(actionId, ApplyContinuityFixAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Apply continuity fix";
            }

            if (string.Equals(actionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Suggest scene card";
            }

            if (string.Equals(actionId, SceneRefineAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Refine scene card";
            }

            if (string.Equals(actionId, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Find open questions";
            }

            if (string.Equals(actionId, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Propose next paragraph";
            }

            if (IsReviseAction(actionId))
            {
                string tone = GetOption(options, "tone");
                string reviseLabel = BuildReviseReason(actionId).Replace('_', ' ');
                if (string.Equals(reviseLabel, "change tone", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(tone))
                {
                    return $"Change tone to {tone}";
                }

                return $"{char.ToUpperInvariant(reviseLabel[0])}{reviseLabel.Substring(1)}";
            }

            if (!string.Equals(actionId, RewriteSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Apply AI change";
            }

            string normalized = instruction?.Trim().ToLowerInvariant() ?? string.Empty;
            string toneOption = GetOption(options, "tone");
            string length = GetOption(options, "length");

            if (normalized.Contains("shorten", StringComparison.Ordinal))
            {
                return "Shorten selected text";
            }

            if (normalized.Contains("fix grammar", StringComparison.Ordinal) || normalized.Contains("grammar", StringComparison.Ordinal))
            {
                return "Fix grammar in selected text";
            }

            if (normalized.Contains("summarize", StringComparison.Ordinal) || normalized.Contains("summary", StringComparison.Ordinal))
            {
                return "Summarize selected text";
            }

            if (string.Equals(length, "Shorter", StringComparison.OrdinalIgnoreCase))
            {
                return "Shorten selected text";
            }

            if (!string.IsNullOrWhiteSpace(toneOption) && !string.Equals(toneOption, "Neutral", StringComparison.OrdinalIgnoreCase))
            {
                return $"Rewrite selected text in a more {toneOption} tone";
            }

            if (normalized.Contains("rewrite", StringComparison.Ordinal))
            {
                return "Rewrite selected text";
            }

            if (!string.IsNullOrWhiteSpace(instruction))
            {
                return $"Rewrite selected text: {instruction}";
            }

            return "Rewrite selected text";
        }

        private static string GetOption(Dictionary<string, object?>? options, string key)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }

        private static string GetInputValue(AiRequest request, string key)
        {
            if (request.Inputs is null || !request.Inputs.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }

        private static bool IsReviseSelectionAction(string actionId)
        {
            return string.Equals(actionId, TightenSelectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ExpandSelectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ChangeToneSelectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ShowDontTellSelectionAction.ActionIdValue, StringComparison.Ordinal);
        }

        private static bool IsReviseSectionAction(string actionId)
        {
            return string.Equals(actionId, TightenSectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ExpandSectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ChangeToneSectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ShowDontTellSectionAction.ActionIdValue, StringComparison.Ordinal);
        }

        private static bool IsReviseAction(string actionId) => IsReviseSelectionAction(actionId) || IsReviseSectionAction(actionId);

        private static string BuildReviseReason(string actionId)
        {
            int separator = actionId.IndexOf('.');
            if (separator <= 0)
            {
                return actionId;
            }

            return actionId.Substring(0, separator);
        }

        private static AiExecutionOutcome BuildApplyContinuityFixOutcome(IAiAction action, AiActionInput input, AiRequest request)
        {
            string suggestedFix = GetOption(input.Options, "suggested_fix");
            if (string.IsNullOrWhiteSpace(suggestedFix))
            {
                AiResult rejectedResult = new(
                    request.RequestId,
                    new List<AiArtifact>(),
                    new AiUsage(0, 0, TimeSpan.Zero),
                    new Dictionary<string, object>
                    {
                        ["provider"] = "local"
                    });
                return AiExecutionOutcome.Rejected(
                    rejectedResult,
                    "local",
                    "ai.continuity_fix_missing_text",
                    "Continuity fix requires suggested text.");
            }

            int start = Math.Max(0, ParseIntOption(input.Options, "anchor_start", input.SelectionRange.Start));
            int length = Math.Max(0, ParseIntOption(input.Options, "anchor_length", input.SelectionRange.Length));
            Guid sectionId = ParseGuidOption(input.Options, "section_id", input.ActiveSectionId);
            TextRange targetRange = new(start, length);
            List<ProposedOperation> operations = new()
            {
                new ReplaceTextRangeOperation(sectionId, targetRange, suggestedFix)
            };

            AiProposal proposal = new(
                Guid.NewGuid(),
                sectionId,
                "Apply continuity fix",
                action.ActionId,
                "local",
                request.RequestId,
                DateTime.UtcNow,
                "continuity_fix",
                operations,
                new List<Guid>(),
                "Apply continuity fix",
                "Section",
                input.Instruction,
                input.SelectedText,
                suggestedFix);

            AiResult result = new(
                request.RequestId,
                new List<AiArtifact>(),
                new AiUsage(0, 0, TimeSpan.Zero),
                new Dictionary<string, object>
                {
                    ["provider"] = "local"
                });

            return AiExecutionOutcome.Success(proposal, result, "local");
        }

        private static int ParseIntOption(Dictionary<string, object?>? options, string key, int fallback)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return fallback;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            return int.TryParse(value.ToString(), out int parsed) ? parsed : fallback;
        }

        private static Guid ParseGuidOption(Dictionary<string, object?>? options, string key, Guid fallback)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return fallback;
            }

            if (value is Guid guidValue)
            {
                return guidValue;
            }

            return Guid.TryParse(value.ToString(), out Guid parsed) ? parsed : fallback;
        }
    }
}
