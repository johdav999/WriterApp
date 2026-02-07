using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WriterApp.Application.Continuity
{
    public sealed record ContinuityEvidence(Guid? SectionId, string Quote);

    public sealed record CharacterFact(string Fact, ContinuityEvidence Evidence);

    public sealed record CharacterEntry(string Name, IReadOnlyList<CharacterFact> Facts, IReadOnlyList<string> Traits);

    public sealed record CharacterBible(string SchemaVersion, IReadOnlyList<CharacterEntry> Characters);

    public sealed record PlaceFact(string Fact, ContinuityEvidence Evidence);

    public sealed record PlaceEntry(string Name, IReadOnlyList<PlaceFact> Facts);

    public sealed record PlaceBible(string SchemaVersion, IReadOnlyList<PlaceEntry> Places);

    public sealed record ContinuityAnchor(int PlainTextStart, int PlainTextLength);

    public sealed record ContinuityIssue(
        string Severity,
        string Type,
        string Message,
        ContinuityEvidence Evidence,
        string SuggestedFix,
        ContinuityAnchor Anchor);

    public sealed record ContinuityReport(string SchemaVersion, IReadOnlyList<ContinuityIssue> Issues);

    public static class ContinuityJson
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static bool TryParseCharacterBible(string? json, out CharacterBible? bible)
        {
            bible = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                CharacterBible? parsed = JsonSerializer.Deserialize<CharacterBible>(json, JsonOptions);
                if (parsed is null)
                {
                    return false;
                }

                bible = parsed with
                {
                    SchemaVersion = string.IsNullOrWhiteSpace(parsed.SchemaVersion) ? "1.0" : parsed.SchemaVersion.Trim()
                };
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryParsePlaceBible(string? json, out PlaceBible? bible)
        {
            bible = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                PlaceBible? parsed = JsonSerializer.Deserialize<PlaceBible>(json, JsonOptions);
                if (parsed is null)
                {
                    return false;
                }

                bible = parsed with
                {
                    SchemaVersion = string.IsNullOrWhiteSpace(parsed.SchemaVersion) ? "1.0" : parsed.SchemaVersion.Trim()
                };
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static bool TryParseContinuityReport(string? json, out ContinuityReport? report)
        {
            report = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                ContinuityReport? parsed = JsonSerializer.Deserialize<ContinuityReport>(json, JsonOptions);
                if (parsed is null)
                {
                    return false;
                }

                List<ContinuityIssue> normalized = new();
                if (parsed.Issues is not null)
                {
                    foreach (ContinuityIssue issue in parsed.Issues)
                    {
                        ContinuityAnchor anchor = NormalizeAnchor(issue.Anchor, int.MaxValue);
                        normalized.Add(issue with
                        {
                            Severity = NormalizeSeverity(issue.Severity),
                            Anchor = anchor
                        });
                    }
                }

                report = new ContinuityReport(
                    string.IsNullOrWhiteSpace(parsed.SchemaVersion) ? "1.0" : parsed.SchemaVersion.Trim(),
                    normalized);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        public static ContinuityAnchor NormalizeAnchor(ContinuityAnchor anchor, int plainTextLength)
        {
            int max = Math.Max(0, plainTextLength);
            int start = Math.Clamp(anchor.PlainTextStart, 0, max);
            int length = Math.Max(0, anchor.PlainTextLength);
            if (start + length > max)
            {
                length = Math.Max(0, max - start);
            }

            return new ContinuityAnchor(start, length);
        }

        public static string ToJson<T>(T model)
        {
            return JsonSerializer.Serialize(model, JsonOptions);
        }

        private static string NormalizeSeverity(string? severity)
        {
            string value = severity?.Trim().ToLowerInvariant() ?? "medium";
            return value is "low" or "medium" or "high" or "critical" ? value : "medium";
        }
    }
}
