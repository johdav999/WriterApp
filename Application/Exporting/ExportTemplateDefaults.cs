using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.Data.Exporting;

namespace WriterApp.Application.Exporting
{
    public static class ExportTemplateDefaults
    {
        public static IReadOnlyList<ExportTemplate> BuildDefaults(string ownerUserId, DateTimeOffset now)
        {
            return ExportTemplatePresets.All
                .Select(preset => CreateFromPreset(ownerUserId, now, preset))
                .ToList();
        }

        public static ExportTemplate CreateManuscript(string ownerUserId, DateTimeOffset now)
        {
            ExportTemplatePresetDefinition? preset = ExportTemplatePresets.GetByKey("manuscript");
            return CreateFromPreset(ownerUserId, now, preset ?? ExportTemplatePresets.All[0]);
        }

        public static ExportTemplate CreateFromPreset(
            string ownerUserId,
            DateTimeOffset now,
            ExportTemplatePresetDefinition preset)
        {
            return new ExportTemplate
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                Name = preset.Name,
                PresetKey = preset.Key,
                PageWidthMm = preset.PageWidthMm,
                PageHeightMm = preset.PageHeightMm,
                MarginTopMm = preset.MarginTopMm,
                MarginRightMm = preset.MarginRightMm,
                MarginBottomMm = preset.MarginBottomMm,
                MarginLeftMm = preset.MarginLeftMm,
                FontFamily = preset.FontFamily,
                BodyFontSizePt = preset.BodyFontSizePt,
                LineHeight = preset.LineHeight,
                ParagraphSpacingPt = preset.ParagraphSpacingPt,
                HeaderEnabled = preset.HeaderEnabled,
                HeaderLeft = preset.HeaderLeft,
                HeaderCenter = preset.HeaderCenter,
                HeaderRight = preset.HeaderRight,
                FooterEnabled = preset.FooterEnabled,
                FooterLeft = preset.FooterLeft,
                FooterCenter = preset.FooterCenter,
                FooterRight = preset.FooterRight,
                PageNumbersEnabled = preset.PageNumbersEnabled,
                PageNumberStart = preset.PageNumberStart,
                TocEnabled = preset.TocEnabled,
                TocDepth = preset.TocDepth,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
    }
}
