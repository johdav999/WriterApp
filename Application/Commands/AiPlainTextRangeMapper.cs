using System;
using System.Collections.Generic;
using System.Net;

namespace WriterApp.Application.Commands
{
    internal static class AiPlainTextRangeMapper
    {
        private static readonly HashSet<string> BlockSeparatorTags = new(StringComparer.OrdinalIgnoreCase)
        {
            "p",
            "div",
            "h1",
            "h2",
            "h3",
            "h4",
            "h5",
            "h6",
            "li",
            "blockquote",
            "ul",
            "ol",
            "section",
            "article",
            "header",
            "footer",
            "pre",
            "code"
        };

        internal static bool TryMapPlainTextRangeToHtml(string html, TextRange range, out int startHtml, out int endHtml)
        {
            startHtml = -1;
            endHtml = -1;

            int targetStart = range.Start;
            int targetEnd = range.Start + range.Length;
            int plainIndex = 0;

            for (int i = 0; i < html.Length; i++)
            {
                char current = html[i];
                if (current == '<')
                {
                    int tagEnd = html.IndexOf('>', i);
                    if (tagEnd < 0)
                    {
                        break;
                    }

                    string tag = html.Substring(i + 1, tagEnd - i - 1);
                    if (IsBlockSeparatorTag(tag))
                    {
                        if (plainIndex == targetStart && startHtml < 0)
                        {
                            startHtml = tagEnd + 1;
                        }

                        plainIndex += 1;
                        if (plainIndex == targetEnd && endHtml < 0)
                        {
                            endHtml = tagEnd + 1;
                            break;
                        }
                    }

                    i = tagEnd;
                    continue;
                }

                if (current == '&')
                {
                    int entityEnd = html.IndexOf(';', i);
                    if (entityEnd > i)
                    {
                        string entity = html.Substring(i, entityEnd - i + 1);
                        string decoded = WebUtility.HtmlDecode(entity) ?? string.Empty;
                        for (int decodedIndex = 0; decodedIndex < decoded.Length; decodedIndex++)
                        {
                            if (plainIndex == targetStart && startHtml < 0)
                            {
                                startHtml = i;
                            }

                            plainIndex += 1;
                            if (plainIndex == targetEnd && endHtml < 0)
                            {
                                endHtml = entityEnd + 1;
                                break;
                            }
                        }

                        i = entityEnd;
                        if (endHtml >= 0)
                        {
                            break;
                        }

                        continue;
                    }
                }

                if (plainIndex == targetStart && startHtml < 0)
                {
                    startHtml = i;
                }

                plainIndex += 1;
                if (plainIndex == targetEnd && endHtml < 0)
                {
                    endHtml = i + 1;
                    break;
                }
            }

            return startHtml >= 0 && endHtml >= 0;
        }

        private static bool IsBlockSeparatorTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            string trimmed = tag.Trim();
            if (trimmed.StartsWith("br", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            bool isClosing = trimmed.StartsWith("/", StringComparison.Ordinal);
            string name = isClosing ? trimmed.Substring(1) : trimmed;
            int spaceIndex = name.IndexOf(' ');
            if (spaceIndex >= 0)
            {
                name = name.Substring(0, spaceIndex);
            }

            return isClosing && BlockSeparatorTags.Contains(name);
        }
    }
}
