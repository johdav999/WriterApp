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
        int MaxBlocks,
        IReadOnlyList<PageVersionDiffBlockDto> Blocks,
        PageVersionDiffStatsDto Stats);

    public sealed record PageVersionDiffBlockDto(
        string Id,
        string Status,
        PageVersionDiffBlockContentDto? Base,
        PageVersionDiffBlockContentDto? Compare,
        IReadOnlyList<PageVersionDiffSpanDto>? InlineSegments,
        string PreviewText);

    public sealed record PageVersionDiffBlockContentDto(
        string Type,
        string Text,
        IReadOnlyList<PageVersionDiffSpanDto>? Segments);

    public sealed record PageVersionDiffStatsDto(
        int AddedWords,
        int RemovedWords,
        int ChangedBlocks,
        int AddedBlocks,
        int RemovedBlocks);

    public sealed record PageVersionDiffSpanDto(
        string Kind,
        string Text);
}
