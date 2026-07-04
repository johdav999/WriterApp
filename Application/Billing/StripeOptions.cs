using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace WriterApp.Application.Billing
{
    public sealed class StripeOptions
    {
        public const string ConfigSectionName = "Stripe";
        public const string LegacyBillingSectionName = "Stripe:Billing";
        public const string TestMode = "test";
        public const string LiveMode = "live";
        public const string DefaultSuccessUrl = "/app/account/billing?success=1&session_id={CHECKOUT_SESSION_ID}";
        public const string DefaultCancelUrl = "/app/account/billing?canceled=1";

        public bool Enabled { get; init; }
        public bool WebhookHandlingEnabled { get; init; } = true;
        public string Mode { get; init; } = string.Empty;
        public string SecretKey { get; init; } = string.Empty;
        public string WebhookSecret { get; init; } = string.Empty;
        public StripePriceOptions Prices { get; init; } = new();
        public StripeCheckoutOptions Checkout { get; init; } = new();
        public string BillingPortalReturnUrl { get; init; } = string.Empty;
        public bool LegacyBillingConfigFallbackUsed { get; init; }

        public bool IsLiveMode => string.Equals(Mode, LiveMode, StringComparison.Ordinal);
        public bool IsTestMode => string.Equals(Mode, TestMode, StringComparison.Ordinal);

        public string CurrentStandardPriceId => IsLiveMode
            ? Prices.Standard.LivePriceId
            : Prices.Standard.TestPriceId;

        public string CurrentProPriceId => IsLiveMode
            ? Prices.Pro.LivePriceId
            : Prices.Pro.TestPriceId;

        public bool HasRequiredPricesForCurrentMode =>
            !string.IsNullOrWhiteSpace(CurrentStandardPriceId)
            && !string.IsNullOrWhiteSpace(CurrentProPriceId);

        public static StripeConfigurationResult Load(IConfiguration configuration, bool isDevelopment)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            IConfigurationSection stripeSection = configuration.GetSection(ConfigSectionName);
            IConfigurationSection legacyBillingSection = configuration.GetSection(LegacyBillingSectionName);

            string mode = ReadCanonicalOrLegacy(configuration, stripeSection, legacyBillingSection, "Mode", "STRIPE_MODE");
            string secretKey = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "SecretKey",
                "STRIPE_SECRET_KEY",
                "STRIPE_SECRETKEY");
            string webhookSecret = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "WebhookSecret",
                "STRIPE_WEBHOOK_SECRET",
                "STRIPE_WEBHOOKSECRET");
            string priceStandard = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Prices:Standard:CurrentPriceId",
                "STRIPE_PRICE_STANDARD",
                "STRIPE_PRICESTANDARD");
            string pricePro = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Prices:Pro:CurrentPriceId",
                "STRIPE_PRICE_PRO",
                "STRIPE_PRICEPRO");
            string livePriceStandard = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Prices:Standard:LivePriceId");
            string testPriceStandard = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Prices:Standard:TestPriceId");
            string livePricePro = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Prices:Pro:LivePriceId");
            string testPricePro = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Prices:Pro:TestPriceId");
            string successUrl = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Checkout:SuccessPath",
                "STRIPE_SUCCESS_URL",
                "STRIPE_SUCCESSURL");
            string cancelUrl = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Checkout:CancelPath",
                "STRIPE_CANCEL_URL",
                "STRIPE_CANCELURL");
            string checkoutBaseUrl = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "Checkout:BaseUrl");
            string billingPortalReturnUrl = ReadCanonicalOrLegacy(
                configuration,
                stripeSection,
                legacyBillingSection,
                "BillingPortalReturnUrl",
                "STRIPE_BILLING_PORTAL_RETURN_URL",
                "STRIPE_BILLINGPORTALRETURNURL");

            bool? enabledSetting = ReadCanonicalOrLegacyBool(configuration, stripeSection, legacyBillingSection, "Enabled");
            bool? webhookHandlingEnabledSetting = ReadCanonicalOrLegacyBool(configuration, stripeSection, legacyBillingSection, "WebhookHandlingEnabled");
            bool legacyFallbackUsed = HasLegacyBillingValues(legacyBillingSection);

            string normalizedMode = string.IsNullOrWhiteSpace(mode)
                ? string.Empty
                : mode.Trim().ToLowerInvariant();
            string normalizedSuccessUrl = string.IsNullOrWhiteSpace(successUrl)
                ? DefaultSuccessUrl
                : successUrl.Trim();
            string normalizedCancelUrl = string.IsNullOrWhiteSpace(cancelUrl)
                ? DefaultCancelUrl
                : cancelUrl.Trim();

            StripePriceOptions prices = new()
            {
                Standard = new StripePlanPriceOptions
                {
                    LivePriceId = livePriceStandard.Trim(),
                    TestPriceId = testPriceStandard.Trim()
                },
                Pro = new StripePlanPriceOptions
                {
                    LivePriceId = livePricePro.Trim(),
                    TestPriceId = testPricePro.Trim()
                }
            };

            if (!string.IsNullOrWhiteSpace(priceStandard))
            {
                if (string.Equals(normalizedMode, LiveMode, StringComparison.Ordinal))
                {
                    prices.Standard.LivePriceId = priceStandard.Trim();
                }
                else if (string.Equals(normalizedMode, TestMode, StringComparison.Ordinal))
                {
                    prices.Standard.TestPriceId = priceStandard.Trim();
                }
            }

            if (!string.IsNullOrWhiteSpace(pricePro))
            {
                if (string.Equals(normalizedMode, LiveMode, StringComparison.Ordinal))
                {
                    prices.Pro.LivePriceId = pricePro.Trim();
                }
                else if (string.Equals(normalizedMode, TestMode, StringComparison.Ordinal))
                {
                    prices.Pro.TestPriceId = pricePro.Trim();
                }
            }

            bool requestedEnabled = enabledSetting ?? !string.IsNullOrWhiteSpace(secretKey);
            bool webhookHandlingEnabled = webhookHandlingEnabledSetting ?? requestedEnabled;

            List<string> errors = new();
            List<string> warnings = new();

            if (!requestedEnabled)
            {
                warnings.Add("Stripe billing is disabled.");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(normalizedMode))
                {
                    errors.Add("Stripe configuration invalid: Stripe:Mode is required when Stripe billing is enabled.");
                }
                else if (!string.Equals(normalizedMode, TestMode, StringComparison.Ordinal)
                    && !string.Equals(normalizedMode, LiveMode, StringComparison.Ordinal))
                {
                    errors.Add("Stripe configuration invalid: Stripe:Mode must be either 'test' or 'live'.");
                }

                if (string.IsNullOrWhiteSpace(secretKey))
                {
                    errors.Add("Stripe configuration invalid: Stripe:SecretKey is required when Stripe billing is enabled.");
                }

                string? keyMode = InferModeFromSecretKey(secretKey);
                if (!string.IsNullOrWhiteSpace(secretKey)
                    && !string.IsNullOrWhiteSpace(normalizedMode)
                    && keyMode is not null
                    && !string.Equals(keyMode, normalizedMode, StringComparison.Ordinal))
                {
                    errors.Add($"Stripe configuration invalid: Stripe:Mode is '{normalizedMode}' but Stripe:SecretKey looks like '{keyMode}'.");
                }

                if (webhookHandlingEnabled && string.IsNullOrWhiteSpace(webhookSecret))
                {
                    errors.Add("Stripe configuration invalid: Stripe:WebhookSecret is required when Stripe webhook handling is enabled.");
                }

                if (string.Equals(normalizedMode, LiveMode, StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(prices.Standard.LivePriceId))
                    {
                        errors.Add("Stripe configuration invalid: Stripe:Prices:Standard:LivePriceId is required in live mode.");
                    }

                    if (string.IsNullOrWhiteSpace(prices.Pro.LivePriceId))
                    {
                        errors.Add("Stripe configuration invalid: Stripe:Prices:Pro:LivePriceId is required in live mode.");
                    }
                }
                else if (string.Equals(normalizedMode, TestMode, StringComparison.Ordinal))
                {
                    if (string.IsNullOrWhiteSpace(prices.Standard.TestPriceId))
                    {
                        errors.Add("Stripe configuration invalid: Stripe:Prices:Standard:TestPriceId is required in test mode.");
                    }

                    if (string.IsNullOrWhiteSpace(prices.Pro.TestPriceId))
                    {
                        errors.Add("Stripe configuration invalid: Stripe:Prices:Pro:TestPriceId is required in test mode.");
                    }
                }
            }

            if (legacyFallbackUsed)
            {
                warnings.Add("Stripe legacy configuration fallback was used from Stripe:Billing. Migrate to Stripe:* settings.");
            }

            StripeOptions options = new()
            {
                Enabled = requestedEnabled && errors.Count == 0,
                WebhookHandlingEnabled = webhookHandlingEnabled,
                Mode = normalizedMode,
                SecretKey = secretKey.Trim(),
                WebhookSecret = webhookSecret.Trim(),
                Prices = prices,
                Checkout = new StripeCheckoutOptions
                {
                    SuccessPath = normalizedSuccessUrl,
                    CancelPath = normalizedCancelUrl,
                    BaseUrl = checkoutBaseUrl.Trim()
                },
                BillingPortalReturnUrl = billingPortalReturnUrl.Trim(),
                LegacyBillingConfigFallbackUsed = legacyFallbackUsed
            };

            return new StripeConfigurationResult(options, errors, warnings);
        }

        private static string ReadCanonicalOrLegacy(
            IConfiguration configuration,
            IConfigurationSection stripeSection,
            IConfigurationSection legacyBillingSection,
            string keyPath,
            params string[] envKeys)
        {
            string? canonicalValue = stripeSection[keyPath];
            if (!string.IsNullOrWhiteSpace(canonicalValue))
            {
                return canonicalValue;
            }

            string? legacyValue = keyPath switch
            {
                "SecretKey" => legacyBillingSection["ApiKey"],
                "Prices:Standard:CurrentPriceId" => ReadLegacyCurrentPrice(legacyBillingSection, stripeSection["Mode"], "Standard"),
                "Prices:Pro:CurrentPriceId" => ReadLegacyCurrentPrice(legacyBillingSection, stripeSection["Mode"], "Pro"),
                _ => legacyBillingSection[keyPath]
            };
            if (!string.IsNullOrWhiteSpace(legacyValue))
            {
                return legacyValue;
            }

            return ReadFirst(configuration, envKeys);
        }

        private static bool? ReadCanonicalOrLegacyBool(
            IConfiguration configuration,
            IConfigurationSection stripeSection,
            IConfigurationSection legacyBillingSection,
            string keyPath)
        {
            string? canonical = stripeSection[keyPath];
            if (bool.TryParse(canonical, out bool canonicalValue))
            {
                return canonicalValue;
            }

            string? legacy = legacyBillingSection[keyPath];
            if (bool.TryParse(legacy, out bool legacyValue))
            {
                return legacyValue;
            }

            string? environmentValue = configuration[$"STRIPE_{keyPath.ToUpperInvariant()}"];
            if (bool.TryParse(environmentValue, out bool parsed))
            {
                return parsed;
            }

            return null;
        }

        private static string ReadLegacyCurrentPrice(IConfigurationSection legacyBillingSection, string? mode, string planKey)
        {
            string normalizedMode = string.IsNullOrWhiteSpace(mode) ? string.Empty : mode.Trim().ToLowerInvariant();
            string liveOrTestKey = string.Equals(normalizedMode, LiveMode, StringComparison.Ordinal)
                ? "LivePriceId"
                : "TestPriceId";
            return legacyBillingSection[$"Prices:{planKey}:{liveOrTestKey}"] ?? string.Empty;
        }

        private static bool HasLegacyBillingValues(IConfigurationSection legacyBillingSection)
        {
            foreach (IConfigurationSection child in legacyBillingSection.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Value) || child.GetChildren().Any())
                {
                    return true;
                }
            }

            return false;
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

        private static string? InferModeFromSecretKey(string secretKey)
        {
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                return null;
            }

            string normalized = secretKey.Trim().ToLowerInvariant();
            if (normalized.StartsWith("sk_test_", StringComparison.Ordinal)
                || normalized.StartsWith("rk_test_", StringComparison.Ordinal))
            {
                return TestMode;
            }

            if (normalized.StartsWith("sk_live_", StringComparison.Ordinal)
                || normalized.StartsWith("rk_live_", StringComparison.Ordinal))
            {
                return LiveMode;
            }

            return null;
        }
    }

    public sealed class StripePriceOptions
    {
        public StripePlanPriceOptions Standard { get; set; } = new();
        public StripePlanPriceOptions Pro { get; set; } = new();
    }

    public sealed class StripePlanPriceOptions
    {
        public string LivePriceId { get; set; } = string.Empty;
        public string TestPriceId { get; set; } = string.Empty;
    }

    public sealed class StripeCheckoutOptions
    {
        public string SuccessPath { get; set; } = StripeOptions.DefaultSuccessUrl;
        public string CancelPath { get; set; } = StripeOptions.DefaultCancelUrl;
        public string BaseUrl { get; set; } = string.Empty;
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
