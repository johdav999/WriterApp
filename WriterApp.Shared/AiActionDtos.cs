using System;
using System.Collections.Generic;
using WriterApp.Application.Documents;

namespace WriterApp.Application.AI
{
    public sealed record AiActionDescriptorDto(
        string ActionKey,
        string DisplayName,
        bool RequiresSelection,
        IReadOnlyList<string> Modalities,
        IReadOnlyList<string> RequiredInputs);

    public sealed record AiActionExecuteRequestDto(
        Guid? DocumentId,
        Guid? SectionId,
        Guid? PageId,
        int? SelectionStart,
        int? SelectionEnd,
        string? OriginalText,
        string? SurroundingText,
        string? OutlineText,
        Dictionary<string, object?>? Parameters);

    public sealed record AiActionExecuteResponseDto(
        Guid ProposalId,
        string? OriginalText,
        string? ProposedText,
        string? ChangesSummary,
        DateTimeOffset CreatedUtc,
        string ActionKey,
        IReadOnlyList<DocumentOutlineNodeDto>? OutlineNodes = null,
        string? PreviewText = null,
        bool? WasTruncated = null,
        SectionSceneCardProposalDto? ProposedSceneCard = null,
        string? ProposalExplanation = null);

    public sealed record AiActionHistoryEntryDto(
        Guid ProposalId,
        string ActionKey,
        string? Summary,
        string? OriginalText,
        string? ProposedText,
        DateTimeOffset CreatedUtc,
        bool IsApplied = false,
        DateTimeOffset? LastAppliedAt = null,
        int AppliedCount = 0);

    public sealed record AiActionUndoRedoRequestDto(
        Guid? DocumentId,
        Guid? SectionId,
        Guid? PageId);

    public sealed record AiActionUndoRedoResponseDto(
        Guid HistoryEntryId,
        string Content);
}
