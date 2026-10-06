using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Shared;

public enum RecommendedOutput { SectionRevision, OpeningRevision, AppendParagraph, Headlines, Summary }
public sealed record RecommendedWritingRun(int Version, string ToolId);
public sealed record RecommendedTextResult(IReadOnlyList<string> Items);
public static class RecommendedWriting
{
    public const string Parameter = "recommended_writing";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };
    public static IReadOnlyList<WritingToolDefinition> Catalog => new[] { "Novel", "ShortStory", "NonFiction", "Blog", "Other" }.SelectMany(PromptStrategyResolver.GetTopWritingToolsForIntent).ToArray();
    public static WritingToolDefinition Tool(string id) => Catalog.SingleOrDefault(t => t.Id == id) ?? throw new InvalidDataException("Choose a supported recommended writing tool.");
    public static RecommendedOutput Output(string id) { _ = Tool(id); return id switch {
        "novel.continue_scene" or "other.expand_idea" => RecommendedOutput.AppendParagraph,
        "blog.generate_headlines" => RecommendedOutput.Headlines,
        "other.summarize_clearly" => RecommendedOutput.Summary,
        "blog.improve_hook" => RecommendedOutput.OpeningRevision,
        _ => RecommendedOutput.SectionRevision
    }; }
    public static bool Revises(string id) => Output(id) is RecommendedOutput.SectionRevision or RecommendedOutput.OpeningRevision;
    public static bool CopyOnly(string id) => Output(id) is RecommendedOutput.Headlines or RecommendedOutput.Summary;
    public static string Target(string id) => Output(id) switch {
        RecommendedOutput.AppendParagraph => "Append one paragraph at the section end",
        RecommendedOutput.Headlines => "Five headline candidates · select and copy",
        RecommendedOutput.Summary => "Section summary · copy for use elsewhere",
        RecommendedOutput.OpeningRevision => "Revise the opening paragraph; retain later writing",
        _ => "Revise all pages in the current section"
    };
    public static string Serialize(string id) { _ = Tool(id); return JsonSerializer.Serialize(new RecommendedWritingRun(1, id), Json); }
    private static T Read<T>(string json) {
        if (json.Length > 100_000) throw new InvalidDataException("The recommended writing result is too large.");
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        void Unique(JsonElement e) { if (e.ValueKind == JsonValueKind.Object) { var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase); foreach (var p in e.EnumerateObject()) { if (!keys.Add(p.Name)) throw new InvalidDataException("Duplicate recommended writing field."); Unique(p.Value); } } else if (e.ValueKind == JsonValueKind.Array) foreach (var v in e.EnumerateArray()) Unique(v); }
        Unique(doc.RootElement);
        return JsonSerializer.Deserialize<T>(json, Json) ?? throw new InvalidDataException("Missing recommended writing result.");
    }
    public static RecommendedWritingRun Parse(string json) { var run = Read<RecommendedWritingRun>(json); if (run.Version != 1) throw new InvalidDataException("Update the backend for this recommendation contract."); _ = Tool(run.ToolId); return run; }
    public static Dictionary<string, object?> Parameters(string id) { var tool = Tool(id); return new() {
        [Parameter] = Serialize(id), ["template"] = tool.PromptTemplate.UserTemplate, ["systemTemplate"] = tool.PromptTemplate.SystemTemplate,
        ["scope"] = "section", ["strictTokens"] = true, ["tone"] = "Neutral", ["length"] = "Same"
    }; }
    public static RecommendedWritingRun? From(IReadOnlyDictionary<string, object?>? parameters) => parameters?.TryGetValue(Parameter, out var value) == true ? Parse(value?.ToString() ?? "") : null;
    public static void ValidateParameters(IReadOnlyDictionary<string, object?> parameters, RecommendedWritingRun run) {
        var expected = Parameters(run.ToolId);
        foreach (var pair in expected) if (!parameters.TryGetValue(pair.Key, out var value) || ReusablePrompts.Canonical(ReusablePrompts.Primitive(value)) != ReusablePrompts.Canonical(pair.Value)) throw new InvalidDataException("Recommendation parameters differ from the supported catalog template.");
        if (parameters.Keys.Any(k => !expected.ContainsKey(k) && k != WritingActions.Parameter)) throw new InvalidDataException("Unsupported recommendation parameters. Choose the catalog tool again.");
    }
    public static int ItemCount(string id) => Output(id) == RecommendedOutput.Headlines ? 5 : 1;
    public static RecommendedTextResult TextResult(string json, string id, string source) {
        if (Revises(id)) throw new InvalidDataException("This recommendation requires a complete section revision.");
        var result = Read<RecommendedTextResult>(json);
        if (result.Items is null || result.Items.Count != ItemCount(id) || result.Items.Distinct(StringComparer.Ordinal).Count() != result.Items.Count
            || result.Items.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > (Output(id) == RecommendedOutput.Headlines ? 300 : 4000) || s.Contains('\n') || s.Contains('\r') || s.Contains('\0') || !WritingActions.IsProse(s)))
            throw new InvalidDataException("The tool returned invalid or incomplete candidates. Your writing is unchanged.");
        if (Output(id) == RecommendedOutput.AppendParagraph && WritingActions.Continuation(result.Items[0], source) != WritingActions.NormalizeParagraph(result.Items[0]))
            throw new InvalidDataException("The paragraph repeats existing writing before its new prose. Run the tool again.");
        return result;
    }
    public static WritingStructure Revision(string json, WritingStructure source, string id) {
        if (!Revises(id)) throw new InvalidDataException("This output cannot replace manuscript writing.");
        ValidateSource(source, id);
        var result = WritingActions.Result(json, source);
        if (Output(id) == RecommendedOutput.OpeningRevision) {
            string firstBlock = source.Pages[0].Runs[0].Id.Split('.')[0];
            for (int p = 0; p < source.Pages.Count; p++) for (int r = 0; r < source.Pages[p].Runs.Count; r++) {
                var before = source.Pages[p].Runs[r];
                bool opening = p == 0 && before.Id.Split('.')[0] == firstBlock;
                if (!opening && result.Pages[p].Runs[r].Text != before.Text) throw new InvalidDataException("Improve Hook may revise only the opening paragraph. Later writing changed; retry the tool.");
            }
        }
        return result;
    }
    public static void ValidateSource(WritingStructure source, string id) {
        WritingActions.Validate(source);
        if (Output(id) == RecommendedOutput.OpeningRevision && (source.Pages[0].Runs.Count == 0 || source.Pages[0].Runs.Any(r => !System.Text.RegularExpressions.Regex.IsMatch(r.Id, @"^\d+(\.\d+)+$"))))
            throw new InvalidDataException("Improve Hook needs a mapped opening paragraph on the first page. Choose another tool or edit the opening manually.");
    }
    public static void ValidateOpeningHtml(string html, string id) {
        if (Output(id) == RecommendedOutput.OpeningRevision && !System.Text.RegularExpressions.Regex.IsMatch(html.TrimStart(), @"^<p(?:\s|>)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            throw new InvalidDataException("Improve Hook supports a first-page opening paragraph. A heading or embedded opening needs manual editing.");
    }
}
