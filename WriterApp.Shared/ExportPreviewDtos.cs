using System;
using System.Collections.Generic;

namespace WriterApp.Application.Exporting
{
    public sealed record SelectionRangeDto(int From, int To);

    public sealed record ExportPreviewRequest(
        Guid DocumentId,
        Guid? TemplateId,
        bool IncludeToc,
        string ScopeType,
        IReadOnlyList<Guid>? ScopeIds = null,
        SelectionRangeDto? SelectionRange = null,
        string? SelectionText = null,
        bool IncludeTitlePage = true,
        int TocDepth = 0,
        IReadOnlyList<string>? ChapterBreakRules = null,
        string? TitlePageTitle = null,
        string? TitlePageSubtitle = null,
        string? TitlePageAuthor = null,
        string? TitlePageDraftLabel = null,
        string? TitlePageDate = null);

    public sealed record ExportPreviewResponse(string Html);
}
