using System;
using System.Collections.Generic;

namespace WriterApp.Application.Exporting
{
    public sealed record ExportOptions(
        bool IncludeTitlePage = true,
        bool IncludeToc = true,
        int TocDepth = 0,
        IReadOnlyList<string>? ChapterBreakRules = null,
        string? TitlePageTitle = null,
        string? TitlePageSubtitle = null,
        string? TitlePageAuthor = null,
        string? TitlePageDraftLabel = null,
        string? TitlePageDate = null,
        Guid? TemplateId = null,
        WriterApp.Data.Exporting.ExportTemplate? Template = null);
}
