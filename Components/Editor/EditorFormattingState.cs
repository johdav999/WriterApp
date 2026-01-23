namespace BlazorApp.Components.Editor
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
        public string? BlockType { get; set; }
        public string? FontFamily { get; set; }
        public string? FontSize { get; set; }
        public string? TextAlign { get; set; }
    }
}
