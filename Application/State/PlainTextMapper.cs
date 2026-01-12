using System.Net;
using System.Text.RegularExpressions;

namespace WriterApp.Application.State
{
    /// <summary>
    /// Provides a deterministic HTML-to-plain-text mapping for selection and word metrics.
    /// </summary>
    public static class PlainTextMapper
    {
        private static readonly Regex BlockSeparatorRegex = new(
            @"</(p|div|h[1-6]|li|blockquote|ul|ol|section|article|header|footer|pre|code)>|<br\s*/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);

        public static string ToPlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            string withSeparators = BlockSeparatorRegex.Replace(html, " ");
            string withoutTags = TagRegex.Replace(withSeparators, string.Empty);
            string decoded = WebUtility.HtmlDecode(withoutTags);
            return decoded ?? string.Empty;
        }
    }
}
