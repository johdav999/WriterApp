using System;

namespace WriterApp.Shared.Billing
{
    public static class BillingSubscriptionPolicy
    {
        public static BillingSubscriptionPolicyDecision Evaluate(string? rawStatus)
        {
            string normalizedStatus = NormalizeStatus(rawStatus);
            return normalizedStatus switch
            {
                SubscriptionStatuses.Active => new(normalizedStatus, true, "paid_active", "Paid access is active."),
                SubscriptionStatuses.Trialing => new(normalizedStatus, true, "trial_active", "Trial access is active until Stripe confirms the next billing step."),
                SubscriptionStatuses.PastDue => new(normalizedStatus, false, "payment_past_due", "Payment is past due, so paid access is paused until billing is fixed."),
                SubscriptionStatuses.Unpaid => new(normalizedStatus, false, "payment_unpaid", "The subscription is unpaid, so paid access is paused until billing is fixed."),
                SubscriptionStatuses.Incomplete => new(normalizedStatus, false, "payment_incomplete", "The subscription is incomplete, so paid access does not start until Stripe confirms payment."),
                SubscriptionStatuses.IncompleteExpired => new(normalizedStatus, false, "payment_incomplete_expired", "The incomplete subscription expired before payment completed."),
                SubscriptionStatuses.Canceled => new(normalizedStatus, false, "subscription_canceled", "The subscription is canceled and paid access has ended."),
                _ => new(SubscriptionStatuses.Unknown, false, "status_unknown", "The billing status is unknown, so paid access is paused until billing is verified.")
            };
        }

        public static string NormalizeStatus(string? rawStatus)
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

        public static string GetStatusDisplayLabel(string? rawStatus)
        {
            return NormalizeStatus(rawStatus) switch
            {
                SubscriptionStatuses.Active => "Active",
                SubscriptionStatuses.Trialing => "Trialing",
                SubscriptionStatuses.PastDue => "Past due",
                SubscriptionStatuses.Unpaid => "Unpaid",
                SubscriptionStatuses.Incomplete => "Incomplete",
                SubscriptionStatuses.IncompleteExpired => "Incomplete expired",
                SubscriptionStatuses.Canceled => "Canceled",
                _ => "Unknown"
            };
        }

        public static string BuildLifecycleMessage(
            string effectivePlanKey,
            string planDisplayName,
            string? rawStatus,
            DateTimeOffset? currentPeriodEndUtc,
            bool cancelAtPeriodEnd)
        {
            if (string.Equals(effectivePlanKey, "free", StringComparison.OrdinalIgnoreCase))
            {
                return "You are on the Free plan. Upgrading starts a Stripe-managed subscription. Canceling a paid plan does not delete your account.";
            }

            BillingSubscriptionPolicyDecision decision = Evaluate(rawStatus);
            string formattedDate = FormatBillingDate(currentPeriodEndUtc);

            if (cancelAtPeriodEnd && currentPeriodEndUtc.HasValue && decision.KeepsPaidAccess)
            {
                return $"Your {planDisplayName} plan is scheduled to cancel on {formattedDate}. Paid access remains active until then, and your account continues on the Free plan afterward.";
            }

            return decision.NormalizedStatus switch
            {
                SubscriptionStatuses.Active when currentPeriodEndUtc.HasValue
                    => $"Your {planDisplayName} plan renews on {formattedDate}.",
                SubscriptionStatuses.Active
                    => $"Your {planDisplayName} plan is active.",
                SubscriptionStatuses.Trialing when currentPeriodEndUtc.HasValue
                    => $"Your {planDisplayName} trial is active until {formattedDate}.",
                SubscriptionStatuses.Trialing
                    => $"Your {planDisplayName} trial is active.",
                SubscriptionStatuses.PastDue
                    => $"Payment for your {planDisplayName} plan is past due. Paid access is paused until billing is fixed in Stripe.",
                SubscriptionStatuses.Unpaid
                    => $"Your {planDisplayName} plan is marked unpaid. Paid access is paused until billing is fixed in Stripe.",
                SubscriptionStatuses.Incomplete
                    => $"Your {planDisplayName} subscription setup is incomplete. Paid access starts only after Stripe confirms the first payment.",
                SubscriptionStatuses.IncompleteExpired
                    => $"Your {planDisplayName} subscription setup expired before payment completed. Start a new checkout to activate paid access.",
                SubscriptionStatuses.Canceled when currentPeriodEndUtc.HasValue
                    => $"Your {planDisplayName} subscription is canceled. Paid access ended on {formattedDate}. Your account continues on the Free plan.",
                SubscriptionStatuses.Canceled
                    => $"Your {planDisplayName} subscription is canceled. Your account continues on the Free plan.",
                _ => $"Your {planDisplayName} billing status could not be confirmed. Paid access is paused until billing is verified."
            };
        }

        public static string BuildPortalExplanation(
            string effectivePlanKey,
            string? rawStatus,
            DateTimeOffset? currentPeriodEndUtc,
            bool cancelAtPeriodEnd)
        {
            if (string.Equals(effectivePlanKey, "free", StringComparison.OrdinalIgnoreCase))
            {
                return "Upgrading starts a Stripe-managed subscription. Canceling a paid plan does not delete your account.";
            }

            BillingSubscriptionPolicyDecision decision = Evaluate(rawStatus);
            if (cancelAtPeriodEnd && currentPeriodEndUtc.HasValue && decision.KeepsPaidAccess)
            {
                return "Manage payment method or resume the subscription in the Stripe billing portal. Paid access remains active until the scheduled end date.";
            }

            return decision.NormalizedStatus switch
            {
                SubscriptionStatuses.PastDue or SubscriptionStatuses.Unpaid
                    => "Open the Stripe billing portal to fix payment details. Paid access stays paused until Stripe confirms the subscription is current again.",
                SubscriptionStatuses.Incomplete or SubscriptionStatuses.IncompleteExpired
                    => "Open the Stripe billing portal or start a new checkout to finish subscription setup and activate paid access.",
                SubscriptionStatuses.Canceled
                    => "Open the Stripe billing portal to review past billing details or start a new checkout to reactivate paid access.",
                _ => "Manage payment method, change plan, or cancel subscription in the Stripe billing portal. Canceling the subscription does not delete your account."
            };
        }

        private static string FormatBillingDate(DateTimeOffset? value)
        {
            return value.HasValue
                ? value.Value.ToLocalTime().ToString("yyyy-MM-dd")
                : "-";
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

    public sealed record BillingSubscriptionPolicyDecision(
        string NormalizedStatus,
        bool KeepsPaidAccess,
        string PolicyCode,
        string Summary);
}
