using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WriterApp.Application.Documents
{
    public static class QualityTextAnalyzer
    {
        private static readonly Regex WordRegex = new(@"\b[\p{L}\p{N}']+\b", RegexOptions.Compiled);
        private static readonly Regex SentenceRegex = new(@"[^.!?]+[.!?]*", RegexOptions.Compiled);
        private static readonly Regex ParagraphSplitRegex = new(@"\r?\n\r?\n+", RegexOptions.Compiled);

        public static IReadOnlyList<QualityToken> GetTokens(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<QualityToken>();
            }

            List<QualityToken> tokens = new();
            foreach (Match match in WordRegex.Matches(text))
            {
                if (!match.Success)
                {
                    continue;
                }

                tokens.Add(new QualityToken(match.Value, match.Index, match.Index + match.Length));
            }

            return tokens;
        }

        public static IReadOnlyList<QualitySentence> GetSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<QualitySentence>();
            }

            List<QualitySentence> sentences = new();
            foreach (Match match in SentenceRegex.Matches(text))
            {
                if (!match.Success)
                {
                    continue;
                }

                string sentence = match.Value.Trim();
                if (sentence.Length == 0)
                {
                    continue;
                }

                int start = match.Index;
                int end = match.Index + match.Length;
                int wordCount = WordRegex.Matches(sentence).Count;
                sentences.Add(new QualitySentence(sentence, start, end, wordCount));
            }

            return sentences;
        }

        public static IReadOnlyList<QualityParagraph> GetParagraphs(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<QualityParagraph>();
            }

            string[] parts = ParagraphSplitRegex.Split(text);
            if (parts.Length == 1)
            {
                int wordCount = WordRegex.Matches(text).Count;
                return new[] { new QualityParagraph(text.Trim(), 0, text.Length, wordCount) };
            }

            List<QualityParagraph> paragraphs = new();
            int index = 0;
            foreach (string part in parts)
            {
                int start = text.IndexOf(part, index, StringComparison.Ordinal);
                if (start < 0)
                {
                    continue;
                }

                int end = start + part.Length;
                int wordCount = WordRegex.Matches(part).Count;
                paragraphs.Add(new QualityParagraph(part.Trim(), start, end, wordCount));
                index = end;
            }

            return paragraphs;
        }

        public static int CountSyllables(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            int count = 0;
            foreach (string word in WordRegex.Matches(text).Select(match => match.Value.ToLowerInvariant()))
            {
                count += CountSyllablesInWord(word);
            }

            return count;
        }

        private static int CountSyllablesInWord(string word)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                return 0;
            }

            int syllables = 0;
            bool prevVowel = false;
            foreach (char c in word)
            {
                bool isVowel = "aeiouyåäö".IndexOf(c) >= 0;
                if (isVowel && !prevVowel)
                {
                    syllables++;
                }
                prevVowel = isVowel;
            }

            if (word.EndsWith("e", StringComparison.OrdinalIgnoreCase) && syllables > 1)
            {
                syllables--;
            }

            return Math.Max(1, syllables);
        }
    }
}
