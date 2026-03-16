using System;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public static class EntitlementAccessEvaluator
    {
        public static EvaluatedEntitlementAccess Evaluate(UserEntitlement entitlement)
        {
            if (entitlement is null)
            {
                throw new ArgumentNullException(nameof(entitlement));
            }

            string rawPlanKey = UserEntitlementDefaults.NormalizePlanKey(entitlement.PlanKey);
            string normalizedStatus = NormalizeSubscriptionStatus(entitlement.SubscriptionStatus);
            bool isFreePlan = string.Equals(rawPlanKey, UserEntitlementDefaults.FreePlanKey, StringComparison.Ordinal);
            bool paidLifecycleActive = IsPaidLifecycleActive(normalizedStatus);

            // Paid access is active only for active/trialing subscriptions. Cancel-at-period-end
            // stays active because Stripe keeps the subscription status active until the term ends.
            // Manual/admin plan overrides bypass Stripe lifecycle checks.
            string effectivePlanKey = entitlement.HasManualPlanOverride || isFreePlan || paidLifecycleActive
                ? rawPlanKey
                : UserEntitlementDefaults.FreePlanKey;

            EntitlementAccessBlockReason blockReason =
                entitlement.HasManualPlanOverride || isFreePlan || paidLifecycleActive
                    ? EntitlementAccessBlockReason.None
                    : EntitlementAccessBlockReason.SubscriptionInactive;

            bool paidAccessActive = !string.Equals(effectivePlanKey, UserEntitlementDefaults.FreePlanKey, StringComparison.Ordinal);
            int effectiveAiMonthlyTokenBudget = paidAccessActive || entitlement.HasManualPlanOverride
                ? Math.Max(0, entitlement.AiMonthlyTokenBudget)
                : UserEntitlementDefaults.FreeMonthlyTokenBudget;

            return new EvaluatedEntitlementAccess(
                rawPlanKey,
                effectivePlanKey,
                normalizedStatus,
                entitlement.HasManualPlanOverride,
                paidAccessActive,
                paidAccessActive,
                effectiveAiMonthlyTokenBudget,
                blockReason);
        }

        public static string NormalizeSubscriptionStatus(string? rawStatus)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                return SubscriptionStatuses.Unknown;
            }

            string normalized = rawStatus.Trim().ToLowerInvariant();
            return normalized switch
            {
                SubscriptionStatuses.Active => SubscriptionStatuses.Active,
                SubscriptionStatuses.Trialing => SubscriptionStatuses.Trialing,
                SubscriptionStatuses.PastDue => SubscriptionStatuses.PastDue,
                SubscriptionStatuses.Unpaid => SubscriptionStatuses.Unpaid,
                SubscriptionStatuses.Incomplete => SubscriptionStatuses.Incomplete,
                SubscriptionStatuses.IncompleteExpired => SubscriptionStatuses.IncompleteExpired,
                SubscriptionStatuses.Canceled => SubscriptionStatuses.Canceled,
                _ => SubscriptionStatuses.Unknown
            };
        }

        public static bool IsPaidLifecycleActive(string normalizedStatus)
        {
            if (string.IsNullOrWhiteSpace(normalizedStatus))
            {
                return false;
            }

            return string.Equals(normalizedStatus, SubscriptionStatuses.Active, StringComparison.Ordinal)
                || string.Equals(normalizedStatus, SubscriptionStatuses.Trialing, StringComparison.Ordinal);
        }

        public static class SubscriptionStatuses
        {
            public const string Active = "active";
            public const string Trialing = "trialing";
            public const string PastDue = "past_due";
            public const string Unpaid = "unpaid";
            public const string Incomplete = "incomplete";
            public const string IncompleteExpired = "incomplete_expired";
            public const string Canceled = "canceled";
            public const string Unknown = "unknown";
        }
    }

    public enum EntitlementAccessBlockReason
    {
        None = 0,
        SubscriptionInactive = 1
    }

    public sealed record EvaluatedEntitlementAccess(
        string RawPlanKey,
        string EffectivePlanKey,
        string NormalizedSubscriptionStatus,
        bool HasManualPlanOverride,
        bool IsPaidAccessActive,
        bool IsAiAccessActive,
        int EffectiveAiMonthlyTokenBudget,
        EntitlementAccessBlockReason BlockReason);
}
