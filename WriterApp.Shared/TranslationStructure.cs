using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Shared.Localization;

namespace WriterApp.Shared;

public sealed record TranslationRun(string Id, string Text);
public sealed record TranslationSelectionOptions(string TargetLanguage, string SourceLanguage = "auto", string Style = "natural");
public sealed record TranslationPage(Guid Id, IReadOnlyList<TranslationRun> Runs);
public sealed record TranslationSection(Guid Id, IReadOnlyList<TranslationPage> Pages);
public sealed record TranslationStructure(int Version, Guid DocumentId, string Scope, string TargetLanguage,
    IReadOnlyList<TranslationSection> Sections);

/// <summary>Bounded translation of text runs; structure and formatting stay in the host.</summary>
public static class TranslationStructures
{
    public const string Parameter = "translation_structure";
    public const int MaxChars = 60000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static string Serialize(TranslationStructure value) { Validate(value); return JsonSerializer.Serialize(value, Json); }
    public static TranslationStructure Parse(string value)
    {
        if (value.Length > 100000) throw new InvalidDataException("Translation exceeds the safe size limit. Translate a smaller scope.");
        // Reject duplicate JSON properties as well as duplicate structural markers.
        using var json = JsonDocument.Parse(value, new() { MaxDepth = 16 });
        void Unique(JsonElement element) {
            if (element.ValueKind == JsonValueKind.Object) {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject()) {
                    if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate translation metadata.");
                    Unique(property.Value);
                }
            } else if (element.ValueKind == JsonValueKind.Array) foreach (var child in element.EnumerateArray()) Unique(child);
        }
        Unique(json.RootElement);
        var result = JsonSerializer.Deserialize<TranslationStructure>(value, Json) ?? throw new InvalidDataException("Missing translation structure.");
        Validate(result); return result;
    }
    public static void Validate(TranslationStructure value)
    {
        if (value.Version != 1 || value.DocumentId == Guid.Empty || value.Scope is not ("section" or "document")
            || !TranslationLanguages.All.Any(l => l.Code == value.TargetLanguage)
            || value.Sections is null || value.Sections.Count is < 1 or > 100
            || value.Scope == "section" && value.Sections.Count != 1)
            throw new InvalidDataException("Invalid translation scope, language or document.");
        var sections = new HashSet<Guid>(); var pages = new HashSet<Guid>(); int chars = 0, runs = 0; bool hasWriting = false;
        foreach (var section in value.Sections) {
            if (section.Id == Guid.Empty || !sections.Add(section.Id) || section.Pages is null || section.Pages.Count == 0)
                throw new InvalidDataException("Missing or duplicate translation section marker.");
            foreach (var page in section.Pages) {
                if (page.Id == Guid.Empty || !pages.Add(page.Id) || page.Runs is null)
                    throw new InvalidDataException("Missing or duplicate translation page marker.");
                var ids = new HashSet<string>();
                foreach (var run in page.Runs) {
                    if (run is null || string.IsNullOrEmpty(run.Id) || run.Id.Length > 120 || !ids.Add(run.Id)
                        || string.IsNullOrEmpty(run.Text) || run.Text.Length > 20000)
                        throw new InvalidDataException("Invalid or duplicate translation text marker.");
                    chars += run.Text.Length; runs++;
                    hasWriting |= !string.IsNullOrWhiteSpace(run.Text);
                    for (int i = 0; i < run.Text.Length; i++) {
                        if (!char.IsSurrogate(run.Text[i])) continue;
                        if (!char.IsHighSurrogate(run.Text[i]) || i + 1 == run.Text.Length || !char.IsLowSurrogate(run.Text[++i]))
                            throw new InvalidDataException("Translation contains incomplete Unicode text.");
                    }
                }
            }
        }
        if (chars > MaxChars || runs > 2000 || pages.Count > 1000)
            throw new InvalidDataException("Translation is too large. Translate a smaller scope (up to 60,000 characters and 2,000 text runs).");
        if (!hasWriting) throw new InvalidDataException("Add writing before translating.");
    }
    public static TranslationStructure Result(string json, TranslationStructure source)
    {
        var result = Parse(json);
        if (result.DocumentId != source.DocumentId || result.Scope != source.Scope || result.TargetLanguage != source.TargetLanguage
            || result.Sections.Count != source.Sections.Count)
            throw new InvalidDataException("Translation language or target does not match the reviewed source.");
        for (int s = 0; s < source.Sections.Count; s++) {
            var before = source.Sections[s]; var after = result.Sections[s];
            if (before.Id != after.Id || before.Pages.Count != after.Pages.Count) throw new InvalidDataException("Missing, reordered or wrong translation section.");
            for (int p = 0; p < before.Pages.Count; p++) {
                var left = before.Pages[p]; var right = after.Pages[p];
                if (left.Id != right.Id || left.Runs.Count != right.Runs.Count) throw new InvalidDataException("Incomplete or reordered translation page.");
                for (int r = 0; r < left.Runs.Count; r++)
                    if (left.Runs[r].Id != right.Runs[r].Id || right.Runs[r].Text.Any(c => c is '\r' or '\n' or '\0')
                        || right.Runs[r].Text.Contains("[[SECTION:", StringComparison.Ordinal)
                        || string.Concat(left.Runs[r].Text.TakeWhile(char.IsWhiteSpace)) != string.Concat(right.Runs[r].Text.TakeWhile(char.IsWhiteSpace))
                        || string.Concat(left.Runs[r].Text.Reverse().TakeWhile(char.IsWhiteSpace)) != string.Concat(right.Runs[r].Text.Reverse().TakeWhile(char.IsWhiteSpace)))
                        throw new InvalidDataException("Translation changed a text marker or introduced unsupported structure.");
            }
        }
        return result;
    }
}
