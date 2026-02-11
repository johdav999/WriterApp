using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;

namespace WriterApp.AI.Providers.OpenAI
{
    public sealed class OpenAiProvider : IAiStreamingProvider, IAiBillingProvider, IAiImageProvider
    {
        private const string ProviderIdValue = "openai";
        private const string DefaultBaseUrl = "https://api.openai.com/v1/";
        private const string ResponsesEndpoint = "responses";
        private const string ActionRewrite = "rewrite.selection";
        private const string ActionTranslateSelection = "translate.selection";
        private const string ActionTranslateSection = "translate.section";
        private const string ActionTranslateDocument = "translate.document";
        private const string ActionCoverImage = "generate.image.cover";
        private const string ActionStoryCoach = "synopsis.story_coach";
        private const string ActionSynopsisEvaluate = "synopsis.evaluate";
        private const string ActionSynopsisQuestions = "synopsis.questions";
        private const string ActionGenerateOutline = "generate.outline";
        private const string ActionGenerateSynopsisOutline = "synopsis.generate_outline";
        private const string ActionExtractCharacterBible = "continuity.extract_character_bible";
        private const string ActionExtractPlaceBible = "continuity.extract_place_bible";
        private const string ActionExtractTimelineBible = "continuity.extract_timeline_bible";
        private const string ActionRefreshCharacterBible = "continuity.refresh_character_bible";
        private const string ActionRefreshPlaceBible = "continuity.refresh_place_bible";
        private const string ActionRefreshTimelineBible = "continuity.refresh_timeline_bible";
        private const string ActionContinuityCheckSection = "continuity.check_section";
        private const string ActionSceneSuggest = "scene.suggest";
        private const string ActionSceneRefine = "scene.refine";
        private const string ActionSceneFindOpenQuestions = "scene.find-open-questions";
        private const string ActionProposeNextParagraph = "propose.next-paragraph";
        private const string ActionTightenSelection = "tighten.selection";
        private const string ActionTightenSection = "tighten.section";
        private const string ActionExpandSelection = "expand.selection";
        private const string ActionExpandSection = "expand.section";
        private const string ActionChangeToneSelection = "change_tone.selection";
        private const string ActionChangeToneSection = "change_tone.section";
        private const string ActionShowDontTellSelection = "show_dont_tell.selection";
        private const string ActionShowDontTellSection = "show_dont_tell.section";
        private const string ActionCustomTransform = "custom_transform";
        private const int ImageTokenCost = 1000;

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly WriterAiOpenAiOptions _options;
        private readonly ILogger<OpenAiProvider> _logger;
        private readonly string _apiKey;

        public OpenAiProvider(
            IHttpClientFactory httpClientFactory,
            IOptions<WriterAiOptions> options,
            OpenAiKeyProvider keyProvider,
            ILogger<OpenAiProvider> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (options?.Value is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = options.Value.Providers.OpenAI ?? new WriterAiOpenAiOptions();
            _apiKey = keyProvider?.ApiKey ?? string.Empty;
        }

        public string ProviderId => ProviderIdValue;

        public AiProviderCapabilities Capabilities => new(true, true);

        public AiStreamingCapabilities StreamingCapabilities => new(true, false);

        public bool RequiresEntitlement => true;

        public bool IsBillable => true;

        public async Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string apiKey = _apiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI API key is not configured.");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (string.Equals(request.ActionId, ActionRewrite, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionTranslateSelection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionTranslateSection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionTranslateDocument, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionTightenSelection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionTightenSection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionExpandSelection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionExpandSection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionChangeToneSelection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionChangeToneSection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionShowDontTellSelection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionShowDontTellSection, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionCustomTransform, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteTextAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "text/plain",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionCoverImage, StringComparison.Ordinal))
                {
                    AiImageResult imageResult = await GenerateImageAsync(request, ct);
                    AiArtifact artifact = BuildImageArtifact(imageResult);
                    LogUsage(request, _options.ImageModel, artifact.BinaryContent?.Length ?? 0, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        new AiUsage(0, ImageTokenCost, stopwatch.Elapsed),
                        imageResult.ProviderMetadata);
                }

                if (string.Equals(request.ActionId, ActionStoryCoach, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteStoryCoachAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "text/plain",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionSynopsisEvaluate, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteSynopsisEvaluateAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "text/plain",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionSynopsisQuestions, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteSynopsisQuestionsAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "text/plain",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionGenerateOutline, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteOutlineAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "text/plain",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionGenerateSynopsisOutline, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteSynopsisOutlineAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "application/json",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionExtractCharacterBible, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteCharacterBibleAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "application/json",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionExtractPlaceBible, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecutePlaceBibleAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "application/json",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionContinuityCheckSection, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteContinuityCheckAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "application/json",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionExtractTimelineBible, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteTimelineBibleAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "application/json",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);
                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionRefreshCharacterBible, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionRefreshPlaceBible, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionRefreshTimelineBible, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteBibleRefreshAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "application/json",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);
                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionSceneSuggest, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionSceneRefine, StringComparison.Ordinal)
                    || string.Equals(request.ActionId, ActionSceneFindOpenQuestions, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteSceneCardAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "text/plain",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                if (string.Equals(request.ActionId, ActionProposeNextParagraph, StringComparison.Ordinal))
                {
                    (string outputText, int inputTokens, int outputTokens) = await ExecuteNextParagraphAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "text/plain",
                        outputText,
                        null,
                        null);

                    AiUsage usage = new(inputTokens, outputTokens, stopwatch.Elapsed);
                    LogUsage(request, _options.TextModel, outputTokens, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        usage,
                        new Dictionary<string, object>
                        {
                            ["provider"] = ProviderIdValue,
                            ["model"] = _options.TextModel
                        });
                }

