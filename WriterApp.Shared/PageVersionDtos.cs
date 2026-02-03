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

    public sealed record PageVersionDiffResultDto(
        Guid PageId,
        Guid FromVersionId,
        Guid? ToVersionId,
        bool CompareToCurrent,
        string Granularity,
        bool Truncated,
        int MaxLines,
        IReadOnlyList<PageVersionDiffLineDto> Lines);

    public sealed record PageVersionDiffLineDto(
        string Kind,
        string Text,
        IReadOnlyList<PageVersionDiffSpanDto>? Spans);

    public sealed record PageVersionDiffSpanDto(
        string Kind,
        string Text);
}
