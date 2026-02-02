using System;

namespace WriterApp.Application.Documents
{
    public sealed record PageVersionListItemDto(
        Guid Id,
        Guid PageId,
        DateTimeOffset CreatedAt,
        string Reason,
        int WordCount,
        int SizeBytes);

    public sealed record PageVersionDetailDto(
        Guid Id,
        Guid PageId,
        Guid DocumentId,
        DateTimeOffset CreatedAt,
        string Reason,
        string Content,
        int WordCount,
        int SizeBytes);

    public sealed record PageVersionDiffDto(
        Guid PageId,
        Guid FromVersionId,
        string FromText,
        string ToText);
}
