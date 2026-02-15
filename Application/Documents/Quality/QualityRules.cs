using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WriterApp.Application.Documents
{
    public sealed class SentenceLengthRule : IQualityRule
    {
        public string Id => "readability.sentence_length";
        private const int MaxWords = 30;

        public IEnumerable<QualityIssue> Evaluate(QualityCheckContext context)
        {
            foreach (QualitySentence sentence in context.Sentences)
            {
                if (sentence.WordCount <= MaxWords)
                {
                    continue;
                }

                yield return new QualityIssue(
                    string.Empty,
                    Id,
                    "sentence-length",
                    "warning",
                    $"Long sentence ({sentence.WordCount} words).",
                    "Consider splitting the sentence for clarity.",
                    sentence.Text,
                    sentence.Start,
                    sentence.End,
                    null);
            }
        }
    }

    public sealed class ParagraphLengthRule : IQualityRule
    {
        public string Id => "readability.paragraph_length";
        private const int MaxWords = 120;

        public IEnumerable<QualityIssue> Evaluate(QualityCheckContext context)
        {
            foreach (QualityParagraph paragraph in context.Paragraphs)
            {
                if (paragraph.WordCount <= MaxWords)
                {
                    continue;
                }

                QualityIssueFix? fix = BuildSplitFix(context, paragraph);

                yield return new QualityIssue(
                    string.Empty,
                    Id,
                    "paragraph-length",
                    "info",
                    $"Long paragraph ({paragraph.WordCount} words).",
                    "Consider breaking the paragraph into smaller chunks.",
                    paragraph.Text,
                    paragraph.Start,
                    paragraph.End,
                    fix);
            }
        }

        private static QualityIssueFix? BuildSplitFix(QualityCheckContext context, QualityParagraph paragraph)
        {
            if (string.IsNullOrEmpty(context.Text))
            {
                return null;
            }

            int paragraphStart = Math.Max(0, paragraph.Start);
            int paragraphEnd = Math.Clamp(paragraph.End, paragraphStart, context.Text.Length);
            if (paragraphEnd <= paragraphStart)
            {
                return null;
            }

            List<QualitySentence> sentences = context.Sentences
                .Where(sentence => sentence.Start >= paragraphStart && sentence.End <= paragraphEnd)
                .ToList();
            if (sentences.Count < 2)
            {
                return null;
            }

            int totalWords = sentences.Sum(sentence => sentence.WordCount);
            if (totalWords <= 1)
            {
                return null;
            }

            int runningWords = 0;
            int splitSentenceIndex = 0;
            int bestDistance = int.MaxValue;
            int targetWords = totalWords / 2;
            for (int i = 0; i < sentences.Count - 1; i++)
            {
                runningWords += sentences[i].WordCount;
                int distance = Math.Abs(targetWords - runningWords);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    splitSentenceIndex = i;
                }
            }

            int splitAbsolute = sentences[splitSentenceIndex].End;
            int splitRelative = splitAbsolute - paragraphStart;
            if (splitRelative <= 0 || splitRelative >= (paragraphEnd - paragraphStart))
            {
                return null;
            }

            string paragraphText = context.Text.Substring(paragraphStart, paragraphEnd - paragraphStart);
            string left = paragraphText[..splitRelative].TrimEnd();
            string right = paragraphText[splitRelative..].TrimStart();
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return null;
            }

            string replacement = $"{left}\n\n{right}";
            if (string.Equals(replacement, paragraphText, StringComparison.Ordinal))
            {
                return null;
            }

            return new QualityIssueFix("replace", paragraphStart, paragraphEnd, replacement);
        }
    }

    public sealed class ReadabilityScoreRule : IQualityRule
    {
        public string Id => "readability.score";

        public IEnumerable<QualityIssue> Evaluate(QualityCheckContext context)
        {
            int sentenceCount = context.Sentences.Count;
            int wordCount = context.Tokens.Count;
            if (sentenceCount == 0 || wordCount == 0)
            {
                yield break;
            }

            int syllables = QualityTextAnalyzer.CountSyllables(context.Text);
            double wordsPerSentence = wordCount / (double)sentenceCount;
            double syllablesPerWord = syllables / (double)wordCount;
            double score = 206.835 - (1.015 * wordsPerSentence) - (84.6 * syllablesPerWord);

            if (score >= 50)
            {
                yield break;
            }

            yield return new QualityIssue(
                string.Empty,
                Id,
                "readability",
                "warning",
                $"Low readability score ({Math.Round(score, 1)}).",
                "Shorter sentences or simpler words can improve readability.",
                null,
                0,
                Math.Min(context.Text.Length, 1),
                null);
        }
    }

    public sealed class RepeatedWordRule : IQualityRule
    {
        public string Id => "style.repeated_words";
        private const int WindowSize = 5;

        public IEnumerable<QualityIssue> Evaluate(QualityCheckContext context)
        {
            Dictionary<string, QualityToken> recent = new(StringComparer.OrdinalIgnoreCase);
            Queue<string> window = new();

            foreach (QualityToken token in context.Tokens)
            {
                string key = token.Text;
                if (recent.TryGetValue(key, out QualityToken? prev))
                {
                    yield return new QualityIssue(
                        string.Empty,
                        Id,
                        "repeated-word",
                        "info",
                        $"Repeated word \"{token.Text}\" within a short span.",
                        "Try varying the word choice.",
                        token.Text,
                        token.Start,
                        token.End,
                        null);
                }

                recent[key] = token;
                window.Enqueue(key);
                if (window.Count > WindowSize)
                {
                    string removed = window.Dequeue();
                    if (recent.TryGetValue(removed, out QualityToken? stored) && stored.End <= token.Start)
                    {
                        recent.Remove(removed);
                    }
                }
            }
        }

    }

    public sealed class PassiveVoiceRule : IQualityRule
    {
        public string Id => "style.passive_voice";
        private static readonly Regex PassiveRegex = new(@"\b(was|were|is|are|been|be)\s+\w+(ed|en)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IEnumerable<QualityIssue> Evaluate(QualityCheckContext context)
        {
            foreach (QualitySentence sentence in context.Sentences)
            {
                Match match = PassiveRegex.Match(sentence.Text);
                if (!match.Success)
                {
                    continue;
                }

                yield return new QualityIssue(
                    string.Empty,
                    Id,
                    "passive-voice",
                    "info",
                    "Possible passive voice detected.",
                    "Consider using active voice for stronger clarity.",
                    match.Value,
                    sentence.Start + match.Index,
                    sentence.Start + match.Index + match.Length,
                    null);
            }
        }
    }

    public sealed class ProperNameConsistencyRule : IQualityRule
    {
        public string Id => "consistency.proper_names";

        public IEnumerable<QualityIssue> Evaluate(QualityCheckContext context)
        {
            Dictionary<string, Dictionary<string, int>> variants = new(StringComparer.OrdinalIgnoreCase);
            foreach (QualityToken token in context.Tokens)
            {
                if (token.Text.Length < 3)
                {
                    continue;
                }

                if (!char.IsUpper(token.Text[0]))
                {
                    continue;
                }

                string key = token.Text.ToLowerInvariant();
                if (!variants.TryGetValue(key, out Dictionary<string, int>? counts))
                {
                    counts = new Dictionary<string, int>(StringComparer.Ordinal);
                    variants[key] = counts;
                }

                counts[token.Text] = counts.TryGetValue(token.Text, out int count) ? count + 1 : 1;
            }

            foreach (KeyValuePair<string, Dictionary<string, int>> entry in variants)
            {
                if (entry.Value.Count < 2)
                {
                    continue;
                }

                string preferred = entry.Value.OrderByDescending(pair => pair.Value).First().Key;
                foreach (QualityToken token in context.Tokens.Where(t => string.Equals(t.Text, entry.Key, StringComparison.OrdinalIgnoreCase)))
                {
                    if (string.Equals(token.Text, preferred, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    yield return new QualityIssue(
                        string.Empty,
                        Id,
                        "name-consistency",
                        "warning",
                        $"Inconsistent casing for \"{token.Text}\" (preferred \"{preferred}\").",
                        $"Use \"{preferred}\" for consistency.",
                        token.Text,
                        token.Start,
                        token.End,
                        new QualityIssueFix("replace", token.Start, token.End, preferred));
                }
            }
        }
    }

    public sealed class TimelineHintRule : IQualityRule
    {
        public string Id => "consistency.timeline_hint";
        private static readonly Regex TimelineRegex = new(@"\b(\d+\s+(years|months|weeks|days)\s+later|later\s+that\s+day|earlier\s+that\s+day|flashback)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public IEnumerable<QualityIssue> Evaluate(QualityCheckContext context)
        {
            foreach (Match match in TimelineRegex.Matches(context.Text))
            {
                if (!match.Success)
                {
                    continue;
                }

                yield return new QualityIssue(
                    string.Empty,
                    Id,
                    "timeline",
                    "info",
                    $"Timeline jump hint: \"{match.Value}\".",
                    "Make sure the timeline shift is clear to readers.",
                    match.Value,
                    match.Index,
                    match.Index + match.Length,
                    null);
            }
        }
    }

    public sealed class GlossaryRule : IQualityRule
    {
        public string Id => "terminology.glossary";

        public IEnumerable<QualityIssue> Evaluate(QualityCheckContext context)
        {
            if (context.GlossaryTerms.Count == 0)
            {
                yield break;
            }

            HashSet<string> glossary = new(context.GlossaryTerms.Select(term => term.ToLowerInvariant()));
            foreach (QualityToken token in context.Tokens)
            {
                string lower = token.Text.ToLowerInvariant();
                if (glossary.Contains(lower))
                {
                    string canonical = context.GlossaryTerms.FirstOrDefault(term => string.Equals(term, lower, StringComparison.OrdinalIgnoreCase)) ?? token.Text;
                    if (!string.Equals(token.Text, canonical, StringComparison.Ordinal))
                    {
                        yield return new QualityIssue(
                            string.Empty,
                            Id,
                            "glossary",
                            "warning",
                            $"Glossary term casing mismatch for \"{token.Text}\".",
                            $"Use \"{canonical}\" to match your glossary.",
                            token.Text,
                            token.Start,
                            token.End,
                            new QualityIssueFix("replace", token.Start, token.End, canonical));
                    }

                    continue;
                }

                string? near = FindNearMatch(lower, glossary);
                if (near is null)
                {
                    continue;
                }

                string canonicalMatch = context.GlossaryTerms.FirstOrDefault(term => string.Equals(term, near, StringComparison.OrdinalIgnoreCase)) ?? near;
                yield return new QualityIssue(
                    string.Empty,
                    Id,
                    "glossary",
                    "info",
                    $"\"{token.Text}\" is close to glossary term \"{canonicalMatch}\".",
                    $"Consider using \"{canonicalMatch}\" for consistency.",
                    token.Text,
                    token.Start,
                    token.End,
                    null);
            }
        }

        private static string? FindNearMatch(string token, HashSet<string> glossary)
        {
            if (token.Length < 4)
            {
                return null;
            }

            foreach (string term in glossary)
            {
                if (term.Length < 4)
                {
                    continue;
                }

                int distance = LevenshteinDistance(token, term);
                if (distance > 0 && distance <= 1)
                {
                    return term;
                }
            }

            return null;
        }

        private static int LevenshteinDistance(string a, string b)
        {
            int n = a.Length;
            int m = b.Length;
            int[,] d = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++)
            {
                d[i, 0] = i;
            }
            for (int j = 0; j <= m; j++)
            {
                d[0, j] = j;
            }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }

            return d[n, m];
        }
    }
}
