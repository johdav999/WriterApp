using System;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Domain.Documents;

namespace WriterApp.AI.Core
{
    public sealed class DefaultProposalApplier : IAiProposalApplier
    {
        private readonly IArtifactStore _artifactStore;

        public DefaultProposalApplier(IArtifactStore artifactStore)
        {
            _artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
        }

        public void Apply(CommandProcessor commandProcessor, AiProposal proposal)
        {
            if (commandProcessor is null)
            {
                throw new ArgumentNullException(nameof(commandProcessor));
            }

            if (proposal is null)
            {
                throw new ArgumentNullException(nameof(proposal));
            }

            string reason = BuildReason(proposal);
            AiEditGroup group = new(proposal.SectionId, reason);
            int appliedOperations = 0;

            for (int index = 0; index < proposal.Operations.Count; index++)
            {
                ProposedOperation operation = proposal.Operations[index];
                if (operation is ReplaceTextRangeOperation replaceOperation)
                {
                    commandProcessor.Execute(new AiReplacePlainTextRangeCommand(
                        replaceOperation.SectionId,
                        replaceOperation.Range,
                        replaceOperation.NewText,
                        group,
                        reason));
                    appliedOperations++;
                }
                else if (operation is ReplaceSynopsisFieldOperation synopsisOperation)
                {
                    string oldValue = proposal.OriginalText ?? string.Empty;
                    commandProcessor.Execute(new UpdateSynopsisFieldCommand(
                        proposal.SectionId,
                        synopsisOperation.FieldKey,
                        synopsisOperation.NewText,
                        oldValue,
                        group,
                        reason));
                    appliedOperations++;
                }
                else if (operation is AttachImageOperation attachOperation)
                {
                    AiArtifact? artifact = _artifactStore.Get(attachOperation.ArtifactId);
                    if (artifact is null)
                    {
                        continue;
                    }

                    string? base64Data = artifact.BinaryContent is null
                        ? null
                        : Convert.ToBase64String(artifact.BinaryContent);

                    string? dataUrl = null;
                    if (artifact.Metadata is not null && artifact.Metadata.TryGetValue("dataUrl", out object? value))
                    {
                        dataUrl = value?.ToString();
                    }

                    if (base64Data is null && !string.IsNullOrWhiteSpace(dataUrl))
                    {
                        base64Data = ExtractBase64FromDataUrl(dataUrl);
                    }

                    DocumentArtifact documentArtifact = new(
                        artifact.ArtifactId,
                        artifact.MimeType,
                        base64Data,
                        dataUrl);

                    commandProcessor.Execute(new SetCoverImageCommand(
                        attachOperation.SectionId,
                        documentArtifact,
                        group,
                        reason));
                    appliedOperations++;
                }
            }

            if (appliedOperations == 0)
            {
                return;
            }

            AIHistoryEntry entry = new()
            {
                EntryId = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                ActionId = proposal.ActionId,
                ProviderId = proposal.ProviderId,
                EditGroupId = group.GroupId,
                OperationSummary = string.IsNullOrWhiteSpace(proposal.UserSummary) ? proposal.SummaryLabel : proposal.UserSummary,
                TargetScope = proposal.TargetScope,
                AffectedSectionId = proposal.SectionId,
                Instruction = proposal.Instruction,
                BeforeText = proposal.OriginalText,
                AfterText = proposal.ProposedText
            };

            commandProcessor.AppendAiHistoryEntry(proposal.SectionId, entry);
        }

        private static string? ExtractBase64FromDataUrl(string dataUrl)
        {
            int commaIndex = dataUrl.IndexOf(',');
            if (commaIndex < 0 || commaIndex >= dataUrl.Length - 1)
            {
                return null;
            }

            return dataUrl.Substring(commaIndex + 1);
        }

        private static string BuildReason(AiProposal proposal)
        {
            if (string.IsNullOrWhiteSpace(proposal.Reason))
            {
                return $"{proposal.ActionId} ({proposal.RequestId})";
            }

            return $"{proposal.Reason} ({proposal.ActionId}:{proposal.RequestId})";
        }

    }
}
