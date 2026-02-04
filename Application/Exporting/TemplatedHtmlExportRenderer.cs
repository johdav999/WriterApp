using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WriterApp.Application.State;
using WriterApp.Data.Exporting;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    public sealed class TemplatedHtmlExportRenderer : IExportRenderer
    {
        private static readonly Regex HeadingRegex = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex BulletRegex = new(@"^\s*-\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex OrderedRegex = new(@"^\s*\d+\.\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex InlineCodeRegex = new(@"`([^`]+)`", RegexOptions.Compiled);
        private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);
        private static readonly Regex ItalicRegex = new(@"_(.+?)_", RegexOptions.Compiled);
        private static readonly Regex HtmlHeadingRegex = new(@"<h([1-6])([^>]*)>(.*?)</h\1>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
        private static readonly Regex TokenRegex = new(@"\{(DocumentTitle|SectionTitle|Date|PageNumber|TotalPages)\}", RegexOptions.Compiled);

        public ExportFormat Format => ExportFormat.Html;
        public ExportKind Kind => ExportKind.Document;

        private readonly SectionNumberingService _numberingService = new();

        public Task<ExportResult> RenderAsync(Document document, ExportOptions options)
        {
            ExportOptions resolved = options ?? new ExportOptions();
            ExportTemplate template = resolved.Template ?? ExportTemplateDefaults.CreateManuscript("unknown", DateTimeOffset.UtcNow);
            RenderedHtml rendered = RenderDocument(document, resolved, template);

            string title = ExportHelpers.GetDocumentTitle(document);

            StringBuilder builder = new();
            builder.Append("<!DOCTYPE html>\n")
                .Append("<html>\n")
                .Append("<head>\n")
                .Append("  <meta charset=\"utf-8\" />\n")
                .Append("  <title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n")
                .Append("  <style>\n")
                .Append(rendered.Css)
                .Append("  </style>\n")
                .Append("</head>\n")
                .Append("<body>\n")
                .Append(rendered.BodyHtml)
                .Append("</body>\n</html>\n");

            string html = ExportHelpers.NormalizeLineEndings(builder.ToString());
            ExportHelpers.AssertSynopsisNotIncluded(html, document);
            byte[] content = Encoding.UTF8.GetBytes(html);
            string fileName = ExportHelpers.SanitizeFileName(document.Metadata.Title, "document", ".html");

            return Task.FromResult(new ExportResult(content, "text/html", fileName));
        }

        public string RenderBodyHtml(Document document, ExportOptions options)
        {
            ExportOptions resolved = options ?? new ExportOptions();
            ExportTemplate template = resolved.Template ?? ExportTemplateDefaults.CreateManuscript("unknown", DateTimeOffset.UtcNow);
            RenderedHtml rendered = RenderDocument(document, resolved, template);

            StringBuilder builder = new();
            builder.Append("<style>\n")
                .Append(rendered.Css)
                .Append("</style>\n")
                .Append(rendered.BodyHtml);

            string html = ExportHelpers.NormalizeLineEndings(builder.ToString());
            ExportHelpers.AssertSynopsisNotIncluded(html, document);
            return html;
        }

        private RenderedHtml RenderDocument(Document document, ExportOptions options, ExportTemplate template)
        {
            string title = ExportHelpers.GetDocumentTitle(document);
            HeadingIdGenerator idGenerator = new();
            List<ExportHeading> headings = new();
            List<string> frontMatterBlocks = new();
            List<string> bodyBlocks = new();
            IReadOnlyDictionary<Guid, SectionNumberingInfo> numbering = _numberingService.BuildIndex(document);

            if (options.IncludeTitlePage)
            {
                frontMatterBlocks.Add(BuildTitlePageBlock(options, title));
            }

            foreach (Section section in ExportHelpers.GetOrderedSections(document))
            {
                string sectionTitle = ExportHelpers.GetSectionTitle(section);
                SectionNumberingInfo? info = numbering.TryGetValue(section.SectionId, out SectionNumberingInfo entry)
                    ? entry
                    : null;
                string headingText = _numberingService.BuildHeading(section, sectionTitle, info);
                string sectionId = idGenerator.NextId(headingText);
                headings.Add(new ExportHeading(2, headingText, sectionId));

                StringBuilder sectionBuilder = new();
                sectionBuilder.Append("<section class=\"export-section\">\n")
                    .Append("  <h2 id=\"").Append(sectionId).Append("\" class=\"export-section-title\">")
                    .Append(WebUtility.HtmlEncode(headingText)).Append("</h2>\n");

                string sectionHtml = ConvertSectionContentToHtml(section.Content, sectionTitle, idGenerator, headings);
                if (!string.IsNullOrWhiteSpace(sectionHtml))
                {
                    string indented = IndentLines(sectionHtml.Trim(), "  ");
                    sectionBuilder.Append(indented).Append("\n");
                }

                sectionBuilder.Append("</section>");
                bodyBlocks.Add(sectionBuilder.ToString());
            }

            bool includeToc = options.IncludeToc;
            int tocDepth = options.TocDepth > 0 ? options.TocDepth : template.TocDepth;
            if (includeToc && tocDepth > 0)
            {
                string tocHtml = BuildToc(headings, tocDepth);
                if (!string.IsNullOrWhiteSpace(tocHtml))
                {
                    int insertIndex = options.IncludeTitlePage ? 1 : 0;
                    frontMatterBlocks.Insert(insertIndex, tocHtml);
                }
            }

            StringBuilder bodyBuilder = new();
            bodyBuilder.Append("<div class=\"export-doc\">\n")
                .Append("<div id=\"preview-frontmatter\" class=\"export-frontmatter\">\n")
                .Append(string.Join("\n", frontMatterBlocks))
                .Append("\n</div>\n")
                .Append("<div id=\"preview-body\" class=\"export-body\">\n")
                .Append(string.Join("\n", bodyBlocks))
                .Append("\n</div>\n")
                .Append("\n</div>\n");

            string css = BuildCss(template, title, options);
            return new RenderedHtml(css, bodyBuilder.ToString());
        }

        private static string BuildCss(ExportTemplate template, string documentTitle, ExportOptions options)
        {
            string dateValue = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            TokenContext tokenContext = new(documentTitle, dateValue);

            bool usesSectionToken = ContainsToken(template.HeaderLeft, "SectionTitle")
                || ContainsToken(template.HeaderCenter, "SectionTitle")
                || ContainsToken(template.HeaderRight, "SectionTitle")
                || ContainsToken(template.FooterLeft, "SectionTitle")
                || ContainsToken(template.FooterCenter, "SectionTitle")
                || ContainsToken(template.FooterRight, "SectionTitle");

            bool usesPageToken = ContainsToken(template.HeaderLeft, "PageNumber")
                || ContainsToken(template.HeaderCenter, "PageNumber")
                || ContainsToken(template.HeaderRight, "PageNumber")
                || ContainsToken(template.FooterLeft, "PageNumber")
                || ContainsToken(template.FooterCenter, "PageNumber")
                || ContainsToken(template.FooterRight, "PageNumber");

            string? headerLeft = template.HeaderEnabled ? template.HeaderLeft : null;
            string? headerCenter = template.HeaderEnabled ? template.HeaderCenter : null;
            string? headerRight = template.HeaderEnabled ? template.HeaderRight : null;

            string? footerLeft = template.FooterEnabled ? template.FooterLeft : null;
            string? footerCenter = template.FooterEnabled ? template.FooterCenter : null;
            string? footerRight = template.FooterEnabled ? template.FooterRight : null;

            if (template.PageNumbersEnabled && !usesPageToken)
            {
                if (string.IsNullOrWhiteSpace(footerCenter) && string.IsNullOrWhiteSpace(footerRight) && string.IsNullOrWhiteSpace(footerLeft))
                {
                    footerCenter = "{PageNumber}";
                }
                else if (string.IsNullOrWhiteSpace(footerRight))
                {
                    footerRight = "{PageNumber}";
                }
                else if (string.IsNullOrWhiteSpace(footerCenter))
                {
                    footerCenter = "{PageNumber}";
                }
                else if (string.IsNullOrWhiteSpace(footerLeft))
                {
                    footerLeft = "{PageNumber}";
                }
                else
                {
                    footerRight = footerRight + " {PageNumber}";
                }
            }

            bool headerEnabled = template.HeaderEnabled && (HasContent(headerLeft) || HasContent(headerCenter) || HasContent(headerRight));
            bool footerEnabled = template.FooterEnabled || template.PageNumbersEnabled;

            StringBuilder css = new();
            css.Append("    :root {\n")
                .Append("      --page-width: ").Append(template.PageWidthMm).Append("mm;\n")
                .Append("      --page-height: ").Append(template.PageHeightMm).Append("mm;\n")
                .Append("      --margin-top: ").Append(template.MarginTopMm).Append("mm;\n")
                .Append("      --margin-right: ").Append(template.MarginRightMm).Append("mm;\n")
                .Append("      --margin-bottom: ").Append(template.MarginBottomMm).Append("mm;\n")
                .Append("      --margin-left: ").Append(template.MarginLeftMm).Append("mm;\n")
                .Append("    }\n\n");

            css.Append("    @page {\n")
                .Append("      size: ").Append(template.PageWidthMm).Append("mm ")
                .Append(template.PageHeightMm).Append("mm;\n")
                .Append("      margin: ").Append(template.MarginTopMm).Append("mm ")
                .Append(template.MarginRightMm).Append("mm ")
                .Append(template.MarginBottomMm).Append("mm ")
                .Append(template.MarginLeftMm).Append("mm;\n");

            if (headerEnabled)
            {
                AppendMarginBox(css, "@top-left", BuildContentExpression(headerLeft, tokenContext, usesSectionToken, template.PageNumbersEnabled));
                AppendMarginBox(css, "@top-center", BuildContentExpression(headerCenter, tokenContext, usesSectionToken, template.PageNumbersEnabled));
                AppendMarginBox(css, "@top-right", BuildContentExpression(headerRight, tokenContext, usesSectionToken, template.PageNumbersEnabled));
            }

            if (footerEnabled)
            {
                AppendMarginBox(css, "@bottom-left", BuildContentExpression(footerLeft, tokenContext, usesSectionToken, template.PageNumbersEnabled));
                AppendMarginBox(css, "@bottom-center", BuildContentExpression(footerCenter, tokenContext, usesSectionToken, template.PageNumbersEnabled));
                AppendMarginBox(css, "@bottom-right", BuildContentExpression(footerRight, tokenContext, usesSectionToken, template.PageNumbersEnabled));
            }

            css.Append("    }\n\n");

            if (template.PageNumbersEnabled)
            {
                int start = Math.Max(1, template.PageNumberStart);
                css.Append("    body { counter-reset: page ").Append(start - 1).Append("; }\n");
            }

            css.Append("    body { margin: 0; font-family: ")
                .Append(CssQuote(template.FontFamily)).Append("; font-size: ")
                .Append(template.BodyFontSizePt.ToString(CultureInfo.InvariantCulture)).Append("pt; line-height: ")
                .Append(template.LineHeight.ToString(CultureInfo.InvariantCulture)).Append("; color: #111; }\n")
                .Append("    .export-doc { padding: 0; }\n")
                .Append("    .export-frontmatter:not(:empty) { break-after: page; page-break-after: always; }\n")
                .Append("    h1 { font-size: 2em; margin: 0 0 0.6em 0; }\n")
                .Append("    h2 { font-size: 1.4em; margin: 1.2em 0 0.4em 0; }\n")
                .Append("    p { margin: 0 0 ")
                .Append(template.ParagraphSpacingPt.ToString(CultureInfo.InvariantCulture))
                .Append("pt 0; }\n")
                .Append("    .export-section { margin-bottom: 1.2em; }\n")
                .Append("    .export-title-page { text-align: center; padding: 120px 0 80px; break-after: page; page-break-after: always; }\n")
                .Append("    .export-title-page h1 { margin-bottom: 0.3em; }\n")
                .Append("    .export-title-page .title-subtitle { font-size: 1.2em; color: #444; margin-bottom: 1.2em; }\n")
                .Append("    .export-title-page .title-meta { margin-top: 0.6em; font-size: 0.95em; color: #555; }\n")
                .Append("    .export-toc { margin: 1.5em 0 2em 0; }\n")
                .Append("    .export-toc h2 { font-size: 1.2em; margin-bottom: 0.6em; }\n")
                .Append("    .export-toc ol { list-style: none; padding-left: 0; }\n")
                .Append("    .export-toc li { margin: 0.25em 0; }\n")
                .Append("    .export-toc .toc-level-2 { margin-left: 1em; }\n")
                .Append("    .export-toc .toc-level-3 { margin-left: 2em; }\n")
                .Append("    .export-toc .toc-level-4 { margin-left: 3em; }\n")
                .Append("    .export-toc .toc-level-5 { margin-left: 4em; }\n")
                .Append("    .export-toc .toc-level-6 { margin-left: 5em; }\n")
                .Append("    .export-toc a { color: inherit; text-decoration: none; }\n");

            if (usesSectionToken)
            {
                css.Append("    h2 { string-set: section-title content(text); }\n");
            }

            if (HasChapterBreak(options, "h1"))
            {
                css.Append("    .export-doc h1:not(.export-title), .export-section-title { break-before: page; page-break-before: always; }\n");
            }

            if (HasChapterBreak(options, "section"))
            {
                css.Append("    .export-section { break-before: page; page-break-before: always; }\n")
                    .Append("    .export-section:first-of-type { break-before: auto; page-break-before: auto; }\n");
            }

            css.Append("    /* TotalPages is not available in HTML-only exports. */\n");

            return css.ToString();
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

            builder.Append("</section>");
            return builder.ToString();
        }

        private static bool HasChapterBreak(ExportOptions options, string rule)
        {
            return options.ChapterBreakRules is not null
                && options.ChapterBreakRules.Any(value => string.Equals(value, rule, StringComparison.OrdinalIgnoreCase));
        }

        private static void AppendMarginBox(StringBuilder css, string marginBox, string contentExpression)
        {
            if (string.IsNullOrWhiteSpace(contentExpression))
            {
                return;
            }

            css.Append("      ").Append(marginBox).Append(" { content: ")
                .Append(contentExpression)
                .Append("; font-size: 9pt; color: #555; }\n");
        }

        private static string BuildContentExpression(
            string? template,
            TokenContext context,
            bool allowSectionTitle,
            bool pageNumbersEnabled)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return string.Empty;
            }

            List<string> parts = new();
            int index = 0;
            foreach (Match match in TokenRegex.Matches(template))
            {
                if (match.Index > index)
                {
                    string literal = template.Substring(index, match.Index - index);
                    AddLiteral(parts, literal);
                }

                string token = match.Groups[1].Value;
                switch (token)
                {
                    case "DocumentTitle":
                        AddLiteral(parts, context.DocumentTitle);
                        break;
                    case "Date":
                        AddLiteral(parts, context.DateValue);
                        break;
                    case "SectionTitle":
                        if (allowSectionTitle)
                        {
                            parts.Add("string(section-title)");
                        }
                        break;
                    case "PageNumber":
                        if (pageNumbersEnabled)
                        {
                            parts.Add("counter(page)");
                        }
                        break;
                    case "TotalPages":
                        if (pageNumbersEnabled)
                        {
                            AddLiteral(parts, "?");
                        }
                        break;
                }

                index = match.Index + match.Length;
            }

            if (index < template.Length)
            {
                AddLiteral(parts, template.Substring(index));
            }

            return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        }

        private static void AddLiteral(ICollection<string> parts, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            parts.Add("\"" + EscapeCssString(value) + "\"");
        }

        private static string EscapeCssString(string value)
        {
            return value.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private static string CssQuote(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "serif";
            }

            return "\"" + EscapeCssString(value.Trim()) + "\", serif";
        }

        private static bool ContainsToken(string? value, string token)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.Contains("{" + token + "}", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasContent(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private string ConvertSectionContentToHtml(
            SectionContent content,
            string sectionTitle,
            HeadingIdGenerator idGenerator,
            List<ExportHeading> headings)
        {
            if (content is null || string.IsNullOrWhiteSpace(content.Value))
            {
                return string.Empty;
            }

            string format = content.Format ?? string.Empty;
            if (string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase))
            {
                return MarkdownToHtml(content.Value, idGenerator, headings);
            }

            string value = ExportHelpers.NormalizeSectionHtmlForExport(content.Value, sectionTitle).Trim();
            if (!value.Contains('<', StringComparison.Ordinal))
            {
                return $"<p>{WebUtility.HtmlEncode(value)}</p>";
            }

            return EnsureHeadingIds(value, idGenerator, headings);
        }

        private static string MarkdownToHtml(string markdown, HeadingIdGenerator idGenerator, List<ExportHeading> headings)
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
                    string text = headingMatch.Groups[2].Value.Trim();
                    string id = idGenerator.NextId(text);
                    headings.Add(new ExportHeading(level, text, id));
                    string htmlText = RenderInlineMarkdown(text);
                    builder.Append("<h").Append(level).Append(" id=\"").Append(id).Append("\">")
                        .Append(htmlText)
                        .Append("</h").Append(level).Append(">\n");
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

        private static string EnsureHeadingIds(string html, HeadingIdGenerator idGenerator, List<ExportHeading> headings)
        {
            return HtmlHeadingRegex.Replace(html, match =>
            {
                string levelText = match.Groups[1].Value;
                string attrs = match.Groups[2].Value;
                string inner = match.Groups[3].Value;
                int level = int.TryParse(levelText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                    ? parsed
                    : 2;

                string existingId = ExtractId(attrs);
                string text = StripHtml(inner);
                string id = existingId;
                if (string.IsNullOrWhiteSpace(id))
                {
                    id = idGenerator.NextId(text);
                }

                headings.Add(new ExportHeading(level, text, id));

                string normalizedAttrs = attrs;
                if (string.IsNullOrWhiteSpace(existingId))
                {
                    normalizedAttrs = " id=\"" + id + "\"" + attrs;
                }

                return $"<h{level}{normalizedAttrs}>{inner}</h{level}>";
            });
        }

        private static string ExtractId(string attrs)
        {
            if (string.IsNullOrWhiteSpace(attrs))
            {
                return string.Empty;
            }

            Match match = Regex.Match(attrs, "\\bid\\s*=\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string StripHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            string stripped = Regex.Replace(html, "<.*?>", string.Empty);
            return WebUtility.HtmlDecode(stripped).Trim();
        }

        private static string BuildToc(IReadOnlyList<ExportHeading> headings, int depth)
        {
            if (headings.Count == 0 || depth < 1)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            builder.Append("<nav class=\"export-toc\">\n")
                .Append("  <h2>Table of Contents</h2>\n")
                .Append("  <ol>\n");

            foreach (ExportHeading heading in headings)
            {
                if (heading.Level > depth)
                {
                    continue;
                }

                builder.Append("    <li class=\"toc-level-").Append(heading.Level).Append("\">")
                    .Append("<a href=\"#").Append(heading.Id).Append("\">")
                    .Append(WebUtility.HtmlEncode(heading.Text))
                    .Append("</a></li>\n");
            }

            builder.Append("  </ol>\n")
                .Append("</nav>");

            return builder.ToString();
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

        private sealed record RenderedHtml(string Css, string BodyHtml);

        private sealed record ExportHeading(int Level, string Text, string Id);

        private sealed record TokenContext(string DocumentTitle, string DateValue);

        private sealed class HeadingIdGenerator
        {
            private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

            public string NextId(string text)
            {
                string slug = Slugify(text);
                if (!_counts.TryGetValue(slug, out int count))
                {
                    _counts[slug] = 1;
                    return slug;
                }

                count++;
                _counts[slug] = count;
                return slug + "-" + count.ToString(CultureInfo.InvariantCulture);
            }

            private static string Slugify(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return "heading";
                }

                StringBuilder builder = new();
                bool lastDash = false;
                foreach (char ch in value.Trim().ToLowerInvariant())
                {
                    if (char.IsLetterOrDigit(ch))
                    {
                        builder.Append(ch);
                        lastDash = false;
                    }
                    else if (!lastDash)
                    {
                        builder.Append('-');
                        lastDash = true;
                    }
                }

                string slug = builder.ToString().Trim('-');
                return string.IsNullOrWhiteSpace(slug) ? "heading" : slug;
            }
        }
    }
}
