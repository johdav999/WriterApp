using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using WriterApp.Application.State;

namespace WriterApp.Application.Importing
{
    public sealed class SectionImportService : ISectionImportService
    {
        private const int MaxListDepth = 2;

        public Task<SectionImportResult> ConvertAsync(
            string fileName,
            byte[] fileBytes,
            SectionImportOptions options,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new InvalidOperationException("File name is required.");
            }

            if (fileBytes is null || fileBytes.Length == 0)
            {
                throw new InvalidOperationException("File is empty.");
            }

            string ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".txt" => Task.FromResult(ConvertTxt(fileBytes, options)),
                ".rtf" => Task.FromResult(ConvertRtf(fileBytes, options)),
                ".docx" => Task.FromResult(ConvertDocx(fileBytes, options)),
                _ => throw new InvalidOperationException("Unsupported file format.")
            };
        }

        private static SectionImportResult ConvertTxt(byte[] bytes, SectionImportOptions options)
        {
            string text = DecodeText(bytes);
            if (options.NormalizeWhitespace)
            {
                text = NormalizeTextWhitespace(text);
            }

            List<string> paragraphs = SplitIntoParagraphs(text);
            StringBuilder html = new();
            foreach (string paragraph in paragraphs)
            {
                string encoded = WebUtility.HtmlEncode(paragraph);
                if (options.PreserveTxtLineBreaks)
                {
                    encoded = encoded.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "<br>", StringComparison.Ordinal);
                }
                else
                {
                    encoded = encoded.Replace("\r\n", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
                    encoded = Regex.Replace(encoded, @"[ \t]{2,}", " ");
                }

                if (string.IsNullOrWhiteSpace(encoded))
                {
                    html.Append("<p><br></p>");
                }
                else
                {
                    html.Append("<p>").Append(encoded).Append("</p>");
                }
            }

            string sanitized = SanitizeHtml(html.ToString());
            return BuildResult("txt", sanitized, Array.Empty<string>());
        }

        private static SectionImportResult ConvertRtf(byte[] bytes, SectionImportOptions options)
        {
            string rtf = DecodeText(bytes);
            string plain = ParseRtfToPlainText(rtf);
            if (options.NormalizeWhitespace)
            {
                plain = NormalizeTextWhitespace(plain);
            }

            SectionImportResult txtResult = ConvertTxt(Encoding.UTF8.GetBytes(plain), options);
            List<string> warnings = new()
            {
                "RTF formatting is partially supported in this phase; content was normalized."
            };
            return txtResult with { Warnings = warnings, Format = "rtf" };
        }

        private static SectionImportResult ConvertDocx(byte[] bytes, SectionImportOptions options)
        {
            using MemoryStream stream = new(bytes, writable: false);
            using WordprocessingDocument doc = WordprocessingDocument.Open(stream, false);
            if (doc.MainDocumentPart?.Document?.Body is null)
            {
                throw new InvalidOperationException("DOCX document body is missing.");
            }

            NumberingResolver numberingResolver = new(doc.MainDocumentPart);
            StringBuilder html = new();
            List<string> warnings = new();
            bool listOpen = false;
            string currentListTag = "ul";

            foreach (var element in doc.MainDocumentPart.Document.Body.ChildElements)
            {
                if (element is Table)
                {
                    if (listOpen)
                    {
                        html.Append("</").Append(currentListTag).Append('>');
                        listOpen = false;
                    }

                    html.Append("<p>[Table omitted]</p>");
                    warnings.Add("Tables were omitted during import.");
                    continue;
                }

                if (element is not Paragraph paragraph)
                {
                    continue;
                }

                ParagraphImportMeta meta = GetParagraphMeta(paragraph, numberingResolver);
                if (meta.IsListItem)
                {
                    string listTag = meta.IsOrderedList ? "ol" : "ul";
                    if (!listOpen || !string.Equals(currentListTag, listTag, StringComparison.Ordinal))
                    {
                        if (listOpen)
                        {
                            html.Append("</").Append(currentListTag).Append('>');
                        }

                        html.Append('<').Append(listTag).Append('>');
                        currentListTag = listTag;
                        listOpen = true;
                    }

                    html.Append("<li>")
                        .Append(ConvertParagraphInlineHtml(paragraph, warnings, options.NormalizeWhitespace))
                        .Append("</li>");
                    continue;
                }

                if (listOpen)
                {
                    html.Append("</").Append(currentListTag).Append('>');
                    listOpen = false;
                }

                string content = ConvertParagraphInlineHtml(paragraph, warnings, options.NormalizeWhitespace);
                string tag = meta.HeadingTag ?? "p";
                if (string.IsNullOrWhiteSpace(content))
                {
                    html.Append("<p><br></p>");
                }
                else
                {
                    html.Append('<').Append(tag).Append('>')
                        .Append(content)
                        .Append("</").Append(tag).Append('>');
                }
            }

            if (listOpen)
            {
                html.Append("</").Append(currentListTag).Append('>');
            }

            string sanitized = SanitizeHtml(html.ToString());
            return BuildResult("docx", sanitized, warnings.Distinct(StringComparer.Ordinal).ToList());
        }

        private static string ConvertParagraphInlineHtml(Paragraph paragraph, List<string> warnings, bool normalizeWhitespace)
        {
            StringBuilder builder = new();
            foreach (var child in paragraph.ChildElements)
            {
                if (child is Run run)
                {
                    builder.Append(ConvertRun(run, warnings, normalizeWhitespace));
                }
                else if (child is Hyperlink hyperlink)
                {
                    foreach (Run runChild in hyperlink.Elements<Run>())
                    {
                        builder.Append(ConvertRun(runChild, warnings, normalizeWhitespace));
                    }
                }
            }

            return builder.ToString();
        }

        private static string ConvertRun(Run run, List<string> warnings, bool normalizeWhitespace)
        {
            bool bold = run.RunProperties?.Bold is not null;
            bool italic = run.RunProperties?.Italic is not null;
            bool underline = run.RunProperties?.Underline is not null
                && run.RunProperties.Underline.Val?.Value != UnderlineValues.None;

            StringBuilder content = new();
            foreach (var part in run.ChildElements)
            {
                if (part is Text text)
                {
                    string value = text.Text ?? string.Empty;
                    if (normalizeWhitespace)
                    {
                        value = value.Replace("\t", " ", StringComparison.Ordinal);
                    }

                    bool preserveWhitespace = text.OuterXml.Contains("xml:space=\"preserve\"", StringComparison.OrdinalIgnoreCase);
                    if (preserveWhitespace)
                    {
                        value = PreserveHtmlSpaces(value);
                    }
                    else
                    {
                        value = WebUtility.HtmlEncode(value);
                    }

                    content.Append(value);
                }
                else if (part is Break)
                {
                    content.Append("<br>");
                }
                else if (part is TabChar)
                {
                    content.Append("    ");
                }
                else if (part is Drawing)
                {
                    content.Append("[Image omitted]");
                    warnings.Add("Images were omitted during import.");
                }
            }

            string valueText = content.ToString();
            if (bold)
            {
                valueText = "<strong>" + valueText + "</strong>";
            }

            if (italic)
            {
                valueText = "<em>" + valueText + "</em>";
            }

            if (underline)
            {
                valueText = "<u>" + valueText + "</u>";
            }

            return valueText;
        }

        private static string PreserveHtmlSpaces(string value)
        {
            string encoded = WebUtility.HtmlEncode(value);
            encoded = encoded.Replace("  ", " &nbsp;", StringComparison.Ordinal);
            if (encoded.StartsWith(" ", StringComparison.Ordinal))
            {
                encoded = "&nbsp;" + encoded[1..];
            }

            if (encoded.EndsWith(" ", StringComparison.Ordinal))
            {
                encoded = encoded[..^1] + "&nbsp;";
            }

            return encoded;
        }

        private static ParagraphImportMeta GetParagraphMeta(Paragraph paragraph, NumberingResolver resolver)
        {
            string? headingTag = ResolveHeadingTag(paragraph);

            NumberingProperties? numbering = paragraph.ParagraphProperties?.NumberingProperties;
            int level = 0;
            if (numbering?.NumberingLevelReference?.Val is not null)
            {
                level = Math.Clamp((int)numbering.NumberingLevelReference.Val.Value, 0, MaxListDepth - 1);
            }

            bool hasList = numbering?.NumberingId?.Val is not null;
            bool ordered = hasList && resolver.IsOrderedList(numbering!.NumberingId!.Val!.Value, level);

            return new ParagraphImportMeta(
                headingTag,
                hasList,
                ordered,
                level);
        }

        private static string? ResolveHeadingTag(Paragraph paragraph)
        {
            string? styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (string.IsNullOrWhiteSpace(styleId))
            {
                return null;
            }

            if (styleId.Equals("Heading1", StringComparison.OrdinalIgnoreCase))
            {
                return "h1";
            }

            if (styleId.Equals("Heading2", StringComparison.OrdinalIgnoreCase))
            {
                return "h2";
            }

            if (styleId.Equals("Heading3", StringComparison.OrdinalIgnoreCase))
            {
                return "h3";
            }

            return null;
        }

        private static string ParseRtfToPlainText(string rtf)
        {
            if (string.IsNullOrWhiteSpace(rtf))
            {
                return string.Empty;
            }

            string text = rtf;
            text = Regex.Replace(text, @"\\par[d]? ?", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\\line ?", "\n", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\\'[0-9a-fA-F]{2}", match =>
            {
                string hex = match.Value.Substring(2);
                byte value = byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                return Encoding.GetEncoding(1252).GetString(new[] { value });
            });
            text = Regex.Replace(text, @"\\u(-?\d+)\??", match =>
            {
                if (int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code))
                {
                    if (code < 0)
                    {
                        code += 65536;
                    }
                    return char.ConvertFromUtf32(code);
                }

                return string.Empty;
            });

            text = text.Replace(@"\{", "{", StringComparison.Ordinal)
                .Replace(@"\}", "}", StringComparison.Ordinal)
                .Replace(@"\\", @"\", StringComparison.Ordinal);

            text = Regex.Replace(text, @"\\[a-zA-Z]+\d* ?", string.Empty);
            text = text.Replace("{", string.Empty, StringComparison.Ordinal)
                .Replace("}", string.Empty, StringComparison.Ordinal);

            return WebUtility.HtmlDecode(text).Trim();
        }

        private static string DecodeText(byte[] bytes)
        {
            if (bytes.Length >= 2)
            {
                if (bytes[0] == 0xFF && bytes[1] == 0xFE)
                {
                    return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
                }

                if (bytes[0] == 0xFE && bytes[1] == 0xFF)
                {
                    return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
                }
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static string NormalizeTextWhitespace(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            normalized = Regex.Replace(normalized, @"[ \t]+\n", "\n");
            normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n");
            return normalized.Trim();
        }

        private static List<string> SplitIntoParagraphs(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string> { string.Empty };
            }

            string normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
            string[] parts = Regex.Split(normalized, @"\n\s*\n");
            return parts.Length == 0 ? new List<string> { normalized } : parts.ToList();
        }

        private static string SanitizeHtml(string html)
        {
            HtmlParser parser = new();
            IHtmlDocument doc = parser.ParseDocument("<body>" + html + "</body>");
            if (doc.Body is null)
            {
                return "<p><br></p>";
            }

            StringBuilder builder = new();
            foreach (INode node in doc.Body.ChildNodes)
            {
                AppendSanitizedNode(node, builder);
            }

            string sanitized = builder.ToString();
            return string.IsNullOrWhiteSpace(sanitized) ? "<p><br></p>" : sanitized;
        }

        private static void AppendSanitizedNode(INode node, StringBuilder builder)
        {
            if (node is IText text)
            {
                builder.Append(WebUtility.HtmlEncode(text.Text));
                return;
            }

            if (node is not IElement element)
            {
                return;
            }

            string tag = element.TagName.ToLowerInvariant();
            if (!IsAllowedTag(tag))
            {
                foreach (INode child in element.ChildNodes)
                {
                    AppendSanitizedNode(child, builder);
                }
                return;
            }

            builder.Append('<').Append(tag).Append('>');
            foreach (INode child in element.ChildNodes)
            {
                AppendSanitizedNode(child, builder);
            }

            builder.Append("</").Append(tag).Append('>');
        }

        private static bool IsAllowedTag(string tag)
        {
            return tag is "p" or "h1" or "h2" or "h3" or "strong" or "b" or "em" or "i" or "u" or "ul" or "ol" or "li" or "br";
        }

        private static SectionImportResult BuildResult(string format, string html, IReadOnlyList<string> warnings)
        {
            string plain = PlainTextMapper.ToPlainText(html);
            HtmlParser parser = new();
            IHtmlDocument doc = parser.ParseDocument("<body>" + html + "</body>");
            int paragraphCount = doc.QuerySelectorAll("p").Length;
            int headingCount = doc.QuerySelectorAll("h1,h2,h3").Length;
            int listCount = doc.QuerySelectorAll("ul,ol").Length;
            SectionImportStats stats = new(paragraphCount, headingCount, listCount, plain.Length);
            return new SectionImportResult(html, stats, warnings, format);
        }

        private sealed record ParagraphImportMeta(
            string? HeadingTag,
            bool IsListItem,
            bool IsOrderedList,
            int Level);

        private sealed class NumberingResolver
        {
            private readonly Dictionary<int, int> _numToAbstract = new();
            private readonly Dictionary<(int AbstractNumId, int Level), string> _formatByLevel = new();

            public NumberingResolver(MainDocumentPart mainPart)
            {
                NumberingDefinitionsPart? numberingPart = mainPart.NumberingDefinitionsPart;
                if (numberingPart?.Numbering is null)
                {
                    return;
                }

                XDocument? xml;
                try
                {
                    xml = XDocument.Parse(numberingPart.Numbering.OuterXml);
                }
                catch
                {
                    return;
                }

                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                foreach (XElement instance in xml.Descendants(w + "num"))
                {
                    int? numId = ParseInt(instance.Attribute(w + "numId")?.Value);
                    int? abstractId = ParseInt(instance.Element(w + "abstractNumId")?.Attribute(w + "val")?.Value);
                    if (numId.HasValue && abstractId.HasValue)
                    {
                        _numToAbstract[numId.Value] = abstractId.Value;
                    }
                }

                foreach (XElement abstractNum in xml.Descendants(w + "abstractNum"))
                {
                    int? abstractId = ParseInt(abstractNum.Attribute(w + "abstractNumId")?.Value);
                    if (!abstractId.HasValue)
                    {
                        continue;
                    }

                    foreach (XElement level in abstractNum.Elements(w + "lvl"))
                    {
                        int? levelIndex = ParseInt(level.Attribute(w + "ilvl")?.Value);
                        if (!levelIndex.HasValue)
                        {
                            continue;
                        }

                        string format = level.Element(w + "numFmt")?.Attribute(w + "val")?.Value ?? "bullet";
                        _formatByLevel[(abstractId.Value, levelIndex.Value)] = format;
                    }
                }
            }

            public bool IsOrderedList(int numId, int level)
            {
                if (!_numToAbstract.TryGetValue(numId, out int abstractId))
                {
                    return false;
                }

                if (!_formatByLevel.TryGetValue((abstractId, level), out string? format))
                {
                    return false;
                }

                return !string.Equals(format, "Bullet", StringComparison.OrdinalIgnoreCase);
            }

            private static int? ParseInt(string? value)
            {
                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    ? parsed
                    : null;
            }
        }
    }
}
