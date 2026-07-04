using System;
using Microsoft.AspNetCore.Http;
using WriterApp.Shared;

namespace WriterApp.Application.Billing
{
    public sealed class StripeRedirectUrlBuilder
    {
        private readonly AppUrlOptions _appUrlOptions;
        private readonly StripeOptions _stripeOptions;

        public StripeRedirectUrlBuilder(
            Microsoft.Extensions.Options.IOptions<AppUrlOptions> appUrlOptions,
            StripeOptions stripeOptions)
        {
            _appUrlOptions = appUrlOptions?.Value ?? throw new ArgumentNullException(nameof(appUrlOptions));
            _stripeOptions = stripeOptions ?? throw new ArgumentNullException(nameof(stripeOptions));
        }

        public string ResolveBaseUrl(HttpRequest request)
        {
            return ResolveBaseUrlContext(request).BaseUrl;
        }

        public StripeBaseUrlResolution ResolveBaseUrlContext(HttpRequest request)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (TryNormalizeBaseUrl(_appUrlOptions.PublicBaseUrl, out string configuredBaseUrl))
            {
                return new StripeBaseUrlResolution(
                    configuredBaseUrl,
                    "AppUrls:PublicBaseUrl",
                    request.Host.Value ?? string.Empty,
                    request.Scheme ?? string.Empty,
                    request.PathBase.Value ?? string.Empty,
                    LooksLikeAzureHost(request.Host.Host));
            }

            if (TryNormalizeBaseUrl(_stripeOptions.Checkout.BaseUrl, out string configuredCheckoutBaseUrl))
            {
                return new StripeBaseUrlResolution(
                    configuredCheckoutBaseUrl,
                    "Stripe:Checkout:BaseUrl",
                    request.Host.Value ?? string.Empty,
                    request.Scheme ?? string.Empty,
                    request.PathBase.Value ?? string.Empty,
                    LooksLikeAzureHost(request.Host.Host));
            }

            return new StripeBaseUrlResolution(
                $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/'),
                "RequestHost",
                request.Host.Value ?? string.Empty,
                request.Scheme ?? string.Empty,
                request.PathBase.Value ?? string.Empty,
                LooksLikeAzureHost(request.Host.Host));
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

        private static bool LooksLikeAzureHost(string? host)
        {
            return !string.IsNullOrWhiteSpace(host)
                && host.Contains(".azurewebsites.net", StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed record StripeBaseUrlResolution(
        string BaseUrl,
        string Source,
        string RequestHost,
        string RequestScheme,
        string RequestPathBase,
        bool RequestHostLooksLikeAzureAppService);
}
