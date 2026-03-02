using System;

namespace WriterApp.Data.Subscriptions
{
    public sealed class TokenAdjustment
    {
        public long Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int DeltaTokens { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string AdjustedBy { get; set; } = string.Empty;
        public string? AdjustedByEmail { get; set; }
        public DateTime OccurredAtUtc { get; set; }
    }
}
