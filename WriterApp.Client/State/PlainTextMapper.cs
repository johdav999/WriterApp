namespace WriterApp.Client.State;

// Keep selection mapping and status metrics on the same HTML text contract.
public static class PlainTextMapper
{
    public static string ToPlainText(string? html) => WriterApp.UI.Shared.EditorWordMetrics.ToPlainText(html);
}
