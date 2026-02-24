using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace WriterApp.Application.Billing
{
    public sealed class StripeOptions
    {
        public const string TestMode = "test";
        public const string LiveMode = "live";
        public const string DefaultSuccessUrl = "/app/account?billing=success";
        public const string DefaultCancelUrl = "/app/account?billing=cancel";

        public bool Enabled { get; init; }
        public string Mode { get; init; } = TestMode;
        public string SecretKey { get; init; } = string.Empty;
        public string WebhookSecret { get; init; } = string.Empty;
        public string PriceStandard { get; init; } = string.Empty;
        public string PricePro { get; init; } = string.Empty;
        public string SuccessUrl { get; init; } = DefaultSuccessUrl;
        public string CancelUrl { get; init; } = DefaultCancelUrl;
        public string BillingPortalReturnUrl { get; init; } = string.Empty;

        public static StripeConfigurationResult Load(IConfiguration configuration, bool isDevelopment)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            string mode = ReadSetting(configuration, "Mode");
            string secretKey = ReadSetting(configuration, "SecretKey");
            string webhookSecret = ReadSetting(configuration, "WebhookSecret");
            string priceStandard = ReadSetting(configuration, "PriceStandard");
            string pricePro = ReadSetting(configuration, "PricePro");
            string successUrl = ReadSetting(configuration, "SuccessUrl");
            string cancelUrl = ReadSetting(configuration, "CancelUrl");
            string billingPortalReturnUrl = ReadSetting(configuration, "BillingPortalReturnUrl");

            string normalizedMode = string.IsNullOrWhiteSpace(mode)
                ? TestMode
                : mode.Trim().ToLowerInvariant();
            string normalizedSuccessUrl = string.IsNullOrWhiteSpace(successUrl)
                ? DefaultSuccessUrl
                : successUrl.Trim();
            string normalizedCancelUrl = string.IsNullOrWhiteSpace(cancelUrl)
                ? DefaultCancelUrl
                : cancelUrl.Trim();

            List<string> errors = new();
            List<string> warnings = new();

            bool hasSecret = !string.IsNullOrWhiteSpace(secretKey);
            if (!hasSecret)
            {
                if (isDevelopment)
                {
                    warnings.Add("Stripe is disabled in development because Stripe__SecretKey is missing.");
                }
                else
                {
                    errors.Add("Stripe configuration invalid: Stripe__SecretKey is required in non-development environments.");
                }
            }

            if (hasSecret)
            {
                if (!string.Equals(normalizedMode, TestMode, StringComparison.Ordinal)
                    && !string.Equals(normalizedMode, LiveMode, StringComparison.Ordinal))
                {
                    errors.Add("Stripe configuration invalid: Stripe__Mode must be either 'test' or 'live'.");
                }

                if (string.IsNullOrWhiteSpace(webhookSecret))
                {
                    errors.Add("Stripe configuration invalid: Stripe__WebhookSecret is required when Stripe is enabled.");
                }

                if (string.IsNullOrWhiteSpace(priceStandard))
                {
                    errors.Add("Stripe configuration invalid: Stripe__PriceStandard is required when Stripe is enabled.");
                }

                if (string.IsNullOrWhiteSpace(pricePro))
                {
                    errors.Add("Stripe configuration invalid: Stripe__PricePro is required when Stripe is enabled.");
                }

                if (string.IsNullOrWhiteSpace(billingPortalReturnUrl))
                {
                    errors.Add("Stripe configuration invalid: Stripe__BillingPortalReturnUrl is required when Stripe is enabled.");
                }
            }

            StripeOptions options = new()
            {
                Enabled = hasSecret && errors.Count == 0,
                Mode = normalizedMode,
                SecretKey = secretKey.Trim(),
                WebhookSecret = webhookSecret.Trim(),
                PriceStandard = priceStandard.Trim(),
                PricePro = pricePro.Trim(),
                SuccessUrl = normalizedSuccessUrl,
                CancelUrl = normalizedCancelUrl,
                BillingPortalReturnUrl = billingPortalReturnUrl.Trim()
            };

            return new StripeConfigurationResult(options, errors, warnings);
        }

        private static string ReadSetting(IConfiguration configuration, string key)
        {
            string fromStripe = configuration[$"Stripe:{key}"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fromStripe))
            {
                return fromStripe;
            }

            string fromWriterAppStripe = configuration[$"WriterApp:Stripe:{key}"] ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(fromWriterAppStripe))
            {
                return fromWriterAppStripe;
            }

            return key switch
            {
                nameof(Mode) => ReadFirst(configuration, "STRIPE_MODE"),
                nameof(SecretKey) => ReadFirst(configuration, "STRIPE_SECRET_KEY", "STRIPE_SECRETKEY"),
                nameof(WebhookSecret) => ReadFirst(configuration, "STRIPE_WEBHOOK_SECRET", "STRIPE_WEBHOOKSECRET"),
                nameof(PriceStandard) => ReadFirst(configuration, "STRIPE_PRICE_STANDARD", "STRIPE_PRICESTANDARD"),
                nameof(PricePro) => ReadFirst(configuration, "STRIPE_PRICE_PRO", "STRIPE_PRICEPRO"),
                nameof(SuccessUrl) => ReadFirst(configuration, "STRIPE_SUCCESS_URL", "STRIPE_SUCCESSURL"),
                nameof(CancelUrl) => ReadFirst(configuration, "STRIPE_CANCEL_URL", "STRIPE_CANCELURL"),
                nameof(BillingPortalReturnUrl) => ReadFirst(configuration, "STRIPE_BILLING_PORTAL_RETURN_URL", "STRIPE_BILLINGPORTALRETURNURL"),
                _ => string.Empty
            };
        }

        private static string ReadFirst(IConfiguration configuration, params string[] keys)
        {
            foreach (string key in keys)
            {
                string? value = configuration[key];
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }
    }

    public sealed class StripeConfigurationResult
    {
        public StripeConfigurationResult(
            StripeOptions options,
            IReadOnlyList<string> errors,
            IReadOnlyList<string> warnings)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Errors = errors ?? throw new ArgumentNullException(nameof(errors));
            Warnings = warnings ?? throw new ArgumentNullException(nameof(warnings));
        }

        public StripeOptions Options { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
    }
}
