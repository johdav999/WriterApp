using System.Text.Json;

namespace WriterApp.Application.Documents;

/// <summary>The same approved-field contract as desktop coaching, adapted to web card DTOs.</summary>
public static class SceneCardApprovals
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static IReadOnlyList<SceneFieldChange> Changes(SectionSceneCardProposalDto before,
        SectionSceneCardProposalDto proposed, string? focus = null)
    {
        proposed = Normalize(proposed);
        var original = JsonSerializer.SerializeToElement(before, Json);
        var candidate = JsonSerializer.SerializeToElement(proposed, Json);
        return Enum.GetValues<SceneCoachingField>()
            .Where(f => focus is null || SceneCoaching.Key(f) == focus)
            .Where(f => candidate.TryGetProperty(SceneCoaching.Key(f), out var value) && Nonempty(value)
                && value.GetRawText() != original.GetProperty(SceneCoaching.Key(f)).GetRawText())
            .Select(f => new SceneFieldChange(f, Display(original.GetProperty(SceneCoaching.Key(f))),
                Display(candidate.GetProperty(SceneCoaching.Key(f))), Error(f,candidate.GetProperty(SceneCoaching.Key(f))))).ToArray();
    }

    public static void Require(IReadOnlyList<SceneCoachingField> approved, IReadOnlyList<SceneFieldChange> changes)
    {
        if (approved.Count == 0 || approved.Distinct().Count() != approved.Count
            || approved.Any(f => !Enum.IsDefined(f) || !changes.Any(c => c.Field == f && c.Error is null)))
            throw new InvalidDataException("Approve at least one changed field from this review.");
    }

    // Unapproved provider values are absent in both the save and its immutable history intent.
    public static SceneCardUpdateRequest Request(SectionSceneCardProposalDto proposed,
        IReadOnlyList<SceneCoachingField> approved, string fingerprint)
    {
        proposed = Normalize(proposed);
        var values = JsonSerializer.SerializeToNode(proposed, Json)!.AsObject();
        foreach (var field in Enum.GetValues<SceneCoachingField>())
            if (!approved.Contains(field)) values[SceneCoaching.Key(field)] = null;
        var request = values.Deserialize<SceneCardUpdateRequest>(Json)!;
        return request with { ExpectedCardFingerprint = fingerprint, ApprovedFields = approved.ToArray() };
    }

    // Persistence uses these names to preserve raw stored values, including JSON and whitespace.
    public static string Property(SceneCoachingField field) => field is SceneCoachingField.Tags
        or SceneCoachingField.SubplotTags or SceneCoachingField.References ? field + "Json" : field.ToString();
    public static bool Writes(IReadOnlyList<SceneCoachingField>? fields, SceneCoachingField field) => fields is null
        || fields.Contains(field);
    private static SectionSceneCardProposalDto Normalize(SectionSceneCardProposalDto proposal) {
        // Legacy purpose is a projection of role/intent, rather than another editable web field.
        if(string.IsNullOrWhiteSpace(proposal.NarrativeRole) && string.IsNullOrWhiteSpace(proposal.NarrativeIntent)
            && !string.IsNullOrWhiteSpace(proposal.NarrativePurpose)) {
            proposal=SceneNarrativeRoleCatalog.TryNormalize(proposal.NarrativePurpose,out var role)
                ? proposal with {NarrativeRole=role} : proposal with {NarrativeIntent=proposal.NarrativePurpose};
        }
        return proposal with {NarrativePurpose=null};
    }
    private static string? Error(SceneCoachingField field,JsonElement value) {
        if(value.ValueKind==JsonValueKind.String) {
            string text=value.GetString()!;
            int max=field==SceneCoachingField.TimeRef?120:field==SceneCoachingField.NarrativeIntent?1000:20_000;
            if(text.Length>max)return $"{SceneCoaching.Label(field)} exceeds {max} characters.";
            if(field==SceneCoachingField.Status && !new[]{"Idea","Draft","Revised","Final"}.Contains(text,StringComparer.OrdinalIgnoreCase))return "Choose a supported scene status.";
            if(field==SceneCoachingField.NarrativeRole && !SceneNarrativeRoleCatalog.TryNormalize(text,out _))return "Choose a supported narrative role.";
        }
        if(field is SceneCoachingField.Tags or SceneCoachingField.SubplotTags && (value.GetArrayLength()>30 || value.EnumerateArray().Any(v=>v.ValueKind!=JsonValueKind.String || string.IsNullOrWhiteSpace(v.GetString()) || v.GetString()!.Length>30)))
            return "Use up to 30 non-empty tags of 30 characters.";
        return null;
    }
    private static bool Nonempty(JsonElement value) => value.ValueKind == JsonValueKind.String
        ? !string.IsNullOrWhiteSpace(value.GetString()) : value.ValueKind == JsonValueKind.Array && value.GetArrayLength() > 0;
    private static string Display(JsonElement value) => value.ValueKind switch {
        JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()) ? "Not set" : value.GetString()!,
        JsonValueKind.Array => value.GetArrayLength() == 0 ? "Not set" : string.Join(", ", value.EnumerateArray().Select(v => v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())),
        _ => "Not set"
    };
}
