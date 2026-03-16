using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Application.Documents;
using WriterApp.Client.Utilities;
using WriterApp.Shared;

namespace WriterApp.Client.Services
{
    internal sealed class CoverApiClient
    {
        private readonly HttpClient _http;

        public CoverApiClient(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        public async Task<CoverGenerationResponse> GenerateCoverConceptsAsync(CoverPrompt prompt, CancellationToken ct = default)
        {
            if (prompt is null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            using HttpResponseMessage response = await _http.PostAsJsonAsync("api/covers/generate", prompt, ct);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorDetails? error = await ApiErrorDetailsReader.ReadAsync(response);
                throw new InvalidOperationException(error?.UserMessage ?? "Cover generation failed.");
            }

            CoverGenerationResponse? payload = await response.Content.ReadFromJsonAsync<CoverGenerationResponse>(cancellationToken: ct);
            if (payload is null)
            {
                throw new InvalidOperationException("Cover generation returned an empty response.");
            }

            return payload;
        }

        public async Task<ProjectDto> SaveProjectCoverAsync(Guid projectId, string imageUrl, CancellationToken ct = default)
        {
            if (projectId == Guid.Empty)
            {
                throw new ArgumentException("Project id is required.", nameof(projectId));
            }

            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                throw new ArgumentException("Image URL is required.", nameof(imageUrl));
            }

            using HttpResponseMessage response = await _http.PostAsJsonAsync(
                $"api/projects/{projectId}/cover",
                new ProjectCoverUpdateRequest(imageUrl),
                ct);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorDetails? error = await ApiErrorDetailsReader.ReadAsync(response);
                throw new InvalidOperationException(error?.UserMessage ?? "Saving project cover failed.");
            }

            ProjectDto? payload = await response.Content.ReadFromJsonAsync<ProjectDto>(cancellationToken: ct);
            if (payload is null)
            {
                throw new InvalidOperationException("Saving project cover returned an empty response.");
            }

            return payload;
        }
    }
}
