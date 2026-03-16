using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Subscriptions;
using WriterApp.Shared;

namespace WriterApp.Application.Covers
{
    public sealed class CoverImageService : ICoverImageService
    {
        private const string OpenAiProviderId = "openai";
        private const string DefaultBaseUrl = "https://api.openai.com/v1/";
        private const string ImagesEndpoint = "images/generations";
        private const int ImageCount = 4;
        private const string ImageSize = "1024x1024";
        private const string ResponseFormat = "b64_json";
        private const string DefaultGptImageOutputFormat = "png";

        private readonly HttpClient _httpClient;
        private readonly OpenAiKeyProvider _keyProvider;
        private readonly IAiProviderRegistry _providerRegistry;
        private readonly IAiUsagePolicy _usagePolicy;
        private readonly WriterAiOpenAiOptions _options;
        private readonly ILogger<CoverImageService> _logger;

        public CoverImageService(
            HttpClient httpClient,
            OpenAiKeyProvider keyProvider,
            IAiProviderRegistry providerRegistry,
            IAiUsagePolicy usagePolicy,
            IOptions<WriterAiOptions> options,
            ILogger<CoverImageService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _keyProvider = keyProvider ?? throw new ArgumentNullException(nameof(keyProvider));
            _providerRegistry = providerRegistry ?? throw new ArgumentNullException(nameof(providerRegistry));
            _usagePolicy = usagePolicy ?? throw new ArgumentNullException(nameof(usagePolicy));
            _options = options?.Value?.Providers.OpenAI ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<string>> GenerateCoverConceptsAsync(CoverPrompt prompt, CancellationToken ct = default)
        {
            if (prompt is null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            if (!_keyProvider.HasKey)
            {
                throw new CoverImageGenerationException("ai.provider_missing", "OpenAI API key is not configured.");
            }

            IAiProvider? provider = _providerRegistry.GetById(OpenAiProviderId);
            if (provider is null)
            {
                throw new CoverImageGenerationException("ai.provider_unavailable", "OpenAI image generation is unavailable.");
            }

            AiUsageDecision decision = await _usagePolicy.EvaluateAsync(provider, GenerateCoverImageAction.ActionIdValue);
            if (!decision.Allowed)
            {
                if (string.Equals(decision.ErrorCode, "plan_upgrade_required", StringComparison.OrdinalIgnoreCase))
                {
                    throw new EntitlementDeniedException(
                        "ai.images.cover",
                        null,
                        string.IsNullOrWhiteSpace(decision.ErrorMessage)
                            ? "Cover image generation is not enabled for your plan."
                            : decision.ErrorMessage!);
                }

                throw new CoverImageGenerationException(
                    decision.ErrorCode ?? "ai.provider_unavailable",
                    string.IsNullOrWhiteSpace(decision.ErrorMessage)
                        ? "Cover generation is temporarily unavailable."
                        : decision.ErrorMessage!);
            }

            string model = string.IsNullOrWhiteSpace(_options.ImageModel) ? "gpt-image-1" : _options.ImageModel;
            string imagePrompt = BuildImagePrompt(prompt);
            Dictionary<string, object> payload = new()
            {
                ["model"] = model,
                ["prompt"] = imagePrompt,
                ["n"] = ImageCount,
                ["size"] = ImageSize
            };

            if (IsGptImageModel(model))
            {
                payload["output_format"] = DefaultGptImageOutputFormat;
            }
            else if (IsDalleModel(model))
            {
                payload["response_format"] = ResponseFormat;
            }

            _logger.LogInformation(
                "Generating cover concepts request. Model={Model} Size={Size} Count={Count}",
                model,
                ImageSize,
                ImageCount);

            using HttpRequestMessage request = new(HttpMethod.Post, BuildUri(ImagesEndpoint))
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };

            ApplyAuthHeaders(request);

            using HttpResponseMessage response = await _httpClient.SendAsync(request, ct);
            await EnsureSuccessAsync(response, ct);

            string json = await response.Content.ReadAsStringAsync(ct);
            List<string> imageUrls = ExtractImageUrls(json, model);
            if (imageUrls.Count == 0)
            {
                throw new CoverImageGenerationException("ai.provider_unavailable", "OpenAI image response did not include any images.");
            }

            _logger.LogInformation(
                "Generated cover concepts. Count={Count} Model={Model}",
                imageUrls.Count,
                model);

            return imageUrls;
        }

        private static string BuildImagePrompt(CoverPrompt prompt)
        {
            ArgumentNullException.ThrowIfNull(prompt);

            List<string> descriptors = new();

            if (!string.IsNullOrWhiteSpace(prompt.Style))
            {
                descriptors.Add($"{prompt.Style.Trim()} style");
            }

            if (!string.IsNullOrWhiteSpace(prompt.Mood))
            {
                descriptors.Add($"{prompt.Mood.Trim()} mood");
            }

            if (!string.IsNullOrWhiteSpace(prompt.Genre))
            {
                descriptors.Add($"{prompt.Genre.Trim()} genre");
            }

            if (!string.IsNullOrWhiteSpace(prompt.ColorPalette))
            {
                descriptors.Add($"{prompt.ColorPalette.Trim()} color palette");
            }

            string descriptorText = descriptors.Count == 0
                ? "cinematic style, atmospheric lighting"
                : string.Join(", ", descriptors);

            StringBuilder builder = new();
            builder.Append("Book cover artwork, ");
            builder.Append(descriptorText);
            builder.Append(", centered composition, atmospheric lighting, typography-safe focal area, professional novel cover illustration");

            if (!string.IsNullOrWhiteSpace(prompt.Description))
            {
                builder.Append(". User description: ");
                builder.Append(prompt.Description.Trim());
            }

            return builder.ToString();
        }

        private void ApplyAuthHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _keyProvider.ApiKey);
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

