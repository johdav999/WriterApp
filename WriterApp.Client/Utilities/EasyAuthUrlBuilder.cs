using System;
using WriterApp.Application.Security;
using WriterApp.Shared;

namespace WriterApp.Client.Utilities
{
    public static class EasyAuthUrlBuilder
    {
        public const string DefaultPostAuthPath = "/documents";
        public const string CustomerAudience = "customer";
        public const string InternalAudience = "internal";

        public static string BuildLoginUrl(string baseUri, string? returnPath, string fallback = DefaultPostAuthPath)
            => BuildLoginUrl(baseUri, returnPath, authOptions: null, fallback);

        public static string BuildLoginUrl(string baseUri, string? returnPath, WriterAuthOptions? authOptions, string fallback = DefaultPostAuthPath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            string absoluteReturnUri = BuildAbsoluteReturnUri(baseUri, safePath);
            return BuildProviderLoginUrl(
                authOptions?.ResolveLoginProvider() ?? WriterAuthOptions.DefaultLoginProvider,
                absoluteReturnUri);
        }

        public static string BuildLogoutUrl(string baseUri, string? returnPath, string fallback = SafeReturnUrl.DefaultHomePath)
            => BuildLogoutUrl(baseUri, returnPath, authOptions: null, fallback);

        public static string BuildLogoutUrl(string baseUri, string? returnPath, WriterAuthOptions? authOptions, string fallback = SafeReturnUrl.DefaultHomePath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            string absoluteReturnUri = BuildAbsoluteReturnUri(baseUri, safePath);
            string encodedReturnPath = Uri.EscapeDataString(absoluteReturnUri);
            string logoutPath = authOptions?.ResolveLogoutPath() ?? WriterAuthOptions.DefaultLogoutPath;
            return $"{logoutPath}?post_logout_redirect_uri={encodedReturnPath}";
        }

        public static string BuildAppLoginUrl(string? returnPath, string fallback = DefaultPostAuthPath)
            => BuildAppLoginUrl(returnPath, audience: null, fallback);

        public static string BuildAppLoginUrl(string? returnPath, string? audience, string fallback = DefaultPostAuthPath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            string query = $"returnUrl={Uri.EscapeDataString(safePath)}";
            if (!string.IsNullOrWhiteSpace(audience))
            {
                query += $"&audience={Uri.EscapeDataString(audience.Trim())}";
            }

            return $"/app/login?{query}";
        }

        public static string BuildAppRegisterUrl(string? returnPath, string fallback = DefaultPostAuthPath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            return $"/app/register?returnUrl={Uri.EscapeDataString(safePath)}";
        }

        public static string BuildAppStartUrl(string? returnPath, string fallback = DefaultPostAuthPath, string plan = "free")
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            string normalizedPlan = string.IsNullOrWhiteSpace(plan) ? "free" : plan.Trim().ToLowerInvariant();
            return $"/app/start?plan={Uri.EscapeDataString(normalizedPlan)}&returnUrl={Uri.EscapeDataString(safePath)}";
        }

        public static string BuildCustomerLoginUrl(string baseUri, string? returnPath, WriterAuthOptions? authOptions, string fallback = DefaultPostAuthPath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            string absoluteReturnUri = BuildAbsoluteReturnUri(baseUri, safePath);
            string provider = authOptions?.ResolveCustomerLoginProvider() ?? WriterAuthOptions.DefaultLoginProvider;
            return BuildProviderLoginUrl(provider, absoluteReturnUri);
        }

        public static string BuildInternalLoginUrl(string baseUri, string? returnPath, WriterAuthOptions? authOptions, string fallback = DefaultPostAuthPath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            string absoluteReturnUri = BuildAbsoluteReturnUri(baseUri, safePath);
            string provider = authOptions?.ResolveInternalLoginProvider() ?? WriterAuthOptions.DefaultLoginProvider;
            return BuildProviderLoginUrl(provider, absoluteReturnUri);
        }

        private static string NormalizePostAuthPath(string? returnPath, string fallback)
        {
            string safeFallback = ReturnUrlSafety.NormalizeOrFallback(fallback, DefaultPostAuthPath);
            string safe = ReturnUrlSafety.NormalizeOrFallback(returnPath, safeFallback);
            if (!safe.StartsWith("/", StringComparison.Ordinal))
            {
                return safeFallback;
            }

            string pathOnly = ExtractPathOnly(safe);
            if (pathOnly.Equals("/.auth", StringComparison.OrdinalIgnoreCase)
                || pathOnly.StartsWith("/.auth/", StringComparison.OrdinalIgnoreCase))
            {
                return safeFallback;
            }

            return safe;
        }

        private static string BuildAbsoluteReturnUri(string baseUri, string safePath)
        {
            Uri origin = Uri.TryCreate(baseUri, UriKind.Absolute, out Uri? parsedBase)
                ? parsedBase
                : new Uri("http://localhost/");
            Uri absolute = new(origin, safePath);
            return absolute.ToString();
        }

        private static string BuildProviderLoginUrl(string provider, string absoluteReturnUri)
        {
            string encodedProvider = Uri.EscapeDataString(provider);
            string encodedReturnPath = Uri.EscapeDataString(absoluteReturnUri);
            return $"/.auth/login/{encodedProvider}?post_login_redirect_uri={encodedReturnPath}";
        }

        private static string ExtractPathOnly(string pathAndSuffix)
        {
            int queryIndex = pathAndSuffix.IndexOf('?');
            int fragmentIndex = pathAndSuffix.IndexOf('#');
            int splitIndex = queryIndex < 0
                ? fragmentIndex
                : fragmentIndex < 0
                    ? queryIndex
                    : Math.Min(queryIndex, fragmentIndex);

            return splitIndex < 0 ? pathAndSuffix : pathAndSuffix[..splitIndex];
        }
    }
}
