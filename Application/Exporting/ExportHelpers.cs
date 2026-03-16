using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    internal static class ExportHelpers
    {
        private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
        private static readonly Regex HeadingRegex = new("<h[1-6][^>]*>(.*?)</h[1-6]>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);

        public static IReadOnlyList<Section> GetOrderedSections(Document document)
        {
            return document.Chapters
                .OrderBy(chapter => chapter.Order)
                .SelectMany(chapter => chapter.Sections.OrderBy(section => section.Order))
                .ToList();
        }

        public static string GetDocumentTitle(Document document)
        {
            string title = document.Metadata.Title ?? string.Empty;
            return string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
        }

        public static string GetSectionTitle(Section section)
        {
            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                return section.Title.Trim();
            }

            string? derived = DeriveTitleFromHtml(section.Content.Value);
            return string.IsNullOrWhiteSpace(derived) ? "Untitled section" : derived;
        }

        public static string SanitizeFileName(string? baseName, string fallbackName, string extension)
        {
            string candidate = string.IsNullOrWhiteSpace(baseName) ? fallbackName : baseName.Trim();
            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string cleaned = new string(candidate.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                cleaned = fallbackName;
            }

            if (!cleaned.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                cleaned += extension;
            }

            return cleaned;
        }

        public static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
        }

        public static string HtmlDecode(string value)
        {
            return WebUtility.HtmlDecode(value) ?? string.Empty;
        }

        public static void AssertSynopsisNotIncluded(string output, Document document)
        {
#if DEBUG
            if (document?.Synopsis is null)
            {
                return;
            }

            string[] values =
            {
                document.Synopsis.Logline,
                document.Synopsis.Premise,
                document.Synopsis.Theme,
                document.Synopsis.ProtagonistArc,
                document.Synopsis.CentralConflict,
                document.Synopsis.Stakes,
                document.Synopsis.Setting,
                document.Synopsis.EndingIntent,
                document.Synopsis.OpenQuestions,
                document.Synopsis.Notes
            };

            foreach (string value in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (output.Contains(value, StringComparison.Ordinal))
                {
                    Debug.Assert(false, "Synopsis content should not appear in document export output.");
                    break;
                }
            }
#endif
        }

        public static bool HasChapterBreak(ExportOptions options, string rule)
        {
            return options.ChapterBreakRules is not null
                && options.ChapterBreakRules.Any(value => string.Equals(value, rule, StringComparison.OrdinalIgnoreCase));
        }

        public static bool ShouldIncludeCover(ExportOptions options)
        {
            return options.IncludeCover && !string.IsNullOrWhiteSpace(options.CoverImageUrl);
        }

        public static string BuildCoverPageBlock(string coverImageUrl)
        {
            return "<section class=\"export-cover-page\">"
                + $"<img src=\"{WebUtility.HtmlEncode(coverImageUrl)}\" alt=\"Project cover\" />"
                + "</section>";
        }

        public static string BuildCoverPageCss()
        {
            return "    .export-cover-page { min-height: calc(var(--page-height, 279mm) - 2rem); display: flex; align-items: center; justify-content: center; padding: 0; break-after: page; page-break-after: always; }\n"
                + "    .export-cover-page img { width: 100%; max-width: 420px; max-height: calc(var(--page-height, 279mm) - 4rem); object-fit: contain; display: block; margin: 0 auto; box-shadow: 0 18px 40px rgba(15, 23, 42, 0.16); }\n";
        }

        public static string BuildSectionHtml(SectionContent content, string sectionTitle, bool allowStripHeading = true)
        {
            if (content is null || string.IsNullOrWhiteSpace(content.Value))
            {
                return string.Empty;
            }

            string format = content.Format ?? string.Empty;
            if (string.Equals(format, "markdown", StringComparison.OrdinalIgnoreCase))
            {
                // TODO: Map markdown properly (headings, lists, and inline marks).
                string encoded = WebUtility.HtmlEncode(content.Value);
                return $"<p>{encoded}</p>";
            }

            string value = content.Value;
            if (allowStripHeading)
            {
                value = NormalizeSectionHtmlForExport(value, sectionTitle);
            }

            value = value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (!value.Contains('<', StringComparison.Ordinal))
            {
                return $"<p>{WebUtility.HtmlEncode(value)}</p>";
            }

            return value;
        }

        public static string NormalizeSectionHtmlForExport(string html, string sectionTitle)
        {
            if (string.IsNullOrWhiteSpace(html) || string.IsNullOrWhiteSpace(sectionTitle))
            {
                return html;
            }

            HtmlParser parser = new();
            var document = parser.ParseDocument(html);
            var firstHeading = document.QuerySelector("h1, h2");
            if (firstHeading is null)
            {
                return html;
            }

            string headingText = NormalizeHeadingText(firstHeading.TextContent);
            string titleText = NormalizeHeadingText(sectionTitle);
            if (!string.Equals(headingText, titleText, StringComparison.OrdinalIgnoreCase))
            {
                return html;
            }

            firstHeading.Remove();
            return document.Body?.InnerHtml ?? html;
        }

        private static string NormalizeHeadingText(string value)
        {
            string trimmed = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return WhitespaceRegex.Replace(trimmed, " ");
        }


        private static string? DeriveTitleFromHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return null;
            }

            Match match = HeadingRegex.Match(html);
            if (!match.Success)
            {
                return null;
            }

            string withoutTags = TagRegex.Replace(match.Groups[1].Value, string.Empty);
            string decoded = HtmlDecode(withoutTags);
            return string.IsNullOrWhiteSpace(decoded) ? null : decoded.Trim();
        }
    }
}
