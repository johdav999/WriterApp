using System.Text.Json;
using WriterApp.Shared.Sync;

namespace WriterApp.Application.Documents;

public enum SceneCoachingField { Summary, NarrativePurpose, NarrativeRole, NarrativeIntent, EmotionalBeat, KeyEvents, OpenQuestions, Status, PovCharacterId, PlaceId, TimelineEventId, TimeRef, SubplotTags, Tags, References }
public enum SceneEntityKind { Character, Place, Timeline, Scene, Chapter, Section, Part }
public sealed record SceneEntity(SceneEntityKind Kind, string Id, string Name);
public sealed record SceneFieldChange(SceneCoachingField Field, string Before, string After, string? Error = null);
public sealed record SceneCoachingProposal(IReadOnlyDictionary<SceneCoachingField, string> Values, IReadOnlyDictionary<SceneCoachingField, string> Errors);

/// <summary>Presence-aware scene updates. Entity IDs are opaque, case-sensitive IDs, never prose.</summary>
public static class SceneCoaching
{
    public static string Key(SceneCoachingField field) => char.ToLowerInvariant(field.ToString()[0]) + field.ToString()[1..];
    public static string Label(SceneCoachingField field) => field switch {
        SceneCoachingField.PovCharacterId => "POV character", SceneCoachingField.PlaceId => "Setting / place", SceneCoachingField.TimelineEventId => "Timeline event",
        _ => System.Text.RegularExpressions.Regex.Replace(field.ToString(), "([a-z])([A-Z])", "$1 $2") };
    public static string? Value(SyncSceneCard c, SceneCoachingField f) => f switch {
        SceneCoachingField.Summary => c.Summary, SceneCoachingField.NarrativePurpose => c.NarrativePurpose,
        SceneCoachingField.NarrativeRole => c.NarrativeRole, SceneCoachingField.NarrativeIntent => c.NarrativeIntent,
        SceneCoachingField.EmotionalBeat => c.EmotionalBeat, SceneCoachingField.KeyEvents => c.KeyEvents, SceneCoachingField.OpenQuestions => c.OpenQuestions,
        SceneCoachingField.Status => c.Status, SceneCoachingField.PovCharacterId => c.PovCharacterId, SceneCoachingField.PlaceId => c.PlaceId,
        SceneCoachingField.TimelineEventId => c.TimelineEventId, SceneCoachingField.TimeRef => c.TimeRef,
        SceneCoachingField.Tags => c.TagsJson, SceneCoachingField.SubplotTags => c.SubplotTagsJson, SceneCoachingField.References => c.ReferencesJson,
        _ => throw new InvalidDataException("Unknown scene field.") };
    public static SyncSceneCard Set(SyncSceneCard c, SceneCoachingField f, string? v) => f switch {
        SceneCoachingField.Summary => c with { Summary = v }, SceneCoachingField.NarrativePurpose => c with { NarrativePurpose = v },
        SceneCoachingField.NarrativeRole => c with { NarrativeRole = v }, SceneCoachingField.NarrativeIntent => c with { NarrativeIntent = v },
        SceneCoachingField.EmotionalBeat => c with { EmotionalBeat = v }, SceneCoachingField.KeyEvents => c with { KeyEvents = v }, SceneCoachingField.OpenQuestions => c with { OpenQuestions = v },
        SceneCoachingField.Status => c with { Status = v }, SceneCoachingField.PovCharacterId => c with { PovCharacterId = v }, SceneCoachingField.PlaceId => c with { PlaceId = v },
        SceneCoachingField.TimelineEventId => c with { TimelineEventId = v }, SceneCoachingField.TimeRef => c with { TimeRef = v },
        SceneCoachingField.Tags => c with { TagsJson = v }, SceneCoachingField.SubplotTags => c with { SubplotTagsJson = v }, SceneCoachingField.References => c with { ReferencesJson = v },
        _ => throw new InvalidDataException("Unknown scene field.") };

