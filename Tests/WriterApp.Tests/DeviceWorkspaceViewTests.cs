using WriterApp.Device.Shared.Services;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceWorkspaceViewTests
{
    [Fact]
    public async Task RepeatedResetDoesNotTriggerLayoutParameterFeedback()
    {
        var view = new DeviceWorkspaceView();
        int notifications = 0;
        view.Changed = () => { notifications++; view.Reset(); return Task.CompletedTask; };
        view.Reset();
        Assert.Equal(0, notifications);
        await view.SelectPanelAsync(DeviceEditorPanel.Navigator);
        Assert.Equal(2, notifications); // Select then one reset; nested reset is inert.
        view.Reset();
        Assert.Equal(2, notifications);
        Assert.Equal(DeviceEditorPanel.Writing, view.Panel);
    }

    [Fact]
    public async Task ShowingContextWhileFocusedRestoresBothShellAndPanel()
    {
        var view = new DeviceWorkspaceView();
        int notifications = 0;
        view.Changed = () => { notifications++; return Task.CompletedTask; };
        await view.ToggleContextAsync();
        await view.ToggleFocusAsync();
        await view.ToggleContextAsync();
        Assert.False(view.FocusMode);
        Assert.False(view.ContextCollapsed);
        Assert.Equal(3, notifications);
    }

    [Fact]
    public async Task LeavingDocumentDoesNotLeaveLibraryInFocusMode()
    {
        var view = new DeviceWorkspaceView();
        await view.ToggleContextAsync();
        await view.ToggleFocusAsync();
        view.Reset();
        Assert.False(view.FocusMode);
        Assert.False(view.ContextCollapsed);
    }
}
