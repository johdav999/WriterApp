namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// Formatting defaults applied to new content and layout decisions.
    /// </summary>
    public record DocumentSettings
    {
        public string DefaultFont { get; init; } = string.Empty;

        public int DefaultFontSize { get; init; }

        public string PageSize { get; init; } = string.Empty;

        public double LineSpacing { get; init; }

        public DocumentSettings()
        {
        }
    }
}
