using System;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Billing
{
    public static class StripeBillingEnvironment
    {
        public const string LegacyMode = "legacy";

        public static string Normalize(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().ToLowerInvariant();
            return normalized switch
            {
                StripeOptions.LiveMode => StripeOptions.LiveMode,
                StripeOptions.TestMode => StripeOptions.TestMode,
                LegacyMode => LegacyMode,
                _ => normalized
            };
        }

        public static string ResolveStoredMode(UserEntitlement entitlement, StripeOptions options)
        {
            if (entitlement is null)
            {
                return string.Empty;
            }

            string normalizedStored = Normalize(entitlement.StripeMode);
            if (!string.IsNullOrWhiteSpace(normalizedStored))
            {
                return normalizedStored;
            }

            return InferModeFromPriceId(options, entitlement.StripePriceId) ?? string.Empty;
        }

        public static bool IsModeMismatch(string? storedMode, string activeMode)
        {
            string normalizedStored = Normalize(storedMode);
            string normalizedActive = Normalize(activeMode);
            if (string.IsNullOrWhiteSpace(normalizedStored) || string.IsNullOrWhiteSpace(normalizedActive))
            {
                return false;
            }

            if (string.Equals(normalizedStored, LegacyMode, StringComparison.Ordinal))
            {
                return false;
            }

            return !string.Equals(normalizedStored, normalizedActive, StringComparison.Ordinal);
        }

        public static bool MatchesActiveOrLegacy(string? storedMode, string activeMode)
        {
            string normalizedStored = Normalize(storedMode);
            if (string.IsNullOrWhiteSpace(normalizedStored))
            {
                return true;
            }

            return string.Equals(normalizedStored, LegacyMode, StringComparison.Ordinal)
                || string.Equals(normalizedStored, Normalize(activeMode), StringComparison.Ordinal);
        }

        public static string? InferModeFromPriceId(StripeOptions options, string? stripePriceId)
        {
            if (options is null || string.IsNullOrWhiteSpace(stripePriceId))
            {
                return null;
            }

            string candidate = stripePriceId.Trim();
            if (string.Equals(candidate, options.Prices.Standard.LivePriceId, StringComparison.Ordinal)
                || string.Equals(candidate, options.Prices.Pro.LivePriceId, StringComparison.Ordinal))
            {
                return StripeOptions.LiveMode;
            }

            if (string.Equals(candidate, options.Prices.Standard.TestPriceId, StringComparison.Ordinal)
                || string.Equals(candidate, options.Prices.Pro.TestPriceId, StringComparison.Ordinal))
            {
                return StripeOptions.TestMode;
            }

            return null;
        }
    }
}
