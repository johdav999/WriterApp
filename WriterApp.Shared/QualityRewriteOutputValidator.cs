using System;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WriterApp.Application.Documents
{
    public static class QualityRewriteOutputValidator
    {
        private static QualityRewriteValidationOptions _options = new();

        public static void Configure(QualityRewriteValidationOptions? options)
        {
            _options = options ?? new QualityRewriteValidationOptions();
        }

        public static string SanitizeCandidateOutput(string? rawOutput)
        {
            if (string.IsNullOrWhiteSpace(rawOutput))
            {
                return string.Empty;
            }

            string candidate = rawOutput.Trim();
            if (TryExtractRevisedTextCandidate(candidate, out string extracted))
            {
                candidate = extracted.Trim();
            }
            else if (ContainsMetaWrapper(candidate))
            {
                // Wrapper markers/JSON envelopes are invalid unless they were parsed into revised text.
                return string.Empty;
            }

            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            if (string.Equals(candidate, "\"\"", StringComparison.Ordinal)
                || string.Equals(candidate, "''", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            if (Regex.IsMatch(candidate, @"^[\p{P}\p{S}\s]+$", RegexOptions.CultureInvariant))
            {
                return string.Empty;
            }

            if (LooksLikeInstructionLeak(candidate))
            {
                return string.Empty;
            }

            return candidate;
        }

        public static string NormalizeRepeatedWordCandidate(string? rawOutput)
        {
            return SanitizeCandidateOutput(rawOutput);
        }

        public static bool IsAcceptableRepeatedWordRewrite(string candidate, string originalText)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return false;
            }

            string normalized = NormalizeRepeatedWordCandidate(candidate);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            string original = (originalText ?? string.Empty).Trim();
            if (original.Length >= 12
                && normalized.Length < Math.Max(_options.MinAbsoluteLength, (int)Math.Round(original.Length * _options.MinLengthRatio, MidpointRounding.AwayFromZero)))
            {
                return false;
            }

            return true;
        }

        public static bool TryValidateRepeatedWordReduction(
            string originalSpan,
            string candidate,
            string anchorText,
            out int originalCount,
            out int candidateCount,
            out string? reason)
        {
            return ValidateRepeatedWordRewrite(
                originalSpan,
                candidate,
                anchorText,
                out originalCount,
                out candidateCount,
                out reason);
        }

        public static bool ValidateRepeatedWordRewrite(
            string originalSpan,
            string candidate,
            string anchorText,
            out int originalCount,
            out int candidateCount,
            out string? reason)
        {
            reason = null;
            originalCount = 0;
            candidateCount = 0;

            string normalized = SanitizeCandidateOutput(candidate);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                reason = "empty_or_invalid";
                return false;
            }

            string anchor = NormalizeAnchorToken(anchorText);
            if (string.IsNullOrWhiteSpace(anchor))
            {
                reason = "missing_anchor";
                return false;
            }

            originalCount = CountOccurrences(originalSpan ?? string.Empty, anchor);
            candidateCount = CountOccurrences(normalized, anchor);

            if (originalCount < 2)
            {
                return true;
            }

            bool strictAnchor = IsStrictAnchor(anchor);
            if (strictAnchor && candidateCount >= originalCount)
            {
                reason = "repetition_not_reduced";
                return false;
            }

            if (!strictAnchor && candidateCount > originalCount)
            {
                reason = "repetition_increased";
                return false;
            }

            if (strictAnchor && candidateCount > _options.PreferMaxAnchorCount)
            {
                reason = "repetition_still_high";
                return false;
            }

            return true;
        }

        public static int CountOccurrences(string text, string anchorText)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            string source = text;
            string anchor = NormalizeAnchorToken(anchorText);
            if (string.IsNullOrWhiteSpace(anchor))
            {
                return 0;
            }

            bool useWordBoundary = ShouldUseWordBoundary(source, anchor);

            if (useWordBoundary)
            {
                string pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(anchor)}(?![\p{{L}}\p{{N}}])";
                return Regex.Matches(source, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Count;
            }

            int count = 0;
            int start = 0;
            while (start <= source.Length - anchor.Length)
            {
                int index = source.IndexOf(anchor, start, StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    break;
                }

                count++;
                start = index + Math.Max(1, anchor.Length);
            }

            return count;
        }

        private static bool TryExtractRevisedTextCandidate(string source, out string revised)
        {
            revised = string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            string value = source.Trim();
            int revisedStart = value.IndexOf("<<REVISED>>", StringComparison.OrdinalIgnoreCase);
            int revisedEnd = value.IndexOf("<<END>>", StringComparison.OrdinalIgnoreCase);
            if (revisedStart >= 0 && revisedEnd > revisedStart)
            {
                int contentStart = revisedStart + "<<REVISED>>".Length;
                revised = value.Substring(contentStart, revisedEnd - contentStart).Trim();
                return !string.IsNullOrWhiteSpace(revised);
            }

            if (value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(value);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("revisedText", out JsonElement revisedText)
                        && revisedText.ValueKind == JsonValueKind.String)
                    {
                        revised = revisedText.GetString()?.Trim() ?? string.Empty;
                        return !string.IsNullOrWhiteSpace(revised);
                    }
                }
                catch (JsonException)
                {
                }
            }

            return false;
        }

        private static bool LooksLikeInstructionLeak(string text)
        {
            string normalized = text.Trim();

            if (Regex.IsMatch(normalized, @"^\s*(?:-|\*|\d+\.)\s+", RegexOptions.Multiline))
            {
                return true;
            }

            string lowered = normalized.ToLowerInvariant();
            if (lowered.Contains("replace with \"\"", StringComparison.Ordinal)
                || lowered.Contains("replace with ''", StringComparison.Ordinal))
            {
                return true;
            }

            if (normalized.Length <= 220
                && (normalized.Contains("->", StringComparison.Ordinal)
                    || normalized.Contains("=>", StringComparison.Ordinal))
                && normalized.Count(ch => ch == '\n') <= 2)
            {
                return true;
            }

            if (ContainsMetaWrapper(normalized))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsCjk(string value)
        {
            foreach (char ch in value)
            {
                if ((ch >= '\u4E00' && ch <= '\u9FFF')
                    || (ch >= '\u3040' && ch <= '\u30FF')
                    || (ch >= '\uAC00' && ch <= '\uD7AF'))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStrictAnchor(string anchor)
        {
            string token = NormalizeAnchorToken(anchor);
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            if (token.Length < _options.StrictAnchorMinLength)
            {
                return false;
            }

            if (!token.Any(char.IsLetterOrDigit))
            {
                return false;
            }

            if (Regex.IsMatch(token, @"^[\p{P}\p{S}]+$", RegexOptions.CultureInvariant))
            {
                return false;
            }

            return true;
        }

        private static string NormalizeAnchorToken(string? anchor)
        {
            if (string.IsNullOrWhiteSpace(anchor))
            {
                return string.Empty;
            }

            string token = anchor.Trim().ToLowerInvariant();
            token = token.Trim('\"', '\'', '`');
            token = token.Trim('.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}');
            return token;
        }

        private static bool ContainsMetaWrapper(string value)
        {
            if (value.Contains("<<REVISED>>", StringComparison.OrdinalIgnoreCase)
                || value.Contains("<<END>>", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (value.Contains("\"revisedText\"", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool ShouldUseWordBoundary(string source, string anchor)
        {
            if (anchor.Length < _options.StrictAnchorMinLength)
            {
                return false;
            }

            if (!anchor.Any(char.IsLetterOrDigit))
            {
                return false;
            }

            if (ContainsCjk(anchor))
            {
                return false;
            }

            int cjkCount = source.Count(IsCjkCharacter);
            if (cjkCount >= 6)
            {
                return false;
            }

            return true;
        }

        private static bool IsCjkCharacter(char ch)
        {
            return (ch >= '\u4E00' && ch <= '\u9FFF')
                || (ch >= '\u3040' && ch <= '\u30FF')
                || (ch >= '\uAC00' && ch <= '\uD7AF');
        }
    }
}
