using System.Net.Http.Json;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Shared;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private bool _targetedStrictRetry;
    private bool TargetedRetryBusy => _clientAiRequest is not null || StyleReviewBusy;
    private void TargetedRetryChanged(bool value) { if (!TargetedRetryBusy) _targetedStrictRetry = value; }
    private Task<PageQualityIssueDto> EnsureRepeatedWordRewriteFixAsync(PageQualityIssueDto issue, CancellationToken ct = default) => EnsureTargetedQualityFix(issue, ct);
    private Task<PageQualityIssueDto> EnsureSentenceLengthRewriteFixAsync(PageQualityIssueDto issue, CancellationToken ct = default) => EnsureTargetedQualityFix(issue, ct);
    private Task<PageQualityIssueDto> EnsurePassiveVoiceRewriteFixAsync(PageQualityIssueDto issue, CancellationToken ct = default) => EnsureTargetedQualityFix(issue, ct);

    private async Task<PageQualityIssueDto> EnsureTargetedQualityFix(PageQualityIssueDto issue, CancellationToken ct)
    {
        if (_pageEditor is null || _activeSection is null) return issue;
        if (issue.Fix is { Kind: "rewrite", ExpectedText: { } before } existing
            && TargetedQualityRetry.Validate(issue, before, existing.Text).Valid) return issue;
        var context = _qualityCheckedSource ?? throw new InvalidOperationException("Run checked quality again before reviewing.");
        if (issue.DocumentId != context.Lease.Source.DocumentId || issue.PageId != context.Page)
            throw new InvalidOperationException("The finding belongs to another checked page.");
        string plain = await _pageEditor.GetPlainTextAsync(ct) ?? "";
        if (issue.StartOffset < 0 || issue.EndOffset > plain.Length || issue.StartOffset >= issue.EndOffset
            || string.IsNullOrWhiteSpace(issue.AnchorText) || plain[issue.StartOffset..issue.EndOffset] != issue.AnchorText)
            throw new InvalidOperationException("The finding anchor no longer matches the exact checked passage. Run checks again.");
        var range = await BuildRepeatedWordApplyRangeAsync(issue, plain, ct);
        if (range is null) throw new InvalidOperationException("The exact finding passage is unavailable. Check again or revise it manually.");
        async Task Check(CancellationToken token)
        {
            await RequireCheckedProposal(context, token);
            if (_pageEditor is null || await _pageEditor.GetPlainTextAsync(token) != plain
                || !await _pageEditor.ValidateTargetedQualityRangeAsync(range.DocFrom, range.DocTo, range.Before, token))
                throw new InvalidOperationException("The targeted passage changed or contains unsupported embedded content. Check again or revise it manually.");
        }
        await Check(ct);
        string? deterministic = QualityIssueCapabilities.IsRepeatedWordIssue(issue) && !string.IsNullOrWhiteSpace(issue.AnchorText)
            ? TargetedQualityRetry.CollapseAdjacent(range.Before, issue.AnchorText) : null;
        string normalized;
        if (deterministic is not null && TargetedQualityRetry.Validate(issue, range.Before, deterministic).Valid) normalized = deterministic;
        else
        {
            var result = await TargetedQualityRetry.RunAsync(_targetedStrictRetry, issue, range.Before,
                (strict, token) => GenerateTargetedQualityFix(issue, plain, range, strict, token), r => r.ProposedText, Check, ct,
                async () => { _qualityStatus = "Trying one strict retry. This extra AI request may consume quota."; await InvokeAsync(StateHasChanged); });
            normalized = result.Text;
            _qualityHistoryProposals[issue.IssueKey] = result.Proposal.ProposalId;
        }
        await Check(ct);
        return issue with { Fix = new("rewrite", range.PlainFrom, range.PlainTo, normalized, issue.AnchorText, issue.IssueKey,
            range.DocFrom, range.DocTo, range.Before) };
    }
    private async Task<AiActionExecuteResponseDto> GenerateTargetedQualityFix(PageQualityIssueDto issue, string plain,
        RepeatedWordApplyRange range, bool strict, CancellationToken ct)
    {
        string task = QualityIssueCapabilities.IsRepeatedWordIssue(issue) ? "Reduce repetition of the word " + issue.AnchorText + "; use it fewer times while preserving necessary uses"
            : QualityIssueCapabilities.IsSentenceLengthIssue(issue) ? "Split or shorten the long sentence" : "Rewrite the passive sentence into active voice without inventing an actor";
        string instruction = task + ". " + StyleQualityReview.GoalInstruction(_qualityStyleGoal) + " " + StyleQualityReview.Criteria
            + " Preserve facts, names, tense, narrative voice, meaning and the input language. Return only the revised selected span, without explanations, labels or markdown."
            + (strict ? " " + TargetedQualityRetry.StrictInstruction : "");
        var request = new AiActionExecuteRequestDto(DocumentId, _activeSection!.Id, _activePage?.Id, range.PlainFrom, range.PlainTo,
            range.Before, plain, GetOutlineTextForAi(), new() { ["instruction"] = instruction, ["tone"] = "Neutral", ["length"] = "Same", ["preserve_terms"] = true });
        using var response = await PostAiActionAsync("rewrite.selection", request, commandLabel: "Rewrite selection", cancellationToken: ct);
        if (!response.IsSuccessStatusCode)
        {
            await TryHandleAiQuotaExceededAsync(response, ct);
            throw new HttpRequestException("The targeted AI request failed. Check sign-in, quota and connection before retrying manually.", null, response.StatusCode);
        }
        var result = await response.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>(ct)
            ?? throw new InvalidDataException("The AI service returned no checked response. Retry manually when available.");
        RequireClientAiRequest(ct);
        return result;
    }
    private void UpsertQualityIssue(PageQualityIssueDto updatedIssue)
    {
        int index = _qualityIssues.FindIndex(i => i.IssueKey == updatedIssue.IssueKey);
        if (index >= 0) _qualityIssues[index] = updatedIssue;
    }
}
