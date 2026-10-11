using System.Net;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WriterApp.Shared.Editor;

namespace WriterApp.Shared.Brainstorming;

// Read-only shared projection/compatibility boundary; never sanitize stored proposals.
public static class BrainstormingDraftContent
{
    private static bool Contains(string list,string value) => EditorContentContract.Definition.GetProperty(list).EnumerateArray().Any(x=>x.GetString()==value);
    public static string Html(string text) => string.Concat(text.Replace("\r\n","\n").Replace('\r','\n').Split("\n\n").Select(p=>"<p>"+WebUtility.HtmlEncode(p).Replace("\n","<br>")+"</p>"));
    public static void RequireHtml(string html) {
        if(string.IsNullOrWhiteSpace(html) || html.Length>BrainstormingValidation.MaxTextCharacters || html.Contains('\0'))throw new InvalidDataException("Invalid or oversized proposal content. Original retained.");
        try {
            var parser=new HtmlParser(new HtmlParserOptions{IsStrictMode=true});
            var context=new HtmlParser().ParseDocument("").CreateElement("body");
            if(!parser.ParseFragment(html,context).All(n=>ValidHtml(n,0)))throw new InvalidDataException("Unsupported proposal formatting. Original retained; no flattening is permitted.");
        } catch(HtmlParseException e){throw new InvalidDataException("Malformed proposal HTML. Original retained.",e);}
    }
    public static string PlainText(string html) {
        var body=new HtmlParser().ParseDocument(html).Body!; var text=new StringBuilder(); bool separated=true,afterBlockLeaf=false,implicitInline=false;
        void Block() {if(!separated)text.Append("\n\n");separated=false;afterBlockLeaf=false;implicitInline=false;}
        void Visit(INode node,int depth) {
            if(depth>100)throw new InvalidDataException("Story source is too deeply nested.");
            if(node is IElement e && e!=body) {
                if(!EditorContentContract.Tags.Contains(e.LocalName) || e.Attributes.Any(a=>!EditorContentContract.AllowedAttribute(e.LocalName,a.Name,a.Value)))throw new InvalidDataException("Unsupported story content. Preserve the original and review a supported source.");
                if(e.LocalName is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "pre") Block();
                if(e.LocalName=="img") {
                    // The shipped common schema's block-image parse rule omits an image nested
                    // inside a text block from textBetween. Original HTML remains untouched.
                    for(var parent=e.Parent;parent is not null&&parent!=body;parent=parent.Parent)
                        if(parent is IElement ancestor && ancestor.LocalName is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6")return;
                    Block();text.Append('\n');afterBlockLeaf=true;return;
                }
                if(e.LocalName=="hr") {Block();text.Append('\n');afterBlockLeaf=true;return;}
                if(e.LocalName=="br") {text.Append('\n');return;}
            }
            if(node is IText t) {
                bool inTextBlock=false;for(var parent=node.Parent;parent is not null&&parent!=body;parent=parent.Parent)
                    if(parent is IElement element && element.LocalName is "p" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "pre") {inTextBlock=true;break;}
                // setContent uses preserveWhitespace: 'full' in both shipped editors;
                // inter-block whitespace becomes an implicit paragraph as well.
                if(t.Data.Length>0) {if(afterBlockLeaf || !inTextBlock&&!implicitInline)Block();if(!inTextBlock)implicitInline=true;text.Append(t.Data);}
            }
            foreach(var child in node.ChildNodes)Visit(child,depth+1);
        }
        Visit(body,0);return text.ToString();
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
        return EditorContentContract.Tags.Contains(element.LocalName)
            && element.Attributes.All(a => EditorContentContract.AllowedAttribute(element.LocalName, a.Name, a.Value))
            && element.ChildNodes.All(child => ValidHtml(child, depth + 1));
    }
}
