namespace WriterApp.Client.Components.Editor
{
    public sealed class EditorFormattingState
    {
        public bool IsBold { get; set; }
        public bool IsItalic { get; set; }
        public bool IsStrike { get; set; }
        public bool IsCode { get; set; }
        public bool CanBold { get; set; }
        public bool CanItalic { get; set; }
        public bool CanStrike { get; set; }
        public bool CanCode { get; set; }
        public bool IsInCodeBlock { get; set; }
        public bool CanApplyHeading { get; set; }
        public bool CanToggleList { get; set; }
        public bool CanBlockquote { get; set; }
        public bool CanHorizontalRule { get; set; }
        public bool IsLink { get; set; }
        public string? LinkHref { get; set; }
        public bool IsInTable { get; set; }
        public bool IsHeaderCell { get; set; }
        public bool CanInsertTable { get; set; }
        public bool CanAddTableRowBefore { get; set; }
        public bool CanAddTableRowAfter { get; set; }
        public bool CanDeleteTableRow { get; set; }
        public bool CanAddTableColumnBefore { get; set; }
        public bool CanAddTableColumnAfter { get; set; }
        public bool CanDeleteTableColumn { get; set; }
        public bool CanDeleteTable { get; set; }
        public bool CanToggleTableHeaderRow { get; set; }
        public bool CanToggleTableHeaderColumn { get; set; }
        public bool CanMergeTableCells { get; set; }
        public bool CanSplitTableCell { get; set; }
        public bool IsImageSelected { get; set; }
        public bool CanInsertImage { get; set; }
        public bool CanRemoveImage { get; set; }
        public string? BlockType { get; set; }
        public string? FontFamily { get; set; }
        public string? FontSize { get; set; }
        public string? TextAlign { get; set; }
    }
}
