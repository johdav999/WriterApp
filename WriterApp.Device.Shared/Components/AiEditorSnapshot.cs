namespace WriterApp.Device.Shared.Components;

public sealed record AiEditorSnapshot(string Html, string PlainText, string SelectedText,
    int SelectionStart, int SelectionEnd, int From, int To, long Version);
