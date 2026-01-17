using System;

namespace WriterApp.Data.Usage
{
    public sealed class UsageEvent
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public long? CostMicros { get; set; }
        public Guid? DocumentId { get; set; }
        public Guid? SectionId { get; set; }
        public DateTime TimestampUtc { get; set; }
        public Guid? CorrelationId { get; set; }
    }
}
