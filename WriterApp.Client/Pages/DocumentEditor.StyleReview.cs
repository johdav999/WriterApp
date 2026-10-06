using System.Net.Http.Json;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Client.Components.Editor;
using WriterApp.Shared;
using WriterApp.Application.Subscriptions;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private sealed record ClientStyleReview(Guid Proposal, WebProposal Checked, PageEditor.StyleQualitySource Source, StyleQualityReport Report, string Goal);
    private ClientStyleReview? _styleReview;
    private IReadOnlyCollection<int> _approvedStyleEdits = Array.Empty<int>();
    private string _styleReviewScope = "page";
    private string? _styleReviewError;
    private bool StyleReviewBusy => _clientAiRequest is not null || _qualityLoading || _isProposalApplying || _isQualityProposalOpen;
    private bool CanRunStyleReview => CanShowQualityChecks && HasAction("custom_transform") && CanUseFeature(FeatureKey.PromptLibrary)
        && IsAiUiEnabled && IsAiEntitled && !IsAiQuotaExceeded && _aiUsageStatus?.SupportsStyleQualityReview == true;
    private string? StyleReviewUnavailable => CanRunStyleReview ? null : _aiUsageStatus?.SupportsStyleQualityReview != true
        ? "Explained style review requires a backend that supports style review. Refresh or update the backend."
        : "Explained style review requires access to AI and custom writing tools, with available quota.";
    private Task StyleReviewScopeChanged(string scope) {
        if (!StyleReviewBusy && scope is "page" or "selection") { _styleReviewScope = scope; DismissStyleReview(); }
        return Task.CompletedTask;
    }
    private Task StyleEditsChanged(IReadOnlyCollection<int> approved) {
        if (!StyleReviewBusy && _styleReview is { } review) {
            _ = StyleQualityReview.Compose(review.Report, review.Source.Text, approved);
            _approvedStyleEdits = approved.Distinct().ToArray();
        }
        return Task.CompletedTask;
    }
    private void DismissStyleReview() { _styleReview = null; _approvedStyleEdits = Array.Empty<int>(); _styleReviewError = null; }
    private Task RunStyleReviewAsync() => RunClientAiRequest(ClientAiRequestKind.StyleReview, "Explained style review", async ct => {
        DismissStyleReview(); CloseQualityProposal();
        if (!CanRunStyleReview || _activeSection is null || _activePage is null || _pageEditor is null)
            throw new InvalidOperationException(StyleReviewUnavailable ?? "Open a saved page before checking style.");
        await FlushActiveEditorAsync("style-review"); RequireClientAiRequest(ct); _pageEditor.RequireSavedForAi();
        var source = await _pageEditor.CaptureStyleQualityAsync(_styleReviewScope == "page", ct);
        if (source.From < 0 || source.Text.Length == 0 || source.Plain.Length > 100_000 || source.Html.Length > 750_000
            || source.From > source.Plain.Length - source.Text.Length || source.Plain.Substring(source.From, source.Text.Length) != source.Text)
            throw new InvalidDataException("The style target is not a bounded, current page or selection.");
        string goal = StyleQualityReview.Goal(_qualityStyleGoal).Key;
        var request = new AiActionExecuteRequestDto(DocumentId, _activeSection.Id, _activePage.Id, source.From, source.From + source.Text.Length,
            source.Text, source.Plain, null, new() { ["scope"] = "selection", ["template"] = StyleQualityReview.GoalInstruction(goal), [StyleQualityReview.Parameter] = goal });
        using var response = await PostAiActionAsync("custom_transform", request, cancellationToken: ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadApiErrorMessageAsync(response, "Style review failed. Your writing is unchanged."));
        var result = await WebAiSources.Read<AiActionExecuteResponseDto>(response.Content, ct);
        var context = _checkedProposals[result.ProposalId];
        if (context.Html != source.Html) throw new InvalidOperationException("The saved writing changed during preparation. Review it again.");
        var report = StyleQualityReview.Parse(result.ProposedText ?? "", source.Text);
        await _pageEditor.PreviewStyleQualityAsync(source, report.Edits, ct);
        await RequireCheckedProposal(context, ct); RequireClientAiRequest(ct);
        _styleReview = new(result.ProposalId, context, source, report, goal);
        _approvedStyleEdits = Enumerable.Range(0, report.Edits.Count).ToArray();
    });
    private async Task ApplyStyleReviewAsync() {
        if (StyleReviewBusy || _styleReview is not { } review || _approvedStyleEdits.Count == 0 || _pageEditor is null) return;
        _isProposalApplying = true; _styleReviewError = null;
        try {
            await FlushActiveEditorAsync("style-review-apply"); _pageEditor.RequireSavedForAi();
            await RequireCheckedProposal(review.Checked);
            if (!ReferenceEquals(review, _styleReview) || !_writingContexts.TryGetValue(review.Proposal, out var writing) || !WritingContextCurrent(writing))
                throw new InvalidOperationException("The reviewed writing context changed. Generate another review.");
            var current = await ReadSavedWritingOutline(DocumentId, default);
            if (current.Fingerprint != writing.Source.Fingerprint) throw new InvalidOperationException("The saved writing outline changed. Review again.");
            _ = StyleQualityReview.Compose(review.Report, review.Source.Text, _approvedStyleEdits);
            await RequireCheckedProposal(review.Checked);
            await _pageEditor.ApplyStyleQualityAsync(review.Source, review.Report.Edits.Where((e, i) => _approvedStyleEdits.Contains(i)).ToArray(), review.Proposal, review.Checked.Lease);
            DismissStyleReview(); _qualityStatus = "Selected style changes applied. Use AI history Undo to restore the original page.";
            await LoadPageVersionsAsync(); await LoadAiHistoryAsync();
        } catch (Exception e) when (e is InvalidOperationException or InvalidDataException or HttpRequestException or JSException) {
            _styleReviewError = e.Message;
        } finally { _isProposalApplying = false; await InvokeAsync(StateHasChanged); }
    }
}
