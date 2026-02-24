using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WriterApp.Application.Billing
{
    public sealed class StripeApiClient
    {
        private static readonly Uri StripeBaseUri = new("https://api.stripe.com/v1/");
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<StripeApiClient> _logger;

        public StripeApiClient(
            IHttpClientFactory httpClientFactory,
            ILogger<StripeApiClient> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<string?> FindCustomerByUserIdAsync(string secretKey, string userId, CancellationToken ct)
        {
            string escapedUserId = userId
                .Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("'", "\\'", StringComparison.Ordinal);
            string query = $"metadata['userId']:'{escapedUserId}'";
            string path = $"customers/search?query={Uri.EscapeDataString(query)}&limit=1";

            try
            {
                using JsonDocument doc = await SendGetAsync(secretKey, path, ct);
                if (!doc.RootElement.TryGetProperty("data", out JsonElement dataElement)
                    || dataElement.ValueKind != JsonValueKind.Array
                    || dataElement.GetArrayLength() == 0)
                {
                    return null;
                }

                JsonElement first = dataElement[0];
                if (!first.TryGetProperty("id", out JsonElement idElement))
                {
                    return null;
                }

                return idElement.GetString();
            }
            catch (StripeApiException ex)
            {
                _logger.LogInformation(
                    ex,
                    "Stripe customer search failed for user metadata lookup. Falling back to customer creation when needed. StatusCode={StatusCode}",
                    (int)ex.StatusCode);
                return null;
            }
        }

        public async Task<string> CreateCustomerAsync(string secretKey, string userId, CancellationToken ct)
        {
            Dictionary<string, string> form = new(StringComparer.Ordinal)
            {
                ["metadata[userId]"] = userId
            };

            using JsonDocument doc = await SendPostFormAsync(secretKey, "customers", form, ct);
            string? customerId = ReadRequiredString(doc.RootElement, "id");
            return customerId;
        }

        public async Task<bool> CustomerExistsAsync(string secretKey, string customerId, CancellationToken ct)
        {
            try
            {
                using JsonDocument _ = await SendGetAsync(secretKey, $"customers/{Uri.EscapeDataString(customerId)}", ct);
                return true;
            }
            catch (StripeApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task<string> CreateCheckoutSessionAsync(
            string secretKey,
            string customerId,
            string userId,
            string planKey,
            string priceId,
            string successUrl,
            string cancelUrl,
            CancellationToken ct)
        {
            Dictionary<string, string> form = new(StringComparer.Ordinal)
            {
                ["mode"] = "subscription",
                ["customer"] = customerId,
                ["client_reference_id"] = userId,
                ["line_items[0][price]"] = priceId,
                ["line_items[0][quantity]"] = "1",
                ["metadata[userId]"] = userId,
                ["metadata[planKey]"] = planKey,
                ["success_url"] = successUrl,
                ["cancel_url"] = cancelUrl
            };

            using JsonDocument doc = await SendPostFormAsync(secretKey, "checkout/sessions", form, ct);
            string? url = ReadRequiredString(doc.RootElement, "url");
            return url;
        }

        public async Task<string> CreateBillingPortalSessionAsync(
            string secretKey,
            string customerId,
            string returnUrl,
            CancellationToken ct)
        {
            Dictionary<string, string> form = new(StringComparer.Ordinal)
            {
                ["customer"] = customerId,
                ["return_url"] = returnUrl
            };

            using JsonDocument doc = await SendPostFormAsync(secretKey, "billing_portal/sessions", form, ct);
            string? url = ReadRequiredString(doc.RootElement, "url");
            return url;
        }

        private async Task<JsonDocument> SendGetAsync(string secretKey, string relativePath, CancellationToken ct)
        {
            using HttpRequestMessage request = BuildRequest(HttpMethod.Get, relativePath, secretKey, content: null);
            return await SendAsync(request, ct);
        }

        private async Task<JsonDocument> SendPostFormAsync(
            string secretKey,
            string relativePath,
            IReadOnlyDictionary<string, string> formData,
            CancellationToken ct)
        {
            using FormUrlEncodedContent content = new(formData.Where(pair => !string.IsNullOrWhiteSpace(pair.Value)));
            using HttpRequestMessage request = BuildRequest(HttpMethod.Post, relativePath, secretKey, content);
            return await SendAsync(request, ct);
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string relativePath, string secretKey, HttpContent? content)
        {
            HttpRequestMessage request = new(method, new Uri(StripeBaseUri, relativePath))
            {
                Content = content
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
            request.Headers.UserAgent.ParseAdd("WriterApp-Billing/1.0");
            return request;
        }

        private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            HttpClient client = _httpClientFactory.CreateClient();
            using HttpResponseMessage response = await client.SendAsync(request, ct);
            string payload = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                string message = TryReadStripeErrorMessage(payload)
                    ?? $"Stripe API call failed with {(int)response.StatusCode}.";
                _logger.LogWarning("Stripe API error. StatusCode={StatusCode} Message={Message}", (int)response.StatusCode, message);
                throw new StripeApiException(message, response.StatusCode);
            }

            try
            {
                return JsonDocument.Parse(payload);
            }
            catch (JsonException ex)
            {
                throw new StripeApiException("Stripe API returned invalid JSON payload.", response.StatusCode, ex);
            }
        }

        private static string? TryReadStripeErrorMessage(string payload)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(payload);
                if (!doc.RootElement.TryGetProperty("error", out JsonElement errorElement))
                {
                    return null;
                }

                if (errorElement.TryGetProperty("message", out JsonElement messageElement))
                {
                    return messageElement.GetString();
                }

                return errorElement.ToString();
            }
            catch
            {
                return null;
            }
        }

        private static string ReadRequiredString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement element))
            {
                throw new StripeApiException($"Stripe response missing required field '{propertyName}'.", HttpStatusCode.BadGateway);
            }

            string? value = element.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new StripeApiException($"Stripe response field '{propertyName}' is empty.", HttpStatusCode.BadGateway);
            }

            return value;
        }
    }

    public sealed class StripeApiException : Exception
    {
        public StripeApiException(string message, HttpStatusCode statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }

        public StripeApiException(string message, HttpStatusCode statusCode, Exception innerException)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }
}
