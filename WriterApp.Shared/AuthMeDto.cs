using System.Collections.Generic;

namespace WriterApp.Application.Security
{
    public sealed class AuthMeDto
    {
        public bool IsAuthenticated { get; init; }
        public string? Name { get; init; }
        public string? Email { get; init; }
        public string? UserId { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = new List<string>();
        public bool IsAdminAccess { get; init; }
        public string? AdminAccessSource { get; init; }
        public string? PlanKey { get; init; }
        public string? EffectivePlanKey { get; init; }
        public string? SubscriptionStatus { get; init; }
        public DateTimeOffset? CurrentPeriodEndUtc { get; init; }
        public bool CancelAtPeriodEnd { get; init; }
        public bool IsPaidAccessActive { get; init; }
        public string? StripeCustomerId { get; init; }
        public int AiMonthlyTokenBudget { get; init; }
        public int AiTokensUsedThisPeriod { get; init; }
        public DateTimeOffset PeriodStartUtc { get; init; }
        public DateTimeOffset EntitlementUpdatedUtc { get; init; }
    }
}
