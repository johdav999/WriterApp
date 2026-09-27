using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public enum DeviceExportFormat { Html, Text }
public enum DeviceImportMode { Append, Replace }
public sealed record DevicePreparedImport(string Title, string Html);
public sealed record DevicePreparedExport(string SuggestedFileName, string Extension, byte[] Content);

/// <summary>Restricted local formats that both the device editor and an offline export can represent.</summary>
public static class DeviceDocumentFormats
{
    public const int MaxImportBytes = 5 * 1024 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly Regex RepeatedNewlines = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedTags = new(StringComparer.OrdinalIgnoreCase)
    { "p", "h1", "h2", "h3", "strong", "b", "em", "i", "s", "del", "code", "pre",
      "blockquote", "ul", "ol", "li", "br", "hr", "a" };
    private static readonly HashSet<string> DropTags = new(StringComparer.OrdinalIgnoreCase)
    { "script", "style", "iframe", "object", "embed", "svg", "math", "img", "picture", "video",
      "audio", "canvas", "form", "input", "button", "link", "meta", "base", "template", "noscript" };
    private static readonly HashSet<string> WindowsReservedNames = new(StringComparer.OrdinalIgnoreCase)
    { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$", "COM1", "COM2", "COM3", "COM4", "COM5",
      "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

    public static DevicePreparedImport ParseImport(DeviceImportFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".txt" or ".html" or ".htm"))
            throw new InvalidDataException("Choose a .txt, .html, or .htm file.");
        if (file.Content.Length == 0 || file.Content.Length > MaxImportBytes)
            throw new InvalidDataException("The import file must be nonempty and at most 5 MB.");
        string source;
        try { source = Utf8.GetString(file.Content).TrimStart('\uFEFF'); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("The import file must use UTF-8 text.", error); }
        if (source.IndexOf('\0') >= 0) throw new InvalidDataException("The import file contains binary data.");
        string html = extension == ".txt" ? TextToHtml(source) : SanitizeHtml(source);
        if (string.IsNullOrWhiteSpace(HtmlToText(html)))
            throw new InvalidDataException("The import file has no readable writing.");
        string title = SafeBaseName(Path.GetFileNameWithoutExtension(file.FileName), "Imported document");
        return new(title, html);
    }

    public static string MergePage(LocalPage page, string importedHtml, DeviceImportMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == DeviceImportMode.Replace) return importedHtml;
        string existing = page.ContentFormat switch
        {
            LocalContentFormat.Html => page.Content,
            LocalContentFormat.LegacyText => TextToHtml(page.Content),
            _ => throw new InvalidDataException("Open and save this legacy page before appending an import.")
        };
        EnsureSafeExistingHtml(existing);
        return string.IsNullOrWhiteSpace(HtmlToText(existing)) ? importedHtml : existing + "<p></p>" + importedHtml;
    }

    public static DevicePreparedExport Export(LocalDocument document, DeviceExportFormat format)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        var sections = document.Sections.OrderBy(s => s.OrderIndex).ToArray();
        string baseName = SafeBaseName(document.Title, "document");
        if (format == DeviceExportFormat.Text)
        {
            StringBuilder text = new();
            text.AppendLine(document.Title).AppendLine();
            foreach (LocalSection section in sections)
            {
                text.AppendLine(section.Title).AppendLine();
                foreach (LocalPage page in section.Pages.OrderBy(p => p.OrderIndex))
                    text.AppendLine(PageText(page)).AppendLine();
            }
            return new(baseName + ".txt", ".txt", Utf8.GetBytes(text.ToString().TrimEnd() + "\n"));
        }
        StringBuilder html = new();
        html.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">")
            .Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'\">")
            .Append("<title>").Append(WebUtility.HtmlEncode(document.Title)).Append("</title>")
            .Append("<style>body{max-width:48rem;margin:3rem auto;padding:0 1rem;font:1.1rem/1.65 Georgia,serif;color:#211c30}")
            .Append("h1,h2,h3{line-height:1.2}section{margin-top:2.5rem}article{margin-top:1.5rem}pre{white-space:pre-wrap}</style>")
            .Append("</head><body><h1>").Append(WebUtility.HtmlEncode(document.Title)).Append("</h1>");
        foreach (LocalSection section in sections)
        {
            html.Append("<section><h2>").Append(WebUtility.HtmlEncode(section.Title)).Append("</h2>");
            foreach (LocalPage page in section.Pages.OrderBy(p => p.OrderIndex))
            {
                html.Append("<article><h3>").Append(WebUtility.HtmlEncode(page.Title)).Append("</h3>")
                    .Append(PageHtml(page)).Append("</article>");
            }
            html.Append("</section>");
        }
        html.Append("</body></html>");
        return new(baseName + ".html", ".html", Utf8.GetBytes(html.ToString()));
    }

    public static string SafeFileName(string title, string extension) =>
        SafeBaseName(title, "document") + (extension is ".txt" or ".html" ? extension : throw new ArgumentException("Unsupported extension.", nameof(extension)));

    private static string SafeBaseName(string? title, string fallback)
    {
        string cleaned = new((title ?? "").Where(c => c >= ' ' && c is not ('<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*')).ToArray());
        cleaned = cleaned.Trim().TrimEnd('.', ' ');
        if (cleaned.Length > 80) cleaned = cleaned[..80].TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(cleaned) || IsReservedWindowsName(cleaned)) cleaned = fallback;
        return cleaned;
    }
    private static bool IsReservedWindowsName(string name) => WindowsReservedNames.Contains(name.Split('.')[0]);

    private static string TextToHtml(string text) => string.Concat(text.Replace("\r\n", "\n").Replace('\r', '\n')
        .Split('\n').Select(line => "<p>" + WebUtility.HtmlEncode(line) + "</p>"));

    private static string PageHtml(LocalPage page) => page.ContentFormat switch
    {
        LocalContentFormat.Html => SanitizeHtml(page.Content),
        LocalContentFormat.LegacyText => TextToHtml(page.Content),
        _ => throw new InvalidDataException("Open and save legacy JSON pages before exporting this document.")
    };
    private static string PageText(LocalPage page) => page.ContentFormat switch
    {
        LocalContentFormat.Html => HtmlToText(SanitizeHtml(page.Content)),
        LocalContentFormat.LegacyText => page.Content,
        _ => throw new InvalidDataException("Open and save legacy JSON pages before exporting this document.")
    };

    public static string SanitizeHtml(string source)
    {
        var document = new HtmlParser().ParseDocument(source);
        if (document.Body is null) return "";
        StringBuilder builder = new();
        foreach (INode node in document.Body.ChildNodes) AppendSafe(node, builder);
        return builder.ToString();
    }
    private static void AppendSafe(INode node, StringBuilder builder)
    {
        if (node is IText text) { builder.Append(WebUtility.HtmlEncode(text.Data)); return; }
        if (node is not IElement element) return;
        string tag = element.LocalName.ToLowerInvariant();
        if (DropTags.Contains(tag)) return;
        if (tag == "a" && SafeLink(element.GetAttribute("href") ?? "") is null)
        { foreach (INode child in element.ChildNodes) AppendSafe(child, builder); return; }
        if (!AllowedTags.Contains(tag))
        { foreach (INode child in element.ChildNodes) AppendSafe(child, builder); return; }
        builder.Append('<').Append(tag);
        if (tag == "a" && SafeLink(element.GetAttribute("href") ?? "") is { } href)
            builder.Append(" href=\"").Append(WebUtility.HtmlEncode(href)).Append('"');
        builder.Append('>');
        if (tag is "br" or "hr") return;
        foreach (INode child in element.ChildNodes) AppendSafe(child, builder);
        builder.Append("</").Append(tag).Append('>');
    }
    private static string? SafeLink(string href) => Uri.TryCreate(href.Trim(), UriKind.Absolute, out Uri? uri)
        && uri.Scheme is "http" or "https" or "mailto" ? href.Trim() : null;

    private static void EnsureSafeExistingHtml(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        if (document.Body is null) throw new InvalidDataException("The existing page cannot be imported into safely.");
        foreach (IElement element in document.Body.QuerySelectorAll("*"))
        {
            string tag = element.LocalName.ToLowerInvariant();
            if (!AllowedTags.Contains(tag) || element.Attributes.Any(a => !AllowedExistingAttribute(tag, a.Name, a.Value))
                || tag == "a" && element.HasAttribute("href") && SafeLink(element.GetAttribute("href") ?? "") is null)
                throw new InvalidDataException("The existing page has unsupported formatting. Open and save it before appending.");
        }
    }
    private static bool AllowedExistingAttribute(string tag, string name, string value) =>
        tag == "a" && name is ("href" or "target" or "rel" or "class")
        || tag == "ol" && name == "start" && int.TryParse(value, out _)
        || tag == "code" && name == "class" && Regex.IsMatch(value, @"^language-[\w-]+$");
    private static string HtmlToText(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        if (document.Body is null) return "";
        StringBuilder builder = new();
        foreach (INode node in document.Body.ChildNodes) AppendText(node, builder);
        return RepeatedNewlines.Replace(builder.ToString().Replace("\r", ""), "\n\n").Trim();
    }
    private static void AppendText(INode node, StringBuilder builder)
    {
        if (node is IText text) { builder.Append(text.Data); return; }
        if (node is not IElement element) return;
        string tag = element.LocalName.ToLowerInvariant();
        if (DropTags.Contains(tag)) return;
        if (tag == "br") { builder.Append('\n'); return; }
        if (tag == "li") builder.Append("- ");
        foreach (INode child in element.ChildNodes) AppendText(child, builder);
        if (tag is "p" or "h1" or "h2" or "h3" or "li" or "blockquote" or "pre") builder.Append("\n\n");
    }
}
