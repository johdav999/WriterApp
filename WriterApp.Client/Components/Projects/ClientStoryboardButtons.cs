using Microsoft.AspNetCore.Components;
using WriterApp.UI.Shared.Projects;

namespace WriterApp.Client.Components.Projects;

internal static class ClientStoryboardButtons
{
    public static RenderFragment Render(StoryboardActionButton button) => builder =>
    {
        builder.OpenComponent<PlanGatedButton>(0);
        builder.AddAttribute(1, "Feature", button.Feature);
        builder.AddAttribute(2, "Class", button.Class);
        builder.AddAttribute(3, "AllowedTitle", button.AllowedTitle);
        builder.AddAttribute(4, "LockedTitle", button.LockedTitle);
        builder.AddAttribute(5, "AriaLabel", button.AriaLabel);
        builder.AddAttribute(6, "ShowLockIndicator", button.ShowLockIndicator);
        builder.AddAttribute(7, "IsBusy", button.IsBusy);
        builder.AddAttribute(8, "IsActionDisabled", button.IsActionDisabled);
        builder.AddAttribute(9, "OnClick", button.OnClick);
        builder.AddAttribute(10, "ChildContent", button.ChildContent);
        builder.CloseComponent();
    };
}
