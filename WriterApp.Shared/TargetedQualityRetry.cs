using System.Text.RegularExpressions;
using WriterApp.Application.Documents;

namespace WriterApp.Shared;

public enum TargetedQualityFailure { None, InvalidTarget, InvalidProse, Unchanged, GoalNotImproved }
public sealed record TargetedQualityValidation(string Text, TargetedQualityFailure Failure, string Message)
{
    public bool Valid => Failure == TargetedQualityFailure.None;
    public bool RetryEligible => Failure is TargetedQualityFailure.Unchanged or TargetedQualityFailure.GoalNotImproved;
}
public sealed record TargetedQualityResult<T>(T Proposal, string Text, bool Retried);

/// <summary>Pure supported-rule validation and a single opt-in semantic retry. Transport/source failures propagate.</summary>
public static class TargetedQualityRetry
{
    public const string Label = "Allow one automatic strict retry for targeted fixes";
    public const string Explanation = "Off by default. For repeated words, long sentences and passive-voice hints, an invalid semantic result may trigger one extra AI request. The extra request may consume quota. Both results are checked; applying a change still requires your approval.";
    public const string StrictInstruction = "The previous candidate did not improve the targeted issue. Return only final revised prose in the input language. Preserve facts, names, meaning, tense and voice. Reduce the targeted repetition, shorten or split the long sentence, or remove the supported passive-voice pattern as requested. No labels, instructions, JSON, HTML or explanations.";
    public static string CollapseAdjacent(string before, string anchor) => Regex.Replace(before,
        @"(?<!\w)(" + Regex.Escape(anchor) + @")(?:[\p{Zs}\t]+\1)+(?!\w)", m => m.Groups[1].Value, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static int Longest(string text) => QualityTextAnalyzer.GetSentences(text).Select(s => s.WordCount).DefaultIfEmpty(0).Max();
    private static int Passive(string text) => new PassiveVoiceRule().Evaluate(new(text, QualityTextAnalyzer.GetTokens(text),
        QualityTextAnalyzer.GetSentences(text), QualityTextAnalyzer.GetParagraphs(text), [])).Count();
    public static void RequireTarget(PageQualityIssueDto issue, string before)
    {
        bool supported = QualityIssueCapabilities.IsRepeatedWordIssue(issue) ? !string.IsNullOrWhiteSpace(issue.AnchorText)
            && QualityRewriteOutputValidator.CountOccurrences(before, issue.AnchorText) >= 2
            : QualityIssueCapabilities.IsSentenceLengthIssue(issue) ? Longest(before) > 30
            : QualityIssueCapabilities.IsPassiveVoiceIssue(issue) && Passive(before) > 0;
        if (!supported || string.IsNullOrWhiteSpace(before) || before.Length > 100_000)
            throw new InvalidOperationException("This finding has no supported exact rewrite target. Check the writing again or revise it manually.");
    }
    public static TargetedQualityValidation Validate(PageQualityIssueDto issue, string before, string? raw)
    {
        TargetedQualityValidation Refuse(TargetedQualityFailure failure, string message) => new("", failure, message);
        string input = raw ?? "";
        if (input.Length > 100_000 || input.TrimStart().StartsWith('{') || input.TrimStart().StartsWith('[') || input.Contains("```", StringComparison.Ordinal)
            || Regex.IsMatch(input, @"<\s*/?\s*[a-zA-Z][^>]*>", RegexOptions.CultureInvariant))
            return Refuse(TargetedQualityFailure.InvalidProse, "The suggestion contained structured metadata or markup instead of usable prose.");
        string text = QualityRewriteOutputValidator.SanitizeCandidateOutput(input);
        if (string.IsNullOrWhiteSpace(text) || QualityFixClientHelpers.LooksLikeProposalMetaLeak(text)
            || Regex.IsMatch(text, @"^\s*(?:rewrite|replace|change|remove|suggestion|analysis|explanation|instruction|revised text|here is|here's)\b", RegexOptions.IgnoreCase | RegexOptions.Multiline))
            return Refuse(TargetedQualityFailure.InvalidProse, "The suggestion did not contain a usable prose revision.");
        if (text.Trim() == before.Trim()) return Refuse(TargetedQualityFailure.Unchanged, "The suggestion left the targeted writing unchanged.");
        bool adjacent = QualityIssueCapabilities.IsRepeatedWordIssue(issue) && !string.IsNullOrWhiteSpace(issue.AnchorText)
            && text == CollapseAdjacent(before, issue.AnchorText);
        if (!adjacent && text.Length < before.Trim().Length / 2)
            return Refuse(TargetedQualityFailure.InvalidProse, "The suggestion removed too much of the checked passage to review safely.");
        if (QualityIssueCapabilities.IsRepeatedWordIssue(issue))
        {
            if (string.IsNullOrWhiteSpace(issue.AnchorText) || QualityRewriteOutputValidator.CountOccurrences(before, issue.AnchorText) < 2)
                return Refuse(TargetedQualityFailure.InvalidTarget, "The checked passage no longer contains the supported repetition.");
            if (!QualityRewriteOutputValidator.TryValidateRepeatedWordReduction(before, text, issue.AnchorText, out _, out _, out _))
                return Refuse(TargetedQualityFailure.GoalNotImproved, "The suggestion did not safely reduce the repeated word.");
        }
        else if (QualityIssueCapabilities.IsSentenceLengthIssue(issue))
        {
            if (Longest(before) <= 30) return Refuse(TargetedQualityFailure.InvalidTarget, "The checked passage no longer contains a supported long sentence.");
            if (Longest(text) >= Longest(before)) return Refuse(TargetedQualityFailure.GoalNotImproved, "The suggestion did not shorten or split the long sentence.");
        }
        else if (QualityIssueCapabilities.IsPassiveVoiceIssue(issue))
        {
            if (Passive(before) == 0) return Refuse(TargetedQualityFailure.InvalidTarget, "The checked passage no longer contains a supported passive-voice pattern.");
            if (Passive(text) >= Passive(before)) return Refuse(TargetedQualityFailure.GoalNotImproved, "The suggestion did not reduce the supported passive-voice pattern.");
        }
        return new(text, TargetedQualityFailure.None, "");
    }
    public static async Task<TargetedQualityResult<T>> RunAsync<T>(bool optIn, PageQualityIssueDto issue, string before,
        Func<bool, CancellationToken, Task<T>> generate, Func<T, string?> candidate, Func<CancellationToken, Task> requireCurrent,
        CancellationToken ct, Func<Task>? retrying = null)
    {
        RequireTarget(issue, before);
        await requireCurrent(ct); ct.ThrowIfCancellationRequested();
        var proposal = await generate(false, ct);
        await requireCurrent(ct); ct.ThrowIfCancellationRequested();
        var result = Validate(issue, before, candidate(proposal));
        bool retried = optIn && result.RetryEligible;
        if (retried)
        {
            await requireCurrent(ct); ct.ThrowIfCancellationRequested();
            if (retrying is not null) await retrying();
            await requireCurrent(ct);
            ct.ThrowIfCancellationRequested();
            proposal = await generate(true, ct);
            await requireCurrent(ct); ct.ThrowIfCancellationRequested();
            result = Validate(issue, before, candidate(proposal));
        }
        if (!result.Valid) throw new InvalidDataException(result.Message + (retried ? " The one strict retry was also invalid." : "")
            + " Writing is unchanged. Review this finding again to retry manually, or revise it yourself.");
        return new(proposal, result.Text, retried);
    }
}
