using System;

namespace WriterApp.Data.AI
{
    public sealed class AiActionHistoryEntryRecord
    {
        public Guid Id { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public Guid? DocumentId { get; set; }
        public Guid? SectionId { get; set; }
        public Guid? PageId { get; set; }
        public string ActionKey { get; set; } = string.Empty;
        public string? ProviderId { get; set; }
        public string? ModelId { get; set; }
        public string RequestJson { get; set; } = string.Empty;
        public string ResultJson { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
