using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;

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

            if (string.Equals(request.ActionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                string json = BuildMockOutlineJson(request);
                AiArtifact outlineArtifact = new(
                    Guid.NewGuid(),
                    AiModality.Text,
                    "application/json",
                    json,
                    null,
                    null);
                AiResult outlineResult = new(
                    request.RequestId,
                    new List<AiArtifact> { outlineArtifact },
                    new AiUsage(0, 0, TimeSpan.Zero),
                    new Dictionary<string, object>
                    {
                        ["provider"] = ProviderId,
                        ["model"] = "mock-text"
                    });
                return Task.FromResult(outlineResult);
            }

            if (string.Equals(request.ActionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(request.ActionId, SceneRefineAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(request.ActionId, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                string json = BuildMockSceneCardJson(request);
                AiArtifact sceneArtifact = new(
                    Guid.NewGuid(),
                    AiModality.Text,
                    "application/json",
                    json,
                    null,
                    null);

                AiResult sceneResult = new(
                    request.RequestId,
                    new List<AiArtifact> { sceneArtifact },
                    new AiUsage(0, 0, TimeSpan.Zero),
                    new Dictionary<string, object>
                    {
                        ["provider"] = ProviderId,
                        ["model"] = "mock-text"
                    });

                return Task.FromResult(sceneResult);
            }

            if (string.Equals(request.ActionId, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal))
            {
                string text = BuildMockNextParagraph(request);
                AiArtifact paragraphArtifact = new(
                    Guid.NewGuid(),
                    AiModality.Text,
                    "text/plain",
                    text,
                    null,
                    null);

                AiResult paragraphResult = new(
                    request.RequestId,
                    new List<AiArtifact> { paragraphArtifact },
                    new AiUsage(0, 0, TimeSpan.Zero),
                    new Dictionary<string, object>
                    {
                        ["provider"] = ProviderId,
                        ["model"] = "mock-text"
                    });

                return Task.FromResult(paragraphResult);
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

            if (string.Equals(request.ActionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                string json = BuildMockOutlineJson(request);
                yield return new AiStreamEvent.Started();
                foreach (string chunk in ChunkText(json, MaxChunkSize))
                {
                    ct.ThrowIfCancellationRequested();
                    yield return new AiStreamEvent.TextDelta(chunk);
                    await Task.Delay(DeltaDelay, ct);
                }
                yield return new AiStreamEvent.Completed();
                yield break;
            }

            if (string.Equals(request.ActionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(request.ActionId, SceneRefineAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(request.ActionId, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                string json = BuildMockSceneCardJson(request);
                yield return new AiStreamEvent.Started();
                foreach (string chunk in ChunkText(json, MaxChunkSize))
                {
                    ct.ThrowIfCancellationRequested();
                    yield return new AiStreamEvent.TextDelta(chunk);
                    await Task.Delay(DeltaDelay, ct);
                }
                yield return new AiStreamEvent.Completed();
                yield break;
            }

            if (string.Equals(request.ActionId, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal))
            {
                string text = BuildMockNextParagraph(request);
                yield return new AiStreamEvent.Started();
                foreach (string chunk in ChunkText(text, MaxChunkSize))
                {
                    ct.ThrowIfCancellationRequested();
                    yield return new AiStreamEvent.TextDelta(chunk);
                    await Task.Delay(DeltaDelay, ct);
                }
                yield return new AiStreamEvent.Completed();
                yield break;
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

        private static string BuildMockOutlineJson(AiRequest request)
        {
            List<string> titles = new();
            if (request.Inputs is not null && request.Inputs.TryGetValue("section_titles", out object? value))
            {
                if (value is IEnumerable<string> stringList)
                {
                    titles.AddRange(stringList);
                }
                else if (value is IEnumerable<object> objectList)
                {
                    foreach (object? item in objectList)
                    {
                        if (item is not null)
                        {
                            titles.Add(item.ToString() ?? string.Empty);
                        }
                    }
                }
            }

            if (titles.Count == 0)
            {
                titles.Add("Outline node");
            }

            List<Dictionary<string, object?>> nodes = new();
            for (int index = 0; index < titles.Count; index++)
            {
                nodes.Add(new Dictionary<string, object?>
                {
                    ["id"] = Guid.NewGuid(),
                    ["documentId"] = request.Context.DocumentId,
                    ["parentId"] = null,
                    ["order"] = index,
                    ["title"] = string.IsNullOrWhiteSpace(titles[index]) ? $"Node {index + 1}" : titles[index],
                    ["notes"] = null,
                    ["linkedSectionId"] = null
                });
            }

            return JsonSerializer.Serialize(nodes, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        private static string BuildMockSceneCardJson(AiRequest request)
        {
            string mode = GetInputValue(request, "mode", "suggest");
            string sectionTitle = GetInputValue(request, "section_title", "Section");
            Dictionary<string, object?> card = new()
            {
                ["narrativePurpose"] = $"Purpose for {sectionTitle}",
                ["emotionalBeat"] = "Emotional shift goes here.",
                ["keyEvents"] = "- Key event 1\n- Key event 2",
                ["openQuestions"] = mode == "open_questions" ? "- What happens next?" : "- Open thread to resolve.",
                ["explanation"] = $"Mock scene card ({mode})."
            };

            return JsonSerializer.Serialize(card, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }

        private static string BuildMockNextParagraph(AiRequest request)
        {
            string purpose = GetInputValue(request, "narrative_purpose", "advance the scene");
            string beat = GetInputValue(request, "emotional_beat", "steady tension");
            string events = GetInputValue(request, "key_events", "a pivotal moment");
            string questions = GetInputValue(request, "open_questions", "an unresolved thread");

            return string.Join(" ", new[]
            {
                "The story leans forward without breaking its rhythm.",
                $"It keeps its focus on {purpose}.",
                $"The mood holds to {beat} even as details sharpen.",
                $"A new beat hints at {events} without spelling it out.",
                "The point of view stays anchored and grounded in the moment.",
                "Sensory detail threads through the action to keep the scene tangible.",
                "Small decisions stack into a larger shift the reader can feel.",
                "The pacing stays natural, neither rushed nor stalled.",
                $"Subtext keeps {questions} alive in the background.",
                "The paragraph closes on a forward motion that invites the next line."
            });
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
