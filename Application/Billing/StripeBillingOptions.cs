using System;

namespace WriterApp.Application.Billing
{
    public sealed class StripeBillingOptions
    {
        public string Mode { get; set; } = "Test";
        public string ApiKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;
        public StripeBillingPricesOptions Prices { get; set; } = new();
        public StripeBillingCheckoutOptions Checkout { get; set; } = new();

        public bool IsLiveMode =>
            string.Equals(Mode, "Live", StringComparison.OrdinalIgnoreCase);
    }

    public sealed class StripeBillingPricesOptions
    {
        public StripeBillingPlanPriceOptions Standard { get; set; } = new();
        public StripeBillingPlanPriceOptions Pro { get; set; } = new();
    }

    public sealed class StripeBillingPlanPriceOptions
    {
        public string LivePriceId { get; set; } = string.Empty;
        public string TestPriceId { get; set; } = string.Empty;
    }

    public sealed class StripeBillingCheckoutOptions
    {
        public string SuccessPath { get; set; } = "/app/account/billing?success=1&session_id={CHECKOUT_SESSION_ID}";
        public string CancelPath { get; set; } = "/app/account/billing?canceled=1";
        public string BaseUrl { get; set; } = string.Empty;
    }
}
