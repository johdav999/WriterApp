using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WriterApp.Shared;

public sealed record ConsistencySource(Guid SectionId, string Title, string Text);
public sealed record ConsistencyComparison(Guid SectionId, string Quote);
public sealed record ConsistencySourceContext(IReadOnlyList<ConsistencySource> Sections, int OmittedSections);
public sealed record ConsistencyCoverage(int ComparedSections, int OmittedSections);

public static class ConsistencyChecks
{
    public const int MaxContextCharacters = 200_000;
    public static ConsistencySourceContext Sources(IEnumerable<ConsistencySource> sections, Guid target)
    {
        var ordered = sections.ToArray();
        if (ordered.Any(s => s.SectionId == Guid.Empty || s.Text is null || s.Title is null)
            || ordered.Select(s => s.SectionId).Distinct().Count() != ordered.Length || !ordered.Any(s => s.SectionId == target))
            throw new InvalidDataException("Invalid consistency source sections.");
        var included = new List<ConsistencySource>(); int length = 0;
        foreach (var section in ordered.OrderBy(s => s.SectionId == target ? 0 : 1))
        {
            // Keep whole passages and expose the limit; never silently truncate evidence.
            int size = JsonSerializer.Serialize(section).Length;
            if (length + size > MaxContextCharacters) {
                if (section.SectionId == target) throw new InvalidDataException("Choose a smaller scene for consistency checking.");
                continue;
            }
            included.Add(section); length += size;
        }
        return new(included, ordered.Length - included.Count);
    }

    public static string DecisionKey(Guid section, string quote, ConsistencyComparison? comparison, string type)
    {
        if (section == Guid.Empty || string.IsNullOrWhiteSpace(quote) || quote.Length > 100_000
            || comparison is { SectionId: var id } && (id == Guid.Empty || string.IsNullOrWhiteSpace(comparison.Quote) || comparison.Quote.Length > 100_000))
            throw new InvalidDataException("A decision needs valid quoted passages.");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { section, quote, comparison, type }))));
    }

    public static bool Contains(ConsistencySourceContext context, ConsistencyComparison comparison) =>
        comparison.SectionId != Guid.Empty && !string.IsNullOrWhiteSpace(comparison.Quote)
        && context.Sections.Any(s => s.SectionId == comparison.SectionId && s.Text.Contains(comparison.Quote, StringComparison.Ordinal));

    public static ConsistencyComparison? Comparison(string json, int index)
    {
        using var parsed = JsonDocument.Parse(json);
        var issues = parsed.RootElement.GetProperty("issues");
        if (index < 0 || index >= issues.GetArrayLength()) throw new InvalidDataException("Finding unavailable.");
        if (!issues[index].TryGetProperty("comparisonEvidence", out var evidence) || evidence.ValueKind == JsonValueKind.Null) return null;
        if (evidence.ValueKind != JsonValueKind.Object || !Guid.TryParse(evidence.GetProperty("sectionId").GetString(), out var section)
            || section == Guid.Empty || evidence.GetProperty("quote").GetString() is not { Length: > 0 and <= 100_000 } quote)
            throw new InvalidDataException("Invalid conflicting passage.");
        return new(section, quote);
    }

    public static string WithVerifiedCoverage(string json, ConsistencySourceContext sources, Guid target)
    {
        if (json.Length > 100_000) throw new InvalidDataException("Consistency response exceeds the supported size.");
        var root = System.Text.Json.Nodes.JsonNode.Parse(json) as System.Text.Json.Nodes.JsonObject
            ?? throw new InvalidDataException("Invalid consistency response.");
        if (root["schemaVersion"]?.ToString() != "1.0" || root["issues"] is not System.Text.Json.Nodes.JsonArray issues || issues.Count > 200)
            throw new InvalidDataException("Invalid consistency response.");
        for (int i = 0; i < issues.Count; i++)
        {
            string? kind = issues[i]?["fixKind"]?.ToString();
            if (kind is not (null or "replace" or "delete") || kind == "delete" && issues[i]?["suggestedFix"]?.ToString() != "")
                throw new InvalidDataException("Invalid consistency edit operation.");
            if (Comparison(json, i) is not { } comparison)
                throw new InvalidDataException("A new consistency finding needs both quoted passages.");
            if (!Contains(sources, comparison) || issues[i]?["evidence"] is not { } primary
                || !Guid.TryParse(primary["sectionId"]?.ToString(), out var section) || section != target
                || !Contains(sources, new(section, primary["quote"]?.ToString() ?? "")))
                throw new InvalidDataException("The consistency response quotes a passage that is not in the analyzed writing. Run the check again.");
        }
        root["coverage"] = new System.Text.Json.Nodes.JsonObject {
            ["comparedSections"] = sources.Sections.Count, ["omittedSections"] = sources.OmittedSections
        };
        return root.ToJsonString();
    }
}
