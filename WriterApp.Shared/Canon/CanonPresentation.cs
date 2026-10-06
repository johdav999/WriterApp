using System.Globalization;
using System.Text.RegularExpressions;

namespace WriterApp.Shared.Canon;

/// <summary>Readable labels and references without changing stored canon or inventing links.</summary>
public static class CanonPresentation
{
    public static string Heading(CanonKind kind) => kind switch
    {
        CanonKind.Character => "Characters", CanonKind.Place => "Places", _ => "Timeline"
    };

    public static string Label(string field) => field switch
    {
        "timeRef" => "When", "order" => "Event number", "locationId" => "Where",
        "participants" => "Who is there", "sectionId" => "Source scene", "characterId" => "Character",
        "placeId" => "Place", "eventId" => "Event", "relationships" => "Relationships",
        "lastUpdatedUtc" => "Last updated", "constraints" => "Timing notes",
        _ => Humanize(field)
    };

    public static string Reference(CanonKind kind, string reference, IReadOnlyDictionary<CanonKind, CanonContent>? contents)
    {
        var matches = contents?.GetValueOrDefault(kind)?.Entries
            .Where(e => e.Id == reference || e.Name == reference).ToArray();
        if (matches is { Length: 1 }) return matches[0].Name;
        return ReadableReference(reference) + " (not linked)";
    }

    public static string ReadableReference(string reference)
    {
        // Old AI responses can contain codes with no matching saved entry.
        // A readable fallback describes the code; it does not validate the link.
        if (Guid.TryParse(reference, out _) || Regex.IsMatch(reference, "^(chr|char|character|loc|plc|place|evt|event)[_-]?[0-9]+$", RegexOptions.IgnoreCase))
            return "Unknown name";
        string text = Regex.Replace(reference, "^(chr|char|character|loc|plc|place|evt|event)[_-]", "", RegexOptions.IgnoreCase);
        return CultureInfo.GetCultureInfo("en").TextInfo.ToTitleCase(Humanize(text));
    }

    private static string Humanize(string text)
    {
        text = Regex.Replace(text, "([a-z])([A-Z])", "$1 $2").Replace('_', ' ').Replace('-', ' ');
        return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];
    }
}
