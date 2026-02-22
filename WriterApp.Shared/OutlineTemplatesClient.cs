using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Application.Documents
{
    public sealed class OutlineTemplatesClient
    {
        private readonly HttpClient _http;

        public OutlineTemplatesClient(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<IReadOnlyList<OutlineTemplateDto>> GetTemplatesAsync(CancellationToken ct = default)
        {
            List<OutlineTemplateDto>? templates = await _http.GetFromJsonAsync<List<OutlineTemplateDto>>(
                "api/outline-templates",
                ct);
            if (templates is null)
            {
                return Array.Empty<OutlineTemplateDto>();
            }

            return templates;
        }

        public Task<HttpResponseMessage> CreateTemplateAsync(
            OutlineTemplateCreateRequest request,
            CancellationToken ct = default)
        {
            return _http.PostAsJsonAsync("api/outline-templates", request, ct);
        }

        public Task<HttpResponseMessage> DeleteTemplateAsync(Guid templateId, CancellationToken ct = default)
        {
            return _http.DeleteAsync($"api/outline-templates/{templateId}", ct);
        }

        public Task<HttpResponseMessage> ApplyTemplateAsync(
            Guid documentId,
            Guid templateId,
            OutlineTemplateApplyOptionsDto options,
            CancellationToken ct = default)
        {
            return _http.PostAsJsonAsync(
                $"api/documents/{documentId}/outline/apply-template/{templateId}",
                options,
                ct);
        }

        public static async Task<string?> ReadErrorMessageAsync(
            HttpResponseMessage response,
            CancellationToken ct = default)
        {
            if (response.Content is null)
            {
                return response.ReasonPhrase;
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (document.RootElement.TryGetProperty("message", out JsonElement message)
                    && message.ValueKind == JsonValueKind.String)
                {
                    string? value = message.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch
            {
            }

            string raw = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                return raw;
            }

            return response.ReasonPhrase;
        }
    }
}