        private static bool IsGptImageModel(string model)
        {
            return model.StartsWith("gpt-image", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDalleModel(string model)
        {
            return string.Equals(model, "dall-e-2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(model, "dall-e-3", StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> ExtractImageUrls(string json, string model)
        {
            List<string> results = new();
            if (string.IsNullOrWhiteSpace(json))
            {
                return results;
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            string defaultMimeType = GetDefaultMimeType(model);
            if (root.TryGetProperty("output_format", out JsonElement rootOutputFormatElement))
            {
                string? rootMimeType = MapOutputFormatToMimeType(rootOutputFormatElement.GetString());
                if (!string.IsNullOrWhiteSpace(rootMimeType))
                {
                    defaultMimeType = rootMimeType;
                }
            }

            if (!root.TryGetProperty("data", out JsonElement dataElement)
                || dataElement.ValueKind != JsonValueKind.Array)
            {
                return results;
            }

            foreach (JsonElement item in dataElement.EnumerateArray())
            {
                if (item.TryGetProperty("url", out JsonElement urlElement))
                {
                    string? url = urlElement.GetString();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        results.Add(url);
                        continue;
                    }
                }

                if (item.TryGetProperty("b64_json", out JsonElement b64Element))
                {
                    string? base64 = b64Element.GetString();
                    if (!string.IsNullOrWhiteSpace(base64))
                    {
                        string mimeType = ResolveMimeType(item, defaultMimeType);
                        results.Add($"data:{mimeType};base64,{base64}");
                    }
                }
            }

            return results;
        }

        private static string GetDefaultMimeType(string model)
        {
            return IsGptImageModel(model)
                ? "image/png"
                : "image/png";
        }

        private static string ResolveMimeType(JsonElement item, string fallbackMimeType)
        {
            if (item.TryGetProperty("output_format", out JsonElement outputFormatElement))
            {
                string? outputFormat = outputFormatElement.GetString();
                string? mimeType = MapOutputFormatToMimeType(outputFormat);
                if (!string.IsNullOrWhiteSpace(mimeType))
                {
                    return mimeType;
                }
            }

            if (item.TryGetProperty("mime_type", out JsonElement mimeTypeElement))
            {
                string? mimeType = mimeTypeElement.GetString();
                if (!string.IsNullOrWhiteSpace(mimeType))
                {
                    return mimeType;
                }
            }

            return fallbackMimeType;
        }

        private static string? MapOutputFormatToMimeType(string? outputFormat)
        {
            if (string.IsNullOrWhiteSpace(outputFormat))
            {
                return null;
            }

            return outputFormat.Trim().ToLowerInvariant() switch
            {
                "png" => "image/png",
                "jpeg" => "image/jpeg",
                "jpg" => "image/jpeg",
                "webp" => "image/webp",
                "gif" => "image/gif",
                _ => null
            };
        }

        private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            string? errorMessage = null;
            string? errorBody = null;
            try
            {
                string json = await response.Content.ReadAsStringAsync(ct);
                errorBody = string.IsNullOrWhiteSpace(json) ? null : json;
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

            if (!string.IsNullOrWhiteSpace(errorBody))
            {
                _logger.LogWarning(
                    "OpenAI image request failed. StatusCode={StatusCode} Body={Body}",
                    (int)response.StatusCode,
                    errorBody);
            }
            else
            {
                _logger.LogWarning(
                    "OpenAI image request failed. StatusCode={StatusCode} Body=(empty)",
                    (int)response.StatusCode);
            }

            throw new CoverImageGenerationException(
                "ai.provider_unavailable",
                string.IsNullOrWhiteSpace(errorMessage)
                    ? $"OpenAI image request failed with status {(int)response.StatusCode}."
                    : $"OpenAI image request failed: {errorMessage}");
        }
    }
}
