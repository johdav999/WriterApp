using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Client.State;

namespace WriterApp.Client.Utilities
{
    internal sealed record DeletedAccountApiResponse(string? Code, string? Message);

    internal static class DeletedAccountApiResponseReader
    {
        public const string DeletedCode = "account_deleted";

        public static async Task<DeletedAccountApiResponse?> TryReadAsync(HttpResponseMessage response, CancellationToken ct = default)
        {
            if (response.StatusCode != HttpStatusCode.Forbidden || response.Content is null)
            {
                return null;
            }

            string payload = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            try
            {
                DeletedAccountApiResponse? deleted = JsonSerializer.Deserialize<DeletedAccountApiResponse>(
                    payload,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                ReplaceContent(response, payload, response.Content.Headers.ContentType);

                if (!string.Equals(deleted?.Code, DeletedCode, StringComparison.Ordinal))
                {
                    return null;
                }

                return deleted with
                {
                    Message = string.IsNullOrWhiteSpace(deleted.Message)
                        ? DeletedAccountStateService.DefaultMessage
                        : deleted.Message
                };
            }
            catch
            {
                ReplaceContent(response, payload, response.Content.Headers.ContentType);
                return null;
            }
        }

        private static void ReplaceContent(HttpResponseMessage response, string payload, MediaTypeHeaderValue? contentType)
        {
            StringContent replacement = new(
                payload,
                Encoding.UTF8,
                contentType?.MediaType ?? "application/json");
            response.Content = replacement;
        }
    }
}
