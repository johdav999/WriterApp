using System;

namespace WriterApp.Application.Documents
{
    public sealed record DocumentListItemDto(
        Guid Id,
        string Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        int WordCount);

    public sealed record DocumentDetailDto(
        Guid Id,
        string Title,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public sealed record DocumentCreateRequest(
        Guid? Id,
        string? Title,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? UpdatedAt,
        bool CreateDefaultStructure = true);

    public sealed record DocumentCreateResponse(
        DocumentDetailDto Document,
        Guid? DefaultSectionId,
        Guid? DefaultPageId);

    public sealed record DocumentUpdateRequest(string? Title);

    public sealed record SectionDto(
        Guid Id,
        Guid DocumentId,
        string Title,
        string? NarrativePurpose,
        int OrderIndex,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public sealed record SectionCreateRequest(
        Guid? Id,
        string? Title,
        string? NarrativePurpose,
        int? OrderIndex,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record SectionUpdateRequest(
        string? Title,
        string? NarrativePurpose);

    public sealed record PageDto(
        Guid Id,
        Guid DocumentId,
        Guid SectionId,
        string Title,
        string Content,
        int OrderIndex,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public sealed record PageCreateRequest(
        Guid? Id,
        string? Title,
        string? Content,
        int? OrderIndex,
        DateTimeOffset? CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record PageUpdateRequest(string? Title, string? Content);

    public sealed record PageMoveRequest(Guid TargetSectionId, int TargetOrderIndex);
}
