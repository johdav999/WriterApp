using System;

namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// Audit record for AI operations applied to a section.
    /// </summary>
    public record AIHistoryEntry
    {
        public Guid EntryId { get; init; } = Guid.NewGuid();

        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

        public string ActionId { get; init; } = string.Empty;

        public string ProviderId { get; init; } = string.Empty;

        public Guid EditGroupId { get; init; }

        public string OperationSummary { get; init; } = string.Empty;

        public string TargetScope { get; init; } = string.Empty;

        public Guid AffectedSectionId { get; init; }

        public string? Instruction { get; init; }

        public string? BeforeText { get; init; }

        public string? AfterText { get; init; }

        public AIHistoryEntry()
        {
        }
    }
}
