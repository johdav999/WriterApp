using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    public sealed class MarkdownExportRenderer : IExportRenderer
    {
        private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex TagNameRegex = new(@"^</?\s*([a-z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

        public ExportFormat Format => ExportFormat.Markdown;

        public Task<ExportResult> RenderAsync(Document document, ExportOptions options)
        {
            ExportOptions resolved = options ?? new ExportOptions();
            string title = ExportHelpers.GetDocumentTitle(document);
            StringBuilder builder = new();

            if (resolved.IncludeTitlePage)
            {
                // Document title maps to a top-level heading.
                builder.Append("# ").Append(title).Append("\n\n");
            }

            foreach (Section section in ExportHelpers.GetOrderedSections(document))
            {
                string sectionTitle = ExportHelpers.GetSectionTitle(section);
                // Section titles map to second-level headings.
                builder.Append("## ").Append(sectionTitle).Append("\n\n");

                string sectionMarkdown = ConvertSectionContentToMarkdown(section.Content, sectionTitle);
                if (!string.IsNullOrWhiteSpace(sectionMarkdown))
                {
                    builder.Append(sectionMarkdown.Trim()).Append("\n\n");
                }
            }

            string markdown = ExportHelpers.NormalizeLineEndings(builder.ToString()).TrimEnd() + "\n";
            byte[] content = Encoding.UTF8.GetBytes(markdown);
            string fileName = ExportHelpers.SanitizeFileName(document.Metadata.Title, "document", ".md");

            ExportResult result = new(content, "text/markdown", fileName);
            return Task.FromResult(result);
        }

        private static string ConvertSectionContentToMarkdown(SectionContent content, string sectionTitle)
        {
            if (content is null || string.IsNullOrWhiteSpace(content.Value))
            {
                return string.Empty;
            }

            string format = content.Format ?? string.Empty;
            if (string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase))
            {
                return ExportHelpers.NormalizeLineEndings(content.Value);
            }

            // HTML content is mapped to Markdown for export.
            string normalizedHtml = ExportHelpers.NormalizeSectionHtmlForExport(content.Value, sectionTitle);
            return HtmlToMarkdown(normalizedHtml);
        }

        private static string HtmlToMarkdown(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            Stack<string> listStack = new();
            bool inPre = false;
            bool atLineStart = true;

            int index = 0;
            foreach (Match match in TagRegex.Matches(html))
            {
                AppendText(html.Substring(index, match.Index - index));
                HandleTag(match.Value);
                index = match.Index + match.Length;
            }
            AppendText(html.Substring(index));

            string output = ExportHelpers.NormalizeLineEndings(builder.ToString());
            output = Regex.Replace(output, "\n{3,}", "\n\n");
            return output.Trim();

            void AppendText(string text)
            {
                if (string.IsNullOrEmpty(text))
                {
                    return;
                }

                string decoded = ExportHelpers.HtmlDecode(text);
                if (!inPre)
                {
                    decoded = WhitespaceRegex.Replace(decoded, " ");
                }

                if (atLineStart)
                {
                    decoded = decoded.TrimStart();
                }

                if (decoded.Length == 0)
                {
                    return;
                }

                builder.Append(decoded);
                atLineStart = decoded.EndsWith("\n", StringComparison.Ordinal);
            }

            void HandleTag(string tag)
            {
                Match nameMatch = TagNameRegex.Match(tag);
                if (!nameMatch.Success)
                {
                    return;
                }

                string name = nameMatch.Groups[1].Value.ToLowerInvariant();
                bool isClosing = tag.StartsWith("</", StringComparison.Ordinal);

                switch (name)
                {
                    case "br":
                        AppendLine();
                        break;
                    case "p":
                        if (isClosing)
                        {
                            AppendParagraphBreak();
                        }
                        break;
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                    case "h5":
                    case "h6":
                        HandleHeading(name, isClosing);
                        break;
                    case "strong":
                    case "b":
                        AppendInlineMarker("**", isClosing);
                        break;
                    case "em":
                    case "i":
                        AppendInlineMarker("_", isClosing);
                        break;
                    case "code":
                        if (!inPre)
                        {
                            AppendInlineMarker("`", isClosing);
                        }
                        break;
                    case "pre":
                        if (!isClosing)
                        {
                            AppendLine();
                            builder.Append("```").Append("\n");
                            atLineStart = true;
                            inPre = true;
                        }
                        else
                        {
                            TrimTrailingWhitespace();
                            builder.Append("\n```").Append("\n\n");
                            atLineStart = true;
                            inPre = false;
                        }
                        break;
                    case "ul":
                    case "ol":
                        if (!isClosing)
                        {
                            listStack.Push(name);
                        }
                        else if (listStack.Count > 0)
                        {
                            listStack.Pop();
                            AppendParagraphBreak();
                        }
                        break;
                    case "li":
                        if (!isClosing)
                        {
                            AppendLine();
                            string prefix = listStack.Count > 0 && listStack.Peek() == "ol" ? "1. " : "- ";
                            builder.Append(prefix);
                            atLineStart = false;
                        }
                        break;
                }
            }

            void HandleHeading(string name, bool isClosing)
            {
                if (isClosing)
                {
                    AppendParagraphBreak();
                    return;
                }

                AppendLine();
                int level = name[1] - '0';
                builder.Append(new string('#', level)).Append(' ');
                atLineStart = false;
            }

            void AppendInlineMarker(string marker, bool isClosing)
            {
                builder.Append(marker);
            }

            void AppendLine()
            {
                TrimTrailingWhitespace();
                if (!atLineStart)
                {
                    builder.Append("\n");
                }

                atLineStart = true;
            }

            void AppendParagraphBreak()
            {
                TrimTrailingWhitespace();
                builder.Append("\n\n");
                atLineStart = true;
            }

            void TrimTrailingWhitespace()
            {
                while (builder.Length > 0 && (builder[^1] == ' ' || builder[^1] == '\t'))
                {
                    builder.Length -= 1;
                }
            }
        }
    }
}
