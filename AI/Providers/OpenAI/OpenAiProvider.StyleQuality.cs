using WriterApp.Shared;

namespace WriterApp.AI.Providers.OpenAI;

public sealed partial class OpenAiProvider
{
    internal const string IncompleteStyleReviewResponse = "OpenAI returned an incomplete style review.";

    private static Dictionary<string, object> StyleReviewResponseFormat()
    {
        var fields = new Dictionary<string, object> {
            ["original"] = new { type = "string", description = "Exact unique source passage, copied verbatim without changing whitespace or punctuation." },
            ["replacement"] = new { type = "string", description = "Changed prose for this passage only; preserve paragraph breaks." },
            ["criterion"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = StyleQualityReview.CriteriaKeys },
            ["kind"] = new Dictionary<string, object> { ["type"] = "string", ["enum"] = new[] { "correction", "preference" } },
            ["reason"] = new { type = "string", description = "Brief explanation of this edit." },
            ["tradeoff"] = new { type = "string", description = "Brief effect on voice or emphasis, or None." }
        };
        return new() { ["format"] = new {
            type = "json_schema", name = "style_quality_review", strict = true,
            schema = new {
                type = "object", additionalProperties = false, required = new[] { "edits" },
                properties = new {
                    edits = new {
                        type = "array", maxItems = 24,
                        items = new { type = "object", properties = fields, required = fields.Keys.ToArray(), additionalProperties = false }
                    }
                }
            }
        }};
    }
}
