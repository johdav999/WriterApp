using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public sealed record SceneCardReferenceDto(
        string Kind,
        string TargetId,
        string? Note);

    public sealed record SectionSceneCardDto(
        Guid SectionId,
        string? NarrativePurpose,
        string? EmotionalBeat,
        string? KeyEvents,
        string? OpenQuestions,
        DateTimeOffset UpdatedUtc,
        string? PovCharacterId = null,
        string? PlaceId = null,
        string? TimelineEventId = null,
        string? TimeRef = null,
        IReadOnlyList<string>? Tags = null,
        IReadOnlyList<SceneCardReferenceDto>? References = null,
        string? Summary = null,
        string? Status = "Draft",
        IReadOnlyList<string>? SubplotTags = null,
        string? NarrativeRole = null,
        string? NarrativeIntent = null);

    public sealed record SectionSceneCardProposalDto(
        string? NarrativePurpose,
        string? EmotionalBeat,
        string? KeyEvents,
        string? OpenQuestions,
        string? PovCharacterId = null,
        string? PlaceId = null,
        string? TimelineEventId = null,
        string? TimeRef = null,
        IReadOnlyList<string>? Tags = null,
        IReadOnlyList<SceneCardReferenceDto>? References = null,
        string? Summary = null,
        string? Status = "Draft",
        IReadOnlyList<string>? SubplotTags = null,
        string? NarrativeRole = null,
        string? NarrativeIntent = null);

    public sealed record SectionSceneCardUpdateRequest(
        string? NarrativePurpose,
        string? EmotionalBeat,
        string? KeyEvents,
        string? OpenQuestions,
        string? PovCharacterId = null,
        string? PlaceId = null,
        string? TimelineEventId = null,
        string? TimeRef = null,
        IReadOnlyList<string>? Tags = null,
        IReadOnlyList<SceneCardReferenceDto>? References = null,
        string? Summary = null,
        string? Status = "Draft",
        IReadOnlyList<string>? SubplotTags = null,
        string? NarrativeRole = null,
        string? NarrativeIntent = null);
}
