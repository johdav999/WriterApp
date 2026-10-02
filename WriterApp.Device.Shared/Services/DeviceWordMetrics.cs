using System.Text;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.UI.Shared;

namespace WriterApp.Device.Shared.Services;

public static class DeviceWordMetrics
{
    public static int? CountPages(IEnumerable<LocalPage> pages)
    {
        int total = 0;
        foreach (var page in pages)
        {
            if (Count(page) is not { } count) return null;
            total = checked(total + count);
        }
        return total;
    }

    public static int? Count(LocalPage page)
    {
        if (page.ContentFormat == LocalContentFormat.Html) return EditorWordMetrics.CountHtml(page.Content);
        if (page.ContentFormat == LocalContentFormat.LegacyText) return EditorWordMetrics.CountPlainText(page.Content);
        try
        {
            using var json = JsonDocument.Parse(page.Content);
            var text = new StringBuilder();
            Append(json.RootElement, text);
            return EditorWordMetrics.CountPlainText(text.ToString());
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException) { return null; }
    }
    private static void Append(JsonElement node, StringBuilder text)
    {
        if (node.ValueKind != JsonValueKind.Object) return;
        string? type = node.TryGetProperty("type", out var kind) ? kind.GetString() : null;
        if (type == "text" && node.TryGetProperty("text", out var value)) text.Append(value.GetString());
        if (node.TryGetProperty("content", out var children) && children.ValueKind == JsonValueKind.Array)
            foreach (var child in children.EnumerateArray()) Append(child, text);
        if (type is "paragraph" or "heading" or "hardBreak" or "codeBlock") text.Append('\n');
    }
}
