using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WriterApp.Shared
{
    public static class ReturnUrlSafety
    {
        public const string ReturnUrlKey = "returnUrl";
        public const string DefaultProjectsPath = "/projects";
        public const string DefaultHomePath = "/";

        private static readonly string[] DefaultAllowedRoots =
        {
            "/",
            "/app",
            "/projects",
            "/documents",
            "/synopsis",
            "/account",
            "/start",
            "/login",
            "/logout",
            "/billing/checkout"
        };

        // Self-check attack strings (must fall back):
        // //evil.com, https://evil.com, /%2f%2fevil.com, /../admin, /\evil
        public static string NormalizeOrFallback(
            string? rawValue,
            string fallback = DefaultProjectsPath,
            IReadOnlyList<string>? allowedRoots = null)
        {
            string safeFallback = NormalizeFallback(fallback);

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return safeFallback;
            }

            string candidate = rawValue.Trim();
            if (ContainsRejectedCharacters(candidate))
            {
                return safeFallback;
            }

            string decoded;
            try
            {
                decoded = Uri.UnescapeDataString(candidate);
            }
            catch
            {
                return safeFallback;
            }

            if (ContainsRejectedCharacters(decoded))
            {
                return safeFallback;
            }

            if (!decoded.StartsWith("/", StringComparison.Ordinal))
            {
                return safeFallback;
            }

            if (decoded.StartsWith("//", StringComparison.Ordinal))
            {
                return safeFallback;
            }

            if (decoded.Contains("://", StringComparison.Ordinal))
            {
                return safeFallback;
            }

            SplitPathAndSuffix(decoded, out string path, out string suffix);
            if (!TryNormalizePath(path, out string normalizedPath))
            {
                return safeFallback;
            }

            if (!IsAllowedPath(normalizedPath, allowedRoots ?? DefaultAllowedRoots))
            {
                return safeFallback;
            }

            return normalizedPath + suffix;
        }

        public static string ResolveFromQuery(
            IReadOnlyDictionary<string, string>? query,
            string key = ReturnUrlKey,
            string fallback = DefaultProjectsPath,
            IReadOnlyList<string>? allowedRoots = null)
        {
            if (query is null || !query.TryGetValue(key, out string? rawValue))
            {
                return NormalizeOrFallback(null, fallback, allowedRoots);
            }

            return NormalizeOrFallback(rawValue, fallback, allowedRoots);
        }

        private static string NormalizeFallback(string fallback)
        {
            if (string.IsNullOrWhiteSpace(fallback))
            {
                return DefaultProjectsPath;
            }

            string value = fallback.Trim();
            if (!value.StartsWith("/", StringComparison.Ordinal))
            {
                return DefaultProjectsPath;
            }

            return value;
        }

        private static bool ContainsRejectedCharacters(string value)
        {
            foreach (char c in value)
            {
                if (c == '\\' || char.IsControl(c) || c == '\u202d' || c == '\u202e')
                {
                    return true;
                }

                if (c >= '\u2066' && c <= '\u2069')
                {
                    return true;
                }
            }

            return false;
        }

        private static void SplitPathAndSuffix(string input, out string path, out string suffix)
        {
            int queryIndex = input.IndexOf('?');
            int fragmentIndex = input.IndexOf('#');
            int splitIndex;

            if (queryIndex < 0)
            {
                splitIndex = fragmentIndex;
            }
            else if (fragmentIndex < 0)
            {
                splitIndex = queryIndex;
            }
            else
            {
                splitIndex = Math.Min(queryIndex, fragmentIndex);
            }

            if (splitIndex < 0)
            {
                path = input;
                suffix = string.Empty;
                return;
            }

            path = input[..splitIndex];
            suffix = input[splitIndex..];
        }

        private static bool TryNormalizePath(string path, out string normalizedPath)
        {
            normalizedPath = DefaultProjectsPath;

            string collapsed = CollapseRepeatedSlashes(path);
            bool hadTrailingSlash = collapsed.Length > 1 && collapsed.EndsWith("/", StringComparison.Ordinal);

            string[] segments = collapsed.Split('/', StringSplitOptions.None);
            List<string> normalizedSegments = new();

            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                if (segment.Length == 0 || string.Equals(segment, ".", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(segment, "..", StringComparison.Ordinal))
                {
                    if (normalizedSegments.Count == 0)
                    {
                        return false;
                    }

                    normalizedSegments.RemoveAt(normalizedSegments.Count - 1);
                    continue;
                }

                normalizedSegments.Add(segment);
            }

            StringBuilder builder = new("/");
            if (normalizedSegments.Count > 0)
            {
                builder.Append(string.Join('/', normalizedSegments));
                if (hadTrailingSlash)
                {
                    builder.Append('/');
                }
            }

            normalizedPath = builder.ToString();
            if (normalizedPath.Contains("/../", StringComparison.Ordinal)
                || normalizedPath.EndsWith("/..", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static string CollapseRepeatedSlashes(string path)
        {
            if (path.Length == 0)
            {
                return path;
            }

            StringBuilder builder = new(path.Length);
            bool previousSlash = false;
            foreach (char c in path)
            {
                if (c == '/')
                {
                    if (!previousSlash)
                    {
                        builder.Append(c);
                    }

                    previousSlash = true;
                    continue;
                }

                previousSlash = false;
                builder.Append(c);
            }

            return builder.ToString();
        }

        private static bool IsAllowedPath(string path, IReadOnlyList<string> allowedRoots)
        {
            if (allowedRoots.Count == 0)
            {
                return true;
            }

            foreach (string root in allowedRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
            {
                string normalizedRoot = root.Trim();
                if (!normalizedRoot.StartsWith("/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (string.Equals(normalizedRoot, "/", StringComparison.Ordinal))
                {
                    if (string.Equals(path, "/", StringComparison.Ordinal))
                    {
                        return true;
                    }

                    continue;
                }

                if (string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string rootPrefix = normalizedRoot.EndsWith("/", StringComparison.Ordinal)
                    ? normalizedRoot
                    : normalizedRoot + "/";

                if (path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
