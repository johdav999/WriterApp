using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.Usage;
using WriterApp.Data.Usage;

namespace WriterApp.AI.Core
{
    public sealed class AiOrchestrator : IAiOrchestrator
    {
        private readonly IAiActionExecutor _executor;
        private readonly IAiProviderRegistry _providerRegistry;
        private readonly IAiRouter _router;
        private readonly IAiUsagePolicy _usagePolicy;
        private readonly IUsageMeter _usageMeter;
        private readonly WriterAiOptions _options;
        private readonly IReadOnlyList<IAiAction> _actions;
        private readonly Dictionary<string, IAiAction> _actionMap;

        public AiOrchestrator(
            IAiActionExecutor executor,
            IAiProviderRegistry providerRegistry,
            IAiRouter router,
            IAiUsagePolicy usagePolicy,
            IUsageMeter usageMeter,
            IOptions<WriterAiOptions> options,
            IEnumerable<IAiAction> actions)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _usagePolicy = usagePolicy ?? throw new ArgumentNullException(nameof(usagePolicy));
            _usageMeter = usageMeter ?? throw new ArgumentNullException(nameof(usageMeter));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            if (actions is null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            _actions = actions.ToList();
            _actionMap = _actions.ToDictionary(action => action.ActionId, StringComparer.Ordinal);
        }

        public IReadOnlyList<IAiAction> Actions => _actions;

        public IAiAction? GetAction(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return null;
            }

            return _actionMap.TryGetValue(actionId, out IAiAction? action) ? action : null;
        }

