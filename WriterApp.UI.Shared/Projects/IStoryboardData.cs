using WriterApp.Application.Documents;
using WriterApp.Application.AI;

namespace WriterApp.UI.Shared.Projects;

// Presentation uses device or server persistence without owning either transport.
public interface IStoryboardData
{
    Task<SceneCardDto?> GetSceneCardAsync(Guid projectId, Guid sceneId, Guid? documentId = null);
    Task SaveSceneCardAsync(Guid projectId, Guid sceneId, SceneCardUpdateRequest request, Guid? documentId = null);
    Task PatchNodeAsync(Guid projectId, Guid nodeId, ProjectNodePatchRequest request, Guid? documentId = null);
    Task ReorderAsync(Guid projectId, Guid parentId, ProjectNodeReorderRequest request, Guid? documentId = null);
    async Task MoveSceneAsync(Guid projectId, Guid sceneId, ProjectNodePatchRequest patch, ProjectNodeReorderRequest order, Guid? documentId = null)
    {
        await PatchNodeAsync(projectId, sceneId, patch, documentId);
        await ReorderAsync(projectId, patch.ParentId!.Value, order, documentId);
    }
    Task<ProjectNodeDto> CreateNodeAsync(Guid projectId, ProjectNodeCreateRequest request, Guid? documentId = null);
    Task<ProjectNodeDuplicateResponse> DuplicateAsync(Guid projectId, Guid nodeId, ProjectNodeDuplicateRequest request, Guid? documentId = null);
    Task DeleteNodeAsync(Guid projectId, Guid nodeId, Guid? documentId = null);
    Task<ProjectSceneOpenTargetDto?> ResolveTargetAsync(Guid projectId, Guid sceneId, Guid? documentId = null);
    Task<AiActionExecuteResponseDto> ExecuteAiAsync(Guid projectId, string key, AiActionExecuteRequestDto request, Guid? documentId = null);
    Task ValidateSuggestionAsync(Guid projectId, Guid? documentId = null);
}
