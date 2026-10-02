using System.Text.Json;

namespace WriterApp.Application.Documents;

public static class SceneCardAiProposalParser
{
    public static bool TryParse(
        string? json,
        out SectionSceneCardProposalDto? proposal,
        out string? explanation)
    {
        proposal = null;
        explanation = null;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(UnwrapCodeFence(json));
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            JsonElement payload = TryGetObject(root, "sceneCard", out JsonElement sceneCard)
                ? sceneCard
                : root;

            string? narrativePurpose = GetFirstNullableString(payload, "narrativePurpose", "narrative_purpose");
            string? narrativeRole = GetFirstNullableString(payload, "narrativeRole", "narrative_role");
            string? narrativeIntent = GetFirstNullableString(payload, "narrativeIntent", "narrative_intent");
            string? emotionalBeat = GetFirstNullableString(payload, "emotionalBeat", "emotional_beat");
            string? keyEvents = GetFirstNullableString(payload, "keyEvents", "key_events");
            string? openQuestions = GetFirstNullableString(payload, "openQuestions", "open_questions");
            string? povCharacterId = GetFirstNullableString(payload, "povCharacterId", "povCharacter", "pov_character_id", "pov");
            string? placeId = GetFirstNullableString(payload, "placeId", "settingPlace", "setting", "location", "place_id");
            string? timelineEventId = GetFirstNullableString(payload, "timelineEventId", "timeline_event_id", "eventId");
            string? timeRef = GetFirstNullableString(payload, "timeRef", "timelineMarker", "timeline_marker", "time_ref");
            string? summary = GetFirstNullableString(payload, "summary", "sceneSummary", "scene_summary");
            string? status = GetFirstNullableString(payload, "status", "sceneStatus", "scene_status");
            List<string> tags = GetStringArray(payload, "tags");
            if (tags.Count == 0)
            {
                string? tagsText = GetFirstNullableString(payload, "tags", "tagsCsv", "tagList", "sceneTags");
                if (!string.IsNullOrWhiteSpace(tagsText))
                {
                    tags = ParseCsvList(tagsText);
                }
            }
            List<string> subplotTags = GetStringArray(payload, "subplotTags");
            if (subplotTags.Count == 0)
            {
                subplotTags = GetStringArray(payload, "subplot_tags");
            }
            if (subplotTags.Count == 0)
            {
                string? subplotTagsText = GetFirstNullableString(payload, "subplotTags", "subplot_tags", "subplotTagsCsv", "subplotTagList");
                if (!string.IsNullOrWhiteSpace(subplotTagsText))
                {
                    subplotTags = ParseCsvList(subplotTagsText);
                }
            }

            List<SceneCardReferenceDto> references = GetReferenceArray(payload, "references");
            explanation = GetFirstNullableString(root, "explanation", "reasoning", "summary");

            string? normalizedRole = NormalizeNarrativeRole(narrativeRole)
                ?? NormalizeNarrativeRole(narrativePurpose);
            string? normalizedIntent = NormalizeNarrativeIntent(SceneCardAiTextNormalizer.NormalizeAiText(narrativeIntent));
            if (normalizedRole is null && normalizedIntent is null)
            {
                normalizedIntent = NormalizeLegacyNarrativeIntent(SceneCardAiTextNormalizer.NormalizeAiText(narrativePurpose));
            }

            if (new[] { narrativePurpose, narrativeRole, narrativeIntent, emotionalBeat, keyEvents, openQuestions, summary }
                .All(string.IsNullOrWhiteSpace)) return false;

            proposal = new SectionSceneCardProposalDto(
                SceneNarrativeRoleCatalog.ToLegacyPurpose(normalizedRole, normalizedIntent),
                SceneCardAiTextNormalizer.NormalizeAiText(emotionalBeat),
                SceneCardAiTextNormalizer.NormalizeAiText(keyEvents),
                SceneCardAiTextNormalizer.NormalizeAiText(openQuestions),
                SceneCardAiTextNormalizer.NormalizeAiText(povCharacterId),
                SceneCardAiTextNormalizer.NormalizeAiText(placeId),
                SceneCardAiTextNormalizer.NormalizeAiText(timelineEventId),
                SceneCardAiTextNormalizer.NormalizeAiText(timeRef),
                SceneCardAiTextNormalizer.NormalizeAiTextList(tags),
                references,
                SceneCardAiTextNormalizer.NormalizeAiText(summary),
                status is null ? null : NormalizeSceneCardStatus(status),
                SceneCardAiTextNormalizer.NormalizeAiTextList(subplotTags),
                normalizedRole,
                normalizedIntent);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? GetNullableString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    private static string? GetFirstNullableString(JsonElement element, params string[] propertyNames)
    {
        foreach (string propertyName in propertyNames)
        {
            string? value = GetNullableString(element, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static List<string> GetStringArray(JsonElement element, string propertyName)
    {
        List<string> values = new();
        if (element.ValueKind != JsonValueKind.Object
            || !TryGetPropertyIgnoreCase(element, propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                string? text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text.Trim());
                }
            }
        }

        return values;
    }

    private static List<SceneCardReferenceDto> GetReferenceArray(JsonElement element, string propertyName)
    {
        List<SceneCardReferenceDto> values = new();
        if (element.ValueKind != JsonValueKind.Object
            || !TryGetPropertyIgnoreCase(element, propertyName, out JsonElement value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return values;
        }

        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? kind = GetNullableString(item, "kind");
            string? targetId = GetNullableString(item, "targetId");
            if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(targetId))
            {
                continue;
            }

            values.Add(new SceneCardReferenceDto(kind.Trim(), targetId.Trim(), GetNullableString(item, "note")));
        }

        return values;
    }

    private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
            && TryGetPropertyIgnoreCase(element, propertyName, out value)
            && value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static List<string> ParseCsvList(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new List<string>();
        }

        return text
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? NormalizeNarrativeRole(string? value)
    {
        return SceneNarrativeRoleCatalog.TryNormalize(value, out string? normalizedRole)
            ? normalizedRole
            : null;
    }

    private static string? NormalizeNarrativeIntent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length > 1000 ? trimmed[..1000].Trim() : trimmed;
    }

    private static string? NormalizeLegacyNarrativeIntent(string? legacyNarrativePurpose)
    {
        return NormalizeNarrativeRole(legacyNarrativePurpose) is null
            ? NormalizeNarrativeIntent(legacyNarrativePurpose)
            : null;
    }

    private static string NormalizeSceneCardStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Draft";
        }

        string trimmed = status.Trim();
        if (string.Equals(trimmed, "Idea", StringComparison.OrdinalIgnoreCase))
        {
            return "Idea";
        }

        if (string.Equals(trimmed, "Revised", StringComparison.OrdinalIgnoreCase))
        {
            return "Revised";
        }

        if (string.Equals(trimmed, "Final", StringComparison.OrdinalIgnoreCase))
        {
            return "Final";
        }

        return "Draft";
    }

    private static string UnwrapCodeFence(string json)
    {
        string trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal)) return trimmed;
        int newline = trimmed.IndexOf('\n');
        int end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return newline >= 0 && end > newline ? trimmed[(newline + 1)..end].Trim() : trimmed;
    }
}
