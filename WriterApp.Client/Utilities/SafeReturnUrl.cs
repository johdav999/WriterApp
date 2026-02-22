using System;
using System.Collections.Generic;
using WriterApp.Shared;

namespace WriterApp.Client.Utilities
{
    internal static class SafeReturnUrl
    {
        public const string DefaultProjectsPath = ReturnUrlSafety.DefaultProjectsPath;
        public const string DefaultHomePath = ReturnUrlSafety.DefaultHomePath;

        public static string ResolveFromQuery(
            Dictionary<string, string> query,
            string key = "returnUrl",
            string fallback = DefaultProjectsPath)
        {
            return ReturnUrlSafety.ResolveFromQuery(query, key, fallback);
        }

        public static string NormalizeOrFallback(string? candidate, string fallback = DefaultProjectsPath)
        {
            return ReturnUrlSafety.NormalizeOrFallback(candidate, fallback);
        }
    }
}
