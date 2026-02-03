using System;
using System.Collections.Generic;

namespace WriterApp.Application.Exporting
{
    public sealed record ExportPresetSettingsDto(
        string Format,
        Guid? TemplateId,
        string Scope,
        bool IncludeToc,
        int TocDepth,
        bool IncludeTitlePage,
        bool HeaderEnabled,
        string? HeaderLeft,
        string? HeaderCenter,
        string? HeaderRight,
        bool FooterEnabled,
        string? FooterLeft,
        string? FooterCenter,
        string? FooterRight,
        IReadOnlyList<string>? ChapterBreakRules,
        double? Zoom,
        string? PreviewMode);

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
