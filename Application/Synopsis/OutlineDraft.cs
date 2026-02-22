using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Application.Synopsis
{
    public sealed record OutlineDraft(
        string SchemaVersion,
        string Mode,
        IReadOnlyList<OutlineItemDraft> Items);

    public sealed record OutlineItemDraft(
        int Index,
        string Title,
        string Summary,
        string Pov,
        string Setting,
        IReadOnlyList<string> Beats,
        string StoryRole,
        string Notes);

    public static class OutlineDraftParser
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static bool TryParse(string? json, out OutlineDraft? outline)
        {
            outline = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                OutlineDraft? parsed = JsonSerializer.Deserialize<OutlineDraft>(json, JsonOptions);
                if (parsed is null)
                {
                    return false;
                }

                string mode = string.IsNullOrWhiteSpace(parsed.Mode) ? "chapters" : parsed.Mode.Trim().ToLowerInvariant();
                if (!string.Equals(mode, "chapters", StringComparison.Ordinal) && !string.Equals(mode, "scenes", StringComparison.Ordinal))
                {
                    mode = "chapters";
                }

                List<OutlineItemDraft> items = new();
                if (parsed.Items is not null)
                {
                    for (int i = 0; i < parsed.Items.Count; i++)
                    {
                        OutlineItemDraft item = parsed.Items[i];
                        List<string> beats = new();
                        if (item.Beats is not null)
                        {
                            foreach (string beat in item.Beats)
                            {
                                if (string.IsNullOrWhiteSpace(beat))
                                {
                                    continue;
                                }

                                beats.Add(beat.Trim());
                            }
                        }

                        items.Add(new OutlineItemDraft(
                            item.Index <= 0 ? i + 1 : item.Index,
                            string.IsNullOrWhiteSpace(item.Title) ? $"Item {i + 1}" : item.Title.Trim(),
                            item.Summary?.Trim() ?? string.Empty,
                            item.Pov?.Trim() ?? string.Empty,
                            item.Setting?.Trim() ?? string.Empty,
                            beats,
                            item.StoryRole?.Trim() ?? string.Empty,
                            item.Notes?.Trim() ?? string.Empty));
                    }
                }

                outline = new OutlineDraft(
                    string.IsNullOrWhiteSpace(parsed.SchemaVersion) ? "1.0" : parsed.SchemaVersion.Trim(),
                    mode,
                    items);

                return outline.Items.Count > 0;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static string ToCanonicalJson(OutlineDraft outline)
        {
            if (outline is null)
            {
                throw new ArgumentNullException(nameof(outline));
            }

            return JsonSerializer.Serialize(outline, JsonOptions);
        }
    }
}
