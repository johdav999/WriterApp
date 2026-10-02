using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Editor;

namespace WriterApp.Device.Shared.Services;

/// <summary>Conservative editing boundary, never a sanitizer for stored writing.</summary>
public static class DeviceContentCompatibility
{
    private static readonly JsonElement Contract = EditorContentContract.Definition;
    public static IReadOnlySet<string> Tags => EditorContentContract.Tags;
    public const string ReadOnlyReason = "This page uses content or formatting the device editor cannot preserve. Keep the original, export a source backup, or edit it in the web app.";
    private static bool Contains(string list, string value) => Contract.GetProperty(list).EnumerateArray().Any(x => x.GetString() == value);
    private static bool AttributeAllowed(string group, string type, string name) =>
        Contract.GetProperty(group).TryGetProperty(type, out var names) && names.EnumerateArray().Any(x => x.GetString() == name);
    public static bool SafeLink(string href) => EditorContentContract.SafeLink(href);
    public static bool AllowedAttribute(string tag, string name, string value) => EditorContentContract.AllowedAttribute(tag, name, value);

    public static bool CanEdit(string content, LocalContentFormat format)
    {
        try
        {
            if (format == LocalContentFormat.LegacyText) return true;
            if (format == LocalContentFormat.LegacyJson)
            {
                using var json = JsonDocument.Parse(content);
                return json.RootElement.TryGetProperty("type", out var type) && type.GetString() == "doc" && ValidNode(json.RootElement);
            }
            if (format != LocalContentFormat.Html) return false;
            var parser = new HtmlParser(new HtmlParserOptions { IsStrictMode = true });
            var context = new HtmlParser().ParseDocument("").CreateElement("body");
            var fragment = parser.ParseFragment(content, context);
            return fragment.All(n => ValidHtml(n, 0));
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or HtmlParseException or FormatException or ArgumentException)
        { return false; }
    }
    public static void RequireEditable(string content, LocalContentFormat format)
    {
        if (!CanEdit(content, format)) throw new InvalidDataException(ReadOnlyReason);
    }
    private static bool ValidHtml(INode node, int depth)
    {
        if (depth > 128) return false;
        if (node is IText) return true;
        if (node is not IElement element) return false;
        string tag = element.LocalName;
        var children = element.Children;
        if (tag is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" || Contains("inlineTags", tag))
            if (children.Any(c => !Contains("inlineTags", c.LocalName))) return false;
        if (tag is "ul" or "ol" && (children.Any(c => c.LocalName != "li") || element.ChildNodes.OfType<IText>().Any(t => !string.IsNullOrWhiteSpace(t.Data)))) return false;
        if (tag == "li" && element.ParentElement?.LocalName is not ("ul" or "ol")) return false;
        if (tag == "pre" && (children.Length != 1 || children[0].LocalName != "code" || element.ChildNodes.OfType<IText>().Any(t => t.Data.Length != 0))) return false;
        if (tag == "code" && (children.Length != 0 || element.ParentElement is { } parent && Contains("inlineTags", parent.LocalName))) return false;
        if (tag == "code" && element.Attributes.Length > 0 && element.ParentElement?.LocalName != "pre") return false;
        if (tag == "img" && !EditorContentContract.SafeImage(element.GetAttribute("src") ?? "")) return false;
        if (tag == "table" && children.Any(c => c.LocalName is not ("colgroup" or "thead" or "tbody" or "tfoot" or "tr"))) return false;
        if (tag is "thead" or "tbody" or "tfoot" && (element.ParentElement?.LocalName != "table" || children.Any(c => c.LocalName != "tr"))) return false;
        if (tag == "tr" && (element.ParentElement?.LocalName is not ("table" or "thead" or "tbody" or "tfoot") || children.Length == 0 || children.Any(c => c.LocalName is not ("td" or "th")))) return false;
        if (tag is "td" or "th" && element.ParentElement?.LocalName != "tr") return false;
        if (tag == "colgroup" && (element.ParentElement?.LocalName != "table" || children.Any(c => c.LocalName != "col"))) return false;
        if (tag == "col" && element.ParentElement?.LocalName != "colgroup") return false;
        return Tags.Contains(element.LocalName)
            && element.Attributes.All(a => AllowedAttribute(element.LocalName, a.Name, a.Value))
            && element.ChildNodes.All(child => ValidHtml(child, depth + 1));
    }
    private static bool ValidNode(JsonElement node)
    {
        if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty("type", out var typeValue)) return false;
        string type = typeValue.GetString()!;
        if (!Contains("nodes", type) || node.EnumerateObject().Any(p => p.Name is not ("type" or "attrs" or "content" or "marks" or "text"))) return false;
        if (node.TryGetProperty("text", out var text) && (type != "text" || text.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(text.GetString()))) return false;
        if (type == "text" && !node.TryGetProperty("text", out _)) return false;
        if (type == "heading" && (!node.TryGetProperty("attrs", out var headingAttrs) || !headingAttrs.TryGetProperty("level", out _))) return false;
        if (type == "image" && (!node.TryGetProperty("attrs", out var imageAttrs) || !imageAttrs.TryGetProperty("src", out var src) || src.ValueKind != JsonValueKind.String || !EditorContentContract.SafeImage(src.GetString()!))) return false;
        if (node.TryGetProperty("attrs", out var attrs))
        {
            if (attrs.ValueKind != JsonValueKind.Object) return false;
            foreach (var attr in attrs.EnumerateObject())
            {
                if (!AttributeAllowed("nodeAttributes", type, attr.Name)) return false;
                if (attr.Name == "level" && (!attr.Value.TryGetInt32(out int level) || level is < 1 or > 6)) return false;
                if (attr.Name == "start" && !attr.Value.TryGetInt32(out _)) return false;
                if (attr.Name == "language" && attr.Value.ValueKind != JsonValueKind.Null && (attr.Value.ValueKind != JsonValueKind.String || !Regex.IsMatch(attr.Value.GetString()!, @"^[\w-]+$"))) return false;
                if (attr.Name is "textAlign" or "align" && attr.Value.ValueKind != JsonValueKind.Null && (attr.Value.ValueKind != JsonValueKind.String || attr.Value.GetString() is not ("left" or "center" or "right" or "justify"))) return false;
                if (attr.Name == "indentLevel" && (!attr.Value.TryGetInt32(out int indent) || indent is < 0 or > 8)) return false;
                if (attr.Name is "colspan" or "rowspan" && (!attr.Value.TryGetInt32(out int span) || span is < 1 or > 10000)) return false;
                if (attr.Name == "colwidth" && attr.Value.ValueKind != JsonValueKind.Null && (attr.Value.ValueKind != JsonValueKind.Array || attr.Value.EnumerateArray().Any(v => !v.TryGetInt32(out int width) || width is < 0 or > 10000))) return false;
                if (type == "image" && attr.Value.ValueKind != JsonValueKind.Null)
                {
                    if (attr.Name == "width") { if (!int.TryParse(attr.Value.ToString(), out int width) || width is < 1 or > 10000) return false; }
                    else if (attr.Value.ValueKind != JsonValueKind.String || attr.Name is "src" or "assetUrl" && !EditorContentContract.SafeImage(attr.Value.GetString()!)) return false;
                }
            }
        }
        if (node.TryGetProperty("marks", out var marks))
        {
            if (marks.ValueKind != JsonValueKind.Array) return false;
            if (marks.GetArrayLength() > 0 && type is not ("text" or "hardBreak")) return false;
            if (marks.GetArrayLength() > 1 && marks.EnumerateArray().Any(m => m.TryGetProperty("type", out var mtype) && mtype.GetString() == "code")) return false;
            foreach (var mark in marks.EnumerateArray())
            {
                if (!mark.TryGetProperty("type", out var mt) || !Contains("marks", mt.GetString()!)) return false;
                if (mt.GetString() == "link" && (!mark.TryGetProperty("attrs", out var linkAttrs) || !linkAttrs.TryGetProperty("href", out var href) || href.ValueKind != JsonValueKind.String || !SafeLink(href.GetString()!))) return false;
                if (mark.EnumerateObject().Any(p => p.Name is not ("type" or "attrs"))) return false;
                if (mark.TryGetProperty("attrs", out var ma) && (ma.ValueKind != JsonValueKind.Object || ma.EnumerateObject().Any(a => mt.GetString() != "link" || !AttributeAllowed("attributes", "a", a.Name)
                    || a.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
                    || a.Name == "href" && !SafeLink(a.Value.GetString() ?? "")))) return false;
            }
        }
        if (!node.TryGetProperty("content", out var children)) return type is "text" or "hardBreak" or "horizontalRule" or "paragraph" or "heading" or "codeBlock" or "image";
        if (children.ValueKind != JsonValueKind.Array || !children.EnumerateArray().All(ValidNode)) return false;
        string[] childTypes = children.EnumerateArray().Select(c => c.GetProperty("type").GetString()!).ToArray();
        return type switch
        {
            "doc" or "blockquote" => childTypes.Length > 0 && childTypes.All(t => Contains("blockNodes", t)),
            "listItem" => childTypes.FirstOrDefault() == "paragraph" && childTypes.All(t => Contains("blockNodes", t)),
            "bulletList" or "orderedList" => childTypes.Length > 0 && childTypes.All(t => t == "listItem"),
            "table" => childTypes.Length > 0 && childTypes.All(t => t == "tableRow"),
            "tableRow" => childTypes.Length > 0 && childTypes.All(t => t is "tableCell" or "tableHeader"),
            "tableCell" or "tableHeader" => childTypes.Length > 0 && childTypes.All(t => Contains("blockNodes", t)),
            "paragraph" or "heading" => childTypes.All(t => Contains("inlineNodes", t)),
            "codeBlock" => childTypes.All(t => t == "text") && children.EnumerateArray().All(c => !c.TryGetProperty("marks", out var m) || m.GetArrayLength() == 0),
            _ => childTypes.Length == 0
        };
    }
}
