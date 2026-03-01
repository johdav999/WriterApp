using System;

namespace WriterApp.Application.Usage
{
    public sealed class UsageSnapshot
    {
        public UsageSnapshot(
            string userId,
            DateTime periodStartUtc,
            DateTime periodEndUtc,
            string kind,
            int totalInputTokens,
            int totalOutputTokens,
            long totalCostMicros,
            DateTime updatedUtc)
        {
            UserId = userId;
            PeriodStartUtc = periodStartUtc;
            PeriodEndUtc = periodEndUtc;
            Kind = kind;
            TotalInputTokens = totalInputTokens;
            TotalOutputTokens = totalOutputTokens;
            TotalCostMicros = totalCostMicros;
            UpdatedUtc = updatedUtc;
        }

        public string UserId { get; }
        public DateTime PeriodStartUtc { get; }
        public DateTime PeriodEndUtc { get; }
        public string Kind { get; }
        public int TotalInputTokens { get; }
        public int TotalOutputTokens { get; }
        public long TotalCostMicros { get; }
        public DateTime UpdatedUtc { get; }
    }
}
