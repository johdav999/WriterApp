using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace WriterApp.Application.Documents
{
    public interface IPageVersionDiffService
    {
        PageVersionDiffResultDto BuildDiff(
            Guid pageId,
            Guid fromVersionId,
            Guid? toVersionId,
            bool compareToCurrent,
            string fromText,
            string toText,
            string granularity,
            int maxLines);
    }

    public sealed class PageVersionDiffService : IPageVersionDiffService
    {
        private const int DefaultMaxLines = 800;

        public PageVersionDiffResultDto BuildDiff(
            Guid pageId,
            Guid fromVersionId,
            Guid? toVersionId,
            bool compareToCurrent,
            string fromText,
            string toText,
            string granularity,
            int maxLines)
        {
            int limit = maxLines > 0 ? maxLines : DefaultMaxLines;
            DiffGranularity mode = ParseGranularity(granularity);
            IReadOnlyList<string> baseLines = mode == DiffGranularity.Paragraph
                ? SplitParagraphs(fromText)
                : SplitLines(fromText);
            IReadOnlyList<string> compareLines = mode == DiffGranularity.Paragraph
                ? SplitParagraphs(toText)
                : SplitLines(toText);

            List<DiffOp<string>> ops = MyersDiff(baseLines, compareLines, StringComparer.Ordinal);
            List<PageVersionDiffLineDto> lines = BuildLinesWithWordDiff(ops);

            bool truncated = lines.Count > limit;
            if (truncated)
            {
                lines = lines.Take(limit).ToList();
            }

            return new PageVersionDiffResultDto(
                pageId,
                fromVersionId,
                toVersionId,
                compareToCurrent,
                mode == DiffGranularity.Paragraph ? "paragraph" : "line",
                truncated,
                limit,
                lines);
        }

        private static DiffGranularity ParseGranularity(string? granularity)
        {
            if (string.Equals(granularity, "paragraph", StringComparison.OrdinalIgnoreCase))
            {
                return DiffGranularity.Paragraph;
            }

            return DiffGranularity.Line;
        }

        private static IReadOnlyList<string> SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            return normalized.Split('\n');
        }

        private static IReadOnlyList<string> SplitParagraphs(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            string normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            string[] parts = Regex.Split(normalized, @"\n\s*\n", RegexOptions.Compiled);
            return parts
                .Select(part => part.Trim())
                .Where(part => part.Length > 0)
                .ToList();
        }

        private static List<PageVersionDiffLineDto> BuildLinesWithWordDiff(List<DiffOp<string>> ops)
        {
            List<PageVersionDiffLineDto> lines = new();
            int index = 0;
            while (index < ops.Count)
            {
                DiffOp<string> op = ops[index];
                if (op.Kind == DiffOpKind.Delete
                    && index + 1 < ops.Count
                    && ops[index + 1].Kind == DiffOpKind.Insert)
                {
                    DiffOp<string> add = ops[index + 1];
                    (List<PageVersionDiffSpanDto> removedSpans, List<PageVersionDiffSpanDto> addedSpans) =
                        BuildWordSpans(op.Value, add.Value);

                    lines.Add(new PageVersionDiffLineDto("removed", op.Value, removedSpans));
                    lines.Add(new PageVersionDiffLineDto("added", add.Value, addedSpans));
                    index += 2;
                    continue;
                }

                if (op.Kind == DiffOpKind.Equal)
                {
                    lines.Add(new PageVersionDiffLineDto("unchanged", op.Value, null));
                }
                else if (op.Kind == DiffOpKind.Insert)
                {
                    lines.Add(new PageVersionDiffLineDto("added", op.Value, null));
                }
                else
                {
                    lines.Add(new PageVersionDiffLineDto("removed", op.Value, null));
                }

                index++;
            }

            return lines;
        }

        private static (List<PageVersionDiffSpanDto> Removed, List<PageVersionDiffSpanDto> Added) BuildWordSpans(
            string removedText,
            string addedText)
        {
            IReadOnlyList<string> removedTokens = Tokenize(removedText);
            IReadOnlyList<string> addedTokens = Tokenize(addedText);
            List<DiffOp<string>> tokenOps = MyersDiff(removedTokens, addedTokens, StringComparer.Ordinal);

            List<PageVersionDiffSpanDto> removedSpans = new();
            List<PageVersionDiffSpanDto> addedSpans = new();

            foreach (DiffOp<string> op in tokenOps)
            {
                if (op.Kind == DiffOpKind.Equal)
                {
                    AppendSpan(removedSpans, "unchanged", op.Value);
                    AppendSpan(addedSpans, "unchanged", op.Value);
                }
                else if (op.Kind == DiffOpKind.Delete)
                {
                    AppendSpan(removedSpans, "removed", op.Value);
                }
                else if (op.Kind == DiffOpKind.Insert)
                {
                    AppendSpan(addedSpans, "added", op.Value);
                }
            }

            return (removedSpans, addedSpans);
        }

        private static IReadOnlyList<string> Tokenize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            MatchCollection matches = Regex.Matches(text, @"\w+|\s+|[^\w\s]+", RegexOptions.Compiled);
            return matches.Select(match => match.Value).ToList();
        }

        private static void AppendSpan(List<PageVersionDiffSpanDto> spans, string kind, string text)
        {
            if (spans.Count == 0)
            {
                spans.Add(new PageVersionDiffSpanDto(kind, text));
                return;
            }

            PageVersionDiffSpanDto last = spans[^1];
            if (string.Equals(last.Kind, kind, StringComparison.Ordinal))
            {
                spans[^1] = last with { Text = last.Text + text };
                return;
            }

            spans.Add(new PageVersionDiffSpanDto(kind, text));
        }

        private static List<DiffOp<T>> MyersDiff<T>(
            IReadOnlyList<T> a,
            IReadOnlyList<T> b,
            IEqualityComparer<T> comparer)
        {
            int n = a.Count;
            int m = b.Count;
            int max = n + m;
            Dictionary<int, int> v = new() { [1] = 0 };
            List<Dictionary<int, int>> trace = new();

            for (int d = 0; d <= max; d++)
            {
                Dictionary<int, int> next = new();
                for (int k = -d; k <= d; k += 2)
                {
                    int x;
                    if (k == -d || (k != d && Get(v, k - 1) < Get(v, k + 1)))
                    {
                        x = Get(v, k + 1);
                    }
                    else
                    {
                        x = Get(v, k - 1) + 1;
                    }

                    int y = x - k;
                    while (x < n && y < m && comparer.Equals(a[x], b[y]))
                    {
                        x++;
                        y++;
                    }

                    next[k] = x;
                    if (x >= n && y >= m)
                    {
                        trace.Add(next);
                        return BuildMyersResult(a, b, trace, comparer);
                    }
                }

                trace.Add(next);
                v = next;
            }

            return new List<DiffOp<T>>();
        }

        private static List<DiffOp<T>> BuildMyersResult<T>(
            IReadOnlyList<T> a,
            IReadOnlyList<T> b,
            List<Dictionary<int, int>> trace,
            IEqualityComparer<T> comparer)
        {
            int x = a.Count;
            int y = b.Count;
            List<DiffOp<T>> result = new();

            for (int d = trace.Count - 1; d >= 0; d--)
            {
                Dictionary<int, int> v = trace[d];
                int k = x - y;
                int prevK;
                if (k == -d || (k != d && Get(v, k - 1) < Get(v, k + 1)))
                {
                    prevK = k + 1;
                }
                else
                {
                    prevK = k - 1;
                }

                int prevX = Get(v, prevK);
                int prevY = prevX - prevK;

                while (x > prevX && y > prevY)
                {
                    result.Add(new DiffOp<T>(DiffOpKind.Equal, a[x - 1]));
                    x--;
                    y--;
                }

                if (d == 0)
                {
                    break;
                }

                if (x == prevX)
                {
                    result.Add(new DiffOp<T>(DiffOpKind.Insert, b[y - 1]));
                    y--;
                }
                else
                {
                    result.Add(new DiffOp<T>(DiffOpKind.Delete, a[x - 1]));
                    x--;
                }
            }

            result.Reverse();
            return result;
        }

        private static int Get(Dictionary<int, int> map, int key)
        {
            return map.TryGetValue(key, out int value) ? value : 0;
        }

        private enum DiffGranularity
        {
            Line,
            Paragraph
        }

        private enum DiffOpKind
        {
            Equal,
            Insert,
            Delete
        }

        private sealed record DiffOp<T>(DiffOpKind Kind, T Value);
    }

}
