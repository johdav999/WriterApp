namespace WriterApp.Device.Shared.Services;
public enum DeviceEditorPanel { Writing, Navigator, Story, Notes, Synopsis, History, Advanced }
public sealed class DeviceWorkspaceView
{
    public DeviceEditorPanel Panel { get; private set; } = DeviceEditorPanel.Writing;
    public Task SelectPanelAsync(DeviceEditorPanel panel)
    {
        if (!Enum.IsDefined(panel)) throw new ArgumentOutOfRangeException(nameof(panel));
        Panel = panel;
        return Changed?.Invoke() ?? Task.CompletedTask;
    }
    public bool FocusMode { get; private set; }
    public bool ContextCollapsed { get; private set; }
    public Func<Task>? Changed { get; set; }
    public Task ToggleFocusAsync() { FocusMode = !FocusMode; return Changed?.Invoke() ?? Task.CompletedTask; }
    public Task ToggleContextAsync()
    {
        if (FocusMode) { FocusMode = false; ContextCollapsed = false; }
        else ContextCollapsed = !ContextCollapsed;
        return Changed?.Invoke() ?? Task.CompletedTask;
    }
    public void Reset()
    {
        if (Panel == DeviceEditorPanel.Writing && !FocusMode && !ContextCollapsed) return;
        Panel = DeviceEditorPanel.Writing; FocusMode = false; ContextCollapsed = false; _ = Changed?.Invoke();
    }
}
