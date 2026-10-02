using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.UI.Shared.Projects;

namespace WriterApp.Client.Services;

public sealed class HttpStoryboardData(HttpClient http) : IStoryboardData
{
    public Task<SceneCardDto?> GetSceneCardAsync(Guid projectId, Guid sceneId, Guid? documentId = null) => http.GetFromJsonAsync<SceneCardDto>(Scope($"api/scenes/{sceneId}/scene-card", documentId));
    public async Task SaveSceneCardAsync(Guid projectId, Guid sceneId, SceneCardUpdateRequest request, Guid? documentId = null)
    { using var response = await http.PutAsJsonAsync(Scope($"api/scenes/{sceneId}/scene-card", documentId), request); await CheckAsync(response); }
    public async Task PatchNodeAsync(Guid projectId, Guid nodeId, ProjectNodePatchRequest request, Guid? documentId = null)
    { using var response = await http.PatchAsJsonAsync(Scope($"api/projects/{projectId}/nodes/{nodeId}", documentId), request); await CheckAsync(response); }
    public async Task ReorderAsync(Guid projectId, Guid parentId, ProjectNodeReorderRequest request, Guid? documentId = null)
    { using var response = await http.PostAsJsonAsync(Scope($"api/projects/{projectId}/nodes/{parentId}/reorder", documentId), request); await CheckAsync(response); }
    public async Task MoveSceneAsync(Guid projectId, Guid sceneId, ProjectNodePatchRequest patch, ProjectNodeReorderRequest order, Guid? documentId = null)
    { using var response = await http.PostAsJsonAsync(Scope($"api/projects/{projectId}/nodes/{sceneId}/move", documentId), new ProjectSceneMoveRequest(patch, order)); await CheckAsync(response); }
    public async Task<ProjectNodeDto> CreateNodeAsync(Guid projectId, ProjectNodeCreateRequest request, Guid? documentId = null)
    { using var response = await http.PostAsJsonAsync(Scope($"api/projects/{projectId}/nodes", documentId), request); return await ReadAsync<ProjectNodeDto>(response); }
    public async Task<ProjectNodeDuplicateResponse> DuplicateAsync(Guid projectId, Guid nodeId, ProjectNodeDuplicateRequest request, Guid? documentId = null)
    { using var response = await http.PostAsJsonAsync(Scope($"api/projects/{projectId}/nodes/{nodeId}/duplicate", documentId), request); return await ReadAsync<ProjectNodeDuplicateResponse>(response); }
    public async Task DeleteNodeAsync(Guid projectId, Guid nodeId, Guid? documentId = null)
    { using var response = await http.DeleteAsync(Scope($"api/projects/{projectId}/nodes/{nodeId}", documentId)); await CheckAsync(response); }
    public async Task<ProjectSceneOpenTargetDto?> ResolveTargetAsync(Guid projectId, Guid sceneId, Guid? documentId = null)
    { using var response = await http.PostAsync(Scope($"api/projects/{projectId}/nodes/{sceneId}/open-scene", documentId), null); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProjectSceneOpenTargetDto>() : null; }
    public async Task<AiActionExecuteResponseDto> ExecuteAiAsync(Guid projectId, string key, AiActionExecuteRequestDto request, Guid? documentId = null)
    { using var response = await http.PostAsJsonAsync(Scope($"api/ai/actions/{key}/execute", documentId), request); return await ReadAsync<AiActionExecuteResponseDto>(response); }
    public Task ValidateSuggestionAsync(Guid projectId, Guid? documentId = null) => Task.CompletedTask;
    private static string Scope(string url, Guid? documentId) => documentId is { } id ? url + $"?documentId={id:D}" : url;
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    { await CheckAsync(response); return await response.Content.ReadFromJsonAsync<T>() ?? throw new InvalidOperationException("The storyboard response was empty."); }
    private static async Task CheckAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        string error = "The storyboard change could not be completed.";
        try
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (json.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String) error = message.GetString() ?? error;
        }
        catch (JsonException) { }
        throw new InvalidOperationException(error);
    }
}
