using System.Globalization;
using System.Text;

namespace WriterApp.UI.Shared;

public sealed record TextSearchMatch(int Start, int Length);

/// <summary>NFC equivalence and ordinal case-insensitive matching; offsets refer to the untouched source.</summary>
public static class TextSearch
{
    public static IReadOnlyList<TextSearchMatch> Find(string text, string query, int limit = 50, CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(limit));
        query = query.Trim();
        if (query.Length is 0 or > 200 || text.Length == 0) return [];
        var normalized = new StringBuilder();
        var starts = new List<int>(); var ends = new List<int>();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();
            string element = elements.GetTextElement();
            string value = element.Normalize(NormalizationForm.FormC);
            normalized.Append(value);
            for (int i = 0; i < value.Length; i++) { starts.Add(elements.ElementIndex); ends.Add(elements.ElementIndex + element.Length); }
        }
        string source = normalized.ToString(), needle = query.Normalize(NormalizationForm.FormC);
        var result = new List<TextSearchMatch>();
        for (int offset = 0; result.Count < limit;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = source.IndexOf(needle, offset, StringComparison.OrdinalIgnoreCase);
            if (index < 0) break;
            int end = ends[index + needle.Length - 1];
            result.Add(new(starts[index], end - starts[index]));
            offset = index + needle.Length;
        }
        return result;
    }
}
