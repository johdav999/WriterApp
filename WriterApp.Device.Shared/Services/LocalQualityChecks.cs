using System.Text.RegularExpressions;
using WriterApp.Application.AI;
using WriterApp.Application.Continuity;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public sealed record LocalQualityAnalysis(LocalDocument Source, Guid PageId, string Html, string PlainText,
    int From, int To, IReadOnlyList<PageQualityIssueDto> Issues, LocalGlossaryContext? Glossary = null);
public sealed record QualityEditorSource(LocalDocument Document, Guid PageId, AiEditorSnapshot Editor);
public sealed record LocalQualityFix(LocalQualityAnalysis Analysis, PageQualityIssueDto Issue,
    int From, int To, string Before, string? LocalReplacement)
{
    public QualityHighlight Highlight => new(Issue.IssueKey, From, To, Before, Issue.Severity);
}
public sealed record LocalQualityPreview(LocalQualityFix Fix, string Proposed, string AfterHtml,
    DeviceAiProposal? Provider, DateTimeOffset CreatedAt);

public static class LocalQualityChecks
{
    public static LocalQualityAnalysis Analyze(LocalDocument source, Guid pageId, AiEditorSnapshot editor, bool selection,
        LocalGlossaryContext? glossary = null)
    {
        if (glossary is not null && (glossary.LocalDocumentId != source.DocumentId || glossary.ServerDocumentId != source.ServerDocumentId))
            throw new InvalidDataException("Glossary context belongs to a different document mapping.");
        var page = source.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == pageId);
        if (source.DeletedAtUtc is not null) throw new InvalidOperationException("Restore this manuscript before checking quality.");
        DeviceContentCompatibility.RequireEditable(page.Content, page.ContentFormat);
        if (editor.PlainText.Length > 500_000 || editor.Html.Length > 500_000)
            throw new InvalidDataException("This page exceeds the supported quality check size.");
        int from = selection ? editor.SelectionStart : 0, to = selection ? editor.SelectionEnd : editor.PlainText.Length;
        if (from < 0 || to < from || selection && to == from || to > editor.PlainText.Length
            || selection && editor.SelectedText != editor.PlainText[from..to])
            throw new InvalidOperationException("Select writing for a selection check, or check the current page.");
        var text = editor.PlainText[from..to];
        var context = new QualityCheckContext(text, QualityTextAnalyzer.GetTokens(text),
            QualityTextAnalyzer.GetSentences(text), QualityTextAnalyzer.GetParagraphs(text), glossary?.Terms ?? []);
        var issues = new QualityCheckEngine(QualityRuleCatalog.Create()).Evaluate(context, 200);
        var mapped = issues.Select(i => new PageQualityIssueDto(i.IssueKey, source.DocumentId, pageId, i.RuleId, i.Kind,
            i.Severity, i.Message, i.Suggestion, i.AnchorText, i.StartOffset + from, i.EndOffset + from,
            i.Fix is null ? null : new QualityIssueFixDto(i.Fix.Kind, i.Fix.From + from, i.Fix.To + from, i.Fix.Text,
                i.AnchorText, i.IssueKey), DateTimeOffset.UtcNow)).ToArray();
        return new(source, pageId, editor.Html, editor.PlainText, from, to, mapped, glossary);
    }
    public static void RequireFresh(LocalDocument current, LocalQualityAnalysis analysis)
    {
        var before = analysis.Source.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == analysis.PageId);
        var now = current.Sections.SelectMany(s => s.Pages).SingleOrDefault(p => p.PageId == analysis.PageId);
        if (current.DocumentId != analysis.Source.DocumentId || current.DeletedAtUtc is not null || current.SyncState == LocalSyncState.Conflict || now is null
            || before.Content != now.Content || before.ContentFormat != now.ContentFormat)
            throw new InvalidOperationException("The checked page changed. Run quality checks again; writing is unchanged.");
    }
    public static QualityHighlight Passage(LocalQualityAnalysis analysis, string key)
    {
        var issue = analysis.Issues.Single(i => i.IssueKey == key);
        if (issue.StartOffset < analysis.From || issue.EndOffset > analysis.To || issue.StartOffset >= issue.EndOffset
            || string.IsNullOrWhiteSpace(issue.AnchorText)
            || analysis.PlainText[issue.StartOffset..issue.EndOffset] != issue.AnchorText)
            throw new InvalidOperationException("This finding has no exact passage. Review it as information.");
        return new(key, issue.StartOffset, issue.EndOffset, issue.AnchorText, issue.Severity);
    }
    public static LocalQualityFix Target(LocalQualityAnalysis analysis, string key)
    {
        var issue = analysis.Issues.Single(i => i.IssueKey == key);
        if (!QualityIssueCapabilities.CanReview(issue))
            throw new InvalidOperationException("This finding is informational. Edit the writing yourself and check again.");
        var anchor = Passage(analysis, key);
        if (!QualityIssueCapabilities.IsAutoProposable(issue) && issue.Fix is { } direct)
        {
            if (direct.From < analysis.From || direct.To > analysis.To || direct.From != anchor.From || direct.To != anchor.To)
                throw new InvalidDataException("The local fix does not match its checked passage.");
            var fix = new LocalQualityFix(analysis, issue, direct.From, direct.To, anchor.ExpectedText, null);
            return fix with { LocalReplacement = Validate(fix, direct.Text ?? "") };
        }
        var span = ContinuityRewriteSpanResolver.ExpandToSentenceSpan(analysis.PlainText, anchor.From, anchor.To - anchor.From);
        if (QualityIssueCapabilities.IsRepeatedWordIssue(issue))
        {
            // The local rule's five-word window can cross a sentence boundary. Include both
            // occurrences so generation and reduction validation use the same evidence.
            var previous = QualityTextAnalyzer.GetTokens(analysis.PlainText[analysis.From..anchor.From])
                .TakeLast(5).LastOrDefault(t => string.Equals(t.Text, issue.AnchorText, StringComparison.OrdinalIgnoreCase));
            if (previous is not null && analysis.From + previous.Start < span.Start)
            {
                var first = ContinuityRewriteSpanResolver.ExpandToSentenceSpan(analysis.PlainText,
                    analysis.From + previous.Start, previous.End - previous.Start);
                span = span with { Start = first.Start, Length = span.Start + span.Length - first.Start };
            }
        }
        // A selection check cannot authorize a rewrite beyond the selected source.
        int from = Math.Max(analysis.From, span.Start), to = Math.Min(analysis.To, span.Start + span.Length);
        if (from > anchor.From || to < anchor.To || from >= to)
            throw new InvalidOperationException("This finding cannot be resolved safely in the checked range.");
        string before = analysis.PlainText[from..to];
        string? local = null;
        if (QualityIssueCapabilities.IsRepeatedWordIssue(issue))
        {
            var match = Regex.Matches(before, RepetitionPattern(issue.AnchorText!), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                .Cast<Match>().FirstOrDefault(m => from + m.Index <= anchor.From && from + m.Index + m.Length >= anchor.To);
            if (match is not null)
            {
                // Collapse only the matching duplicate pair, preserving all surrounding marks and writing.
                from += match.Index; to = from + match.Length; before = match.Value;
                local = Validate(new(analysis, issue, from, to, before, null), match.Groups[1].Value);
            }
        }
        return new(analysis, issue, from, to, before, local);
    }
    public static string Validate(LocalQualityFix fix, string candidate)
    {
        if (QualityIssueCapabilities.IsAutoProposable(fix.Issue))
        {
            var validation = WriterApp.Shared.TargetedQualityRetry.Validate(fix.Issue, fix.Before, candidate);
            if (!validation.Valid) throw new InvalidDataException(validation.Message + " Regenerate it or revise this passage manually.");
            return validation.Text;
        }
        if (candidate.Length > 100_000 || candidate.TrimStart().StartsWith('{') || candidate.TrimStart().StartsWith('[') || candidate.Contains("```", StringComparison.Ordinal))
            throw new InvalidDataException("The suggestion contained structured metadata instead of prose. Regenerate it.");
        var result = QualityRewriteOutputValidator.SanitizeCandidateOutput(candidate);
        if (string.IsNullOrWhiteSpace(result) || result.Length > 100_000 || result == fix.Before
            || QualityFixClientHelpers.LooksLikeProposalMetaLeak(result)
            || Regex.IsMatch(result, @"<\s*/?\s*[a-zA-Z][^>]*>|^\s*(?:rewrite|replace|change|remove|suggestion|analysis|explanation|instruction|revised text|here is|here's)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            throw new InvalidDataException("The suggestion did not contain a usable prose revision. Regenerate it.");
        return result;
    }
    private static string RepetitionPattern(string anchor) => @"(?<!\w)(" + Regex.Escape(anchor) + @")(?:[\p{Zs}\t]+\1)+(?!\w)";
    public static DeviceAiPrepared Rewrite(LocalDocument synchronized, LocalQualityFix fix, string styleGoal = WriterApp.Shared.StyleQualityReview.DefaultGoal)
    {
        RequireFresh(synchronized, fix.Analysis);
        if (synchronized.ServerDocumentId is null || synchronized.ServerVersion is null || synchronized.SyncState != LocalSyncState.Synced)
            throw new InvalidOperationException("Save and synchronize before requesting an AI quality fix.");
        var section = synchronized.Sections.Single(s => s.Pages.Any(p => p.PageId == fix.Analysis.PageId));
        var page = section.Pages.Single(p => p.PageId == fix.Analysis.PageId);
        string task = QualityIssueCapabilities.IsRepeatedWordIssue(fix.Issue) ? "Reduce repetition of the word " + fix.Issue.AnchorText + "; use it fewer times while preserving necessary uses"
            : QualityIssueCapabilities.IsSentenceLengthIssue(fix.Issue) ? "Split or shorten the long sentence"
            : "Rewrite the passive sentence into active voice without inventing an actor";
        var request = new AiActionExecuteRequestDto(synchronized.ServerDocumentId, section.ServerSectionId ?? section.SectionId,
            page.ServerPageId ?? page.PageId, fix.From, fix.To, fix.Before, fix.Analysis.PlainText, null,
            new() { ["instruction"] = task + ". " + WriterApp.Shared.StyleQualityReview.GoalInstruction(styleGoal) + " " + WriterApp.Shared.StyleQualityReview.Criteria + " Preserve facts, names, tense, narrative voice, meaning and the input language. Return only the revised selected span, without explanations, labels or markdown.",
                ["tone"] = "Neutral", ["length"] = "Same", ["preserve_terms"] = true }, synchronized.ServerVersion);
        return new("rewrite.selection", request, DeviceAiAction.Rewrite, page.PageId, fix.Analysis.Html,
            fix.From, fix.To, fix.Before, "replace");
    }
    public static LocalDocument Apply(LocalDocument current, LocalQualityPreview preview)
    {
        RequireFresh(current, preview.Fix.Analysis);
        if (DateTimeOffset.UtcNow - preview.CreatedAt > TimeSpan.FromMinutes(30))
            throw new InvalidOperationException("This quality preview expired. Check and review again.");
        if (Validate(preview.Fix, preview.Proposed) != preview.Proposed)
            throw new InvalidDataException("The reviewed replacement changed.");
        DeviceContentCompatibility.RequireEditable(preview.AfterHtml, LocalContentFormat.Html);
        var page = current.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == preview.Fix.Analysis.PageId);
        if (preview.AfterHtml.Length > 500_000) throw new InvalidDataException("The revised page exceeds the supported size.");
        if (page.Content == preview.AfterHtml) throw new InvalidOperationException("The quality preview made no change.");
        return current with { Sections = current.Sections.Select(s => s with { Pages = s.Pages.Select(p => p.PageId == page.PageId
            ? p with { Content = preview.AfterHtml, ContentFormat = LocalContentFormat.Html } : p).ToArray() }).ToArray() };
    }
}

public sealed class LocalQualityActions(LocalDocumentRepository documents, LocalAiStore history, DeviceAiService ai)
{
    public async Task<LocalDocument> ApplyAsync(Guid documentId, LocalQualityPreview preview, CancellationToken ct)
    {
        if (preview.Provider is { } provider) ai.RequireCurrentAccount(provider);
        var current = await documents.LoadAsync(documentId, ct) ?? throw new IOException("Manuscript unavailable.");
        if (preview.Provider is { } bound && (current.ServerDocumentId != bound.Prepared.Request.DocumentId
            || current.ServerVersion != bound.Prepared.Request.ExpectedDocumentVersion))
            throw new InvalidOperationException("The synchronized source changed. Check and review the quality fix again.");
        if (preview.Provider is { } generated)
        {
            var prepared = generated.Prepared; var fix = preview.Fix;
            prepared.RequireManuscriptTarget();
            var section = current.Sections.Single(s => s.Pages.Any(p => p.PageId == fix.Analysis.PageId));
            var page = section.Pages.Single(p => p.PageId == fix.Analysis.PageId);
            if (prepared.Key != "rewrite.selection" || prepared.LocalPageId != page.PageId || prepared.BaseHtml != fix.Analysis.Html
                || prepared.From != fix.From || prepared.To != fix.To || prepared.SelectedText != fix.Before
                || prepared.Request.SectionId != (section.ServerSectionId ?? section.SectionId)
                || prepared.Request.PageId != (page.ServerPageId ?? page.PageId)
                || prepared.Request.OriginalText != fix.Before || prepared.Request.SurroundingText != fix.Analysis.PlainText
                || LocalQualityChecks.Validate(fix, generated.ProposedText) != preview.Proposed)
                throw new InvalidDataException("The AI proposal does not match the reviewed quality passage. Check and review again.");
        }
        var next = LocalQualityChecks.Apply(current, preview);
        var record = new LocalAiHistory(1, Guid.NewGuid(), documentId, "quality.apply_issue", "Manuscript revision",
            current.LocalRevision, current.ServerVersion, DateTimeOffset.UtcNow, "Applying", current, preview.Proposed,
            PageId: preview.Fix.Analysis.PageId, AfterHtml: preview.AfterHtml, OriginalText: preview.Fix.Before, CloudOrigin:preview.Provider?.HistoryOrigin);
        await history.SaveHistoryAsync(record, ct);
        if (preview.Provider is { } reviewed) ai.RequireCurrentAccount(reviewed);
        ct.ThrowIfCancellationRequested();
        var saved = await documents.SaveAsync(next, ct);
        try { await history.SaveHistoryAsync(record with { Status = "Applied", After = saved }, CancellationToken.None); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { throw new IOException("The approved quality change was saved, but history completion failed. Reopen History; the original remains in its recovery record.", e); }
        return saved;
    }
}

