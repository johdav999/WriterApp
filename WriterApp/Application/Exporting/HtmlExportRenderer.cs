using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WriterApp.Application.State;
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
        public ExportKind Kind => ExportKind.Document;

        private readonly SectionNumberingService _numberingService = new();

        public Task<ExportResult> RenderAsync(Document document, ExportOptions options)
        {
            ExportOptions resolved = options ?? new ExportOptions();
            string title = ExportHelpers.GetDocumentTitle(document);

            string bodyHtml = RenderBodyHtml(document, resolved);

            StringBuilder builder = new();


            builder.Append("<!DOCTYPE html>\n")
                .Append("<html>\n")
                .Append("<head>\n")
                .Append("  <meta charset=\"utf-8\" />\n")
                .Append("  <title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n")
                .Append("  <style>\n")
                .Append("    body { max-width: 700px; margin: 3rem auto; font-family: serif; }\n")
                .Append("    .export-frontmatter:not(:empty) { break-after: page; page-break-after: always; }\n")
                .Append("    h1, h2 { margin-top: 2rem; }\n")
                .Append("    p { line-height: 1.6; }\n")
                .Append("    .export-title-page { text-align: center; padding: 120px 0 80px; break-after: page; page-break-after: always; }\n")
                .Append("    .export-title-page h1 { margin-bottom: 0.3em; }\n")
                .Append("    .export-title-page .title-subtitle { font-size: 1.2em; color: #444; margin-bottom: 1.2em; }\n")
                .Append("    .export-title-page .title-meta { margin-top: 0.6em; font-size: 0.95em; color: #555; }\n")
                .Append("    .export-toc { margin: 1.5em 0 2em 0; }\n")
                .Append("    .export-toc h2 { font-size: 1.2em; margin-bottom: 0.6em; }\n")
                .Append("    .export-toc ol { list-style: none; padding-left: 0; }\n")
                .Append("    .export-toc li { margin: 0.25em 0; }\n")
                .Append("    .export-toc a { color: inherit; text-decoration: none; }\n")
                .Append(BuildChapterBreakCss(resolved))
                .Append("  </style>\n")
                .Append("</head>\n")
                .Append("<body>\n")
                .Append(bodyHtml)
                .Append("</body>\n</html>\n");

            string html = ExportHelpers.NormalizeLineEndings(builder.ToString());
            ExportHelpers.AssertSynopsisNotIncluded(html, document);
            byte[] content = Encoding.UTF8.GetBytes(html);
            string fileName = ExportHelpers.SanitizeFileName(document.Metadata.Title, "document", ".html");

            ExportResult result = new(content, "text/html", fileName);
            return Task.FromResult(result);
        }

        public string RenderBodyHtml(Document document, ExportOptions options)
        {
            ExportOptions resolved = options ?? new ExportOptions();
            string title = ExportHelpers.GetDocumentTitle(document);
            StringBuilder frontMatterBuilder = new();
            StringBuilder bodyBuilder = new();
            IReadOnlyDictionary<Guid, SectionNumberingInfo> numbering = _numberingService.BuildIndex(document);
            List<(string Text, string Id, int Level)> headings = new();
            string titlePageHtml = string.Empty;

            if (resolved.IncludeTitlePage)
            {
                titlePageHtml = BuildTitlePageBlock(resolved, title);
                frontMatterBuilder.Append(titlePageHtml);
            }

            foreach (Section section in ExportHelpers.GetOrderedSections(document))
            {
                string sectionTitle = ExportHelpers.GetSectionTitle(section);
                SectionNumberingInfo? info = numbering.TryGetValue(section.SectionId, out SectionNumberingInfo? entry)
                    ? entry
                    : null;
                string heading = _numberingService.BuildHeading(section, sectionTitle, info);
                string sectionId = $"section-{section.SectionId}";
                headings.Add((heading, sectionId, 2));
                bodyBuilder.Append("  <section>\n");
                // Section titles map to second-level headings.
                bodyBuilder.Append("    <h2 id=\"").Append(sectionId).Append("\" class=\"export-section-title\">")
                    .Append(WebUtility.HtmlEncode(heading)).Append("</h2>\n");


                string sectionHtml = ConvertSectionContentToHtml(section.Content, sectionTitle);


                if (!string.IsNullOrWhiteSpace(sectionHtml))
                {
                    string indented = IndentLines(sectionHtml.Trim(), "    ");
                    bodyBuilder.Append(indented).Append("\n");
                }

                bodyBuilder.Append("  </section>\n");
            }

            if (resolved.IncludeToc && (resolved.TocDepth <= 0 || resolved.TocDepth >= 2))
            {
                string tocHtml = BuildSimpleToc(headings);
                if (!string.IsNullOrWhiteSpace(tocHtml))
                {
                    frontMatterBuilder.Append(tocHtml).Append("\n");
                }
            }

            string html = "<div class=\"export-doc\">\n" +
                          "<div id=\"preview-frontmatter\" class=\"export-frontmatter\">\n" +
                          frontMatterBuilder +
                          "\n</div>\n" +
                          "<div id=\"preview-body\" class=\"export-body\">\n" +
                          bodyBuilder +
                          "\n</div>\n" +
                          "</div>\n";
            ExportHelpers.AssertSynopsisNotIncluded(html, document);
            return html;
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

        private static string BuildTitlePageBlock(ExportOptions options, string documentTitle)
        {
            string title = string.IsNullOrWhiteSpace(options.TitlePageTitle) ? documentTitle : options.TitlePageTitle!;
            string subtitle = options.TitlePageSubtitle ?? string.Empty;
            string author = options.TitlePageAuthor ?? string.Empty;
            string draft = options.TitlePageDraftLabel ?? string.Empty;
            string date = options.TitlePageDate ?? string.Empty;

            StringBuilder builder = new();
            builder.Append("<section class=\"export-title-page\">\n")
                .Append("  <h1 class=\"export-title\">").Append(WebUtility.HtmlEncode(title)).Append("</h1>\n");

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                builder.Append("  <div class=\"title-subtitle\">")
                    .Append(WebUtility.HtmlEncode(subtitle))
                    .Append("</div>\n");
            }

            if (!string.IsNullOrWhiteSpace(author))
            {
                builder.Append("  <div class=\"title-meta\">")
                    .Append(WebUtility.HtmlEncode(author))
                    .Append("</div>\n");
            }

            if (!string.IsNullOrWhiteSpace(draft))
            {
                builder.Append("  <div class=\"title-meta\">")
                    .Append(WebUtility.HtmlEncode(draft))
                    .Append("</div>\n");
            }

            if (!string.IsNullOrWhiteSpace(date))
            {
                builder.Append("  <div class=\"title-meta\">")
                    .Append(WebUtility.HtmlEncode(date))
                    .Append("</div>\n");
            }

            builder.Append("</section>\n");
            return builder.ToString();
        }

        private static string BuildSimpleToc(IEnumerable<(string Text, string Id, int Level)> headings)
        {
            List<(string Text, string Id, int Level)> items = headings.ToList();
            if (items.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            builder.Append("<nav class=\"export-toc\">\n")
                .Append("  <h2>Table of Contents</h2>\n")
                .Append("  <ol>\n");

            foreach ((string text, string id, int _) in items)
            {
                builder.Append("    <li><a href=\"#").Append(id).Append("\">")
                    .Append(WebUtility.HtmlEncode(text))
                    .Append("</a></li>\n");
            }

            builder.Append("  </ol>\n</nav>");
            return builder.ToString();
        }

        private static string BuildChapterBreakCss(ExportOptions options)
        {
            if (options.ChapterBreakRules is null || options.ChapterBreakRules.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder css = new();
            if (options.ChapterBreakRules.Any(rule => string.Equals(rule, "h1", StringComparison.OrdinalIgnoreCase)))
            {
                css.Append("    h1:not(.export-title), .export-section-title { break-before: page; page-break-before: always; }\n");
            }

            if (options.ChapterBreakRules.Any(rule => string.Equals(rule, "section", StringComparison.OrdinalIgnoreCase)))
            {
                css.Append("    section { break-before: page; page-break-before: always; }\n")
                    .Append("    section:first-of-type { break-before: auto; page-break-before: auto; }\n");
            }

            return css.ToString();
        }
    }
}

