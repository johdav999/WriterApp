using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace WriterApp.Client.Utilities
{
    internal sealed record ApiErrorDetails(
        int StatusCode,
        string? Code,
        string? FeatureKey,
        string? UpgradePath,
        string? UserMessage);

    internal static class ApiErrorDetailsReader
    {
        public static async Task<ApiErrorDetails?> ReadAsync(HttpResponseMessage response)
        {
            if (response is null || response.Content is null)
            {
                return null;
            }

            string payload;
            try
            {
                payload = (await response.Content.ReadAsStringAsync()).Trim();
            }
            catch
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(payload);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return new ApiErrorDetails(
                        (int)response.StatusCode,
                        null,
                        null,
                        null,
                        payload);
                }

                string? detail = GetString(root, "detail");
                string? message = GetString(root, "message");
                string? title = GetString(root, "title");

                return new ApiErrorDetails(
                    (int)response.StatusCode,
                    GetString(root, "code"),
                    GetString(root, "featureKey"),
                    GetString(root, "upgradePath"),
                    FirstNonEmpty(detail, message, title));
            }
            catch (JsonException)
            {
                return new ApiErrorDetails(
                    (int)response.StatusCode,
                    null,
                    null,
                    null,
                    payload);
            }
        }

        private static string? GetString(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.ToString();
        }

        private static string? FirstNonEmpty(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
