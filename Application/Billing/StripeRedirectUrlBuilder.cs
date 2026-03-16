using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using WriterApp.Shared;

namespace WriterApp.Application.Billing
{
    public sealed class StripeRedirectUrlBuilder
    {
        private readonly AppUrlOptions _appUrlOptions;
        private readonly StripeBillingOptions _stripeBillingOptions;

        public StripeRedirectUrlBuilder(
            IOptions<AppUrlOptions> appUrlOptions,
            IOptions<StripeBillingOptions> stripeBillingOptions)
        {
            _appUrlOptions = appUrlOptions?.Value ?? throw new ArgumentNullException(nameof(appUrlOptions));
            _stripeBillingOptions = stripeBillingOptions?.Value ?? throw new ArgumentNullException(nameof(stripeBillingOptions));
        }

        public string ResolveBaseUrl(HttpRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (TryNormalizeBaseUrl(_appUrlOptions.PublicBaseUrl, out string configuredBaseUrl))
            {
                return configuredBaseUrl;
            }

            // Keep the older checkout-specific setting as a compatibility fallback.
            if (TryNormalizeBaseUrl(_stripeBillingOptions.Checkout.BaseUrl, out string legacyCheckoutBaseUrl))
            {
                return legacyCheckoutBaseUrl;
            }

            return $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
        }

        public string BuildAbsoluteUrl(HttpRequest request, string? configuredUrl, string fallbackRelativePath)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string candidate = string.IsNullOrWhiteSpace(configuredUrl)
                ? fallbackRelativePath
                : configuredUrl.Trim();

            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? absolute)
                && IsSupportedExternalScheme(absolute))
            {
                return absolute.ToString();
            }

            string relative = ReturnUrlSafety.NormalizeOrFallback(candidate, fallbackRelativePath);
            return $"{ResolveBaseUrl(request)}{relative}";
        }

        private static bool TryNormalizeBaseUrl(string? value, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri) || !IsSupportedExternalScheme(uri))
            {
                return false;
            }

            normalized = uri.ToString().TrimEnd('/');
            return true;
        }

        private static bool IsSupportedExternalScheme(Uri uri)
        {
            return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        }
    }
}
