namespace WriterApp.Shared;

public sealed record ConsistencyPageSource(Guid PageId, string Html, string PlainText);
public sealed record ConsistencyPrimaryPassage(Guid PageId, int Start, int Length, string Text);

/// <summary>Provider offsets are hints. Only unique evidence in the captured pages authorizes a target.</summary>
public static class ConsistencyPagePassages
{
    public static string Text(IReadOnlyList<ConsistencyPageSource> pages)
    {
        if (pages.Count is 0 or > 1000 || pages.Any(p => p.PageId == Guid.Empty)
            || pages.Select(p => p.PageId).Distinct().Count() != pages.Count
            || pages.Sum(p => (long)p.Html.Length) > 2_000_000)
            throw new InvalidDataException("Invalid or oversized consistency page source.");
        string text = string.Join("\n\n", pages.Select(p => p.PlainText));
        if (string.IsNullOrWhiteSpace(text) || text.Length > 100_000)
            throw new InvalidDataException("Check a section with 1–100,000 characters of writing. Context was not truncated.");
        return text;
    }
    public static ConsistencyPrimaryPassage Resolve(IReadOnlyList<ConsistencyPageSource> pages, string quote, int start, int length)
    {
        string context = Text(pages);
        if (string.IsNullOrWhiteSpace(quote) || quote.Length > 100_000)
            throw new InvalidDataException("This finding has no verified primary passage. Run the check again.");
        var matches = new List<(ConsistencyPageSource Page, int Start, int Global)>(); int offset = 0;
        foreach (var page in pages)
        {
            for (int at = page.PlainText.IndexOf(quote, StringComparison.Ordinal); at >= 0 && matches.Count < 2;
                at = page.PlainText.IndexOf(quote, at + 1, StringComparison.Ordinal))
                matches.Add((page, at, offset + at));
            if (matches.Count > 1) break;
            offset += page.PlainText.Length + 2;
        }
        if (matches.Count != 1)
            throw new InvalidOperationException(matches.Count == 0
                ? "The primary passage is missing from the checked pages. Run the consistency check again."
                : "The primary passage is ambiguous across the checked pages. Use a unique passage and run the check again.");
        var match = matches[0];
        if (!Boundary(match.Page.PlainText, match.Start) || !Boundary(match.Page.PlainText, match.Start + quote.Length))
            throw new InvalidDataException("The quoted passage cuts a word or Unicode character. Use a complete passage and check again.");
        // A narrower anchor must lie entirely inside the unique quoted evidence and preserve word/Unicode boundaries.
        if (length > 0 && start >= match.Global && start <= match.Global + quote.Length - length
            && Boundary(context, start) && Boundary(context, start + length))
            return new(match.Page.PageId, match.Start + start - match.Global, length, context.Substring(start, length));
        return new(match.Page.PageId, match.Start, quote.Length, quote);
    }
    private static bool Boundary(string text, int at)
    {
        if (at == 0 || at == text.Length) return true;
        if (char.IsHighSurrogate(text[at - 1]) && char.IsLowSurrogate(text[at])) return false;
        if (char.GetUnicodeCategory(text[at]) is System.Globalization.UnicodeCategory.NonSpacingMark
            or System.Globalization.UnicodeCategory.SpacingCombiningMark) return false;
        return !(char.IsLetterOrDigit(text[at - 1]) && char.IsLetterOrDigit(text[at]));
    }
}
