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
    internal sealed partial class CoverApiClient
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

            using var request = new HttpRequestMessage(HttpMethod.Post,"api/covers/generate") { Content=JsonContent.Create(prompt) };
            using HttpResponseMessage response = await _http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
            if (!response.IsSuccessStatusCode)
            {
                ApiErrorDetails? error = await ApiErrorDetailsReader.ReadAsync(response);
                throw new InvalidOperationException(error?.UserMessage ?? "Cover generation failed.");
            }

            const int limit=16*1024*1024;
            if(response.Content.Headers.ContentLength>limit) throw new InvalidOperationException("Cover response exceeds the safe limit.");
            await using var stream=await response.Content.ReadAsStreamAsync(ct); using var buffer=new System.IO.MemoryStream(); byte[] block=new byte[16384];
            int read; while((read=await stream.ReadAsync(block,ct))>0) { if(buffer.Length+read>limit) throw new InvalidOperationException("Cover response exceeds the safe limit."); await buffer.WriteAsync(block.AsMemory(0,read),ct); }
            CoverGenerationResponse? payload = System.Text.Json.JsonSerializer.Deserialize<CoverGenerationResponse>(buffer.ToArray(),new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (payload is null)
            {
                throw new InvalidOperationException("Cover generation returned an empty response.");
            }

            return payload;
        }

        public async Task<ProjectDto> SaveProjectCoverAsync(Guid projectId, string imageUrl, long? metadataRevision = null, CancellationToken ct = default)
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
                new ProjectCoverUpdateRequest(imageUrl, metadataRevision),
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
