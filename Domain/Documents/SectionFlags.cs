namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// Flags that control visibility and editing behavior.
    /// </summary>
    public record SectionFlags
    {
        public bool Locked { get; init; }

        public bool Hidden { get; init; }

        public SectionFlags()
        {
        }
    }
}
