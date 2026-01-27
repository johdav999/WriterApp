using System;

namespace WriterApp.Data.AI
{
    public sealed class AiActionAppliedEventRecord
    {
        public Guid Id { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public Guid HistoryEntryId { get; set; }
        public DateTimeOffset AppliedAt { get; set; }
        public Guid? AppliedToPageId { get; set; }
        public Guid? AppliedToSectionId { get; set; }
        public Guid? AppliedToDocumentId { get; set; }
        public AiActionHistoryEntryRecord? HistoryEntry { get; set; }
    }
}
