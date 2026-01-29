using System;
using System.Collections.Generic;
using WriterApp.Data.Exporting;

namespace WriterApp.Application.Exporting
{
    public static class ExportTemplateDefaults
    {
        public static IReadOnlyList<ExportTemplate> BuildDefaults(string ownerUserId, DateTimeOffset now)
        {
            return new List<ExportTemplate>
            {
                CreateManuscript(ownerUserId, now),
                CreatePaperbackSixByNine(ownerUserId, now),
                CreateA4(ownerUserId, now)
            };
        }

        public static ExportTemplate CreateManuscript(string ownerUserId, DateTimeOffset now)
        {
            return new ExportTemplate
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                Name = "Manuscript",
                PresetKey = "manuscript",
                PageWidthMm = 216,
                PageHeightMm = 279,
                MarginTopMm = 25,
                MarginRightMm = 25,
                MarginBottomMm = 25,
                MarginLeftMm = 30,
                FontFamily = "Georgia",
                BodyFontSizePt = 12,
                LineHeight = 2.0m,
                ParagraphSpacingPt = 12,
                HeaderEnabled = true,
                HeaderLeft = "{DocumentTitle}",
                HeaderCenter = null,
                HeaderRight = "{SectionTitle}",
                FooterEnabled = false,
                FooterLeft = null,
                FooterCenter = null,
                FooterRight = null,
                PageNumbersEnabled = true,
                PageNumberStart = 1,
                TocEnabled = true,
                TocDepth = 2,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        public static ExportTemplate CreatePaperbackSixByNine(string ownerUserId, DateTimeOffset now)
        {
            return new ExportTemplate
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                Name = "Paperback 6x9",
                PresetKey = "paperback_6x9",
                PageWidthMm = 152,
                PageHeightMm = 229,
                MarginTopMm = 16,
                MarginRightMm = 16,
                MarginBottomMm = 20,
                MarginLeftMm = 20,
                FontFamily = "Georgia",
                BodyFontSizePt = 11,
                LineHeight = 1.4m,
                ParagraphSpacingPt = 6,
                HeaderEnabled = true,
                HeaderLeft = null,
                HeaderCenter = "{DocumentTitle}",
                HeaderRight = null,
                FooterEnabled = false,
                FooterLeft = null,
                FooterCenter = null,
                FooterRight = null,
                PageNumbersEnabled = true,
                PageNumberStart = 1,
                TocEnabled = false,
                TocDepth = 2,
                CreatedAt = now,
                UpdatedAt = now
            };
        }

        public static ExportTemplate CreateA4(string ownerUserId, DateTimeOffset now)
        {
            return new ExportTemplate
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                Name = "A4",
                PresetKey = "a4",
                PageWidthMm = 210,
                PageHeightMm = 297,
                MarginTopMm = 20,
                MarginRightMm = 20,
                MarginBottomMm = 20,
                MarginLeftMm = 20,
                FontFamily = "Georgia",
                BodyFontSizePt = 12,
                LineHeight = 1.5m,
                ParagraphSpacingPt = 6,
                HeaderEnabled = false,
                HeaderLeft = null,
                HeaderCenter = null,
                HeaderRight = null,
                FooterEnabled = true,
                FooterLeft = null,
                FooterCenter = null,
                FooterRight = null,
                PageNumbersEnabled = true,
                PageNumberStart = 1,
                TocEnabled = true,
                TocDepth = 2,
                CreatedAt = now,
                UpdatedAt = now
            };
        }
    }
}
