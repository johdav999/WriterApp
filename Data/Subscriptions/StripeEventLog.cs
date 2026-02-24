using System;

namespace WriterApp.Data.Subscriptions
{
    public sealed class StripeEventLog
    {
        public string StripeEventId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public DateTimeOffset ReceivedUtc { get; set; }
        public DateTimeOffset? ProcessedUtc { get; set; }
        public string? UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}
