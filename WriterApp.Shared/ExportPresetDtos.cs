using System;
using System.Collections.Generic;

namespace WriterApp.Application.Exporting
{
    public sealed record ExportPresetSettingsDto(
        string Format,
        Guid? TemplateId,
        string Scope,
        IReadOnlyList<Guid>? ScopeIds,
        SelectionRangeDto? SelectionRange,
        bool IncludeToc,
        int TocDepth,
        bool IncludeTitlePage,
        string? TitlePageTitle = null,
        string? TitlePageSubtitle = null,
        string? TitlePageAuthor = null,
        string? TitlePageDraftLabel = null,
        string? TitlePageDate = null,
        bool HeaderEnabled = false,
        string? HeaderLeft = null,
        string? HeaderCenter = null,
        string? HeaderRight = null,
        bool FooterEnabled = false,
        string? FooterLeft = null,
        string? FooterCenter = null,
        string? FooterRight = null,
        IReadOnlyList<string>? ChapterBreakRules = null,
        double? Zoom = null,
        string? PreviewMode = null,
        bool? IncludeCover = null);

    public sealed record ExportPresetDto(
        Guid Id,
        string Name,
        bool IsGlobalDefault,
        ExportPresetSettingsDto Settings,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);

    public sealed record ExportPresetCreateRequest(
        string Name,
        bool IsGlobalDefault,
        ExportPresetSettingsDto Settings);

    public sealed record ExportPresetUpdateRequest(
        string Name,
        bool IsGlobalDefault,
        ExportPresetSettingsDto Settings);

    public sealed record ProjectExportSettingsDto(
        Guid DocumentId,
        Guid? DefaultPresetId,
        ExportPresetSettingsDto? Overrides,
        DateTimeOffset? UpdatedAt);

    public sealed record ProjectExportSettingsUpdateRequest(
        Guid? DefaultPresetId,
        ExportPresetSettingsDto? Overrides);
}
