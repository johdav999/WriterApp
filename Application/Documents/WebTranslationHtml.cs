using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using WriterApp.Shared;
using WriterApp.Shared.Editor;

namespace WriterApp.Application.Documents;

/// <summary>Server-side text substitution. The client/provider cannot supply HTML.</summary>
public static class WebTranslationHtml
{
    public static string Map(string html, TranslationPage source, TranslationPage target)
    {
        if (html.Length > 500000 || html.Contains("<!--", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported or oversized translation page. Use selection translation.");
        var document = new HtmlParser().ParseDocument("");
        var body = document.Body!;
        foreach (var node in new HtmlParser().ParseFragment(html, body).ToArray()) body.AppendChild(node);
        var texts = new List<IText>();
        void Visit(INode node, int depth)
        {
            if (depth > 128) throw new InvalidDataException("Translation page is too deeply nested.");
            if (node is IElement element && element != body)
            {
                if (!EditorContentContract.Tags.Contains(element.LocalName) || element.LocalName is "img" or "pre" or "code"
                    || element.Attributes.Any(a => !EditorContentContract.AllowedAttribute(element.LocalName, a.Name, a.Value)))
                    throw new InvalidDataException("Unsupported translation content. Use selection translation.");
            }
            if (node is IText text && text.Data.Length != 0) texts.Add(text);
            foreach (var child in node.ChildNodes) Visit(child, depth + 1);
        }
        Visit(body, 0);
        // TipTap can merge adjacent inline elements carrying identical marks. Match their
        // exact combined source text while preserving each host-owned DOM element.
        var original = string.Concat(source.Runs.Select(r => r.Text));
        if (string.Concat(texts.Select(t => t.Data)) != original || texts.Count != source.Runs.Count
            || !texts.Select(t => t.Data).SequenceEqual(source.Runs.Select(r => r.Text)))
            throw new InvalidDataException("Saved HTML text boundaries differ from the editor mapping. Normalize the page in the editor or use selection translation; no writing changed.");
        for (int i = 0; i < texts.Count; i++) texts[i].Data = target.Runs[i].Text;
        return body.InnerHtml;
    }
}
