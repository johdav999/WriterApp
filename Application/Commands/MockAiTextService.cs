using System;
using System.Collections.Generic;

namespace WriterApp.Application.Commands
{
    public sealed class MockAiTextService : IAiTextService
    {
        private const int SelectionWordLimit = 12;
        private const int ParagraphWordLimit = 24;
        private const int SectionWordLimit = 36;

        public AiTextProposal ProposeText(
            Guid sectionId,
            TextRange selectionRange,
            string originalText,
            string instruction,
            AiActionScope scope)
        {
            string safeOriginal = originalText ?? string.Empty;
            string normalizedInstruction = instruction ?? string.Empty;
            string instructionKey = normalizedInstruction.Trim().ToLowerInvariant();

            string proposedText = instructionKey switch
            {
                _ when instructionKey.Contains("rewrite", StringComparison.Ordinal) =>
                    $"[AI rewrite] {safeOriginal}",
                _ when instructionKey.Contains("shorten", StringComparison.Ordinal) =>
                    BuildShortenedText(safeOriginal, scope),
                _ when instructionKey.Contains("fix grammar", StringComparison.Ordinal)
                    || instructionKey.Contains("grammar", StringComparison.Ordinal) =>
                    $"[AI grammar fix] {safeOriginal}",
                _ when instructionKey.Contains("tone", StringComparison.Ordinal) =>
                    $"[AI tone shift] {safeOriginal}",
                _ when instructionKey.Contains("summarize", StringComparison.Ordinal)
                    || instructionKey.Contains("summary", StringComparison.Ordinal) =>
                    $"[AI summary] {TrimToWords(safeOriginal, GetWordLimit(scope))}",
                _ => $"[AI] {safeOriginal}"
            };

            string explanation = $"Mock AI proposal ({scope}).";
            return new AiTextProposal(proposedText, explanation);
        }

        private static string BuildShortenedText(string originalText, AiActionScope scope)
        {
            string shortened = TrimToWords(originalText, GetWordLimit(scope));
            if (string.IsNullOrWhiteSpace(shortened))
            {
                return "[AI shortened]";
            }

            return $"{shortened} [AI shortened]";
        }

        private static int GetWordLimit(AiActionScope scope)
        {
            return scope switch
            {
                AiActionScope.Paragraph => ParagraphWordLimit,
                AiActionScope.Section => SectionWordLimit,
                _ => SelectionWordLimit
            };
        }

        private static string TrimToWords(string text, int maxWords)
        {
            if (string.IsNullOrWhiteSpace(text) || maxWords <= 0)
            {
                return string.Empty;
            }

            List<string> words = new();
            int index = 0;
            while (index < text.Length)
            {
                while (index < text.Length && char.IsWhiteSpace(text[index]))
                {
                    index++;
                }

                if (index >= text.Length)
                {
                    break;
                }

                int start = index;
                while (index < text.Length && !char.IsWhiteSpace(text[index]))
                {
                    index++;
                }

                words.Add(text.Substring(start, index - start));
                if (words.Count >= maxWords)
                {
                    break;
                }
            }

            return string.Join(" ", words);
        }
    }
}
