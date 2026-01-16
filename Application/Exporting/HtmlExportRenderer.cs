using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    public sealed class HtmlExportRenderer : IExportRenderer
    {
        private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex BulletRegex = new(@"^\s*-\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex OrderedRegex = new(@"^\s*\d+\.\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex InlineCodeRegex = new(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new(@"_(.+?)_", RegexOptions.Compiled);

        public ExportFormat Format => ExportFormat.Html;

        public Task<ExportResult> RenderAsync(Document document, ExportOptions options)
        {
            ExportOptions resolved = options ?? new ExportOptions();
            string title = ExportHelpers.GetDocumentTitle(document);
            StringBuilder builder = new();

            builder.Append("<!DOCTYPE html>\n")
                .Append("<html>\n")
                .Append("<head>\n")
                .Append("  <meta charset=\"utf-8\" />\n")
                .Append("  <title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n")
                .Append("  <style>\n")
                .Append("    body { max-width: 700px; margin: 3rem auto; font-family: serif; }\n")
                .Append("    h1, h2 { margin-top: 2rem; }\n")
                .Append("    p { line-height: 1.6; }\n")
                .Append("  </style>\n")
                .Append("</head>\n")
                .Append("<body>\n");

            if (resolved.IncludeTitlePage)
            {
                // Document title maps to a top-level heading.
                builder.Append("  <h1>").Append(WebUtility.HtmlEncode(title)).Append("</h1>\n");
            }

            foreach (Section section in ExportHelpers.GetOrderedSections(document))
            {
                string sectionTitle = ExportHelpers.GetSectionTitle(section);
                builder.Append("  <section>\n");
                // Section titles map to second-level headings.
                builder.Append("    <h2>").Append(WebUtility.HtmlEncode(sectionTitle)).Append("</h2>\n");

                string sectionHtml = ConvertSectionContentToHtml(section.Content, sectionTitle);
                if (!string.IsNullOrWhiteSpace(sectionHtml))
                {
                    string indented = IndentLines(sectionHtml.Trim(), "    ");
                    builder.Append(indented).Append("\n");
                }

                builder.Append("  </section>\n");
            }

            builder.Append("</body>\n</html>\n");

            string html = ExportHelpers.NormalizeLineEndings(builder.ToString());
            byte[] content = Encoding.UTF8.GetBytes(html);
            string fileName = ExportHelpers.SanitizeFileName(document.Metadata.Title, "document", ".html");

            ExportResult result = new(content, "text/html", fileName);
            return Task.FromResult(result);
        }

        private static string ConvertSectionContentToHtml(SectionContent content, string sectionTitle)
        {
            if (content is null || string.IsNullOrWhiteSpace(content.Value))
            {
                return string.Empty;
            }

            string format = content.Format ?? string.Empty;
            if (string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase))
            {
                // Markdown content is mapped into semantic HTML elements.
                return MarkdownToHtml(content.Value);
            }

            string value = ExportHelpers.NormalizeSectionHtmlForExport(content.Value, sectionTitle).Trim();
            if (!value.Contains('<', StringComparison.Ordinal))
            {
                return $"<p>{WebUtility.HtmlEncode(value)}</p>";
            }

            return value;
        }

        private static string MarkdownToHtml(string markdown)
        {
            string normalized = ExportHelpers.NormalizeLineEndings(markdown);
            string[] lines = normalized.Split('\n');
            StringBuilder builder = new();
            bool inCodeBlock = false;
            bool inBulletList = false;
            bool inOrderedList = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCodeBlock)
                    {
                        builder.Append("</code></pre>\n");
                        inCodeBlock = false;
                    }
                    else
                    {
                        CloseLists();
                        builder.Append("<pre><code>");
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    builder.Append(WebUtility.HtmlEncode(rawLine)).Append("\n");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    CloseLists();
                    continue;
                }

                Match headingMatch = HeadingRegex.Match(line);
                if (headingMatch.Success)
                {
                    CloseLists();
                    int level = headingMatch.Groups[1].Value.Length;
                    string text = RenderInlineMarkdown(headingMatch.Groups[2].Value);
                    builder.Append("<h").Append(level).Append('>').Append(text).Append("</h").Append(level).Append(">\n");
                    continue;
                }

                Match bulletMatch = BulletRegex.Match(line);
                if (bulletMatch.Success)
                {
                    if (!inBulletList)
                    {
                        CloseOrderedList();
                        builder.Append("<ul>\n");
                        inBulletList = true;
                    }

                    string text = RenderInlineMarkdown(bulletMatch.Groups[1].Value);
                    builder.Append("  <li>").Append(text).Append("</li>\n");
                    continue;
                }

                Match orderedMatch = OrderedRegex.Match(line);
                if (orderedMatch.Success)
                {
                    if (!inOrderedList)
                    {
                        CloseBulletList();
                        builder.Append("<ol>\n");
                        inOrderedList = true;
                    }

                    string text = RenderInlineMarkdown(orderedMatch.Groups[1].Value);
                    builder.Append("  <li>").Append(text).Append("</li>\n");
                    continue;
                }

                CloseLists();
                string paragraph = RenderInlineMarkdown(line);
                builder.Append("<p>").Append(paragraph).Append("</p>\n");
            }

            if (inCodeBlock)
            {
                builder.Append("</code></pre>\n");
            }

            CloseLists();
            return builder.ToString().TrimEnd();

            void CloseLists()
            {
                CloseBulletList();
                CloseOrderedList();
            }

            void CloseBulletList()
            {
                if (!inBulletList)
                {
                    return;
                }

                builder.Append("</ul>\n");
                inBulletList = false;
            }

            void CloseOrderedList()
            {
                if (!inOrderedList)
                {
                    return;
                }

                builder.Append("</ol>\n");
                inOrderedList = false;
            }
        }

        private static string RenderInlineMarkdown(string text)
        {
            string encoded = WebUtility.HtmlEncode(text);
            encoded = InlineCodeRegex.Replace(encoded, "<code>$1</code>");
            encoded = BoldRegex.Replace(encoded, "<strong>$1</strong>");
            encoded = ItalicRegex.Replace(encoded, "<em>$1</em>");
            return encoded;
        }

        private static string IndentLines(string text, string indent)
        {
            string normalized = ExportHelpers.NormalizeLineEndings(text);
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = indent + lines[i];
            }

            return string.Join("\n", lines);
        }
    }
}
