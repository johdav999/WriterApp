using System;

namespace WriterApp.Data.Exporting
{
    public sealed class ExportTemplate
    {
        public Guid Id { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? PresetKey { get; set; }
        public int PageWidthMm { get; set; }
        public int PageHeightMm { get; set; }
        public int MarginTopMm { get; set; }
        public int MarginRightMm { get; set; }
        public int MarginBottomMm { get; set; }
        public int MarginLeftMm { get; set; }
        public string FontFamily { get; set; } = "Georgia";
        public int BodyFontSizePt { get; set; }
        public decimal LineHeight { get; set; }
        public int ParagraphSpacingPt { get; set; }
        public bool HeaderEnabled { get; set; }
        public string? HeaderLeft { get; set; }
        public string? HeaderCenter { get; set; }
        public string? HeaderRight { get; set; }
        public bool FooterEnabled { get; set; }
        public string? FooterLeft { get; set; }
        public string? FooterCenter { get; set; }
        public string? FooterRight { get; set; }
        public bool PageNumbersEnabled { get; set; }
        public int PageNumberStart { get; set; } = 1;
        public bool TocEnabled { get; set; }
        public int TocDepth { get; set; } = 2;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
