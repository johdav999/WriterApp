using System;

namespace WriterApp.Application.Billing
{
    public sealed class StripePriceResolver : IStripePriceResolver
    {
        private readonly StripeOptions _options;

        public StripePriceResolver(StripeOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public string ResolvePriceId(string planKey, out string normalizedPlanKey)
        {
            if (string.IsNullOrWhiteSpace(planKey))
            {
                throw new InvalidOperationException("planKey is required.");
            }

            normalizedPlanKey = planKey.Trim().ToLowerInvariant();
            StripePlanPriceOptions prices = normalizedPlanKey switch
            {
                "standard" => _options.Prices.Standard,
                "pro" => _options.Prices.Pro,
                _ => throw new InvalidOperationException("planKey must be either 'standard' or 'pro'.")
            };

            string priceId = _options.IsLiveMode
                ? prices.LivePriceId
                : prices.TestPriceId;
            if (string.IsNullOrWhiteSpace(priceId))
            {
                string mode = _options.IsLiveMode ? "live" : "test";
                throw new InvalidOperationException(
                    $"Stripe price id is not configured for plan '{normalizedPlanKey}' in {mode} mode.");
            }

            return priceId.Trim();
        }

        public string? ResolvePlanKey(string priceId)
        {
            if (string.IsNullOrWhiteSpace(priceId))
            {
                return null;
            }

            // This is the single authoritative Stripe price id -> app plan mapping path.
            string candidate = priceId.Trim();
            if (string.Equals(candidate, _options.Prices.Standard.LivePriceId, StringComparison.Ordinal)
                || string.Equals(candidate, _options.Prices.Standard.TestPriceId, StringComparison.Ordinal))
            {
                return "standard";
            }

            if (string.Equals(candidate, _options.Prices.Pro.LivePriceId, StringComparison.Ordinal)
                || string.Equals(candidate, _options.Prices.Pro.TestPriceId, StringComparison.Ordinal))
            {
                return "pro";
            }

            return null;
        }
    }
}
