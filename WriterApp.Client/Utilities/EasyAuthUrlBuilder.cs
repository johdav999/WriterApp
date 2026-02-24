using System;
using WriterApp.Shared;

namespace WriterApp.Client.Utilities
{
    internal static class EasyAuthUrlBuilder
    {
        public const string DefaultPostAuthPath = "/documents";

        public static string BuildLoginUrl(string baseUri, string? returnPath, string fallback = DefaultPostAuthPath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            string absoluteReturnUri = BuildAbsoluteReturnUri(baseUri, safePath);
            string encodedReturnPath = Uri.EscapeDataString(absoluteReturnUri);
            return $"/.auth/login/aad?post_login_redirect_uri={encodedReturnPath}";
        }

        public static string BuildLogoutUrl(string baseUri, string? returnPath, string fallback = SafeReturnUrl.DefaultHomePath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            string absoluteReturnUri = BuildAbsoluteReturnUri(baseUri, safePath);
            string encodedReturnPath = Uri.EscapeDataString(absoluteReturnUri);
            return $"/.auth/logout?post_logout_redirect_uri={encodedReturnPath}";
        }

        public static string BuildAppLoginUrl(string? returnPath, string fallback = DefaultPostAuthPath)
        {
            string safePath = NormalizePostAuthPath(returnPath, fallback);
            return $"/app/login?returnUrl={Uri.EscapeDataString(safePath)}";
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
