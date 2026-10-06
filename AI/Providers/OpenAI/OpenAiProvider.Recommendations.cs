using WriterApp.Shared;
namespace WriterApp.AI.Providers.OpenAI;
public sealed partial class OpenAiProvider
{
    private static Dictionary<string, object> RecommendedResponseFormat(string id) => new() { ["format"] = new {
        type = "json_schema", name = "recommended_writing_items", strict = true,
        schema = new { type = "object", additionalProperties = false, required = new[] { "items" }, properties = new {
            items = new { type = "array", minItems = RecommendedWriting.ItemCount(id), maxItems = RecommendedWriting.ItemCount(id), items = new { type = "string" } }
        } }
    }};
}
