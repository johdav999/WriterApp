using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public sealed record SceneContentDto(
        Guid SceneNodeId,
        Guid ProjectId,
        string ContentJson,
        string? LanguageCode,
        DateTimeOffset UpdatedAtUtc);

    public sealed record SceneContentUpdateRequest(
        string? ContentJson,
        string? LanguageCode);

    public sealed record SceneNotesDto(
        Guid SceneNodeId,
        string NotesText,
        DateTimeOffset UpdatedAtUtc);

    public sealed record SceneNotesUpdateRequest(
        string? NotesText);

    public sealed record SceneCardDto(
        Guid SceneNodeId,
        string? NarrativePurpose,
        string? EmotionalBeat,
        string? KeyEvents,
        string? OpenQuestions,
        DateTimeOffset UpdatedAtUtc,
        string? PovCharacterId = null,
        string? PlaceId = null,
        string? TimelineEventId = null,
        string? TimeRef = null,
        IReadOnlyList<string>? Tags = null,
        IReadOnlyList<SceneCardReferenceDto>? References = null,
        string? Summary = null,
        string? Status = "Draft",
        IReadOnlyList<string>? SubplotTags = null);

    public sealed record SceneCardUpdateRequest(
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
        IReadOnlyList<string>? SubplotTags = null);

    public sealed record SceneAnnotationDto(
        Guid Id,
        Guid SceneNodeId,
        string Kind,
        string Status,
        int AnchorFrom,
        int AnchorTo,
        string? AnchorText,
        string? Content,
        string AuthorUserId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ResolvedAt);

    public sealed record SceneAnnotationCreateRequest(
        string Kind,
        int AnchorFrom,
        int AnchorTo,
        string? AnchorText,
        string? Content);

    public sealed record SceneQualityIssueDto(
        string IssueKey,
        Guid SceneNodeId,
        string RuleId,
        string Kind,
        string Severity,
        string Message,
        string? Suggestion,
        string? AnchorText,
        int StartOffset,
        int EndOffset,
        DateTimeOffset CreatedAt);

    public sealed record SceneVersionListItemDto(
        Guid Id,
        Guid SceneNodeId,
        DateTimeOffset CreatedAt,
        string Reason,
        int WordCount,
        int SizeBytes);

    public sealed record SceneVersionCreateRequest(
        string? Reason,
        string? ContentJson);
}
