namespace WriterApp.Client.Components.Projects
{
    public sealed record StoryboardSelectionUpdate(Guid? PrimarySceneId, IReadOnlyList<Guid> SelectedSceneIds);
}
