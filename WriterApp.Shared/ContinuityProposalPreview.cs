using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace WriterApp.Application.Continuity
{
    public sealed record ContinuityProposalPreview(
        string Before,
        string After,
        string Prefix,
        string Suffix,
        int Start,
        int Length);

    public static class ContinuityProposalPreviewBuilder
    {
        public static ContinuityProposalPreview Build(
            string? plainText,
            int anchorStart,
            int anchorLength,
            string? suggestedFix,
            int contextRadius = 40)
        {
            string text = plainText ?? string.Empty;
            int start = Math.Clamp(anchorStart, 0, text.Length);
            int length = Math.Max(0, anchorLength);
            if (start + length > text.Length)
            {
                length = Math.Max(0, text.Length - start);
            }

            int end = start + length;
            int radius = Math.Max(0, contextRadius);

            string before = length > 0 ? text.Substring(start, length) : string.Empty;
            string after = suggestedFix ?? string.Empty;
            int contextStart = Math.Max(0, start - radius);
            int contextEnd = Math.Min(text.Length, end + radius);
            string prefix = contextStart < start
                ? text.Substring(contextStart, start - contextStart)
                : string.Empty;
            string suffix = end < contextEnd
                ? text.Substring(end, contextEnd - end)
                : string.Empty;

            return new ContinuityProposalPreview(
                before,
                after,
                prefix,
                suffix,
                start,
                length);
        }
    }

    public sealed record ContinuityRewriteSpan(
        int Start,
        int Length,
        string Before,
        string Prefix,
        string Suffix,
        bool StartsSentence,
        bool EndsSentence);

    public static class ContinuityRewriteSpanResolver
    {
        public static ContinuityRewriteSpan BuildFromRange(string? plainText, int start, int length, int contextRadius = 48)
        {
            string text = plainText ?? string.Empty;
            int clampedStart = Math.Clamp(start, 0, text.Length);
            int clampedLength = Math.Max(0, length);
            if (clampedStart + clampedLength > text.Length)
            {
                clampedLength = Math.Max(0, text.Length - clampedStart);
            }

            int end = clampedStart + clampedLength;
            int radius = Math.Max(0, contextRadius);
            int contextStart = Math.Max(0, clampedStart - radius);
            int contextEnd = Math.Min(text.Length, end + radius);

            string before = clampedLength > 0 ? text.Substring(clampedStart, clampedLength) : string.Empty;
            string prefix = contextStart < clampedStart
                ? text.Substring(contextStart, clampedStart - contextStart)
                : string.Empty;
            string suffix = end < contextEnd
                ? text.Substring(end, contextEnd - end)
                : string.Empty;

            int paragraphStart = FindParagraphStart(text, clampedStart);
            int paragraphEnd = FindParagraphEnd(text, end);
            bool startsSentence = clampedStart == paragraphStart || IsSentenceBoundaryBefore(text, clampedStart);
            bool endsSentence = end == paragraphEnd || IsSentenceBoundaryAt(text, end);

            return new ContinuityRewriteSpan(
                clampedStart,
                clampedLength,
                before,
                prefix,
                suffix,
                startsSentence,
                endsSentence);
        }

        public static ContinuityRewriteSpan ExpandToSentenceSpan(string? plainText, int anchorStart, int anchorLength, int contextRadius = 48)
        {
            string text = plainText ?? string.Empty;
            int start = Math.Clamp(anchorStart, 0, text.Length);
            int length = Math.Max(0, anchorLength);
            if (start + length > text.Length)
            {
                length = Math.Max(0, text.Length - start);
            }

            int end = start + length;
            int paragraphStart = FindParagraphStart(text, start);
            int paragraphEnd = FindParagraphEnd(text, end);

            int expandedStart = FindSentenceStart(text, paragraphStart, start);
            int expandedEnd = FindSentenceEnd(text, end, paragraphEnd);
            (expandedStart, expandedEnd) = ExpandToWordBoundaries(text, expandedStart, expandedEnd, paragraphStart, paragraphEnd);

            expandedStart = Math.Clamp(expandedStart, paragraphStart, paragraphEnd);
            expandedEnd = Math.Clamp(expandedEnd, expandedStart, paragraphEnd);
            int expandedLength = Math.Max(0, expandedEnd - expandedStart);

            string before = expandedLength > 0 ? text.Substring(expandedStart, expandedLength) : string.Empty;
            int radius = Math.Max(0, contextRadius);
            int contextStart = Math.Max(0, expandedStart - radius);
            int contextEnd = Math.Min(text.Length, expandedEnd + radius);
            string prefix = contextStart < expandedStart
                ? text.Substring(contextStart, expandedStart - contextStart)
                : string.Empty;
            string suffix = expandedEnd < contextEnd
                ? text.Substring(expandedEnd, contextEnd - expandedEnd)
                : string.Empty;

            bool startsSentence = expandedStart == paragraphStart || IsSentenceBoundaryBefore(text, expandedStart);
            bool endsSentence = expandedEnd == paragraphEnd || IsSentenceBoundaryAt(text, expandedEnd);

            return new ContinuityRewriteSpan(
                expandedStart,
                expandedLength,
                before,
                prefix,
                suffix,
                startsSentence,
                endsSentence);
        }

        private static int FindParagraphStart(string text, int index)
        {
            int searchEnd = Math.Max(0, Math.Min(index, text.Length));
            int breakIndex = text.LastIndexOf("\n\n", searchEnd > 0 ? searchEnd - 1 : 0, StringComparison.Ordinal);
            return breakIndex < 0 ? 0 : Math.Min(text.Length, breakIndex + 2);
        }

        private static int FindParagraphEnd(string text, int index)
        {
            int searchStart = Math.Max(0, Math.Min(index, text.Length));
            int breakIndex = text.IndexOf("\n\n", searchStart, StringComparison.Ordinal);
            return breakIndex < 0 ? text.Length : breakIndex;
        }

        private static int FindSentenceStart(string text, int paragraphStart, int index)
        {
            for (int i = Math.Max(paragraphStart, index - 1); i >= paragraphStart; i--)
            {
                char ch = text[i];
                if (!IsSentenceTerminal(ch))
                {
                    continue;
                }

                int candidate = i + 1;
                while (candidate < text.Length && char.IsWhiteSpace(text[candidate]))
                {
                    candidate++;
                }

                return Math.Clamp(candidate, paragraphStart, text.Length);
            }

            return paragraphStart;
        }

        private static int FindSentenceEnd(string text, int index, int paragraphEnd)
        {
            int start = Math.Clamp(index, 0, text.Length);
            int max = Math.Clamp(paragraphEnd, start, text.Length);
            for (int i = start; i < max; i++)
            {
                char ch = text[i];
                if (!IsSentenceTerminal(ch))
                {
                    continue;
                }

                int candidate = i + 1;
                while (candidate < max && IsSentenceTrailing(text[candidate]))
                {
                    candidate++;
                }

                return Math.Clamp(candidate, start, max);
            }

            return max;
        }

        private static (int Start, int End) ExpandToWordBoundaries(string text, int start, int end, int min, int max)
        {
            int safeStart = Math.Clamp(start, min, max);
            int safeEnd = Math.Clamp(end, safeStart, max);

            while (safeStart > min && IsWordChar(text[safeStart]) && IsWordChar(text[safeStart - 1]))
            {
                safeStart--;
            }

            while (safeEnd < max && safeEnd > 0 && IsWordChar(text[safeEnd - 1]) && IsWordChar(text[safeEnd]))
            {
                safeEnd++;
            }

            return (safeStart, safeEnd);
        }

        private static bool IsSentenceBoundaryBefore(string text, int index)
        {
            if (index <= 0)
            {
                return true;
            }

            int cursor = index - 1;
            while (cursor >= 0 && char.IsWhiteSpace(text[cursor]))
            {
                cursor--;
            }

            return cursor < 0 || IsSentenceTerminal(text[cursor]);
        }

        private static bool IsSentenceBoundaryAt(string text, int index)
        {
            if (index <= 0 || index > text.Length)
            {
                return true;
            }

            int cursor = index - 1;
            while (cursor >= 0 && IsSentenceTrailing(text[cursor]))
            {
                cursor--;
            }

            return cursor < 0 || IsSentenceTerminal(text[cursor]);
        }

        private static bool IsSentenceTerminal(char ch) => ch == '.' || ch == '!' || ch == '?';

        private static bool IsSentenceTrailing(char ch) => char.IsWhiteSpace(ch) || ch == '"' || ch == '\'' || ch == ')' || ch == ']' || ch == '\u201d' || ch == '\u2019';

        private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_' || ch == '\u2019' || ch == '\'';
    }

    public static class ContinuityRewriteValidator
    {
        public static bool ValidateReplacement(
            string? prefix,
            string? replacement,
            string? suffix,
            bool startsSentence,
            bool endsSentence,
            int beforeLength,
            out string? error)
        {
            string left = prefix ?? string.Empty;
            string middle = (replacement ?? string.Empty).Trim();
            string right = suffix ?? string.Empty;

            if (string.IsNullOrWhiteSpace(middle))
            {
                error = "Suggestion didn't contain revised prose.";
                return false;
            }

            if (HasMidWordJoin(left, middle))
            {
                error = "Suggestion starts mid-word against surrounding text.";
                return false;
            }

            if (HasMidWordJoin(middle, right))
            {
                error = "Suggestion ends mid-word against surrounding text.";
                return false;
            }

            if (HasLargeEdgeOverlap(middle, right, minOverlapChars: 18))
            {
                error = "Suggestion duplicates trailing text from the kept suffix.";
                return false;
            }

            if (beforeLength >= 40 && middle.Length < Math.Max(10, (int)Math.Floor(beforeLength * 0.4)))
            {
                error = "Suggestion is too short for the selected continuity span.";
                return false;
            }

            if (startsSentence)
            {
                char firstLetter = middle.FirstOrDefault(char.IsLetter);
                if (firstLetter != default && !char.IsUpper(firstLetter))
                {
                    error = "Suggestion should start like a sentence for this span.";
                    return false;
                }
            }

            if (endsSentence)
            {
                if (!Regex.IsMatch(middle, @"[.!?][""'\)\]\u201d\u2019]*\s*$"))
                {
                    error = "Suggestion should end with sentence punctuation for this span.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool HasMidWordJoin(string left, string right)
        {
            char? leftChar = LastNonWhitespace(left);
            char? rightChar = FirstNonWhitespace(right);
            if (leftChar is null || rightChar is null)
            {
                return false;
            }

            return IsWordChar(leftChar.Value) && IsWordChar(rightChar.Value);
        }

        private static bool HasLargeEdgeOverlap(string left, string right, int minOverlapChars)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            string leftNormalized = left.TrimEnd();
            string rightNormalized = right.TrimStart();
            int max = Math.Min(leftNormalized.Length, rightNormalized.Length);
            for (int length = max; length >= minOverlapChars; length--)
            {
                string leftTail = leftNormalized.Substring(leftNormalized.Length - length, length);
                string rightHead = rightNormalized.Substring(0, length);
                if (string.Equals(leftTail, rightHead, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static char? FirstNonWhitespace(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return value[i];
                }
            }

            return null;
        }

        private static char? LastNonWhitespace(string value)
        {
            for (int i = value.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return value[i];
                }
            }

            return null;
        }

        private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch) || ch == '_' || ch == '\'' || ch == '\u2019';
    }
}
