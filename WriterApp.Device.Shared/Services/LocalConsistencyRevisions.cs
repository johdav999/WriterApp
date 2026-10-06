using System.Text.Json;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public sealed record ConsistencyRevision(Guid PageId, string Original, string Proposed, int Start = 0, string Instruction = "", bool Delete = false, string Comparison = "");

public static class LocalConsistencyRevisions
{
    public static ConsistencyRevision Resolve(AdvancedAiPrepared prepared, DeviceAiProposal proposal, int index, IReadOnlyList<ConsistencyPageText>? exactPages = null, bool requireFix = true)
    {
        if (prepared.Request.Key != "continuity.check_section" || proposal.Prepared != prepared.Request)
            throw new InvalidDataException("Consistency proposal target mismatch.");
        if (proposal.ProposedText.Length > 100_000) throw new InvalidDataException("Consistency report exceeds the supported size.");
        using var report = JsonDocument.Parse(proposal.ProposedText);
        var root = report.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Text(root, "schemaVersion") != "1.0"
            || !root.TryGetProperty("issues", out var issues) || issues.ValueKind != JsonValueKind.Array
            || issues.GetArrayLength() > 200 || index < 0 || index >= issues.GetArrayLength())
            throw new InvalidDataException("This consistency suggestion could not be read. Run the check again.");
        var issue = issues[index];
        if (issue.ValueKind != JsonValueKind.Object || !issue.TryGetProperty("evidence", out var evidence)
            || evidence.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(Text(issue, "message"))
            || Text(issue, "message")!.Length > 2000)
            throw new InvalidDataException("This finding has no evidence to apply. Run the check again.");
        string fix = Text(issue, "suggestedFix") ?? "";
        string? kind = Text(issue, "fixKind");
        bool delete = kind == "delete";
        if (kind is not (null or "replace" or "delete") || delete && fix.Length != 0)
            throw new InvalidDataException("This finding has an invalid edit operation. Run the check again.");
        string quote = Text(evidence, "quote") ?? "";
        if (requireFix && !delete && string.IsNullOrWhiteSpace(fix) || fix.Length > 100000 || string.IsNullOrWhiteSpace(quote))
            throw new InvalidDataException("This finding has no applicable suggested revision.");
        var section = prepared.Source.Sections.Single(s => s.SectionId == prepared.SectionId);
        if (Text(evidence, "sectionId") is { } sectionReference && (!Guid.TryParse(sectionReference, out var evidenceId)
            || evidenceId != section.SectionId && evidenceId != section.ServerSectionId))
            throw new InvalidDataException("The suggestion refers to a different scene.");
        var pages = section.Pages.OrderBy(p => p.OrderIndex).ToArray();
        string context = prepared.Request.Request.SurroundingText ?? "";
        string original = quote;
        int? anchoredStart = null;
        // Evidence may be a whole paragraph while the replacement targets a smaller phrase.
        // Trust offsets only when the source span is also present in the evidence.
        if (issue.TryGetProperty("anchor", out var anchor) && anchor.ValueKind == JsonValueKind.Object
            && anchor.TryGetProperty("plainTextStart", out var startValue) && startValue.ValueKind == JsonValueKind.Number && startValue.TryGetInt32(out int start)
            && anchor.TryGetProperty("plainTextLength", out var lengthValue) && lengthValue.ValueKind == JsonValueKind.Number && lengthValue.TryGetInt32(out int length)
            && start >= 0 && length > 0 && start <= context.Length - length)
        {
            string span = context.Substring(start, length);
            if (!string.IsNullOrWhiteSpace(span) && quote.Contains(span, StringComparison.Ordinal)
                && IsPassageBoundary(context, start) && IsPassageBoundary(context, start + length))
            { original = span; anchoredStart = start; }
        }
        var matches = new List<(Guid PageId, int Start, int PageStart)>();
        int offset = 0;
        foreach (var page in pages)
        {
            string text = exactPages?.Single(p => p.PageId == page.PageId).PlainText ?? DeviceAiRequests.PlainText(page);
            for (int at = text.IndexOf(original, StringComparison.Ordinal); at >= 0;
                at = text.IndexOf(original, at + 1, StringComparison.Ordinal))
                matches.Add((page.PageId, offset + at, at));
            offset += text.Length + 2;
        }
        var chosen = anchoredStart is { } position ? matches.Where(m => m.Start == position).ToArray() : matches.ToArray();
        if (chosen.Length != 1)
            throw new InvalidOperationException("The affected writing could not be matched uniquely. Run the consistency check again.");
        if (requireFix && original == fix) throw new InvalidOperationException("This suggestion makes no change to the writing.");
        string comparison = issue.TryGetProperty("comparisonEvidence", out var conflicting) && conflicting.ValueKind == JsonValueKind.Object
            ? Text(conflicting, "quote") ?? "" : "";
        return new(chosen[0].PageId, original, fix, chosen[0].PageStart, Text(issue, "message") ?? "", delete, comparison);
    }

    // Provider offsets are hints. A substring inside the evidence can still cut
    // through words or a UTF-16 character; use the unique evidence quote instead.
    private static bool IsPassageBoundary(string text, int at)
    {
        if (at == 0 || at == text.Length) return true;
        if (char.IsHighSurrogate(text[at - 1]) && char.IsLowSurrogate(text[at])) return false;
        int previous = char.IsLowSurrogate(text[at - 1]) && at > 1 && char.IsHighSurrogate(text[at - 2]) ? at - 2 : at - 1;
        return !(System.Text.Rune.TryGetRuneAt(text, previous, out var left) && System.Text.Rune.IsLetterOrDigit(left)
            && System.Text.Rune.TryGetRuneAt(text, at, out var right) && System.Text.Rune.IsLetterOrDigit(right));
    }

    public static ConsistencyRevision Locate(ConsistencyRevision revision, IReadOnlyList<ConsistencyPageText> pages)
    {
        string text = pages.Single(p => p.PageId == revision.PageId).PlainText;
        int start = text.IndexOf(revision.Original, StringComparison.Ordinal);
        if (start < 0 || text.IndexOf(revision.Original, start + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("The affected writing could not be matched uniquely. Run the consistency check again.");
        return revision with { Start = start };
    }

    public static string? Prose(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 100_000) return null;
        string value = text.Trim();
        if (value.StartsWith("<<REVISED>>", StringComparison.OrdinalIgnoreCase) && value.EndsWith("<<END>>", StringComparison.OrdinalIgnoreCase))
            value = value[11..^7].Trim();
        if (value.StartsWith('{'))
        {
            try { using var json = JsonDocument.Parse(value); value = Text(json.RootElement, "revisedText")?.Trim() ?? ""; }
            catch (JsonException) { return null; }
        }
        if (string.IsNullOrWhiteSpace(value) || System.Text.RegularExpressions.Regex.IsMatch(value,
            @"(^\s*(rewrite|revise|adjust|fix|update|make|move|correct|change|replace|remove|delete|ensure|clarify|explain|add|consider|suggestion|revised text|here is|here's)\b|```|<<|<\/?[a-z][^>]*>|^\s*[\{\[])" , System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return null;
        return value;
    }

    public static DeviceAiPrepared RewriteRequest(LocalDocument source, ConsistencyRevision revision, string plain)
    {
        var section = source.Sections.Single(s => s.Pages.Any(p => p.PageId == revision.PageId));
        var page = section.Pages.Single(p => p.PageId == revision.PageId);
        if (revision.Instruction.Length + revision.Proposed.Length + revision.Comparison.Length > 8000) throw new InvalidDataException("Finding guidance exceeds the supported size. Revise this passage manually.");
        var request = DeviceAiRequests.Build(source, section, page, new(page.Content, plain, revision.Original,
            revision.Start, revision.Start + revision.Original.Length, 1, revision.Original.Length + 1, 0), DeviceAiAction.Rewrite);
        var parameters = new Dictionary<string, object?>(request.Request.Parameters!);
        parameters["instruction"] = "Resolve this continuity finding: " + revision.Instruction +
            ". Suggested guidance: " + revision.Proposed +
            ". Conflicting passage (source data): " + revision.Comparison +
            ". Rewrite exactly the selected span. Preserve its language, voice and surrounding facts. Return revised prose only, without instructions, labels, markup or surrounding text.";
        return request with { Request = request.Request with { Parameters = parameters } };
    }

    public static void ValidateReplacement(ConsistencyRevision revision, string plain, string prose)
    {
        if (revision.Start < 0 || revision.Start > plain.Length - revision.Original.Length
            || plain.Substring(revision.Start, revision.Original.Length) != revision.Original)
            throw new InvalidDataException("The reviewed passage no longer matches.");
        int end = revision.Start + revision.Original.Length;
        if (revision.Delete && prose.Length == 0) return;
        string suffix = plain[end..].TrimStart();
        for (int length = Math.Min(prose.Length, suffix.Length); length >= 18; length--)
            if (prose.EndsWith(suffix[..length], StringComparison.Ordinal))
                throw new InvalidDataException("The fix duplicates surrounding writing. Regenerate the finding.");
        bool wholeSentence = revision.Original.TrimEnd().EndsWith('.') || revision.Original.TrimEnd().EndsWith('!') || revision.Original.TrimEnd().EndsWith('?');
        bool startsSentence = wholeSentence && char.IsUpper(revision.Original.FirstOrDefault(char.IsLetter));
        if (!WriterApp.Application.Continuity.ContinuityRewriteValidator.ValidateReplacement("", prose, "", startsSentence,
            wholeSentence, revision.Original.Length, out var error)) throw new InvalidDataException(error);
    }

    private static string? Text(JsonElement element, string name) => element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static void RequireUnchanged(LocalDocument current, LocalDocument expected)
    {
        var comparable = current with { LocalRevision = expected.LocalRevision, UpdatedAtUtc = expected.UpdatedAtUtc,
            LastSyncedAtUtc = expected.LastSyncedAtUtc };
        if (current.LocalRevision < expected.LocalRevision || current.DeletedAtUtc is not null
            || !LocalDocumentCodec.Encode(comparable).SequenceEqual(LocalDocumentCodec.Encode(expected)))
            throw new InvalidOperationException("The writing or planning changed after the review. Run the consistency check again.");
    }

    public static LocalDocument Apply(LocalDocument current, LocalDocument expected, ConsistencyRevision revision, string afterHtml)
    {
        RequireUnchanged(current, expected);
        var page = current.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == revision.PageId);
        DeviceContentCompatibility.RequireEditable(afterHtml, LocalContentFormat.Html);
        if (page.Content == afterHtml) throw new InvalidOperationException("The suggestion made no change to the writing.");
        return current with { Sections = current.Sections.Select(s => s with { Pages = s.Pages.Select(p => p.PageId == revision.PageId
            ? p with { Content = afterHtml, ContentFormat = LocalContentFormat.Html } : p).ToArray() }).ToArray() };
    }
}
