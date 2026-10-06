using System.Text.Json;
using WriterApp.Shared.Sync;

namespace WriterApp.Shared;

public sealed record StoryboardAnalysisNode(Guid Id, Guid? ParentId, int Order, string Type, string Title,
    SyncSceneCard? Card = null, string? MetadataJson = null);

public static class StoryboardAnalysis
{
    public static string Build(string title, IEnumerable<StoryboardAnalysisNode> source)
    {
        var nodes = source.ToArray();
        var ordered = new List<StoryboardAnalysisNode>();
        var visited = new HashSet<Guid>();
        void Visit(Guid? parent)
        {
            foreach (var node in nodes.Where(n => n.ParentId == parent).OrderBy(n => n.Order).ThenBy(n => n.Id))
            {
                if (!visited.Add(node.Id)) continue;
                ordered.Add(node);
                Visit(node.Id);
            }
        }
        Visit(null);
        var chapters = ordered.Where(n => n.Type == "chapter").Select((chapter, index) => new
        {
            id = chapter.Id, order = index + 1, title = chapter.Title,
            scenes = ordered.Where(n => n.Type == "scene" && n.ParentId == chapter.Id).Select((scene, sceneIndex) => new
            {
                id = scene.Id, order = sceneIndex + 1, title = scene.Title,
                summary = scene.Card?.Summary ?? Metadata(scene, "summary"),
                narrativeRole = scene.Card?.NarrativeRole ?? Metadata(scene, "narrativeRole"),
                narrativeIntent = scene.Card?.NarrativeIntent ?? Metadata(scene, "narrativeIntent"),
                pov = scene.Card?.PovCharacterId ?? Metadata(scene, "pov"),
                subplotTags = Tags(scene.Card?.SubplotTagsJson ?? Metadata(scene, "subplotTags")),
                emotionalBeat = scene.Card?.EmotionalBeat,
                keyEvents = scene.Card?.KeyEvents, openQuestions = scene.Card?.OpenQuestions
            }).ToArray()
        }).ToArray();
        string json = JsonSerializer.Serialize(new { projectTitle = title, chapters });
        if (json.Length > 100_000) throw new InvalidDataException("Storyboard context exceeds 100,000 characters. Reduce scene notes before running this analysis.");
        return json;
    }

    private static string? Metadata(StoryboardAnalysisNode node, string key)
    {
        try
        {
            using var json = JsonDocument.Parse(node.MetadataJson ?? "{}");
            foreach (var property in json.RootElement.EnumerateObject())
                if (string.Equals(property.Name, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText();
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { }
        return null;
    }

    public static string[] Tags(string? json)
    {
        try { return (JsonSerializer.Deserialize<string[]>(json ?? "[]") ?? []).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }
        catch (JsonException) { return []; }
    }
}

public sealed record SubplotContinuityFinding(string SubplotName, string IssueType, string Explanation,
    IReadOnlyList<string> AffectedScenes, string? Recommendation);

public sealed record SubplotContinuityReport(string? Assessment, string? Summary, IReadOnlyList<SubplotContinuityFinding> Findings)
{
    public static SubplotContinuityReport Parse(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? "");
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("findings", out var items) || items.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("The continuity response is incomplete. Run the check again.");
            string? Text(JsonElement item, string key) => item.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            var findings = new List<SubplotContinuityFinding>();
            foreach (var item in items.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(Text(item, "subplotName"))
                    || string.IsNullOrWhiteSpace(Text(item, "issueType")) || string.IsNullOrWhiteSpace(Text(item, "explanation")))
                    throw new InvalidDataException("The continuity response contains an incomplete finding. Run the check again.");
                string[] scenes = item.TryGetProperty("affectedScenes", out var refs) && refs.ValueKind == JsonValueKind.Array
                    ? refs.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).ToArray() : [];
                findings.Add(new(Text(item, "subplotName")!, Text(item, "issueType")!, Text(item, "explanation")!, scenes, Text(item, "recommendation")));
            }
            return new(Text(root, "assessment"), Text(root, "summary"), findings);
        }
        catch (JsonException e) { throw new InvalidDataException("The continuity response could not be read. Run the check again.", e); }
    }
}