    public static SceneCoachingProposal Parse(string json, IReadOnlyList<SceneEntity> entities)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 100_000) throw new InvalidDataException("Scene proposal is empty or too large.");
        var text = json.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal)) {
            int start = text.IndexOf('\n'), end = text.LastIndexOf("```", StringComparison.Ordinal);
            if (start < 0 || end <= start) throw new InvalidDataException("Invalid scene proposal fence.");
            text = text[(start + 1)..end];
        }
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 16 });
        var root = document.RootElement;
        RequireUnique(root);
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Scene proposal must be an object.");
        var payload = root.EnumerateObject().FirstOrDefault(p => p.Name.Equals("sceneCard", StringComparison.OrdinalIgnoreCase)).Value;
        if (payload.ValueKind == JsonValueKind.Undefined) payload = root;
        if (payload.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid scene card object.");
        var values = new Dictionary<SceneCoachingField, string>(); var errors = new Dictionary<SceneCoachingField, string>();
        var seen = new HashSet<SceneCoachingField>();
        foreach (var p in payload.EnumerateObject()) {
            string key = p.Name.Replace("_", "");
            if (!Enum.TryParse<SceneCoachingField>(key, true, out var field) || !Enum.IsDefined(field) || !field.ToString().Equals(key, StringComparison.OrdinalIgnoreCase)) {
                if (key.Equals("explanation", StringComparison.OrdinalIgnoreCase) || key.Equals("reasoning", StringComparison.OrdinalIgnoreCase)) continue;
                throw new InvalidDataException($"Unknown scene proposal field: {p.Name}.");
            }
            if (!seen.Add(field)) throw new InvalidDataException("Duplicate scene proposal field.");
            if (p.Value.ValueKind == JsonValueKind.Null) continue;
            try {
                string? value = Validate(field, p.Value, entities);
                if (value is not null) values.Add(field, value);
            } catch (InvalidDataException e) { errors.Add(field, e.Message); }
        }
        if (values.Count == 0 && errors.Count == 0) throw new InvalidDataException("No non-empty scene fields were proposed. Authored values are preserved.");
        return new(values, errors);
    }
    private static void RequireUnique(JsonElement e) {
        if (e.ValueKind == JsonValueKind.Object) {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in e.EnumerateObject()) { if (!keys.Add(p.Name)) throw new InvalidDataException("Duplicate scene JSON property."); RequireUnique(p.Value); }
        } else if (e.ValueKind == JsonValueKind.Array) foreach (var item in e.EnumerateArray()) RequireUnique(item);
    }
    private static string? Validate(SceneCoachingField f, JsonElement e, IReadOnlyList<SceneEntity> entities) {
        if (f is SceneCoachingField.Tags or SceneCoachingField.SubplotTags) {
            string[] list = e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().Select(Text).ToArray()
                : e.ValueKind == JsonValueKind.String ? Text(e).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                : throw new InvalidDataException("Tags must be strings or an array of strings.");
            if (list.Length > 100 || list.Any(v => v.Length > 200)) throw new InvalidDataException("Use up to 100 tags of 200 characters.");
            var tags = list.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return tags.Length == 0 ? null : JsonSerializer.Serialize(tags);
        }
        if (f == SceneCoachingField.References) {
            if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() > 100) throw new InvalidDataException("References must be an array of up to 100 typed links.");
            var references = new List<SceneCardReferenceDto>();
            foreach (var r in e.EnumerateArray()) {
                if (r.ValueKind != JsonValueKind.Object || r.EnumerateObject().Any(p => p.Name is not ("kind" or "targetId" or "note"))
                    || !r.TryGetProperty("kind", out var k) || !Enum.TryParse<SceneEntityKind>(Text(k), true, out var kind) || !Enum.IsDefined(kind) || !kind.ToString().Equals(Text(k), StringComparison.OrdinalIgnoreCase)
                    || !r.TryGetProperty("targetId", out var id)) throw new InvalidDataException("Each reference needs a supported kind and exact target ID.");
                string target = Text(id); RequireEntity(entities, kind, target);
                string? note = r.TryGetProperty("note", out var n) && n.ValueKind != JsonValueKind.Null ? Text(n) : null;
                if (note?.Length > 2000) throw new InvalidDataException("Reference note is too long.");
                references.Add(new(kind.ToString().ToLowerInvariant(), target, note));
            }
            return references.Count == 0 ? null : JsonSerializer.Serialize(references);
        }
        string value = Text(e).Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        int max = f == SceneCoachingField.TimeRef ? 120 : f == SceneCoachingField.NarrativeIntent ? 1000 : 20_000;
        if (value.Length > max) throw new InvalidDataException($"{Label(f)} exceeds {max} characters.");
        if (f is SceneCoachingField.PovCharacterId or SceneCoachingField.PlaceId or SceneCoachingField.TimelineEventId) {
            RequireEntity(entities, f == SceneCoachingField.PovCharacterId ? SceneEntityKind.Character : f == SceneCoachingField.PlaceId ? SceneEntityKind.Place : SceneEntityKind.Timeline, value);
            return value;
        }
        if (f == SceneCoachingField.Status) return new[] { "Idea", "Draft", "Revised", "Final" }.SingleOrDefault(s => s.Equals(value, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("Status must be Idea, Draft, Revised or Final.");
        if (f == SceneCoachingField.NarrativeRole) return SceneNarrativeRoleCatalog.TryNormalize(value, out var role) ? role : throw new InvalidDataException("Choose a supported narrative role.");
        return SceneCardAiTextNormalizer.NormalizeAiText(value);
    }
    private static string Text(JsonElement e) => e.ValueKind == JsonValueKind.String ? e.GetString()! : throw new InvalidDataException("Expected a text value; authored data is preserved.");
    private static void RequireEntity(IReadOnlyList<SceneEntity> entities, SceneEntityKind kind, string id) {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 200 || entities.Count(e => e.Kind == kind && e.Id == id) != 1)
            throw new InvalidDataException($"Unresolved {kind.ToString().ToLowerInvariant()} ID '{id}'. Load current project canon; labels and foreign IDs cannot be applied.");
    }
    public static string Display(SceneCoachingField f, string? value, IReadOnlyList<SceneEntity> entities) {
        if (string.IsNullOrWhiteSpace(value)) return "Not set";
        if (f is SceneCoachingField.Tags or SceneCoachingField.SubplotTags) {
            try { return string.Join(", ", JsonSerializer.Deserialize<string[]>(value) ?? []); } catch (JsonException) { return value; }
        }
        if (f == SceneCoachingField.References) {
            try { return string.Join("\n", (JsonSerializer.Deserialize<SceneCardReferenceDto[]>(value) ?? []).Select(r => {
                var entity = entities.FirstOrDefault(e => e.Kind.ToString().Equals(r.Kind, StringComparison.OrdinalIgnoreCase) && e.Id == r.TargetId);
                return $"{r.Kind}: {entity?.Name ?? r.TargetId}" + (string.IsNullOrWhiteSpace(r.Note) ? "" : " — " + r.Note);
            })); } catch (JsonException) { return value; }
        }
        var kind = f == SceneCoachingField.PovCharacterId ? SceneEntityKind.Character : f == SceneCoachingField.PlaceId ? SceneEntityKind.Place : SceneEntityKind.Timeline;
        return f is SceneCoachingField.PovCharacterId or SceneCoachingField.PlaceId or SceneCoachingField.TimelineEventId
            ? entities.FirstOrDefault(e => e.Kind == kind && e.Id == value)?.Name + " (" + value + ")" : value;
    }
}
