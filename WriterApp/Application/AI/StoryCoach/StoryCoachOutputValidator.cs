using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using WriterApp.Application.Synopsis;

namespace WriterApp.Application.AI.StoryCoach
{
    public static class StoryCoachOutputValidator
    {
        private static readonly Regex HeadingRegex = new(
            @"^\s*#{1,6}\s+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool TryValidate(string proposedText, string focusFieldKey, string existingValue, out string reason)
        {
            string trimmed = (proposedText ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                reason = "empty_output";
                return false;
            }

            string existingTrimmed = (existingValue ?? string.Empty).Trim();
            if (string.Equals(trimmed, existingTrimmed, StringComparison.Ordinal))
            {
                reason = "no_change";
                return false;
            }

            if (ContainsHeading(trimmed))
            {
                reason = "heading_detected";
                return false;
            }

            if (ContainsFieldLabel(trimmed, focusFieldKey))
            {
                reason = "field_label_detected";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private static bool ContainsHeading(string text)
        {
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string line in lines)
            {
                if (HeadingRegex.IsMatch(line))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsFieldLabel(string text, string focusFieldKey)
        {
            foreach (SynopsisFieldDefinition field in SynopsisFieldCatalog.Fields)
            {
                if (string.Equals(field.Key, focusFieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (HasLabel(text, field.Label) || HasLabel(text, field.Key))
                    {
                        return true;
                    }

                    continue;
                }

                if (HasLabel(text, field.Label) || HasLabel(text, field.Key))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasLabel(string text, string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            string normalizedLabel = StripOptionalSuffix(label);
            List<string> candidates = new() { label };
            if (!string.Equals(normalizedLabel, label, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(normalizedLabel);
            }

            foreach (string candidate in candidates)
            {
                string pattern = $@"(^|\n)\s*{Regex.Escape(candidate)}\s*[:\-]\s+";
                if (Regex.IsMatch(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                {
                    return true;
                }
            }

            return false;
        }

        private static string StripOptionalSuffix(string label)
        {
            const string suffix = " (optional)";
            return label.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                ? label[..^suffix.Length]
                : label;
        }
    }
}
