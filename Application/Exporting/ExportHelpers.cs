using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using WriterApp.Domain.Documents;

namespace WriterApp.Application.Exporting
{
    internal static class ExportHelpers
    {
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
