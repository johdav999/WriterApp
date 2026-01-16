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
    public sealed class OpenAiProvider : IAiStreamingProvider
    {
        private const string ProviderIdValue = "openai";
        private const string DefaultBaseUrl = "https://api.openai.com/v1/";
        private const string ResponsesEndpoint = "responses";
        private const string ImagesEndpoint = "images";
        private const string ActionRewrite = "rewrite.selection";
        private const string ActionCoverImage = "generate.image.cover";

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly WriterAiOpenAiOptions _options;
        private readonly ILogger<OpenAiProvider> _logger;

        public OpenAiProvider(
            IHttpClientFactory httpClientFactory,
            IOptions<WriterAiOptions> options,
            ILogger<OpenAiProvider> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (options?.Value is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = options.Value.Providers.OpenAI ?? new WriterAiOpenAiOptions();
        }

        public string ProviderId => ProviderIdValue;

        public AiProviderCapabilities Capabilities => new(true, true);

        public AiStreamingCapabilities StreamingCapabilities => new(true, false);

        public async Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct)
        {
            if (request is null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            string apiKey = ResolveApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI API key is not configured.");
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                if (string.Equals(request.ActionId, ActionRewrite, StringComparison.Ordinal))
                {
                    string outputText = await ExecuteTextAsync(request, apiKey, ct);
                    AiArtifact artifact = new(
                        Guid.NewGuid(),
                        AiModality.Text,
                        "text/plain",
                        outputText,
                        null,
                        null);

                    LogUsage(request, _options.TextModel, outputText.Length, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        new AiUsage(0, 0, stopwatch.Elapsed),
                        new Dictionary<string, object> { ["provider"] = ProviderIdValue });
                }

                if (string.Equals(request.ActionId, ActionCoverImage, StringComparison.Ordinal))
                {
                    AiArtifact artifact = await ExecuteImageAsync(request, apiKey, ct);
                    LogUsage(request, _options.ImageModel, artifact.BinaryContent?.Length ?? 0, stopwatch.Elapsed);

                    return new AiResult(
                        request.RequestId,
                        new List<AiArtifact> { artifact },
                        new AiUsage(0, 0, stopwatch.Elapsed),
                        new Dictionary<string, object> { ["provider"] = ProviderIdValue });
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

            string apiKey = ResolveApiKey();
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

        private async Task<string> ExecuteTextAsync(AiRequest request, string apiKey, CancellationToken ct)
        {
            HttpRequestMessage requestMessage = BuildResponsesRequest(request, apiKey, stream: false);
            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));

            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            return ExtractResponseText(json);
        }

        private async Task<AiArtifact> ExecuteImageAsync(AiRequest request, string apiKey, CancellationToken ct)
        {
            string prompt = GetInputValue(request, "prompt", string.Empty);
            string instruction = GetInputValue(request, "instruction", string.Empty);
            string combinedPrompt = string.IsNullOrWhiteSpace(instruction)
                ? prompt
                : $"{prompt}\n\nInstruction: {instruction}";

            Dictionary<string, object> payload = new()
            {
                ["model"] = _options.ImageModel,
                ["prompt"] = combinedPrompt,
                ["size"] = "1024x1024",
                ["response_format"] = "b64_json"
            };

            HttpRequestMessage requestMessage = new(HttpMethod.Post, BuildUri(ImagesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(requestMessage, apiKey);

            HttpClient client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));
            using HttpResponseMessage response = await client.SendAsync(requestMessage, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            byte[] bytes = ExtractImageBytes(json);

            string dataUrl = $"data:image/png;base64,{Convert.ToBase64String(bytes)}";

            return new AiArtifact(
                Guid.NewGuid(),
                AiModality.Image,
                "image/png",
                null,
                bytes,
                new Dictionary<string, object> { ["dataUrl"] = dataUrl });
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

        private string ResolveApiKey()
        {
            string? key = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrWhiteSpace(key))
            {
                return key;
            }

            return _options.ApiKey ?? string.Empty;
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

        private static string ExtractResponseText(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("output_text", out JsonElement outputTextElement)
                && outputTextElement.ValueKind == JsonValueKind.String)
            {
                return outputTextElement.GetString() ?? string.Empty;
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

                return builder.ToString();
            }

            return string.Empty;
        }

        private static byte[] ExtractImageBytes(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI image response was empty.");
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (!root.TryGetProperty("data", out JsonElement dataElement)
                || dataElement.ValueKind != JsonValueKind.Array
                || dataElement.GetArrayLength() == 0)
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI image response did not include data.");
            }

            JsonElement first = dataElement[0];
            if (!first.TryGetProperty("b64_json", out JsonElement b64Element))
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI image response did not include image bytes.");
            }

            string? base64 = b64Element.GetString();
            if (string.IsNullOrWhiteSpace(base64))
            {
                throw new AiProviderException(ProviderIdValue, "OpenAI image response contained empty image data.");
            }

            return Convert.FromBase64String(base64);
        }

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

        private void LogUsage(AiRequest request, string model, int outputLength, TimeSpan latency)
        {
            _logger.LogInformation(
                "OpenAI request {ActionId} model={Model} document={DocumentId} section={SectionId} selectionLength={SelectionLength} outputLength={OutputLength} latencyMs={LatencyMs}",
                request.ActionId,
                model,
                request.Context.DocumentId,
                request.Context.SectionId,
                request.Context.SelectionLength,
                outputLength,
                (int)latency.TotalMilliseconds);
        }
    }
}
