using System;

namespace WriterApp.Application.Documents
{
    public sealed record PageAnnotationDto(
        Guid Id,
        Guid DocumentId,
        Guid PageId,
        string Kind,
        string Status,
        int AnchorFrom,
        int AnchorTo,
        string? AnchorText,
        string? Content,
        string AuthorUserId,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ResolvedAt);

    public sealed record PageAnnotationCreateRequest(
        string Kind,
        int AnchorFrom,
        int AnchorTo,
        string? AnchorText,
        string? Content);

    public sealed record PageAnnotationUpdateRequest(
        string? Content);

    public sealed record PageAnnotationAnchorUpdateRequest(
        Guid Id,
        int AnchorFrom,
        int AnchorTo,
        string? AnchorText);
}
