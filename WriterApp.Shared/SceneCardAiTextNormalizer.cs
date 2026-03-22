using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public static class SceneCardAiTextNormalizer
    {
        public static string? NormalizeAiText(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return null;
            }

            string normalized = input.Replace('_', ' ').Trim();
            if (normalized.Length == 0)
            {
                return null;
            }

            char first = normalized[0];
            char capitalized = char.ToUpperInvariant(first);
            if (capitalized == first)
            {
                return normalized;
            }

            return $"{capitalized}{normalized[1..]}";
        }

        public static IReadOnlyList<string> NormalizeAiTextList(IReadOnlyList<string>? values)
        {
            if (values is null || values.Count == 0)
            {
                return Array.Empty<string>();
            }

            List<string> normalized = new();
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (string? value in values)
            {
                string? item = NormalizeAiText(value);
                if (string.IsNullOrWhiteSpace(item) || !seen.Add(item))
                {
                    continue;
                }

                normalized.Add(item);
            }

            return normalized;
        }
    }
}
