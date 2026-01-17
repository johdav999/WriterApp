using System;

namespace WriterApp.Data.Usage
{
    public sealed class UsageAggregate
    {
        public string UserId { get; set; } = string.Empty;
        public DateTime PeriodStartUtc { get; set; }
        public DateTime PeriodEndUtc { get; set; }
        public string Kind { get; set; } = string.Empty;
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }
        public long TotalCostMicros { get; set; }
        public DateTime UpdatedUtc { get; set; }
    }
}
