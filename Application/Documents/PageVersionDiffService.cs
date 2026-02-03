using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;

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
            InlineGranularity mode = ParseGranularity(granularity);

            List<DiffBlockSource> baseBlocks = ExtractBlocks(fromText);
            List<DiffBlockSource> compareBlocks = ExtractBlocks(toText);

            List<DiffOp<DiffBlockSource>> ops = MyersDiff(baseBlocks, compareBlocks, DiffBlockComparer.Instance);
            List<PageVersionDiffBlockDto> blocks = BuildBlocks(ops, mode, out PageVersionDiffStatsDto stats);

            bool truncated = blocks.Count > limit;
            if (truncated)
            {
                blocks = blocks.Take(limit).ToList();
            }

            return new PageVersionDiffResultDto(
                pageId,
                fromVersionId,
                toVersionId,
                compareToCurrent,
                mode == InlineGranularity.Sentence ? "sentence" : "word",
                truncated,
                limit,
                blocks,
                stats);
        }

        private static InlineGranularity ParseGranularity(string? granularity)
        {
            if (string.Equals(granularity, "sentence", StringComparison.OrdinalIgnoreCase))
            {
                return InlineGranularity.Sentence;
            }

            return InlineGranularity.Word;
        }

        private static List<PageVersionDiffBlockDto> BuildBlocks(
            List<DiffOp<DiffBlockSource>> ops,
            InlineGranularity mode,
            out PageVersionDiffStatsDto stats)
        {
            List<PageVersionDiffBlockDto> blocks = new();
            int addedWords = 0;
            int removedWords = 0;
            int changedBlocks = 0;
            int addedBlocks = 0;
            int removedBlocks = 0;
            int index = 0;

            while (index < ops.Count)
            {
                DiffOp<DiffBlockSource> op = ops[index];
                if (op.Kind == DiffOpKind.Delete
                    && index + 1 < ops.Count
                    && ops[index + 1].Kind == DiffOpKind.Insert)
                {
                    DiffBlockSource removed = op.Value;
                    DiffBlockSource added = ops[index + 1].Value;
                    if (IsChangedPair(removed, added))
                    {
                        (InlineDiffResult inline, InlineDiffResult baseInline, InlineDiffResult compareInline) =
                            BuildInlineDiff(removed.Text, added.Text, mode);

                        blocks.Add(new PageVersionDiffBlockDto(
                            BuildBlockId(blocks.Count),
                            "changed",
                            new PageVersionDiffBlockContentDto(removed.Type, removed.Text, baseInline.Segments),
                            new PageVersionDiffBlockContentDto(added.Type, added.Text, compareInline.Segments),
                            inline.Segments,
                            BuildPreviewText(added.Text)));

                        addedWords += compareInline.AddedWords;
                        removedWords += baseInline.RemovedWords;
                        changedBlocks += 1;
                        index += 2;
                        continue;
                    }
                }

                if (op.Kind == DiffOpKind.Equal)
                {
                    DiffBlockSource block = op.Value;
                    blocks.Add(new PageVersionDiffBlockDto(
                        BuildBlockId(blocks.Count),
                        "unchanged",
                        new PageVersionDiffBlockContentDto(block.Type, block.Text, null),
                        new PageVersionDiffBlockContentDto(block.Type, block.Text, null),
                        null,
                        BuildPreviewText(block.Text)));
                }
                else if (op.Kind == DiffOpKind.Insert)
                {
                    DiffBlockSource block = op.Value;
                    blocks.Add(new PageVersionDiffBlockDto(
                        BuildBlockId(blocks.Count),
                        "added",
                        null,
                        new PageVersionDiffBlockContentDto(block.Type, block.Text, null),
                        new[] { new PageVersionDiffSpanDto("added", block.Text) },
                        BuildPreviewText(block.Text)));
                    addedBlocks += 1;
                    addedWords += CountWords(block.Text);
                }
                else
                {
                    DiffBlockSource block = op.Value;
                    blocks.Add(new PageVersionDiffBlockDto(
                        BuildBlockId(blocks.Count),
                        "removed",
                        new PageVersionDiffBlockContentDto(block.Type, block.Text, null),
                        null,
                        new[] { new PageVersionDiffSpanDto("removed", block.Text) },
                        BuildPreviewText(block.Text)));
                    removedBlocks += 1;
                    removedWords += CountWords(block.Text);
                }

                index++;
            }

            stats = new PageVersionDiffStatsDto(addedWords, removedWords, changedBlocks, addedBlocks, removedBlocks);
            return blocks;
        }

        private static bool IsChangedPair(DiffBlockSource removed, DiffBlockSource added)
        {
            if (!string.Equals(removed.Type, added.Type, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            double similarity = ComputeSimilarity(removed.Text, added.Text);
            return similarity >= 0.4;
        }

        private static double ComputeSimilarity(string left, string right)
        {
            IReadOnlyList<string> leftTokens = TokenizeWords(left);
            IReadOnlyList<string> rightTokens = TokenizeWords(right);
            if (leftTokens.Count == 0 && rightTokens.Count == 0)
            {
                return 1d;
            }

            List<DiffOp<string>> ops = MyersDiff(leftTokens, rightTokens, StringComparer.OrdinalIgnoreCase);
            int equal = ops.Count(op => op.Kind == DiffOpKind.Equal);
            int max = Math.Max(leftTokens.Count, rightTokens.Count);
            return max == 0 ? 0d : (double)equal / max;
        }

        private static (InlineDiffResult inline, InlineDiffResult baseInline, InlineDiffResult compareInline) BuildInlineDiff(
            string removedText,
            string addedText,
            InlineGranularity mode)
        {
            IReadOnlyList<string> removedTokens = mode == InlineGranularity.Sentence
                ? TokenizeSentences(removedText)
                : TokenizeWords(removedText);
            IReadOnlyList<string> addedTokens = mode == InlineGranularity.Sentence
                ? TokenizeSentences(addedText)
                : TokenizeWords(addedText);
            List<DiffOp<string>> tokenOps = MyersDiff(removedTokens, addedTokens, StringComparer.Ordinal);

            List<PageVersionDiffSpanDto> inlineSpans = new();
            List<PageVersionDiffSpanDto> baseSpans = new();
            List<PageVersionDiffSpanDto> compareSpans = new();
            int addedWords = 0;
            int removedWords = 0;

            foreach (DiffOp<string> op in tokenOps)
            {
                if (op.Kind == DiffOpKind.Equal)
                {
                    AppendSpan(inlineSpans, "unchanged", op.Value);
                    AppendSpan(baseSpans, "unchanged", op.Value);
                    AppendSpan(compareSpans, "unchanged", op.Value);
                }
                else if (op.Kind == DiffOpKind.Delete)
                {
                    AppendSpan(inlineSpans, "removed", op.Value);
                    AppendSpan(baseSpans, "removed", op.Value);
                    removedWords += CountWords(op.Value);
                }
                else if (op.Kind == DiffOpKind.Insert)
                {
                    AppendSpan(inlineSpans, "added", op.Value);
                    AppendSpan(compareSpans, "added", op.Value);
                    addedWords += CountWords(op.Value);
                }
            }

            return (
                new InlineDiffResult(inlineSpans, addedWords, removedWords),
                new InlineDiffResult(baseSpans, 0, removedWords),
                new InlineDiffResult(compareSpans, addedWords, 0));
        }

        private static IReadOnlyList<string> TokenizeWords(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            MatchCollection matches = Regex.Matches(text, @"\w+|\s+|[^\w\s]+", RegexOptions.Compiled);
            return matches.Select(match => match.Value).ToList();
        }

        private static IReadOnlyList<string> TokenizeSentences(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Array.Empty<string>();
            }

            MatchCollection matches = Regex.Matches(text, @"[^.!?]+[.!?]*\s*", RegexOptions.Compiled);
            return matches.Select(match => match.Value).Where(value => value.Length > 0).ToList();
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

        private static string BuildBlockId(int index) => $"block-{index}";

        private static string BuildPreviewText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.Trim();
            if (trimmed.Length <= 120)
            {
                return trimmed;
            }

            return trimmed[..117] + "...";
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return 0;
            }

            return Regex.Matches(text, @"\b\w+\b", RegexOptions.Compiled).Count;
        }

        private static readonly Regex BlockSeparatorRegex = new(
            @"</(p|div|h[1-6]|li|blockquote|ul|ol|section|article|header|footer|pre|code)>|<br\s*/?>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex TagRegex = new("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex HeadingRegex = new("<h[1-6]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ListItemRegex = new("<li", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static List<DiffBlockSource> ExtractBlocks(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return new List<DiffBlockSource>();
            }

            string normalized = html.Replace("\r\n", "\n").Replace("\r", "\n");
            string withBreaks = BlockSeparatorRegex.Replace(normalized, "\n\n");
            string[] parts = Regex.Split(withBreaks, @"\n\s*\n", RegexOptions.Compiled);

            List<DiffBlockSource> blocks = new();
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                {
                    continue;
                }

                string type = DetectBlockType(trimmed);
                string withoutTags = TagRegex.Replace(trimmed, string.Empty);
                string decoded = WebUtility.HtmlDecode(withoutTags) ?? string.Empty;
                string text = decoded.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                blocks.Add(new DiffBlockSource(type, text));
            }

            return blocks;
        }

        private static string DetectBlockType(string htmlChunk)
        {
            if (HeadingRegex.IsMatch(htmlChunk))
            {
                return "heading";
            }

            if (ListItemRegex.IsMatch(htmlChunk))
            {
                return "listItem";
            }

            return "paragraph";
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

        private enum DiffOpKind
        {
            Equal,
            Insert,
            Delete
        }

        private sealed record DiffOp<T>(DiffOpKind Kind, T Value);

        private enum InlineGranularity
        {
            Word,
            Sentence
        }

        private sealed record DiffBlockSource(string Type, string Text);

        private sealed class DiffBlockComparer : IEqualityComparer<DiffBlockSource>
        {
            public static readonly DiffBlockComparer Instance = new();

            public bool Equals(DiffBlockSource? x, DiffBlockSource? y)
            {
                if (x is null || y is null)
                {
                    return false;
                }

                return string.Equals(x.Type, y.Type, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.Text, y.Text, StringComparison.Ordinal);
            }

            public int GetHashCode(DiffBlockSource obj)
            {
                return HashCode.Combine(obj.Type.ToLowerInvariant(), obj.Text);
            }
        }

        private sealed record InlineDiffResult(IReadOnlyList<PageVersionDiffSpanDto> Segments, int AddedWords, int RemovedWords);
    }

}
