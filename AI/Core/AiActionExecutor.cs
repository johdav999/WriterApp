using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;

namespace WriterApp.AI.Core
{
    public sealed class AiActionExecutor : IAiActionExecutor
    {
        private readonly IAiRouter _router;
        private readonly IArtifactStore _artifactStore;

        public AiActionExecutor(IAiRouter router, IArtifactStore artifactStore)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        }

        public async Task<AiProposal> ExecuteAsync(IAiAction action, AiActionInput input, CancellationToken ct)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AiRequest request = action.BuildRequest(input);
            AiProviderSelection selection = _router.Route(request);
            AiResult result = await selection.Provider.ExecuteAsync(request, ct);
            return BuildProposal(action, input, request, result);
        }

        private AiProposal BuildProposal(IAiAction action, AiActionInput input, AiRequest request, AiResult result)
        {
            List<ProposedOperation> operations = new();
            List<Guid> artifactIds = new();
            string summaryLabel = string.IsNullOrWhiteSpace(input.Instruction) ? action.DisplayName : input.Instruction;

            if (string.Equals(action.ActionId, RewriteSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                string proposedText = textArtifact?.TextContent ?? string.Empty;
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
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
    }
}
