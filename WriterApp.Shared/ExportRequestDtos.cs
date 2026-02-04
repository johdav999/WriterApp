using System;
using System.Collections.Generic;

namespace WriterApp.Application.Exporting
{
    public sealed record ExportDocumentRequest(
        Guid DocumentId,
        string Format,
        Guid? TemplateId,
        string ScopeType,
        IReadOnlyList<Guid>? ScopeIds = null,
        SelectionRangeDto? SelectionRange = null,
        string? SelectionText = null,
        bool IncludeTitlePage = true,
        bool IncludeToc = true,
        int TocDepth = 0,
        IReadOnlyList<string>? ChapterBreakRules = null,
        string? TitlePageTitle = null,
        string? TitlePageSubtitle = null,
        string? TitlePageAuthor = null,
        string? TitlePageDraftLabel = null,
        string? TitlePageDate = null);
}
