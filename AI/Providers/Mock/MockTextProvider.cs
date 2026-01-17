using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;

namespace WriterApp.AI.Providers.Mock
{
    public sealed class MockTextProvider : IAiStreamingProvider, IAiBillingProvider
    {
        private static readonly TimeSpan DeltaDelay = TimeSpan.FromMilliseconds(120);
        private const int MaxChunkSize = 28;

        public string ProviderId => "mock-text";

        public AiProviderCapabilities Capabilities => new(true, false);

        public AiStreamingCapabilities StreamingCapabilities => new(true, false);

        public bool RequiresEntitlement => false;

        public bool IsBillable => false;

        public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string instruction = GetInstruction(request);
            string original = request.Context.SelectionText ?? request.Context.OriginalText ?? string.Empty;
            string tone = GetInputValue(request, "tone", "Neutral");
            string length = GetInputValue(request, "length", "Same");
            bool preserveTerms = GetInputValue(request, "preserve_terms", true);
            string proposed = BuildProposalText(instruction, original, tone, length, preserveTerms);

            AiArtifact artifact = new(
                Guid.NewGuid(),
                AiModality.Text,
                "text/plain",
                proposed,
                null,
                null);

            AiUsage usage = new(0, 0, TimeSpan.Zero);
            AiResult result = new(
                request.RequestId,
                new List<AiArtifact> { artifact },
                usage,
                new Dictionary<string, object>
                {
                    ["provider"] = ProviderId,
                    ["model"] = "mock-text"
                });

            return Task.FromResult(result);
        }

        public async IAsyncEnumerable<AiStreamEvent> StreamAsync(
            AiRequest request,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string instruction = GetInstruction(request);
            string original = request.Context.SelectionText ?? request.Context.OriginalText ?? string.Empty;
            string tone = GetInputValue(request, "tone", "Neutral");
            string length = GetInputValue(request, "length", "Same");
            bool preserveTerms = GetInputValue(request, "preserve_terms", true);
            string proposed = BuildProposalText(instruction, original, tone, length, preserveTerms);

            yield return new AiStreamEvent.Started();

            foreach (string chunk in ChunkText(proposed, MaxChunkSize))
            {
                ct.ThrowIfCancellationRequested();
                yield return new AiStreamEvent.TextDelta(chunk);
                await Task.Delay(DeltaDelay, ct);
            }

            yield return new AiStreamEvent.Completed();
        }

        private static string GetInstruction(AiRequest request)
        {
            if (request.Inputs is null || !request.Inputs.TryGetValue("instruction", out object? value))
            {
                return string.Empty;
            }

            return value?.ToString() ?? string.Empty;
        }

        private static string BuildProposalText(string instruction, string original, string tone, string length, bool preserveTerms)
        {
            string trimmed = original ?? string.Empty;
            string instructionKey = instruction?.Trim().ToLowerInvariant() ?? string.Empty;
            string header = $"[AI rewrite:{tone}:{length}:{(preserveTerms ? "Preserve" : "Flex")}] ";

            if (instructionKey.Contains("shorten", StringComparison.Ordinal))
            {
                return $"{header}{TrimToWords(trimmed, 12)} [AI shortened]";
            }

            if (instructionKey.Contains("fix grammar", StringComparison.Ordinal) || instructionKey.Contains("grammar", StringComparison.Ordinal))
            {
                return $"{header}[AI grammar fix] {trimmed}";
            }

            if (instructionKey.Contains("tone", StringComparison.Ordinal))
            {
                return $"{header}[AI tone shift] {trimmed}";
            }

            if (instructionKey.Contains("summarize", StringComparison.Ordinal) || instructionKey.Contains("summary", StringComparison.Ordinal))
            {
                return $"{header}[AI summary] {TrimToWords(trimmed, 20)}";
            }

            if (string.Equals(length, "Shorter", StringComparison.OrdinalIgnoreCase))
            {
                return $"{header}{TrimToWords(trimmed, 12)}";
            }

            if (string.Equals(length, "Longer", StringComparison.OrdinalIgnoreCase))
            {
                return $"{header}{trimmed} [AI expanded]";
            }

            return $"{header}{trimmed}";
        }

        private static string TrimToWords(string text, int maxWords)
        {
            if (string.IsNullOrWhiteSpace(text) || maxWords <= 0)
            {
                return string.Empty;
            }

            string[] words = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length <= maxWords)
            {
                return string.Join(" ", words);
            }

            return string.Join(" ", words.Take(maxWords));
        }

        private static string GetInputValue(AiRequest request, string key, string defaultValue)
        {
            if (request.Inputs is null || !request.Inputs.TryGetValue(key, out object? value) || value is null)
            {
                return defaultValue;
            }

            return value.ToString() ?? defaultValue;
        }

        private static bool GetInputValue(AiRequest request, string key, bool defaultValue)
        {
            if (request.Inputs is null || !request.Inputs.TryGetValue(key, out object? value) || value is null)
            {
                return defaultValue;
            }

            if (value is bool boolValue)
            {
                return boolValue;
            }

            return bool.TryParse(value.ToString(), out bool parsed) ? parsed : defaultValue;
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
