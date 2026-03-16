using System;
using System.ComponentModel.DataAnnotations.Schema;

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
        public string? StripeCustomerId { get; set; }
        public string? StripeSubscriptionId { get; set; }
        public string? StripePriceId { get; set; }
        public DateTimeOffset? CurrentPeriodEndUtc { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public DateTimeOffset UpdatedUtc { get; set; }

        [NotMapped]
        public bool HasManualPlanOverride { get; set; }
    }
}
