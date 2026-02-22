using System;

namespace WriterApp.Data.Subscriptions
{
    public sealed class UserEntitlement
    {
        public string UserId { get; set; } = string.Empty;
        public string PlanKey { get; set; } = string.Empty;
        public string SubscriptionStatus { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public int AiMonthlyTokenBudget { get; set; }
        public int AiTokensUsedThisPeriod { get; set; }
        public DateTimeOffset PeriodStartUtc { get; set; }
        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
