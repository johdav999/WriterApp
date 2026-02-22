using System.Net;
using System.Text.RegularExpressions;

namespace WriterApp.Client.State
{
    /// <summary>
    /// Provides a deterministic HTML-to-plain-text mapping for selection and word metrics.
    /// </summary>
    public static class PlainTextMapper
    {
        private static readonly Regex BlockBoundaryRegex = new(
            @"</(p|div|h[1-6]|li|blockquote|section|article|header|footer|pre)>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LineBreakRegex = new(@"<br\s*/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex ExcessNewlinesRegex = new(@"\n{3,}", RegexOptions.Compiled);

        public static string ToPlainText(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return string.Empty;
            }

            string normalizedInput = html.Replace("\r\n", "\n").Replace('\r', '\n');
            string withBlockBoundaries = BlockBoundaryRegex.Replace(normalizedInput, "\n\n");
            string withLineBreaks = LineBreakRegex.Replace(withBlockBoundaries, "\n");
            string withoutTags = TagRegex.Replace(withLineBreaks, string.Empty);
            string decoded = WebUtility.HtmlDecode(withoutTags) ?? string.Empty;
            string normalizedOutput = decoded.Replace("\r\n", "\n").Replace('\r', '\n');
            normalizedOutput = ExcessNewlinesRegex.Replace(normalizedOutput, "\n\n");
            return normalizedOutput.TrimEnd('\n');
        }
    }
}
