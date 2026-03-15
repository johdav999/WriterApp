using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Application.Security;
using WriterApp.Client.State;

namespace WriterApp.Client.Utilities
{
    internal static class DuplicateAccountApiResponseReader
    {
        public static async Task<AuthDuplicateAccountDto?> TryReadAsync(HttpResponseMessage response, CancellationToken ct = default)
        {
            if (response.StatusCode != HttpStatusCode.Conflict || response.Content is null)
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
                AuthDuplicateAccountDto? duplicate = JsonSerializer.Deserialize<AuthDuplicateAccountDto>(
                    payload,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                ReplaceContent(response, payload, response.Content.Headers.ContentType);

                if (!string.Equals(duplicate?.Code, AuthDuplicateAccountDto.DuplicateCode, StringComparison.Ordinal))
                {
                    return null;
                }

                return duplicate with
                {
                    Message = string.IsNullOrWhiteSpace(duplicate.Message)
                        ? DuplicateAccountStateService.DefaultMessage
                        : duplicate.Message
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
            response.Content = new StringContent(
                payload,
                Encoding.UTF8,
                contentType?.MediaType ?? "application/json");
        }
    }
}
