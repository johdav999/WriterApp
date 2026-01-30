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
            else if (string.Equals(action.ActionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
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

            AiProposal proposal = new(
                Guid.NewGuid(),
                input.ActiveSectionId,
                summaryLabel,
                action.ActionId,
                providerId,
                request.RequestId,
                DateTime.UtcNow,
                input.Instruction,
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

            if (string.Equals(actionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis";
            }

            if (string.Equals(actionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Document";
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

            if (string.Equals(actionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Story Coach suggestion";
            }

            if (string.Equals(actionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Generate outline";
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

            if (!string.Equals(actionId, RewriteSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Apply AI change";
            }

            string normalized = instruction?.Trim().ToLowerInvariant() ?? string.Empty;
            string tone = GetOption(options, "tone");
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

            if (!string.IsNullOrWhiteSpace(tone) && !string.Equals(tone, "Neutral", StringComparison.OrdinalIgnoreCase))
            {
                return $"Rewrite selected text in a more {tone} tone";
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
    }
}
