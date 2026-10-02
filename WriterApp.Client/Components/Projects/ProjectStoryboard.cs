namespace WriterApp.Client.Components.Projects;

public sealed class ProjectStoryboard : WriterApp.UI.Shared.Projects.StoryboardBoard
{
    protected override void OnInitialized()
    {
        ActionTemplate = ClientStoryboardButtons.Render;
        base.OnInitialized();
    }
}
