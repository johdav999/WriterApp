using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WriterApp.Application.Documents
{
    public sealed record HeadingPageContent(Guid PageId, string Content);

    public sealed class HeadingPrefixCountersService
    {
        private static readonly Regex HeadingRegex = new(@"<h([1-6])\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public int[] CreateCounters()
        {
            return new int[7];
        }

        public int CountHeadings(string? content, int[] counters, out bool jsonParseFailed)
        {
            jsonParseFailed = false;
            if (string.IsNullOrWhiteSpace(content) || counters is null)
            {
                return 0;
            }

            string trimmed = content.TrimStart();
            if (trimmed.StartsWith("{", StringComparison.Ordinal) || trimmed.StartsWith("[", StringComparison.Ordinal))
            {
                if (TryApplyFromJson(trimmed, counters, out int jsonCount))
                {
                    return jsonCount;
                }

                jsonParseFailed = true;
            }

            return ApplyFromHtml(trimmed, counters);
        }

        public bool TryComputePrefix(
            IReadOnlyList<HeadingPageContent> orderedPages,
            Guid upToPageId,
            int[] counters)
        {
            if (orderedPages is null || counters is null)
            {
                return false;
            }

            for (int index = 0; index < orderedPages.Count; index += 1)
            {
                HeadingPageContent entry = orderedPages[index];
                if (entry.PageId == upToPageId)
                {
                    return true;
                }

                ApplyContent(entry.Content, counters, out _);
            }

            return false;
        }

        public void ApplyContent(string? content, int[] counters, out bool jsonParseFailed)
        {
            _ = CountHeadings(content, counters, out jsonParseFailed);
        }

        private static bool TryApplyFromJson(string json, int[] counters, out int headingCount)
        {
            headingCount = 0;
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                headingCount = WalkJson(doc.RootElement, counters);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static int WalkJson(JsonElement element, int[] counters)
        {
            int count = 0;
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    if (element.TryGetProperty("type", out JsonElement typeElement)
                        && string.Equals(typeElement.GetString(), "heading", StringComparison.OrdinalIgnoreCase))
                    {
                        int level = 1;
                        if (element.TryGetProperty("attrs", out JsonElement attrs)
                            && attrs.TryGetProperty("level", out JsonElement levelElement)
                            && levelElement.ValueKind == JsonValueKind.Number
                            && levelElement.TryGetInt32(out int parsed))
                        {
                            level = parsed;
                        }

                        ApplyHeading(level, counters);
                        count += 1;
                    }

                    if (element.TryGetProperty("content", out JsonElement contentElement))
                    {
                        count += WalkJson(contentElement, counters);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (JsonElement child in element.EnumerateArray())
                    {
                        count += WalkJson(child, counters);
                    }
                    break;
            }

            return count;
        }

        private static int ApplyFromHtml(string html, int[] counters)
        {
            int count = 0;
            foreach (Match match in HeadingRegex.Matches(html))
            {
                if (!match.Success || match.Groups.Count < 2)
                {
                    continue;
                }

                if (!int.TryParse(match.Groups[1].Value, out int level))
                {
                    continue;
                }

                ApplyHeading(level, counters);
                count += 1;
            }

            return count;
        }

        private static void ApplyHeading(int level, int[] counters)
        {
            int normalized = Math.Max(1, Math.Min(6, level));
            counters[normalized] += 1;
            for (int index = normalized + 1; index <= 6; index += 1)
            {
                counters[index] = 0;
            }
        }
    }
}
