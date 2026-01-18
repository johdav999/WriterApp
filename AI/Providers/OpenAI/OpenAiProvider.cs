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
        private const string ActionCoverImage = "generate.image.cover";
        private const string ActionStoryCoach = "synopsis.story_coach";
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
                if (string.Equals(request.ActionId, ActionRewrite, StringComparison.Ordinal))
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

                throw new AiProviderException(ProviderIdValue, $"OpenAI provider does not support action '{request.ActionId}'.");
            }
            catch (OperationCanceledException)
            {
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

            if (!string.Equals(request.ActionId, ActionRewrite, StringComparison.Ordinal))
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
