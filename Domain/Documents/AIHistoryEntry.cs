using System;

namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// Audit record for AI operations applied to a section.
    /// </summary>
    public record AIHistoryEntry
    {
        public Guid OperationId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// Allowed values: "rewrite", "translate", "continue", or "summarize".
        /// </summary>
        public string Type { get; init; } = string.Empty;

        public string Model { get; init; } = string.Empty;

        public string PromptPreset { get; init; } = string.Empty;

        public string InputExcerpt { get; init; } = string.Empty;

        public string OutputExcerpt { get; init; } = string.Empty;

        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        public bool Accepted { get; init; }

        public AIHistoryEntry()
        {
        }
    }
}
