using System;

namespace WriterApp.Client.Utilities
{
    internal static class AuthFlowRoute
    {
        public static bool IsAuthFlowPath(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            string path = ExtractPathOnly(candidate);
            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path.TrimStart('/');
            }

            return path.Equals("/login", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/register", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/logout", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/start", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/deleted-account", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/duplicate-account", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/app/login", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/app/register", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/app/logout", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/app/start", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/app/deleted-account", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/app/duplicate-account", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/.auth", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/.auth/", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractPathOnly(string candidate)
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? absolute))
            {
                return absolute.AbsolutePath;
            }

            int queryIndex = candidate.IndexOf('?');
            int fragmentIndex = candidate.IndexOf('#');
            int splitIndex = queryIndex < 0
                ? fragmentIndex
                : fragmentIndex < 0
                    ? queryIndex
                    : Math.Min(queryIndex, fragmentIndex);

            return splitIndex < 0
                ? candidate
                : candidate[..splitIndex];
        }
    }
}