                throw new AiProviderException(ProviderIdValue, $"OpenAI provider does not support action '{request.ActionId}'.");
            }
            catch (OperationCanceledException ex)
            {
                Debug.WriteLine("OpenAI request was canceled." + ex);
                if (ex is TaskCanceledException && !ct.IsCancellationRequested)
                {
                    throw new AiProviderException(ProviderIdValue, "OpenAI request timed out.", ex);
                }

                throw;
            }
            catch (AiProviderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI request failed.", ex);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public async IAsyncEnumerable<AiStreamEvent> StreamAsync(
            AiRequest request,
            [EnumeratorCancellation] CancellationToken ct)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!string.Equals(request.ActionId, ActionRewrite, StringComparison.Ordinal)
                && !string.Equals(request.ActionId, ActionTightenSelection, StringComparison.Ordinal)
                && !string.Equals(request.ActionId, ActionTightenSection, StringComparison.Ordinal)
                && !string.Equals(request.ActionId, ActionExpandSelection, StringComparison.Ordinal)
                && !string.Equals(request.ActionId, ActionExpandSection, StringComparison.Ordinal)
                && !string.Equals(request.ActionId, ActionChangeToneSelection, StringComparison.Ordinal)
                && !string.Equals(request.ActionId, ActionChangeToneSection, StringComparison.Ordinal)
                && !string.Equals(request.ActionId, ActionShowDontTellSelection, StringComparison.Ordinal)
                && !string.Equals(request.ActionId, ActionShowDontTellSection, StringComparison.Ordinal))
            {
                yield return new AiStreamEvent.Started();
                yield return new AiStreamEvent.Failed($"OpenAI streaming is not available for action '{request.ActionId}'.");
                yield break;
            }

            string apiKey = _apiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                yield return new AiStreamEvent.Started();
                yield return new AiStreamEvent.Failed("OpenAI API key is not configured.");
                yield break;
            }

            yield return new AiStreamEvent.Started();

            HttpRequestMessage requestMessage = BuildResponsesRequest(
                request,
                apiKey,
                stream: true);

            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            Stopwatch stopwatch = Stopwatch.StartNew();
            string? accumulated = null;

            try
            {
                using HttpResponseMessage response = await client.SendAsync(
                    requestMessage,
                    HttpCompletionOption.ResponseHeadersRead,
                    ct);

                await EnsureSuccessAsync(response, ct);

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var reader = new System.IO.StreamReader(stream, Encoding.UTF8);

                while (!reader.EndOfStream)
                {
                    ct.ThrowIfCancellationRequested();
                    string? line = await reader.ReadLineAsync();
                    if (line is null)
                    {
                        break;
                    }

                    if (!line.StartsWith("data:", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string payload = line.Substring(5).Trim();

                    if (string.Equals(payload, "[DONE]", StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (TryGetTextDelta(payload, out string delta))
                    {
                        accumulated = accumulated is null ? delta : accumulated + delta;
                        yield return new AiStreamEvent.TextDelta(delta);
                    }
                }

                yield return new AiStreamEvent.Completed();
                LogUsage(request, _options.TextModel, accumulated?.Length ?? 0, stopwatch.Elapsed);
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteTextAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildResponsesRequest(request, apiKey, stream: false);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteStoryCoachAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildStoryCoachRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteSynopsisEvaluateAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildSynopsisEvaluateRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteSynopsisQuestionsAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildSynopsisQuestionsRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteOutlineAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildOutlineRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteSynopsisOutlineAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildSynopsisOutlineRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteCharacterBibleAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildCharacterBibleRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecutePlaceBibleAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildPlaceBibleRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteContinuityCheckAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildContinuityCheckRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteTimelineBibleAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildTimelineBibleRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteBibleRefreshAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildBibleRefreshRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteSceneCardAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildSceneCardRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        private async Task<(string OutputText, int InputTokens, int OutputTokens)> ExecuteNextParagraphAsync(
            AiRequest request,
            string apiKey,
            CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildNextParagraphRequest(request, apiKey);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseTextAndUsage(json);
        }

        public async Task<AiImageResult> GenerateImageAsync(AiRequest request, CancellationToken ct)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string apiKey = _apiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI API key is not configured.");
            }

            HttpRequestMessage requestMessage = BuildResponsesImageRequest(request, apiKey);

            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));
            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            AiImagePayload payload = ExtractResponseImage(json);

            return new AiImageResult(
                payload.Bytes,
                payload.ContentType,
                new Dictionary<string, object>
                {
                    ["provider"] = ProviderIdValue,
                    ["model"] = _options.ImageModel,
                    ["requestId"] = payload.RequestId ?? request.RequestId.ToString()
                });
        }

        private HttpRequestMessage BuildResponsesRequest(AiRequest request, string apiKey, bool stream)
        {
            string selection = request.Context.SelectionText ?? request.Context.OriginalText ?? string.Empty;
            string instruction = GetInputValue(request, "instruction", string.Empty);
            string tone = GetInputValue(request, "tone", "Neutral");
            string length = GetInputValue(request, "length", "Same");
            bool preserveTerms = GetInputValue(request, "preserve_terms", true);

            string systemPrompt = BuildSystemPrompt(request.Context.LanguageHint);
            string userPrompt = BuildUserPrompt(selection, instruction, tone, length, preserveTerms, request.Context);

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = userPrompt
                            }
                        }
                    }
                },
                ["max_output_tokens"] = _options.MaxOutputTokens
            };

            if (stream)
            {
                payload["stream"] = true;
            }

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            if (stream)
            {
                requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            }

            return requestMessage;
        }

        private HttpRequestMessage BuildOutlineRequest(AiRequest request, string apiKey)
        {
            string instruction = GetInputValue(request, "instruction", "Generate an outline.");
            string documentTitle = GetInputValue(request, "document_title", string.Empty);
            string sections = GetInputValue(request, "sections", string.Empty);
            bool truncated = GetInputValue(request, "truncated", false);

            string systemPrompt = "You are an outlining assistant. Return JSON only.";
            StringBuilder userPrompt = new();
            userPrompt.AppendLine("Create a hierarchical outline as a JSON object with a top-level \"nodes\" array.");
            userPrompt.AppendLine("Schema: {\"nodes\":[{\"id\":\"<guid or empty>\",\"parentId\":null|\"<guid>\",\"order\":0,\"title\":\"...\",\"notes\":null,\"linkedSectionId\":null}]}");
            userPrompt.AppendLine("Rules: Use parentId to nest children, order starts at 0 per parent, keep titles concise.");
            if (!string.IsNullOrWhiteSpace(documentTitle))
            {
                userPrompt.AppendLine($"Document title: {documentTitle}");
            }
            if (truncated)
            {
                userPrompt.AppendLine("Note: Some section content was truncated.");
            }
            userPrompt.AppendLine("Sections:");
            userPrompt.AppendLine(sections);
            userPrompt.AppendLine("Return JSON only, no prose.");

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = $"{instruction}\n\n{userPrompt}"
                            }
                        }
                    }
                },
                ["max_output_tokens"] = _options.MaxOutputTokens,
                ["text"] = new Dictionary<string, object>
                {
                    ["format"] = new Dictionary<string, object>
                    {
                        ["type"] = "json_object"
                    }
                }
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private HttpRequestMessage BuildStoryCoachRequest(AiRequest request, string apiKey)
        {
            string fieldKey = GetInputValue(request, "focus_field_key", "field");
            string focusPrompt = GetInputValue(request, "focus_field_prompt", string.Empty);
            string otherContext = GetInputValue(request, "other_fields_context", string.Empty);
            string existing = GetInputValue(request, "existing_value", string.Empty);
            string notes = GetInputValue(request, "user_notes", string.Empty);

            string systemPrompt = StoryCoachPromptBuilder.BuildSystemPrompt();
            string prompt = StoryCoachPromptBuilder.BuildUserPrompt(otherContext, fieldKey, focusPrompt, existing, notes);

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = prompt
                            }
                        }
                    }
                },
                ["max_output_tokens"] = _options.MaxOutputTokens
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private HttpRequestMessage BuildSynopsisOutlineRequest(AiRequest request, string apiKey)
        {
            string instruction = GetInputValue(request, "instruction", "Generate a structured outline.");
            string documentTitle = GetInputValue(request, "document_title", string.Empty);
            string synopsisContext = GetInputValue(request, "synopsis_context", string.Empty);
            string mode = GetInputValue(request, "mode", "chapters");
            string desiredCount = GetInputValue(request, "desired_count", "12");

            string systemPrompt =
                "You are a story architect. Return strict JSON only with no markdown fences and no commentary.";

            StringBuilder userPrompt = new();
            userPrompt.AppendLine("Produce an outline draft from the synopsis context.");
            userPrompt.AppendLine("Schema:");
            userPrompt.AppendLine("{\"schemaVersion\":\"1.0\",\"mode\":\"chapters|scenes\",\"items\":[{\"index\":1,\"title\":\"string\",\"summary\":\"string\",\"pov\":\"string\",\"setting\":\"string\",\"beats\":[\"string\"],\"storyRole\":\"string\",\"notes\":\"string\"}]}");
            userPrompt.AppendLine("Rules:");
            userPrompt.AppendLine("- JSON only.");
            userPrompt.AppendLine("- Keep chronology coherent.");
            userPrompt.AppendLine("- Keep names, facts, POV and language consistent with synopsis.");
            userPrompt.AppendLine("- Preserve story intent and stakes.");
            userPrompt.AppendLine($"- Mode: {mode}");
            userPrompt.AppendLine($"- Desired item count: {desiredCount}");
            if (!string.IsNullOrWhiteSpace(documentTitle))
            {
                userPrompt.AppendLine($"Document title: {documentTitle}");
            }

            userPrompt.AppendLine("Synopsis:");
            userPrompt.AppendLine(string.IsNullOrWhiteSpace(synopsisContext) ? "(empty synopsis)" : synopsisContext);

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = $"{instruction}\n\n{userPrompt}"
                            }
                        }
                    }
                },
                ["max_output_tokens"] = _options.MaxOutputTokens,
                ["text"] = new Dictionary<string, object>
                {
                    ["format"] = new Dictionary<string, object>
                    {
                        ["type"] = "json_object"
                    }
                }
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private HttpRequestMessage BuildCharacterBibleRequest(AiRequest request, string apiKey)
        {
            string context = GetInputValue(request, "context", string.Empty);
            string instruction = GetInputValue(request, "instruction", "Extract character bible.");
            string systemPrompt = "You are a continuity analyst. Return strict JSON only.";
            string userPrompt =
                $"{instruction}\n\nReturn JSON schema: {{\"schemaVersion\":\"1.0\",\"characters\":[{{\"name\":\"...\",\"facts\":[{{\"fact\":\"...\",\"evidence\":{{\"sectionId\":\"<guid>\",\"quote\":\"...\"}}}}],\"traits\":[\"...\"]}}]}}\n\nContext:\n{context}";

            return BuildStrictJsonRequest(systemPrompt, userPrompt, apiKey);
        }

        private HttpRequestMessage BuildPlaceBibleRequest(AiRequest request, string apiKey)
        {
            string context = GetInputValue(request, "context", string.Empty);
            string instruction = GetInputValue(request, "instruction", "Extract place bible.");
            string systemPrompt = "You are a continuity analyst. Return strict JSON only.";
            string userPrompt =
                $"{instruction}\n\nReturn JSON schema: {{\"schemaVersion\":\"1.0\",\"places\":[{{\"name\":\"...\",\"facts\":[{{\"fact\":\"...\",\"evidence\":{{\"sectionId\":\"<guid>\",\"quote\":\"...\"}}}}]}}]}}\n\nContext:\n{context}";

            return BuildStrictJsonRequest(systemPrompt, userPrompt, apiKey);
        }

        private HttpRequestMessage BuildContinuityCheckRequest(AiRequest request, string apiKey)
        {
            string sectionText = GetInputValue(request, "section_text", string.Empty);
            string characterBible = GetInputValue(request, "character_bible_json", "{}");
            string placeBible = GetInputValue(request, "place_bible_json", "{}");
            string timelineBible = GetInputValue(request, "timeline_bible_json", "{}");
            string instruction = GetInputValue(request, "instruction", "Check continuity issues.");
            string sectionId = request.Context.SectionId.ToString();

            string systemPrompt = "You are a manuscript continuity coach. Return strict JSON only.";
            string userPrompt =
                $"{instruction}\n\nReturn JSON schema: {{\"schemaVersion\":\"1.0\",\"issues\":[{{\"severity\":\"low|medium|high|critical\",\"type\":\"character|place|timeline\",\"message\":\"...\",\"evidence\":{{\"sectionId\":\"{sectionId}\",\"quote\":\"...\"}},\"suggestedFix\":\"...\",\"anchor\":{{\"plainTextStart\":0,\"plainTextLength\":10}}}}]}}\n\nRules:\n- Return strict JSON only.\n- Include only the top 25 highest-impact issues.\n- Keep message/suggestedFix concise (1-2 sentences).\n- Do not include explanations outside JSON.\n\nCharacter bible:\n{characterBible}\n\nPlace bible:\n{placeBible}\n\nTimeline bible:\n{timelineBible}\n\nSection text:\n{sectionText}";

            int continuityMaxOutputTokens = Math.Max(_options.MaxOutputTokens, 1800);
            return BuildStrictJsonRequest(systemPrompt, userPrompt, apiKey, continuityMaxOutputTokens);
        }

        private HttpRequestMessage BuildTimelineBibleRequest(AiRequest request, string apiKey)
        {
            string context = GetInputValue(request, "context", string.Empty);
            string instruction = GetInputValue(request, "instruction", "Extract timeline bible.");
            string systemPrompt = "You are a continuity analyst. Return strict JSON only.";
            string userPrompt =
                $"{instruction}\n\nReturn JSON schema: {{\"schemaVersion\":\"1.0\",\"events\":[{{\"id\":\"evt_...\",\"title\":\"...\",\"timeRef\":\"...\",\"order\":1,\"locationId\":\"\",\"participants\":[\"chr_...\"],\"summary\":\"...\",\"evidence\":[{{\"sectionId\":\"<guid>\",\"quote\":\"...\"}}],\"constraints\":[\"...\"],\"lastUpdatedUtc\":\"...\"}}]}}\n\nContext:\n{context}";

            return BuildStrictJsonRequest(systemPrompt, userPrompt, apiKey);
        }

        private HttpRequestMessage BuildBibleRefreshRequest(AiRequest request, string apiKey)
        {
            string existingBibleJson = GetInputValue(request, "existing_bible_json", "{}");
            string deltaSectionsJson = GetInputValue(request, "delta_sections_json", "[]");
            string instruction = GetInputValue(request, "instruction", "Update bible incrementally.");
            string outputContract = GetInputValue(
                request,
                "output_contract",
                "Return strict JSON patch: {\"bibleType\":\"Character|Place|Timeline\",\"schemaVersion\":1,\"ops\":[],\"stats\":{}}");

            string systemPrompt = "You are a continuity analyst. Return strict JSON patch only.";
            string userPrompt =
                $"{instruction}\n\n{outputContract}\n\nRules:\n- Output valid JSON only.\n- Use ops list with deterministic updates.\n- Preserve existing IDs.\n- Add flagReview when evidence is conflicting or missing.\n\nExisting bible JSON:\n{existingBibleJson}\n\nChanged/new section deltas:\n{deltaSectionsJson}";

            int maxTokens = Math.Max(_options.MaxOutputTokens, 1800);
            return BuildStrictJsonRequest(systemPrompt, userPrompt, apiKey, maxTokens);
        }

        private HttpRequestMessage BuildStrictJsonRequest(
            string systemPrompt,
            string userPrompt,
            string apiKey,
            int? maxOutputTokens = null)
        {
            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = userPrompt
                            }
                        }
                    }
                },
                ["max_output_tokens"] = maxOutputTokens ?? _options.MaxOutputTokens,
                ["text"] = new Dictionary<string, object>
                {
                    ["format"] = new Dictionary<string, object>
                    {
                        ["type"] = "json_object"
                    }
                }
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private HttpRequestMessage BuildSynopsisEvaluateRequest(AiRequest request, string apiKey)
        {
            string synopsisContext = GetInputValue(request, "synopsis_context", string.Empty);
            string userNotes = GetInputValue(request, "user_notes", string.Empty);
            string language = string.IsNullOrWhiteSpace(request.Context.LanguageHint) ? "en" : request.Context.LanguageHint;

            string systemPrompt = SynopsisEvaluatePromptBuilder.BuildSystemPrompt(language);
            string prompt = SynopsisEvaluatePromptBuilder.BuildUserPrompt(synopsisContext, userNotes);

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = prompt
                            }
                        }
                    }
                },
                ["max_output_tokens"] = _options.MaxOutputTokens
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private HttpRequestMessage BuildSynopsisQuestionsRequest(AiRequest request, string apiKey)
        {
            string synopsisContext = GetInputValue(request, "synopsis_context", string.Empty);
            string userNotes = GetInputValue(request, "user_notes", string.Empty);
            string language = string.IsNullOrWhiteSpace(request.Context.LanguageHint) ? "en" : request.Context.LanguageHint;

            string systemPrompt = SynopsisQuestionsPromptBuilder.BuildSystemPrompt(language);
            string prompt = SynopsisQuestionsPromptBuilder.BuildUserPrompt(synopsisContext, userNotes);

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = prompt
                            }
                        }
                    }
                },
                ["max_output_tokens"] = _options.MaxOutputTokens
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private HttpRequestMessage BuildSceneCardRequest(AiRequest request, string apiKey)
        {
            string instruction = GetInputValue(request, "instruction", "Suggest scene card fields.");
            string mode = GetInputValue(request, "mode", "suggest");
            string sectionTitle = GetInputValue(request, "section_title", string.Empty);
            string sectionText = GetInputValue(request, "section_text", string.Empty);
            string narrativePurpose = GetInputValue(request, "narrative_purpose", string.Empty);
            string emotionalBeat = GetInputValue(request, "emotional_beat", string.Empty);
            string keyEvents = GetInputValue(request, "key_events", string.Empty);
            string openQuestions = GetInputValue(request, "open_questions", string.Empty);
            string povCharacterId = GetInputValue(request, "pov_character_id", string.Empty);
            string placeId = GetInputValue(request, "place_id", string.Empty);
            string timelineEventId = GetInputValue(request, "timeline_event_id", string.Empty);
            string timeRef = GetInputValue(request, "time_ref", string.Empty);
            string tagsJson = GetInputValue(request, "tags_json", "[]");
            string referencesJson = GetInputValue(request, "references_json", "[]");

            string systemPrompt = "You are a story editor. Return JSON only.";
            StringBuilder userPrompt = new();
            userPrompt.AppendLine("Produce a JSON object with keys:");
            userPrompt.AppendLine("narrativePurpose, emotionalBeat, keyEvents, openQuestions, povCharacterId, placeId, timeRef, tags, explanation.");
            userPrompt.AppendLine("Use concise, readable sentences. Keep user intent.");
            userPrompt.AppendLine("Return tags as a JSON array of short strings.");
            userPrompt.AppendLine($"Mode: {mode}");
            if (!string.IsNullOrWhiteSpace(sectionTitle))
            {
                userPrompt.AppendLine($"Section title: {sectionTitle}");
            }
            if (!string.IsNullOrWhiteSpace(sectionText))
            {
                userPrompt.AppendLine("Section text:");
                userPrompt.AppendLine(sectionText);
            }
            userPrompt.AppendLine("Existing scene card fields:");
            userPrompt.AppendLine($"Narrative purpose: {narrativePurpose}");
            userPrompt.AppendLine($"Emotional beat: {emotionalBeat}");
            userPrompt.AppendLine($"Key events: {keyEvents}");
            userPrompt.AppendLine($"Open questions: {openQuestions}");
            userPrompt.AppendLine($"POV character: {povCharacterId}");
            userPrompt.AppendLine($"Setting/place: {placeId}");
            userPrompt.AppendLine($"Timeline event id: {timelineEventId}");
            userPrompt.AppendLine($"Timeline marker: {timeRef}");
            userPrompt.AppendLine($"Tags JSON: {tagsJson}");
            userPrompt.AppendLine($"References JSON: {referencesJson}");
            userPrompt.AppendLine("Return JSON only, no prose outside the JSON.");

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = $"{instruction}\n\n{userPrompt}"
                            }
                        }
                    }
                },
                ["max_output_tokens"] = _options.MaxOutputTokens,
                ["text"] = new Dictionary<string, object>
                {
                    ["format"] = new Dictionary<string, object>
                    {
                        ["type"] = "json_object"
                    }
                }
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private HttpRequestMessage BuildNextParagraphRequest(AiRequest request, string apiKey)
        {
            string instruction = GetInputValue(request, "instruction", "Propose the next paragraph for the current section.");
            string sectionTitle = GetInputValue(request, "section_title", string.Empty);
            string recentContext = GetInputValue(request, "recent_context", string.Empty);
            string narrativePurpose = GetInputValue(request, "narrative_purpose", string.Empty);
            string emotionalBeat = GetInputValue(request, "emotional_beat", string.Empty);
            string keyEvents = GetInputValue(request, "key_events", string.Empty);
            string openQuestions = GetInputValue(request, "open_questions", string.Empty);
            string language = string.IsNullOrWhiteSpace(request.Context.LanguageHint) ? "en" : request.Context.LanguageHint;

            string systemPrompt = $"You are a writing assistant. Language: {language}. Output one paragraph of prose only. "
                                  + "Use at least 10 sentences. No headings, lists, or markdown. "
                                  + "Maintain the section's voice, POV, tense, and style. "
                                  + "Continue naturally from the provided context. Avoid meta language.";

            StringBuilder userPrompt = new();
            userPrompt.AppendLine("Task: Write the next paragraph of the current section.");
            userPrompt.AppendLine("Rules:");
            userPrompt.AppendLine("- Output a single paragraph only (no line breaks).");
            userPrompt.AppendLine("- Minimum 10 sentences.");
            userPrompt.AppendLine("- No headings, lists, or markdown.");
            userPrompt.AppendLine("- Stay consistent with voice, POV, tense, and style.");
            userPrompt.AppendLine("- Use the scene metadata and continue from the recent context.");
            userPrompt.AppendLine("- Avoid meta language (no references to being an AI).");

            if (!string.IsNullOrWhiteSpace(sectionTitle))
            {
                userPrompt.AppendLine($"Section title: {sectionTitle}");
            }

            userPrompt.AppendLine("Scene metadata:");
            userPrompt.AppendLine($"Narrative purpose: {narrativePurpose}");
            userPrompt.AppendLine($"Emotional beat: {emotionalBeat}");
            userPrompt.AppendLine($"Key events: {keyEvents}");
            userPrompt.AppendLine($"Open questions: {openQuestions}");

            if (!string.IsNullOrWhiteSpace(recentContext))
            {
                userPrompt.AppendLine("Recent context:");
                userPrompt.AppendLine(recentContext);
            }

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.TextModel,
                ["input"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["role"] = "system",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = systemPrompt
                            }
                        }
                    },
                    new Dictionary<string, object>
                    {
                        ["role"] = "user",
                        ["content"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["type"] = "input_text",
                                ["text"] = $"{instruction}\n\n{userPrompt}"
                            }
                        }
                    }
                },
                ["max_output_tokens"] = _options.MaxOutputTokens
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private HttpRequestMessage BuildResponsesImageRequest(AiRequest request, string apiKey)
        {
            string prompt = GetInputValue(request, "prompt", string.Empty);
            string instruction = GetInputValue(request, "instruction", string.Empty);
            string size = GetInputValue(request, "size", "1024x1024");
            string style = GetInputValue(request, "style", string.Empty);

            string combinedPrompt = string.IsNullOrWhiteSpace(instruction)
                ? prompt
                : $"{prompt}\n\nInstruction: {instruction}";

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.ImageModel,
                ["input"] = combinedPrompt,
                ["tools"] = new object[]
                {
                    new Dictionary<string, object>
                    {
                        ["type"] = "image_generation",
                        ["size"] = size,
                        ["response_format"] = "b64_json"
                    }
                }
            };

            if (!string.IsNullOrWhiteSpace(style))
            {
                payload["style"] = style;
            }

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ResponsesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);
            return requestMessage;
        }

        private void ApplyAuthHeaders(HttpRequestMessage request, string apiKey)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            if (!string.IsNullOrWhiteSpace(_options.Organization))
            {
                request.Headers.Add("OpenAI-Organization", _options.Organization);
            }
        }

        private Uri BuildUri(string path)
        {
            string baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
                ? DefaultBaseUrl
                : _options.BaseUrl!;
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            {
                baseUrl += "/";
            }

            return new Uri(new Uri(baseUrl, UriKind.Absolute), path);
        }

        private static string BuildSystemPrompt(string? languageHint)
        {
            string language = string.IsNullOrWhiteSpace(languageHint) ? "en" : languageHint;
            return $"You are a writing assistant. Rewrite only the provided selection. Return plain text only. Language: {language}.";
        }

        private static string BuildUserPrompt(
            string selection,
            string instruction,
            string tone,
            string length,
            bool preserveTerms,
            AiRequestContext context)
        {
            StringBuilder prompt = new();
            prompt.AppendLine("Rewrite the selection below. Return only the rewritten selection text.");
            prompt.AppendLine($"Tone: {tone}.");
            prompt.AppendLine($"Length: {length}.");
            prompt.AppendLine($"Preserve terms: {(preserveTerms ? "yes" : "no")}.");

            if (!string.IsNullOrWhiteSpace(instruction))
            {
                prompt.AppendLine($"Instruction: {instruction}");
            }

            if (!string.IsNullOrWhiteSpace(context.DocumentTitle))
            {
                prompt.AppendLine($"Document title: {context.DocumentTitle}");
            }

            if (!string.IsNullOrWhiteSpace(context.ContainingParagraph))
            {
                prompt.AppendLine("Context paragraph:");
                prompt.AppendLine(context.ContainingParagraph);
            }

            prompt.AppendLine("Selection:");
            prompt.AppendLine(selection);

            return prompt.ToString();
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

        private static bool TryGetTextDelta(string payload, out string delta)
        {
            delta = string.Empty;
            if (string.IsNullOrWhiteSpace(payload))
            {
                return false;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(payload);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("type", out JsonElement typeElement)
                    && typeElement.GetString() == "response.output_text.delta"
                    && root.TryGetProperty("delta", out JsonElement deltaElement))
                {
                    delta = deltaElement.GetString() ?? string.Empty;
                    return delta.Length > 0;
                }
            }
            catch (JsonException)
            {
            }

            return false;
        }

        private static (string OutputText, int InputTokens, int OutputTokens) ExtractResponseTextAndUsage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return (string.Empty, 0, 0);
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            int inputTokens = 0;
            int outputTokens = 0;
            if (root.TryGetProperty("usage", out JsonElement usageElement))
            {
                if (usageElement.TryGetProperty("input_tokens", out JsonElement inputTokensElement))
                {
                    inputTokens = inputTokensElement.GetInt32();
                }

                if (usageElement.TryGetProperty("output_tokens", out JsonElement outputTokensElement))
                {
                    outputTokens = outputTokensElement.GetInt32();
                }
            }

            if (root.TryGetProperty("output_text", out JsonElement outputTextElement)
                && outputTextElement.ValueKind == JsonValueKind.String)
            {
                return (outputTextElement.GetString() ?? string.Empty, inputTokens, outputTokens);
            }

            if (root.TryGetProperty("output", out JsonElement outputElement)
                && outputElement.ValueKind == JsonValueKind.Array)
            {
                StringBuilder builder = new();
                foreach (JsonElement item in outputElement.EnumerateArray())
                {
                    if (!item.TryGetProperty("content", out JsonElement contentElement)
                        || contentElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement part in contentElement.EnumerateArray())
                    {
                        if (part.TryGetProperty("type", out JsonElement typeElement)
                            && typeElement.GetString() == "output_text"
                            && part.TryGetProperty("text", out JsonElement textElement))
                        {
                            builder.Append(textElement.GetString());
                        }
                    }
                }

                return (builder.ToString(), inputTokens, outputTokens);
            }

            return (string.Empty, inputTokens, outputTokens);
        }

        private static AiImagePayload ExtractResponseImage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI image response was empty.");
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            string? requestId = root.TryGetProperty("id", out JsonElement idElement) ? idElement.GetString() : null;

            if (root.TryGetProperty("output", out JsonElement outputElement)
                && outputElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement output in outputElement.EnumerateArray())
                {
                    if (!output.TryGetProperty("content", out JsonElement contentElement)
                        || contentElement.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement content in contentElement.EnumerateArray())
                    {
                        if (content.TryGetProperty("type", out JsonElement typeElement))
                        {
                            string? type = typeElement.GetString();
                            if (string.Equals(type, "image", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(type, "image_generation", StringComparison.OrdinalIgnoreCase))
                            {
                                if (TryExtractBase64(content, out string base64, out string contentType))
                                {
                                    return new AiImagePayload(Convert.FromBase64String(base64), contentType, requestId);
                                }
                            }
                        }
                    }
                }
            }

            if (root.TryGetProperty("data", out JsonElement dataElement)
                && dataElement.ValueKind == JsonValueKind.Array
                && dataElement.GetArrayLength() > 0)
            {
                JsonElement first = dataElement[0];
                if (TryExtractBase64(first, out string base64, out string contentType))
                {
                    return new AiImagePayload(Convert.FromBase64String(base64), contentType, requestId);
                }
            }

            throw new AiProviderException(ProviderIdValue, "OpenAI image response did not include image bytes.");
        }

        private static bool TryExtractBase64(JsonElement element, out string base64, out string contentType)
        {
            base64 = string.Empty;
            contentType = "image/png";

            if (element.TryGetProperty("content_type", out JsonElement typeElement))
            {
                string? type = typeElement.GetString();
                if (!string.IsNullOrWhiteSpace(type))
                {
                    contentType = type;
                }
            }

            if (element.TryGetProperty("b64_json", out JsonElement b64Element))
            {
                base64 = b64Element.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(base64);
            }

            if (element.TryGetProperty("image_base64", out JsonElement imageElement))
            {
                base64 = imageElement.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(base64);
            }

            if (element.TryGetProperty("data", out JsonElement dataElement))
            {
                base64 = dataElement.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(base64);
            }

            return false;
        }

        private static AiArtifact BuildImageArtifact(AiImageResult imageResult)
        {
            string dataUrl = $"data:{imageResult.ContentType};base64,{Convert.ToBase64String(imageResult.ImageBytes)}";

            return new AiArtifact(
                Guid.NewGuid(),
                AiModality.Image,
                imageResult.ContentType,
                null,
                imageResult.ImageBytes,
                new Dictionary<string, object> { ["dataUrl"] = dataUrl });
        }

        private sealed record AiImagePayload(byte[] Bytes, string ContentType, string? RequestId);

        private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string? errorMessage = null;
            try
            {
                string json = await response.Content.ReadAsStringAsync(ct);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using JsonDocument doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("error", out JsonElement errorElement)
                        && errorElement.TryGetProperty("message", out JsonElement messageElement))
                    {
                        errorMessage = messageElement.GetString();
                    }
                }
            }
            catch (JsonException)
            {
            }

            string message = string.IsNullOrWhiteSpace(errorMessage)
                ? $"OpenAI request failed with status {(int)response.StatusCode}."
                : $"OpenAI request failed: {errorMessage}";

            throw new AiProviderException(ProviderIdValue, message);
        }

        private void LogUsage(AiRequest request, string model, int outputTokens, TimeSpan latency)
        {
            _logger.LogInformation(
                "OpenAI request {ActionId} model={Model} document={DocumentId} section={SectionId} selectionLength={SelectionLength} outputTokens={OutputTokens} latencyMs={LatencyMs}",
                request.ActionId,
                model,
                request.Context.DocumentId,
                request.Context.SectionId,
                request.Context.SelectionLength,
                outputTokens,
                (int)latency.TotalMilliseconds);
        }
    }
}
