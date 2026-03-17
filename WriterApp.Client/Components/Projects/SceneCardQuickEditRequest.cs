namespace WriterApp.Client.Components.Projects
{
    public sealed record SceneCardTitleQuickEditRequest(Guid SceneId, string Title);
    public sealed record SceneCardStatusQuickEditRequest(Guid SceneId, string Status);
}
