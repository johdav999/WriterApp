using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WriterApp.Application.Documents;

namespace WriterApp.Shared;

public enum WritingScope { Selection, Section, Continuation }
public sealed record WritingSettings(string Tone = "Neutral", string Length = "Same", bool PreserveTerms = true);
public sealed record WritingToneDescriptor(string Value, string Label, string ClientPresetLabel, string? SelectionRewriteInstruction = null);
public sealed record WritingCommand(string Key, WritingScope Scope, WritingSettings Settings);
public sealed record WritingAvailability(IReadOnlyList<string> AllowedActions, bool StructuredSections, bool StructuredPresets = false, bool SavedOutlineContext = false, bool Recommendations = false);
public sealed record WritingStructure(int Version, Guid DocumentId, Guid SectionId, IReadOnlyList<TranslationPage> Pages);

public static class WritingActions
{
    public const string Parameter = "writing_structure";
    // Stable wire values and legacy client labels; saved custom tone text remains supported separately.
    public static readonly IReadOnlyList<WritingToneDescriptor> ToneDescriptors = Array.AsReadOnly<WritingToneDescriptor>([
        new("Neutral", "Neutral", "Rewrite (Neutral)"), new("Formal", "Formal", "Rewrite (Formal)"),
        new("Casual", "Casual", "Rewrite (Casual)"), new("Friendly", "Friendly", "Change tone (Friendly)"),
        new("Technical", "Technical", "Change tone (Technical)"), new("Executive", "Executive", "Rewrite (Executive)", "Rewrite (Executive)")
    ]);
    public static readonly string[] Tones = ToneDescriptors.Select(t => t.Value).ToArray();
    public static readonly string[] Lengths = ["Same", "Shorter", "Longer"];
    public static readonly string[] SectionKeys = ["expand.section", "tighten.section", "change_tone.section", "show_dont_tell.section"];
    public static readonly string[] SelectionKeys = ["rewrite.selection", "expand.selection", "tighten.selection", "change_tone.selection", "show_dont_tell.selection"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 };
    public static void Validate(WritingCommand command) {
        if (!Tones.Contains(command.Settings.Tone) || !Lengths.Contains(command.Settings.Length)
            || !(command.Scope switch { WritingScope.Selection => SelectionKeys.Contains(command.Key) || command.Key == "custom_transform", WritingScope.Section => SectionKeys.Contains(command.Key) || command.Key == "custom_transform",
                WritingScope.Continuation => command.Key == "propose.next-paragraph", _ => false }))
            throw new InvalidDataException("Choose a supported writing action and settings.");
    }
    public static string Serialize(WritingStructure source) {
        Validate(source); var text = JsonSerializer.Serialize(source, Json);
        if (text.Length > 100000) throw new InvalidDataException("The mapped section is too large. Revise a smaller scope.");
        return text;
    }
    public static WritingStructure Parse(string json) {
        if (json.Length > 100000) throw new InvalidDataException("Revise a smaller section (maximum 60,000 characters).");
        using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        void Unique(JsonElement element) {
            if (element.ValueKind == JsonValueKind.Object) {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in element.EnumerateObject()) { if (!names.Add(p.Name)) throw new InvalidDataException("Duplicate writing field."); Unique(p.Value); }
            } else if (element.ValueKind == JsonValueKind.Array) foreach (var v in element.EnumerateArray()) Unique(v);
        }
        Unique(parsed.RootElement);
        var source = JsonSerializer.Deserialize<WritingStructure>(json, Json) ?? throw new InvalidDataException("Missing writing structure."); Validate(source); return source;
    }
    public static void Validate(WritingStructure source) {
        // Reuse the exact bounded Unicode, page/run identity and text constraints of the shared editor mapping.
        TranslationStructures.Validate(new(1, source.DocumentId, "section", "en", [new(source.SectionId, source.Pages)]));
        if (source.Version != 1) throw new InvalidDataException("Unsupported writing structure version.");
    }
    public static WritingStructure Result(string text, WritingStructure source) {
        var result = Parse(text);
        _ = TranslationStructures.Result(JsonSerializer.Serialize(new TranslationStructure(1, result.DocumentId, "section", "en", [new(result.SectionId, result.Pages)]), Json),
            new(1, source.DocumentId, "section", "en", [new(source.SectionId, source.Pages)]));
        for (int p = 0; p < result.Pages.Count; p++)
            for (int r = 0; r < result.Pages[p].Runs.Count; r++) {
                var run = result.Pages[p].Runs[r];
                // Formatting can create whitespace/punctuation-only runs; unchanged runs are valid source data.
                if (run.Text != source.Pages[p].Runs[r].Text && !IsProse(run.Text))
                    throw new InvalidDataException("The revision contains instructions or invalid prose. Retry a smaller selection.");
            }
        return result;
    }
    public static string NormalizeParagraph(string text) => Regex.Replace(text.Replace('\r', ' ').Replace('\n', ' '), " {2,}", " ").Trim();
    public static string TrimLeadingEcho(string text, string source) {
        string candidate = NormalizeParagraph(text), context = NormalizeParagraph(source);
        for (int overlap = Math.Min(candidate.Length, context.Length); overlap >= 80; overlap--)
            if (context.EndsWith(candidate[..overlap], StringComparison.Ordinal)) return candidate[overlap..].TrimStart();
        return candidate;
    }
    public static string Continuation(string text, string source) {
        if (text.Length > 20000 || !IsProse(text))
            throw new InvalidDataException("The continuation is not valid prose. Run AI again.");
        var candidate = TrimLeadingEcho(text, source);
        TranslationStructures.Validate(new(1, Guid.NewGuid(), "section", "en", [new(Guid.NewGuid(), [new(Guid.NewGuid(), [new("0.0", candidate)])])]));
        if (candidate.Length == 0 || NormalizeParagraph(source).Contains(candidate, StringComparison.Ordinal)
            || candidate.Contains('\0') || candidate.StartsWith("```", StringComparison.Ordinal))
            throw new InvalidDataException("The continuation only repeats existing writing. Run AI again.");
        return candidate;
    }
    public static bool IsProse(string text) => QualityRewriteOutputValidator.SanitizeCandidateOutput(text) == text.Trim()
        && !Regex.IsMatch(text.Trim(), @"^(?:as an AI\b|here(?:'s| is| are) (?:the |a |your )?(?:revised|rewritten|next|new)\b|(?:revised|next|new) (?:paragraph|text):|sure[,!])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
