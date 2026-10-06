using System.Text.Json;
using System.Text.Encodings.Web;
using WriterApp.AI.Abstractions;
using WriterApp.Shared;

namespace WriterApp.AI.Providers.OpenAI;

public sealed partial class OpenAiProvider
{
    internal const string InvalidWritingResponse = "OpenAI returned an invalid structured writing revision.";
    private static WritingStructure WritingSource(AiRequest request)
        => WritingActions.Parse(request.Context.SelectionText ?? request.Context.OriginalText);

    private static string RunKey(int page, int run) => $"p{page}_r{run}";

    private static string WritingWireSource(WritingStructure source)
        => JsonSerializer.Serialize(new {
            documentId = source.DocumentId, sectionId = source.SectionId,
            pages = source.Pages.Select((page, p) => new {
                id = page.Id,
                runs = page.Runs.Select((run, r) => new { id = run.Id, key = RunKey(p, r), text = run.Text })
            })
        }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

    private static Dictionary<string, object> WritingResponseFormat(WritingStructure source)
    {
        var fields = new Dictionary<string, object>();
        for (int p = 0; p < source.Pages.Count; p++)
            for (int r = 0; r < source.Pages[p].Runs.Count; r++)
                fields[RunKey(p, r)] = new { type = "string", pattern = RunTextPattern(source.Pages[p].Runs[r].Text) };
        object ObjectSchema(Dictionary<string, object> properties) => new {
            type = "object", properties, required = properties.Keys.ToArray(), additionalProperties = false
        };
        return new() { ["format"] = new {
            type = "json_schema", name = "section_writing_revision", strict = true,
            schema = ObjectSchema(new() { ["runs"] = ObjectSchema(fields) })
        }};
    }

    private static string RunTextPattern(string text)
    {
        // Require the same whitespace boundaries and prohibit editor line-break/control markers
        // during generation itself, rather than relying on the model's prose instructions.
        static string Literal(string value) => string.Concat(value.Select(c => "\\.^$|?*+()[]{}".Contains(c) ? "\\" + c : c.ToString()));
        if (string.IsNullOrWhiteSpace(text)) return "^" + Literal(text) + "$";
        string leading = text[..(text.Length - text.TrimStart().Length)];
        string trailing = text[text.TrimEnd().Length..];
        return "^" + Literal(leading) + "[^\\s\\u0000](?:[^\\r\\n\\u0000]*[^\\s\\u0000])?" + Literal(trailing) + "$";
    }

    private static string RestoreWritingRuns(string output, WritingStructure source)
    {
        if (output.Length > 100000) throw new InvalidDataException("The structured writing response is too large.");
        using var json = JsonDocument.Parse(output, new JsonDocumentOptions { MaxDepth = 4 });
        var root = json.RootElement;
        if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1
            || !root.TryGetProperty("runs", out var runs) || runs.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Missing structured writing runs.");
        var text = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in runs.EnumerateObject())
            if (field.Value.ValueKind != JsonValueKind.String || !text.TryAdd(field.Name, field.Value.GetString()!))
                throw new InvalidDataException("Invalid or duplicate structured writing run.");
        if (text.Count != source.Pages.Sum(page => page.Runs.Count))
            throw new InvalidDataException("Incomplete structured writing runs.");
        var pages = source.Pages.Select((page, p) => new TranslationPage(page.Id,
            page.Runs.Select((run, r) => new TranslationRun(run.Id,
                text.TryGetValue(RunKey(p, r), out var revised) ? revised : throw new InvalidDataException("Missing structured writing run."))).ToArray())).ToArray();
        var candidate = WritingActions.Serialize(source with { Pages = pages });
        // Keep the complete editor ownership, boundary, Unicode and prose validation after transport mapping.
        _ = WritingActions.Result(candidate, source);
        return candidate;
    }
}
