using System.Net;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WriterApp.Device.Shared.Storage;
using WriterApp.UI.Shared;

namespace WriterApp.Device.Shared.Services;

public sealed record LocalPreviewPage(Guid PageId, string Title, string Html, string? Warning);
public sealed record LocalPreviewSection(Guid SectionId, string Title, IReadOnlyList<LocalPreviewPage> Pages);
public sealed record LocalPreviewSnapshot(Guid DocumentId, long Revision, string Title, IReadOnlyList<LocalPreviewSection> Sections);
public sealed record LocalPreviewHighlight(string Html, int Matches);

public static class LocalDocumentPreview
{
    public static LocalPreviewSnapshot Create(LocalDocument saved)
    {
        if (saved.DeletedAtUtc is not null) throw new InvalidOperationException("Restore the document before previewing it.");
        return new(saved.DocumentId, saved.LocalRevision, saved.Title, saved.Sections.OrderBy(s => s.OrderIndex)
            .Select(s => new LocalPreviewSection(s.SectionId, s.Title, s.Pages.OrderBy(p => p.OrderIndex).Select(Render).ToArray())).ToArray());
    }

    public static LocalPreviewPage Render(LocalPage page)
    {
        if (!DeviceContentCompatibility.CanEdit(page.Content, page.ContentFormat))
            return new(page.PageId, page.Title, "<pre>" + WebUtility.HtmlEncode(PlainText(page)) + "</pre>",
                "Text-only preview: this original contains unsupported formatting or content. Nothing has been converted or saved; export a source backup to preserve the complete original.");
        string html = page.ContentFormat switch
        {
            LocalContentFormat.Html => DeviceDocumentFormats.SanitizeHtml(page.Content, preserveImages: true),
            LocalContentFormat.LegacyText => "<pre>" + WebUtility.HtmlEncode(page.Content) + "</pre>",
            LocalContentFormat.LegacyJson => JsonHtml(page.Content),
            _ => throw new ArgumentOutOfRangeException(nameof(page))
        };
        var doc = new HtmlParser().ParseDocument(html);
        foreach (var link in doc.QuerySelectorAll("a"))
        {
            link.SetAttribute("target", "_blank"); link.SetAttribute("rel", "noopener noreferrer");
        }
        return new(page.PageId, page.Title, doc.Body?.InnerHtml ?? "", null);
    }

    public static string PlainText(LocalPage page)
    {
        if (page.ContentFormat == LocalContentFormat.LegacyText) return page.Content;
        if (page.ContentFormat == LocalContentFormat.LegacyJson)
        {
            try
            {
                using var json = JsonDocument.Parse(page.Content);
                var text = new StringBuilder();
                AppendJsonText(json.RootElement, text, 0);
                return text.ToString();
            }
            catch (JsonException) { return ""; }
        }
        var doc = new HtmlParser().ParseDocument(page.Content);
        return Flatten(doc.Body!, new()).Text;
    }

