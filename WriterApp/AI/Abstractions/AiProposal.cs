using System;
using System.Collections.Generic;

namespace WriterApp.AI.Abstractions
{
    public sealed record AiProposal(
        Guid ProposalId,
        Guid SectionId,
        string SummaryLabel,
        string ActionId,
        string ProviderId,
        Guid RequestId,
        DateTime CreatedUtc,
        string? Reason,
        List<ProposedOperation> Operations,
        List<Guid> ArtifactIds,
        string UserSummary,
        string TargetScope,
        string? Instruction,
        string? OriginalText,
        string? ProposedText);
}
