namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// Precomputed statistics for fast UI summaries and analytics.
    /// </summary>
    public record SectionStats
    {
        public int WordCount { get; init; }

        public int CharacterCount { get; init; }

        public SectionStats()
        {
        }
    }
}
