using System;
using System.Collections.Generic;

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
        string ActionKey);

    public sealed record AiActionHistoryEntryDto(
        Guid ProposalId,
        string ActionKey,
        string? Summary,
        string? OriginalText,
        string? ProposedText,
        DateTimeOffset CreatedUtc);
}
