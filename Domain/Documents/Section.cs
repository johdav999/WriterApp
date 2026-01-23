using System;

namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// A section of text with content, statistics, flags, and AI audit data.
    /// </summary>
    public record Section
    {
        public Guid SectionId { get; init; } = Guid.NewGuid();

        public int Order { get; init; }

        public string Title { get; init; } = string.Empty;

        public SectionKind Kind { get; init; } = SectionKind.Chapter;

        public bool IncludeInNumbering { get; init; } = true;

        public SectionNumberingStyle NumberingStyle { get; init; } = SectionNumberingStyle.Decimal;

        public SectionContent Content { get; init; } = new();

        public SectionStats Stats { get; init; } = new();

        public SectionFlags Flags { get; init; } = new();

        /// <summary>
        /// Tracks AI involvement to support auditability and tooling.
        /// </summary>
        public SectionAIInfo AI { get; init; } = new();

        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        public DateTime ModifiedUtc { get; init; } = DateTime.UtcNow;

        public Section()
        {
        }
    }
}
