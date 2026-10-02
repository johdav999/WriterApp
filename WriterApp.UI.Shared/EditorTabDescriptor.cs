namespace WriterApp.UI.Shared;

public sealed record EditorTabDescriptor<TKey>(TKey Key, string Label, string Id,
    bool Available = true, string? Tooltip = null);

public static class EditorTabNavigation
{
    public static IReadOnlyList<EditorTabDescriptor<TKey>> Available<TKey>(IEnumerable<EditorTabDescriptor<TKey>> tabs) =>
        tabs.Where(tab => tab.Available).ToArray();

    public static int TargetIndex(string key, int current, int count) => count == 0 || current < 0 ? -1 : key switch
    {
        "ArrowRight" => (current + 1) % count,
        "ArrowLeft" => (current + count - 1) % count,
        "Home" => 0,
        "End" => count - 1,
        _ => -1
    };
}
