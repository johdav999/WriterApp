using System.Text.Json;

namespace WriterApp.Shared.Canon;

public enum CanonKind { Character, Place, Timeline }
public sealed record DeviceBibleRefreshRequest(string ExpectedDocumentVersion, string ExpectedSnapshotVersion,
    bool FullRebuild, Guid? ActiveSectionId, WriterApp.Shared.WebAiSource? WebSource = null);
public sealed record DeviceBibleSnapshot(int ContractVersion, Guid DocumentId, CanonKind Kind,
    string SnapshotVersion, string? SourceDocumentVersion, string CheckedDocumentVersion, string SourceHash,
    DateTimeOffset? RefreshedAt, string ContentJson, int ChangedSections, bool Exists);
public sealed record CanonEntry(string? Id, string Name, IReadOnlyDictionary<string, JsonElement> Details);
public sealed record CanonContent(CanonKind Kind, IReadOnlyList<CanonEntry> Entries)
{
    public static string Collection(CanonKind kind) => kind switch
    { CanonKind.Character => "characters", CanonKind.Place => "places", CanonKind.Timeline => "events", _ => throw new InvalidDataException("Unknown canon type.") };
    public static CanonContent Parse(CanonKind kind, string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 1_000_000) throw new InvalidDataException("Canon is empty or exceeds the supported size.");
        using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        var root = parsed.RootElement;
        ValidatePropertyNames(root);
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var schema)
            || schema.ToString() is not ("1" or "1.0") || !root.TryGetProperty(Collection(kind), out var entries)
            || entries.ValueKind != JsonValueKind.Array || entries.GetArrayLength() > 5000)
            throw new InvalidDataException("Invalid or unsupported canon structure. Previous canon is preserved.");
        var result = new List<CanonEntry>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Invalid canon entry.");
            string? id = Text(entry, "id");
            string? name = Text(entry, kind == CanonKind.Timeline ? "title" : "name");
            if (string.IsNullOrWhiteSpace(name) || name.Length > 1000 || id?.Length > 200
                || (id is not null && (string.IsNullOrWhiteSpace(id) || !ids.Add(id))))
                throw new InvalidDataException("Canon entries need names and unique, valid IDs when supplied.");
            ValidateReferences(entry);
            result.Add(new(id, name, entry.EnumerateObject().Where(p => p.Name is not ("name" or "title" or "id"))
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal)));
        }
        return new(kind, result);
    }
    private static string? Text(JsonElement entry, string key) => entry.TryGetProperty(key, out var value)
        && value.ValueKind != JsonValueKind.Null ? value.ValueKind == JsonValueKind.String ? value.GetString()
        : throw new InvalidDataException("Invalid canon text field.") : null;
    private static void ValidatePropertyNames(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!keys.Add(property.Name)) throw new InvalidDataException("Duplicate canon field.");
                ValidatePropertyNames(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) ValidatePropertyNames(item);
    }
    private static void ValidateReferences(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var p in value.EnumerateObject())
            {
                if (p.Name == "sectionId" && p.Value.ValueKind != JsonValueKind.Null
                    && (p.Value.ValueKind != JsonValueKind.String || !Guid.TryParse(p.Value.GetString(), out var id) || id == Guid.Empty))
                    throw new InvalidDataException("Invalid canon evidence reference.");
                if (p.Name == "locationId" && p.Value.ValueKind != JsonValueKind.Null
                    && (p.Value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(p.Value.GetString())))
                    throw new InvalidDataException("Invalid canon place reference.");
                if (p.Name == "participants" && (p.Value.ValueKind != JsonValueKind.Array
                    || p.Value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(v.GetString()))))
                    throw new InvalidDataException("Invalid canon participant references.");
                ValidateReferences(p.Value);
            }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) ValidateReferences(item);
    }
}

public sealed record DeviceCanonContext(Guid DocumentId, string DocumentVersion,
    IReadOnlyDictionary<CanonKind, DeviceBibleSnapshot> Snapshots)
{
    public IReadOnlyList<string> UnresolvedReferences()
    {
        var names = new Dictionary<CanonKind, HashSet<string>>();
        foreach (var pair in Snapshots)
            names[pair.Key] = CanonContent.Parse(pair.Key, pair.Value.ContentJson).Entries
                .SelectMany(e => e.Id is null ? new[] { e.Name } : new[] { e.Id, e.Name }).ToHashSet(StringComparer.Ordinal);
        var warnings = new List<string>();
        if (Snapshots.TryGetValue(CanonKind.Timeline, out var timeline))
            foreach (var entry in CanonContent.Parse(CanonKind.Timeline, timeline.ContentJson).Entries)
            {
                if (entry.Details.TryGetValue("locationId", out var location) && location.ValueKind == JsonValueKind.String
                    && (!names.TryGetValue(CanonKind.Place, out var places) || !places.Contains(location.GetString()!)))
                    warnings.Add($"{entry.Name}: {CanonPresentation.ReadableReference(location.GetString()!)} is not linked to a saved place.");
                if (entry.Details.TryGetValue("participants", out var participants))
                    foreach (var participant in participants.EnumerateArray())
                        if (!names.TryGetValue(CanonKind.Character, out var characters) || !characters.Contains(participant.GetString()!))
                            warnings.Add($"{entry.Name}: {CanonPresentation.ReadableReference(participant.GetString()!)} is not linked to a saved character.");
            }
        return warnings.Distinct().ToArray();
    }
}
