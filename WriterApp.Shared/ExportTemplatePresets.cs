using System;
using System.Collections.Generic;

namespace WriterApp.Application.Exporting
{
    public sealed record ExportTemplatePresetDefinition(
        string Key,
        string Name,
        int PageWidthMm,
        int PageHeightMm,
        int MarginTopMm,
        int MarginRightMm,
        int MarginBottomMm,
        int MarginLeftMm,
        string FontFamily,
        int BodyFontSizePt,
        decimal LineHeight,
        int ParagraphSpacingPt,
        bool HeaderEnabled,
        string? HeaderLeft,
        string? HeaderCenter,
        string? HeaderRight,
        bool FooterEnabled,
        string? FooterLeft,
        string? FooterCenter,
        string? FooterRight,
        bool PageNumbersEnabled,
        int PageNumberStart,
        bool TocEnabled,
        int TocDepth);

    public static class ExportTemplatePresets
    {
        public static readonly IReadOnlyList<ExportTemplatePresetDefinition> All = new List<ExportTemplatePresetDefinition>
        {
            new(
                "manuscript",
                "Manuscript",
                216,
                279,
                25,
                25,
                25,
                30,
                "Georgia",
                12,
                2.0m,
                12,
                true,
                "{DocumentTitle}",
                null,
                "{SectionTitle}",
                false,
                null,
                null,
                null,
                true,
                1,
                true,
                2),
            new(
                "paperback_6x9",
                "Paperback 6x9",
                152,
                229,
                16,
                16,
                20,
                20,
                "Georgia",
                11,
                1.4m,
                6,
                true,
                null,
                "{DocumentTitle}",
                null,
                false,
                null,
                null,
                null,
                true,
                1,
                false,
                2),
            new(
                "a4",
                "A4",
                210,
                297,
                20,
                20,
                20,
                20,
                "Georgia",
                12,
                1.5m,
                6,
                false,
                null,
                null,
                null,
                true,
                null,
                "{PageNumber}",
                null,
                true,
                1,
                true,
                2)
        };

        public static ExportTemplatePresetDefinition? GetByKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            foreach (ExportTemplatePresetDefinition preset in All)
            {
                if (string.Equals(preset.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return preset;
                }
            }

            return null;
        }
    }
}
