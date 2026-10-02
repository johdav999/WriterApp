using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WriterApp.Shared.Editor;

/// <summary>Versioned common content vocabulary for device editing and sync validation.</summary>
public static class EditorContentContract
{
    public static JsonElement Definition { get; } = Load();
    public static IReadOnlySet<string> Tags { get; } = Definition.GetProperty("tags").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
    private static JsonElement Load()
    {
        using var stream = typeof(EditorContentContract).Assembly.GetManifestResourceStream("WriterApp.ContentContract")!;
        using var json = JsonDocument.Parse(stream);
        return json.RootElement.Clone();
    }
    public static bool SafeLink(string href) => Uri.TryCreate(href.Trim(), UriKind.Absolute, out var uri)
        && Definition.GetProperty("linkSchemes").EnumerateArray().Any(s => s.GetString() == uri.Scheme) && !href.Any(char.IsControl);
    public static bool SafeImage(string source) => !source.Any(char.IsControl) &&
        (Regex.IsMatch(source, @"^data:image/(png|jpeg|gif|webp);base64,[A-Za-z0-9+/]+={0,2}$")
         || source.StartsWith('/') && !source.StartsWith("//") && !source.Contains('\\')
         || Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo));

    public static bool SafeStyle(string tag, string value) => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).All(part =>
    {
        string[] pair = part.Split(':', 2, StringSplitOptions.TrimEntries);
        if (pair.Length != 2) return false;
        string property = pair[0].ToLowerInvariant(), setting = pair[1].ToLowerInvariant();
        if (tag is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6")
            return property == "text-align" && setting is "left" or "center" or "right" or "justify"
                || property == "margin-left" && Regex.IsMatch(setting, @"^(0|2|4|6|8|10|12|14|16)em$");
        if (tag is "td" or "th") return property == "text-align" && setting is "left" or "center" or "right" or "justify";
        return tag is "table" or "col" && property is "width" or "min-width" && Regex.IsMatch(setting, @"^[0-9]+(\.[0-9]+)?px$");
    });

    public static bool AllowedAttribute(string tag, string name, string value) =>
        Definition.GetProperty("attributes").TryGetProperty(tag, out var names) && names.EnumerateArray().Any(n => n.GetString() == name)
        && (tag != "ol" || Regex.IsMatch(value, @"^-?[0-9]+$") && int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        && (tag != "code" || Regex.IsMatch(value, @"^language-[A-Za-z0-9_-]+$"))
        && (name != "href" || SafeLink(value))
        && (name is not ("src" or "data-asset-url") || SafeImage(value))
        && (name != "style" || SafeStyle(tag, value))
        && (name != "data-indent-level" || Regex.IsMatch(value, @"^[0-8]$"))
        && (name is not ("colspan" or "rowspan" or "width") || int.TryParse(value, out int size) && size is > 0 and <= 10000)
        && (name != "colwidth" || value.Split(',').All(part => int.TryParse(part, out int width) && width is >= 0 and <= 10000))
        && (name != "align" || value is "left" or "center" or "right" or "justify");
}
