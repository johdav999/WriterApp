using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WriterApp.Application.Documents
{
    public static class QualityFixClientHelpers
    {
        public static bool IsAppendMode(string? mode)
        {
            return string.Equals(mode?.Trim(), "append", StringComparison.OrdinalIgnoreCase);
        }

        public static string MergeImportedHtmlForAppend(string? existingHtml, string? importedHtml)
        {
            string current = existingHtml?.Trim() ?? string.Empty;
            string incoming = importedHtml?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(incoming))
            {
                return current;
            }

            if (string.IsNullOrWhiteSpace(current))
            {
                return incoming;
            }

            return $"{current}\n<p><br /></p>\n{incoming}";
        }

        public static string BuildProposalAfterText(QualityIssueFixDto? fix)
        {
            if (fix is null)
            {
                return string.Empty;
            }

            if (string.Equals(fix.Kind, "delete", StringComparison.OrdinalIgnoreCase))
            {
                return "(removed)";
            }

            string candidate = fix.Text ?? string.Empty;
            if (LooksLikeProposalMetaLeak(candidate))
            {
                return string.Empty;
            }

            return candidate;
        }

        public static bool LooksLikeProposalMetaLeak(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string normalized = text.Trim();
            string lowered = normalized.ToLowerInvariant();
            if (lowered.Contains("openai", StringComparison.Ordinal)
                || lowered.Contains("responses", StringComparison.Ordinal)
                || lowered.Contains("tool:", StringComparison.Ordinal)
                || lowered.Contains("system:", StringComparison.Ordinal)
                || lowered.Contains("assistant:", StringComparison.Ordinal)
                || lowered.Contains("you are ", StringComparison.Ordinal)
                || lowered.Contains("\"model\"", StringComparison.Ordinal)
                || lowered.Contains("\"input\"", StringComparison.Ordinal)
                || lowered.StartsWith("instruction:", StringComparison.Ordinal)
                || lowered.StartsWith("analysis:", StringComparison.Ordinal)
                || lowered.StartsWith("explanation:", StringComparison.Ordinal))
            {
                return true;
            }

            return Regex.IsMatch(normalized, @"^\s*\{[\s\S]*""model""[\s\S]*\}\s*$");
        }

        public static string SanitizeUiLabel(string? text, string fallback = "")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return fallback;
            }

            StringBuilder builder = new(text.Length);
            foreach (char ch in text)
            {
                if (ch == '\uFFFD')
                {
                    continue;
                }

                UnicodeCategory category = char.GetUnicodeCategory(ch);
                if (category == UnicodeCategory.Control
                    || category == UnicodeCategory.PrivateUse
                    || category == UnicodeCategory.OtherNotAssigned)
                {
                    continue;
                }

                builder.Append(ch);
            }

            string normalized = Regex.Replace(builder.ToString(), @"\s+", " ").Trim();
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }
    }
}
