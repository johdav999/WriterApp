using System;

namespace WriterApp.Application.Documents
{
    public sealed record SectionSceneCardDto(
        Guid SectionId,
        string? NarrativePurpose,
        string? EmotionalBeat,
        string? KeyEvents,
        string? OpenQuestions,
        DateTimeOffset UpdatedUtc);

    public sealed record SectionSceneCardProposalDto(
        string? NarrativePurpose,
        string? EmotionalBeat,
        string? KeyEvents,
        string? OpenQuestions);

    public sealed record SectionSceneCardUpdateRequest(
        string? NarrativePurpose,
        string? EmotionalBeat,
        string? KeyEvents,
        string? OpenQuestions);
}
