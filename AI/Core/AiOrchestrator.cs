using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;

namespace WriterApp.AI.Core
{
    public sealed class AiOrchestrator : IAiOrchestrator
    {
        private readonly IAiActionExecutor _executor;
        private readonly IAiProviderRegistry _providerRegistry;
        private readonly IAiRouter _router;
        private readonly WriterAiOptions _options;
        private readonly ILogger<AiOrchestrator> _logger;
        private readonly IReadOnlyList<IAiAction> _actions;
        private readonly Dictionary<string, IAiAction> _actionMap;

        public AiOrchestrator(
            IAiActionExecutor executor,
            IAiProviderRegistry providerRegistry,
            IAiRouter router,
            IOptions<WriterAiOptions> options,
            ILogger<AiOrchestrator> logger,
            IEnumerable<IAiAction> actions)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            if (!_options.Enabled)
            {
                return false;
            }

            IAiAction? action = GetAction(actionId);
            if (action is null)
            {
                return false;
            }

            bool needsText = Array.Exists(action.Modalities, modality => modality == AiModality.Text);
            bool needsImage = Array.Exists(action.Modalities, modality => modality == AiModality.Image);

            foreach (IAiProvider provider in _providerRegistry.GetAll())
            {
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

        public Task<AiProposal> ExecuteActionAsync(string actionId, AiActionInput input, CancellationToken ct)
        {
            EnsureAiEnabled();
            IAiAction? action = GetAction(actionId);
            if (action is null)
            {
                throw new InvalidOperationException($"AI action '{actionId}' was not registered.");
            }

            return _executor.ExecuteAsync(action, input, ct);
        }

        public AiStreamingSession StreamActionAsync(string actionId, AiActionInput input, CancellationToken ct)
        {
            EnsureAiEnabled();
            IAiAction? action = GetAction(actionId);
            if (action is null)
            {
                throw new InvalidOperationException($"AI action '{actionId}' was not registered.");
            }

            AiRequest request = action.BuildRequest(input);
            IAiProvider provider = _router.Route(request).Provider;
            TaskCompletionSource<AiProposal?> completionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
            IAsyncEnumerable<AiStreamEvent> events = StreamProposalAsync(
                provider,
                action,
                input,
                request,
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
                                AiProposal? proposal = BuildStreamingProposal(action, input, request, textBuilder.ToString(), imageReference);
                                completionSource.TrySetResult(proposal);
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
                nonStreamingProposal = await _executor.ExecuteAsync(action, input, ct);
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
            string? imageReference)
        {
            string summaryLabel = string.IsNullOrWhiteSpace(input.Instruction) ? action.DisplayName : input.Instruction;
            List<ProposedOperation> operations = new();
            List<Guid> artifactIds = new();

            if (string.Equals(action.ActionId, RewriteSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText ?? string.Empty));
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
                request.RequestId,
                DateTime.UtcNow,
                input.Instruction,
                operations,
                artifactIds);
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

        private void EnsureAiEnabled()
        {
            if (_options.Enabled)
            {
                return;
            }

            _logger.LogWarning("AI execution blocked because AI is disabled by configuration.");
            throw new InvalidOperationException("AI is disabled by configuration.");
        }

    }
}
