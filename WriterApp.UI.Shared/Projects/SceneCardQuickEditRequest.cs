namespace WriterApp.UI.Shared.Projects
{
    public sealed record SceneCardTitleQuickEditRequest(Guid SceneId, string Title);
    public sealed record SceneCardStatusQuickEditRequest(Guid SceneId, string Status);
}
