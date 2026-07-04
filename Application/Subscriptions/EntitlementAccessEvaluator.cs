using System;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared.Billing;

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
            BillingSubscriptionPolicyDecision policyDecision = BillingSubscriptionPolicy.Evaluate(entitlement.SubscriptionStatus);
            string normalizedStatus = policyDecision.NormalizedStatus;
            bool isFreePlan = string.Equals(rawPlanKey, UserEntitlementDefaults.FreePlanKey, StringComparison.Ordinal);
            bool paidLifecycleActive = policyDecision.KeepsPaidAccess;

            // Paid access policy is explicit and centralized in BillingSubscriptionPolicy.
            // Cancel-at-period-end stays active because Stripe keeps the status active until term end.
            // Manual/admin plan overrides bypass Stripe lifecycle checks.
            string effectivePlanKey = entitlement.HasManualPlanOverride || isFreePlan || paidLifecycleActive
                ? rawPlanKey
                : UserEntitlementDefaults.FreePlanKey;

            EntitlementAccessBlockReason blockReason =
                entitlement.HasManualPlanOverride || isFreePlan || paidLifecycleActive
                    ? EntitlementAccessBlockReason.None
                    : ResolveBlockReason(normalizedStatus);

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
            return BillingSubscriptionPolicy.NormalizeStatus(rawStatus);
        }

        public static bool IsPaidLifecycleActive(string normalizedStatus)
        {
            return BillingSubscriptionPolicy.Evaluate(normalizedStatus).KeepsPaidAccess;
        }

        private static EntitlementAccessBlockReason ResolveBlockReason(string normalizedStatus)
        {
            return normalizedStatus switch
            {
                SubscriptionStatuses.PastDue => EntitlementAccessBlockReason.PaymentPastDue,
                SubscriptionStatuses.Unpaid => EntitlementAccessBlockReason.PaymentUnpaid,
                SubscriptionStatuses.Incomplete => EntitlementAccessBlockReason.SubscriptionIncomplete,
                SubscriptionStatuses.IncompleteExpired => EntitlementAccessBlockReason.SubscriptionIncompleteExpired,
                SubscriptionStatuses.Canceled => EntitlementAccessBlockReason.SubscriptionCanceled,
                _ => EntitlementAccessBlockReason.SubscriptionUnknown
            };
        }

        public static class SubscriptionStatuses
        {
            public const string Active = BillingSubscriptionPolicy.SubscriptionStatuses.Active;
            public const string Trialing = BillingSubscriptionPolicy.SubscriptionStatuses.Trialing;
            public const string PastDue = BillingSubscriptionPolicy.SubscriptionStatuses.PastDue;
            public const string Unpaid = BillingSubscriptionPolicy.SubscriptionStatuses.Unpaid;
            public const string Incomplete = BillingSubscriptionPolicy.SubscriptionStatuses.Incomplete;
            public const string IncompleteExpired = BillingSubscriptionPolicy.SubscriptionStatuses.IncompleteExpired;
            public const string Canceled = BillingSubscriptionPolicy.SubscriptionStatuses.Canceled;
            public const string Unknown = BillingSubscriptionPolicy.SubscriptionStatuses.Unknown;
        }
    }

    public enum EntitlementAccessBlockReason
    {
        None = 0,
        PaymentPastDue = 1,
        PaymentUnpaid = 2,
        SubscriptionIncomplete = 3,
        SubscriptionIncompleteExpired = 4,
        SubscriptionCanceled = 5,
        SubscriptionUnknown = 6
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
