namespace WriterApp.Client.Components.Projects;

public sealed class StoryboardInsightsPanel : WriterApp.UI.Shared.Projects.StoryboardInsights
{
    protected override void OnInitialized()
    {
        ActionTemplate = ClientStoryboardButtons.Render;
        base.OnInitialized();
    }
}