    public static LocalPreviewHighlight Highlight(string safeHtml, string query)
    {
        var doc = new HtmlParser().ParseDocument(safeHtml);
        var nodes = new List<(IText Node, int Start)>();
        var flattened = Flatten(doc.Body!, nodes);
        var matches = TextSearch.Find(flattened.Text, query, 200);
        foreach (var (node, start) in nodes)
        {
            var overlaps = matches.Where(m => m.Start < start + node.Data.Length && m.Start + m.Length > start).ToArray();
            if (overlaps.Length == 0 || node.Parent is null) continue;
            var parent = node.Parent; int offset = 0;
            foreach (var match in overlaps)
            {
                int from = Math.Max(offset, match.Start - start), to = Math.Min(node.Data.Length, match.Start + match.Length - start);
                if (from > offset) parent.InsertBefore(doc.CreateTextNode(node.Data[offset..from]), node);
                var mark = doc.CreateElement("mark"); mark.TextContent = node.Data[from..to]; parent.InsertBefore(mark, node);
                offset = to;
            }
            if (offset < node.Data.Length) parent.InsertBefore(doc.CreateTextNode(node.Data[offset..]), node);
            parent.RemoveChild(node);
        }
        return new(doc.Body!.InnerHtml, matches.Count);
    }

    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        { "script", "style", "template", "iframe", "object", "svg", "math", "head" };
    private static readonly HashSet<string> Blocks = new(StringComparer.OrdinalIgnoreCase)
        { "p", "div", "h1", "h2", "h3", "h4", "h5", "h6", "li", "blockquote", "pre", "td", "th" };
    private static (string Text, int Nodes) Flatten(INode root, List<(IText Node, int Start)> nodes)
    {
        var text = new StringBuilder(); int count = 0;
        void Append(INode node, int depth)
        {
            if (++count > 100_000 || depth > 128) return;
            if (node is IText value) { nodes.Add((value, text.Length)); text.Append(value.Data); return; }
            if (node is IElement element && Excluded.Contains(element.LocalName)) return;
            foreach (var child in node.ChildNodes) Append(child, depth + 1);
            if (node is IElement block && (Blocks.Contains(block.LocalName) || block.LocalName is "br" or "hr")) text.Append('\n');
        }
        Append(root, 0); return (text.ToString(), count);
    }
    private static void AppendJsonText(JsonElement node, StringBuilder text, int depth)
    {
        if (depth > 128 || node.ValueKind != JsonValueKind.Object) return;
        string? type = node.TryGetProperty("type", out var kind) && kind.ValueKind == JsonValueKind.String ? kind.GetString() : null;
        if (type == "text" && node.TryGetProperty("text", out var value) && value.ValueKind == JsonValueKind.String) text.Append(value.GetString());
        if (node.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
            foreach (var child in content.EnumerateArray()) AppendJsonText(child, text, depth + 1);
        if (type is "paragraph" or "heading" or "hardBreak" or "codeBlock") text.Append('\n');
    }
    private static string JsonHtml(string source)
    {
        using var json = JsonDocument.Parse(source);
        string RenderNode(JsonElement node)
        {
            string type = node.GetProperty("type").GetString()!;
            string content = node.TryGetProperty("content", out var children) ? string.Concat(children.EnumerateArray().Select(RenderNode)) : "";
            if (type == "text")
            {
                content = WebUtility.HtmlEncode(node.GetProperty("text").GetString()) ?? "";
                if (node.TryGetProperty("marks", out var marks)) foreach (var mark in marks.EnumerateArray())
                {
                    string markType = mark.GetProperty("type").GetString()!;
                    string tag = markType switch { "bold" => "strong", "italic" => "em", "strike" => "s", "underline" => "u", "code" => "code", "link" => "a", _ => "span" };
                    string attrs = markType == "link" ? " href=\"" + WebUtility.HtmlEncode(mark.GetProperty("attrs").GetProperty("href").GetString()) + "\"" : "";
                    content = $"<{tag}{attrs}>{content}</{tag}>";
                }
                return content;
            }
            var attributes = node.TryGetProperty("attrs", out var a) ? a : default;
            string HtmlAttributes()
            {
                if (attributes.ValueKind != JsonValueKind.Object) return "";
                var result = new StringBuilder(); var styles = new List<string>();
                void Attribute(string name, string value) => result.Append(' ').Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
                if (type is "paragraph" or "heading")
                {
                    if (attributes.TryGetProperty("textAlign", out var align) && align.ValueKind == JsonValueKind.String) styles.Add("text-align: " + align.GetString());
                    if (attributes.TryGetProperty("indentLevel", out var indent) && indent.GetInt32() > 0)
                    { Attribute("data-indent-level", indent.ToString()); styles.Add($"margin-left: {indent.GetInt32() * 2}em"); }
                }
                if (type is "tableCell" or "tableHeader")
                {
                    foreach (string name in new[] { "colspan", "rowspan", "align" })
                        if (attributes.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null) Attribute(name, value.ToString());
                    if (attributes.TryGetProperty("colwidth", out var widths) && widths.ValueKind == JsonValueKind.Array)
                        Attribute("colwidth", string.Join(',', widths.EnumerateArray().Select(w => w.ToString())));
                }
                if (type == "image") foreach (var property in attributes.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Null) continue;
                    Attribute(property.Name switch { "assetUrl" => "data-asset-url", "assetId" => "data-asset-id", _ => property.Name }, property.Value.ToString());
                }
                if (styles.Count > 0) Attribute("style", string.Join("; ", styles) + ";");
                return result.ToString();
            }
            string htmlAttributes = HtmlAttributes();
            return type switch
            {
                "doc" => content, "paragraph" => $"<p{htmlAttributes}>{content}</p>",
                "heading" => $"<h{attributes.GetProperty("level").GetInt32()}{htmlAttributes}>{content}</h{attributes.GetProperty("level").GetInt32()}>",
                "image" => $"<img{htmlAttributes}>", "table" => $"<table><tbody>{content}</tbody></table>",
                "tableRow" => $"<tr>{content}</tr>", "tableCell" => $"<td{htmlAttributes}>{content}</td>", "tableHeader" => $"<th{htmlAttributes}>{content}</th>",
                "bulletList" => $"<ul>{content}</ul>",
                "orderedList" => $"<ol start=\"{(attributes.ValueKind == JsonValueKind.Object && attributes.TryGetProperty("start", out var start) ? start.GetInt32() : 1)}\">{content}</ol>",
                "listItem" => $"<li>{content}</li>", "blockquote" => $"<blockquote>{content}</blockquote>",
                "hardBreak" => "<br>", "horizontalRule" => "<hr>",
                "codeBlock" => "<pre><code" + (attributes.ValueKind == JsonValueKind.Object && attributes.TryGetProperty("language", out var language) && language.ValueKind == JsonValueKind.String
                    ? " class=\"language-" + WebUtility.HtmlEncode(language.GetString()) + "\"" : "") + ">" + content + "</code></pre>",
                _ => throw new InvalidDataException("Unsupported legacy preview node.")
            };
        }
        return RenderNode(json.RootElement);
    }
}