        public bool CanRunAction(string actionId)
        {
            IAiAction? action = GetAction(actionId);
            if (action is null)
            {
                return false;
            }

            bool needsText = Array.Exists(action.Modalities, modality => modality == AiModality.Text);
            bool needsImage = Array.Exists(action.Modalities, modality => modality == AiModality.Image);

            foreach (IAiProvider provider in _providerRegistry.GetAll())
            {
                if (!_options.Enabled && ProviderRequiresEntitlement(provider))
                {
                    continue;
                }

                AiProviderCapabilities capabilities = provider.Capabilities;
                if (needsText && !capabilities.SupportsText)
                {
                    continue;
                }

                if (needsImage && !capabilities.SupportsImage)
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public AiStreamingCapabilities GetStreamingCapabilities(string actionId)
        {
            if (!_options.Streaming.Enabled)
            {
                return new AiStreamingCapabilities(false, false);
            }

            IAiAction? action = GetAction(actionId);
            if (action is null)
            {
                return new AiStreamingCapabilities(false, false);
            }

            bool needsText = Array.Exists(action.Modalities, modality => modality == AiModality.Text);
            bool needsImage = Array.Exists(action.Modalities, modality => modality == AiModality.Image);

            foreach (IAiProvider provider in _providerRegistry.GetAll())
            {
                if (!_options.Enabled && ProviderRequiresEntitlement(provider))
                {
                    continue;
                }

                AiProviderCapabilities capabilities = provider.Capabilities;
                if (needsText && !capabilities.SupportsText)
                {
                    continue;
                }

                if (needsImage && !capabilities.SupportsImage)
                {
                    continue;
                }

                if (provider is IAiStreamingProvider streamingProvider)
                {
                    return streamingProvider.StreamingCapabilities;
                }

                return new AiStreamingCapabilities(false, false);
            }

            return new AiStreamingCapabilities(false, false);
        }

        public async Task<AiExecutionResult> ExecuteActionAsync(string actionId, AiActionInput input, CancellationToken ct)
        {
            IAiAction? action = GetAction(actionId);
            if (action is null)
            {
                return AiExecutionResult.Blocked("ai.action_missing", $"AI action '{actionId}' was not registered.");
            }

            AiRequest request = action.BuildRequest(input);
            IAiProvider provider;
            try
            {
                provider = _router.Route(request).Provider;
            }
            catch (InvalidOperationException ex)
            {
                return AiExecutionResult.Blocked("ai.provider_unavailable", ex.Message);
            }

            AiUsageDecision decision = await _usagePolicy.EvaluateAsync(provider, action.ActionId);
            if (!decision.Allowed)
            {
                return AiExecutionResult.Blocked(decision.ErrorCode ?? "ai.blocked", decision.ErrorMessage ?? "AI usage is not permitted.");
            }

            AiExecutionOutcome outcome = await _executor.ExecuteAsync(action, input, ct);

            await RecordUsageAsync(
                decision.UserId,
                action.ActionId,
                outcome.ProviderId,
                outcome.Result,
                input);

            return AiExecutionResult.Success(outcome.Proposal);
        }

        public AiStreamingSession StreamActionAsync(string actionId, AiActionInput input, CancellationToken ct)
        {
            IAiAction? action = GetAction(actionId);
            if (action is null)
            {
                return CreateBlockedSession("ai.action_missing", $"AI action '{actionId}' was not registered.");
            }

            AiRequest request = action.BuildRequest(input);
            IAiProvider provider;
            try
            {
                provider = _router.Route(request).Provider;
            }
            catch (InvalidOperationException ex)
            {
                return CreateBlockedSession("ai.provider_unavailable", ex.Message);
            }

            AiUsageDecision decision = _usagePolicy.EvaluateAsync(provider, action.ActionId).GetAwaiter().GetResult();
            if (!decision.Allowed)
            {
                return CreateBlockedSession(decision.ErrorCode ?? "ai.blocked", decision.ErrorMessage ?? "AI usage is not permitted.");
            }

            TaskCompletionSource<AiProposal?> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            IAsyncEnumerable<AiStreamEvent> events = StreamProposalAsync(
                provider,
                action,
                input,
                request,
                decision.UserId,
                completionSource,
                _options.Streaming.Enabled,
                ct);

            return new AiStreamingSession(events, completionSource.Task);
        }

        private async IAsyncEnumerable<AiStreamEvent> StreamProposalAsync(
            IAiProvider provider,
            IAiAction action,
            AiActionInput input,
            AiRequest request,
            string userId,
            TaskCompletionSource<AiProposal?> completionSource,
            bool allowStreaming,
            [EnumeratorCancellation] CancellationToken ct)
        {
            // Stream preview events while building the proposal only when the stream completes.
            if (provider is IAiStreamingProvider streamingProvider
                && allowStreaming
                && SupportsStreaming(action, streamingProvider))
            {
                StringBuilder textBuilder = new();
                string? imageReference = null;
                Exception? streamError = null;
                IAsyncEnumerator<AiStreamEvent>? enumerator = null;

                try
                {
                    enumerator = streamingProvider.StreamAsync(request, ct).GetAsyncEnumerator(ct);

                    while (true)
                    {
                        AiStreamEvent? streamEvent;
                        try
                        {
                            if (!await enumerator.MoveNextAsync())
                            {
                                break;
                            }

                            streamEvent = enumerator.Current;
                        }
                        catch (OperationCanceledException)
                        {
                            completionSource.TrySetResult(null);
                            yield break;
                        }
                        catch (Exception ex)
                        {
                            streamError = ex;
                            break;
                        }

                        switch (streamEvent)
                        {
                            case AiStreamEvent.TextDelta textDelta:
                                if (!string.IsNullOrEmpty(textDelta.Delta))
                                {
                                    textBuilder.Append(textDelta.Delta);
                                }

                                break;
                            case AiStreamEvent.ImageDelta imageDelta:
                                if (!string.IsNullOrWhiteSpace(imageDelta.Reference))
                                {
                                    imageReference = imageDelta.Reference;
                                }

                                break;
                            case AiStreamEvent.Completed:
                                AiProposal? proposal = BuildStreamingProposal(action, input, request, textBuilder.ToString(), imageReference, provider.ProviderId);
                                completionSource.TrySetResult(proposal);
                                await RecordStreamingUsageAsync(userId, action.ActionId, provider.ProviderId, input);
                                break;
                            case AiStreamEvent.Failed:
                                completionSource.TrySetResult(null);
                                break;
                        }

                        yield return streamEvent;

                        if (streamEvent is AiStreamEvent.Completed || streamEvent is AiStreamEvent.Failed)
                        {
                            yield break;
                        }
                    }
                }
                finally
                {
                    if (enumerator is not null)
                    {
                        await enumerator.DisposeAsync();
                    }
                }

                if (streamError is not null)
                {
                    completionSource.TrySetResult(null);
                    yield return new AiStreamEvent.Failed(streamError.Message);
                    yield break;
                }

                completionSource.TrySetResult(null);
                yield break;
            }

            yield return new AiStreamEvent.Started();

            AiProposal? nonStreamingProposal = null;
            Exception? nonStreamingError = null;
            try
            {
                AiExecutionOutcome outcome = await _executor.ExecuteAsync(action, input, ct);
                nonStreamingProposal = outcome.Proposal;
                await RecordUsageAsync(userId, action.ActionId, outcome.ProviderId, outcome.Result, input);
            }
            catch (OperationCanceledException)
            {
                completionSource.TrySetResult(null);
                yield break;
            }
            catch (Exception ex)
            {
                nonStreamingError = ex;
                completionSource.TrySetResult(null);
            }

            if (nonStreamingError is not null)
            {
                yield return new AiStreamEvent.Failed(nonStreamingError.Message);
                yield break;
            }

            if (nonStreamingProposal is null)
            {
                completionSource.TrySetResult(null);
                yield break;
            }

            string? proposedText = nonStreamingProposal.Operations.OfType<ReplaceTextRangeOperation>().FirstOrDefault()?.NewText;
            if (!string.IsNullOrEmpty(proposedText))
            {
                yield return new AiStreamEvent.TextDelta(proposedText);
            }

            completionSource.TrySetResult(nonStreamingProposal);
            yield return new AiStreamEvent.Completed();
        }

        private static AiProposal? BuildStreamingProposal(
            IAiAction action,
            AiActionInput input,
            AiRequest request,
            string proposedText,
            string? imageReference,
            string providerId)
        {
            string summaryLabel = string.IsNullOrWhiteSpace(input.Instruction) ? action.DisplayName : input.Instruction;
            List<ProposedOperation> operations = new();
            List<Guid> artifactIds = new();
            string? originalText = null;
            string? resolvedProposedText = null;

            if (string.Equals(action.ActionId, RewriteSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText ?? string.Empty));
                originalText = input.SelectedText;
                resolvedProposedText = proposedText;
            }
            else if (string.Equals(action.ActionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
            {
                if (!string.IsNullOrWhiteSpace(imageReference))
                {
                    Guid artifactId = Guid.NewGuid();
                    artifactIds.Add(artifactId);
                    operations.Add(new AttachImageOperation(input.ActiveSectionId, artifactId, "cover"));
                }
            }

            return new AiProposal(
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
                resolvedProposedText);
        }

        private static bool SupportsStreaming(IAiAction action, IAiStreamingProvider provider)
        {
            bool needsText = Array.Exists(action.Modalities, modality => modality == AiModality.Text);
            bool needsImage = Array.Exists(action.Modalities, modality => modality == AiModality.Image);
            AiStreamingCapabilities capabilities = provider.StreamingCapabilities;

            if (needsText && !capabilities.SupportsTextStreaming)
            {
                return false;
            }

            if (needsImage && !capabilities.SupportsImageStreaming)
            {
                return false;
            }

            return true;
        }

        private static string BuildTargetScope(string actionId)
        {
            if (string.Equals(actionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
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

        private static bool ProviderRequiresEntitlement(IAiProvider provider)
        {
            return provider is IAiBillingProvider billingProvider && billingProvider.RequiresEntitlement;
        }

        private async Task RecordUsageAsync(
            string userId,
            string actionId,
            string providerId,
            AiResult result,
            AiActionInput input)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            if (!_options.Enabled || result is null)
            {
                return;
            }

            if (!_providerRegistry.GetAll().Any(provider => provider.ProviderId == providerId && provider is IAiBillingProvider billing && billing.IsBillable))
            {
                return;
            }

            string model = GetProviderMeta(result, "model") ?? string.Empty;

            UsageEvent usageEvent = new()
            {
                UserId = userId,
                Kind = actionId,
                Provider = providerId,
                Model = model,
                InputTokens = result.Usage.InputTokens,
                OutputTokens = result.Usage.OutputTokens,
                CostMicros = null,
                DocumentId = input.Document.DocumentId,
                SectionId = input.ActiveSectionId,
                TimestampUtc = DateTime.UtcNow,
                CorrelationId = result.RequestId
            };

            await _usageMeter.RecordAsync(usageEvent);
        }

        private async Task RecordStreamingUsageAsync(
            string userId,
            string actionId,
            string providerId,
            AiActionInput input)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            if (!_providerRegistry.GetAll().Any(provider => provider.ProviderId == providerId && provider is IAiBillingProvider billing && billing.IsBillable))
            {
                return;
            }

            UsageEvent usageEvent = new()
            {
                UserId = userId,
                Kind = actionId,
                Provider = providerId,
                Model = providerId,
                InputTokens = 0,
                OutputTokens = 0,
                CostMicros = null,
                DocumentId = input.Document.DocumentId,
                SectionId = input.ActiveSectionId,
                TimestampUtc = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid()
            };

            await _usageMeter.RecordAsync(usageEvent);
        }

        private static string? GetProviderMeta(AiResult result, string key)
        {
            if (result.ProviderMeta is null || !result.ProviderMeta.TryGetValue(key, out object? value))
            {
                return null;
            }

            return value?.ToString();
        }

        private static AiStreamingSession CreateBlockedSession(string errorCode, string errorMessage)
        {
            async IAsyncEnumerable<AiStreamEvent> BlockedEvents()
            {
                yield return new AiStreamEvent.Started();
                yield return new AiStreamEvent.Failed($"{errorCode}: {errorMessage}");
            }

            TaskCompletionSource<AiProposal?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            completion.TrySetResult(null);
            return new AiStreamingSession(BlockedEvents(), completion.Task);
        }

    }
}
