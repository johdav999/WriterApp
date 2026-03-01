using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;

namespace WriterApp.Application.Commands
{
    public sealed class MockAiTextService : IAiTextService
    {
        private const int SelectionWordLimit = 12;
        private const int ParagraphWordLimit = 24;
        private const int SectionWordLimit = 36;
        private static readonly TimeSpan DeltaDelay = TimeSpan.FromMilliseconds(120);
        private const int MaxChunkSize = 28;

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

        public async IAsyncEnumerable<AiStreamEvent> StreamTextAsync(
            Guid sectionId,
            TextRange selectionRange,
            string originalText,
            string instruction,
            AiActionScope scope,
            [EnumeratorCancellation] CancellationToken ct)
        {
            AiTextProposal proposal = ProposeText(sectionId, selectionRange, originalText, instruction, scope);

            yield return new AiStreamEvent.Started();

            foreach (string chunk in ChunkText(proposal.ProposedText, MaxChunkSize))
            {
                ct.ThrowIfCancellationRequested();
                yield return new AiStreamEvent.TextDelta(chunk);
                await Task.Delay(DeltaDelay, ct);
            }

            yield return new AiStreamEvent.Completed();
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

        private static IEnumerable<string> ChunkText(string text, int maxChunkSize)
        {
            if (string.IsNullOrEmpty(text))
            {
                yield break;
            }

            int index = 0;
            while (index < text.Length)
            {
                int length = Math.Min(maxChunkSize, text.Length - index);
                int nextIndex = index + length;

                if (nextIndex < text.Length && !char.IsWhiteSpace(text[nextIndex - 1]))
                {
                    int lastSpace = text.LastIndexOf(' ', nextIndex - 1, length);
                    if (lastSpace > index)
                    {
                        nextIndex = lastSpace + 1;
                        length = nextIndex - index;
                    }
                }

                yield return text.Substring(index, length);
                index += length;
            }
        }
    }
}
