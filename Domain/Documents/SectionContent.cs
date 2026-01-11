namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// Section content payload with explicit format for renderers.
    /// </summary>
    public record SectionContent
    {
        /// <summary>
        /// Allowed values: "html" or "markdown".
        /// </summary>
        public string Format { get; init; } = "markdown";

        public string Value { get; init; } = string.Empty;

        public SectionContent()
        {
        }
    }
}
