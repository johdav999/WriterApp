using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.UI.Shared.Projects;
using WriterApp.Shared;

namespace WriterApp.Client.Services;

public sealed class HttpStoryboardData(HttpClient http,WebCheckedAi? checkedAi = null) : IStoryboardData
{
    private WebCheckedAi Checked => checkedAi ?? new WebCheckedAi(http);
    private (WebCheckedAi.Lease Lease,DateTimeOffset Created)? _suggestion;
    private Guid _latestProposal;
    private readonly Dictionary<Guid,(WebCheckedAi.Lease Lease,DateTimeOffset Created)> _suggestions=new();
    private (WebCheckedAi.Lease Lease,Guid Proposal)? _approval;
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
    {
        HttpResponseMessage response;
        if(_approval is { } approval) {
            await ValidateSuggestionAsync(projectId,documentId,approval.Proposal);
            using var mutation=Checked.Mutation(HttpMethod.Post,Scope($"api/projects/{projectId}/nodes",documentId),request,approval.Lease);
            response=await http.SendAsync(mutation);
            if(response.IsSuccessStatusCode)WebCheckedAi.RequireMutationReceipt(response);_approval=null;
        } else response=await http.PostAsJsonAsync(Scope($"api/projects/{projectId}/nodes",documentId),request);
        using(response)return await ReadAsync<ProjectNodeDto>(response);
    }
    public async Task<ProjectNodeDuplicateResponse> DuplicateAsync(Guid projectId, Guid nodeId, ProjectNodeDuplicateRequest request, Guid? documentId = null)
    { using var response = await http.PostAsJsonAsync(Scope($"api/projects/{projectId}/nodes/{nodeId}/duplicate", documentId), request); return await ReadAsync<ProjectNodeDuplicateResponse>(response); }
    public async Task DeleteNodeAsync(Guid projectId, Guid nodeId, Guid? documentId = null)
    { using var response = await http.DeleteAsync(Scope($"api/projects/{projectId}/nodes/{nodeId}", documentId)); await CheckAsync(response); }
    public async Task<ProjectSceneOpenTargetDto?> ResolveTargetAsync(Guid projectId, Guid sceneId, Guid? documentId = null)
    { using var response = await http.PostAsync(Scope($"api/projects/{projectId}/nodes/{sceneId}/open-scene", documentId), null); return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ProjectSceneOpenTargetDto>() : null; }
    public async Task<AiActionExecuteResponseDto> ExecuteAiAsync(Guid projectId, string key, AiActionExecuteRequestDto request, Guid? documentId = null)
    {
        var lease=await Checked.Capture(request.DocumentId ?? throw new InvalidOperationException("Choose a saved manuscript."),request.SectionId,request.PageId);
        if(lease.Source.ProjectId!=projectId || documentId is { } id && id!=lease.Source.DocumentId)throw new InvalidOperationException("Storyboard target belongs to another manuscript.");
        using var response=await Checked.Execute(key,request,lease);
        var result=await ReadAsync<AiActionExecuteResponseDto>(response);_suggestion=(lease,result.CreatedUtc);
        if(_suggestions.Count>=128)_suggestions.Remove(_suggestions.MinBy(p=>p.Value.Created).Key);
        _suggestions[result.ProposalId]=(lease,result.CreatedUtc);_latestProposal=result.ProposalId;return result;
    }
    public async Task ValidateSuggestionAsync(Guid projectId, Guid? documentId = null) {
        var suggestion=_suggestion ?? throw new InvalidOperationException("Regenerate this unchecked storyboard suggestion.");
        if(suggestion.Lease.Source.ProjectId!=projectId || documentId is { } id && id!=suggestion.Lease.Source.DocumentId)throw new InvalidOperationException("Storyboard target changed.");
        await ValidateSuggestionAsync(projectId,documentId,_latestProposal);
    }
    public async Task ValidateSuggestionAsync(Guid projectId,Guid? documentId,Guid proposalId) {
        if(!_suggestions.TryGetValue(proposalId,out var suggestion))throw new InvalidOperationException("Regenerate this unchecked storyboard suggestion.");
        if(suggestion.Lease.Source.ProjectId!=projectId || documentId is { } id && id!=suggestion.Lease.Source.DocumentId)throw new InvalidOperationException("Storyboard target changed.");
        WebAiSources.RequireTime(suggestion.Created);await Checked.Confirm(suggestion.Lease);_approval=(suggestion.Lease,proposalId);
    }
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
