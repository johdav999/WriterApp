using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public sealed record PageQualityIssueDto(
        string IssueKey,
        Guid DocumentId,
        Guid PageId,
        string RuleId,
        string Kind,
        string Severity,
        string Message,
        string? Suggestion,
        string? AnchorText,
        int StartOffset,
        int EndOffset,
        DateTimeOffset CreatedAt);

    public sealed record QualityCheckRunRequest(
        string Scope,
        string? Text,
        bool Force);

    public sealed record QualityCheckRunResultDto(
        Guid PageId,
        string Scope,
        string ContentHash,
        bool FromCache,
        IReadOnlyList<PageQualityIssueDto> Issues);

    public sealed record GlossaryEntryDto(
        Guid Id,
        Guid DocumentId,
        string Term,
        string? Notes,
        DateTimeOffset UpdatedAt);

    public sealed record GlossaryEntryCreateRequest(
        string Term,
        string? Notes);
}
