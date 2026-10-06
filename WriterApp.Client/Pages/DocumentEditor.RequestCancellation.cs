using WriterApp.Client.Services;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private enum ClientAiRequestKind { Writing, QualityCheck, QualityRevision, ConsistencyCheck, ConsistencyRevision, ConsistencyNavigation, StyleReview }
    private sealed class ClientAiRequest(ClientAiRequestKind kind, string label, Guid document, Guid? section, Guid? page, string backend) : IDisposable
    {
        public ClientAiRequestKind Kind { get; } = kind;
        public string Label { get; } = label;
        public Guid Document { get; } = document;
        public Guid? Section { get; } = section;
        public Guid? Page { get; set; } = page;
        public string Backend { get; } = backend;
        public CancellationTokenSource Cancellation { get; } = new();
        public CancellationToken Token => Cancellation.Token;
        public HashSet<Guid> Proposals { get; } = [];
        public List<WebCheckedAi.Lease> Leases { get; } = [];
        public void Dispose() => Cancellation.Dispose();
    }
    private ClientAiRequest? _clientAiRequest;
    private bool _clientAiDisposed;
    private string? _clientAiRequestMessage;
    private ClientAiRequest? _clientAiUsageRefresh;
    private string? _clientAiUsageAccess;
    private string ClientAiAccess() => AuthMeStateService is null ? "unavailable" : string.Join("|",
        AuthMeStateService.IsAuthenticated, AuthMeStateService.IsDeletedAccount, AuthMeStateService.IsDuplicateAccount,
        AuthMeStateService.StripeCustomerId, AuthMeStateService.EffectivePlanKey,
        AuthMeStateService.IsPaidAccessActive, AuthMeStateService.IsAdminAccess);

    private bool ClientAiCurrent(ClientAiRequest request) => !_clientAiDisposed && ReferenceEquals(_clientAiRequest, request)
        && !request.Token.IsCancellationRequested && DocumentId == request.Document && _activeSection?.Id == request.Section
        && _activePage?.Id == request.Page && (Http.BaseAddress?.AbsoluteUri ?? "") == request.Backend;

    private void RequireClientAiRequest(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (ct.CanBeCanceled && _clientAiRequest is { } request && request.Token == ct && !ClientAiCurrent(request))
        {
            CancelClientAiRequestCore(false);
            throw new OperationCanceledException(ct);
        }
    }
    private async Task RunClientAiRequest(ClientAiRequestKind kind, string label, Func<CancellationToken, Task> action)
    {
        if (_clientAiDisposed || _clientAiRequest is not null || _webTranslationBusy || _isProposalApplying || _isApplyingContinuityProposal) return;
        using var request = new ClientAiRequest(kind, label, DocumentId, _activeSection?.Id, _activePage?.Id, Http.BaseAddress?.AbsoluteUri ?? "");
        _clientAiRequest = request;
        _clientAiRequestMessage = null;
        DismissStyleReview();
        DismissRecommendedCopy();
        if (kind == ClientAiRequestKind.Writing) _pendingAiProposal = null;
        if (kind is ClientAiRequestKind.QualityCheck or ClientAiRequestKind.QualityRevision) { _qualityLoading = true; CloseQualityProposal(); }
        if (kind is ClientAiRequestKind.ConsistencyCheck or ClientAiRequestKind.ConsistencyRevision) { _continuityBusy = true; CloseContinuityProposal(); }
        if (kind == ClientAiRequestKind.ConsistencyNavigation) _continuityBusy = true;
        AiCommandStatusService.Start(label);
        try
        {
            await InvokeAsync(StateHasChanged);
            RequireClientAiRequest(request.Token);
            await action(request.Token);
            RequireClientAiRequest(request.Token);
        }
        catch (OperationCanceledException) when (request.Token.IsCancellationRequested || !ClientAiCurrent(request)) { }
        catch (Exception error)
        {
            if (ClientAiCurrent(request))
            {
                _clientAiRequestMessage = error.Message;
                if (kind == ClientAiRequestKind.StyleReview) _styleReviewError = error.Message;
                if (kind is ClientAiRequestKind.QualityCheck or ClientAiRequestKind.QualityRevision) _qualityError = error.Message;
                if (kind is ClientAiRequestKind.ConsistencyCheck or ClientAiRequestKind.ConsistencyRevision or ClientAiRequestKind.ConsistencyNavigation) _continuityStatus = error.Message;
            }
        }
        finally
        {
            // Cancel detaches the owner immediately. A late operation cannot end
            // a newer request, clear its busy state, or refresh its account state.
            if (ReferenceEquals(_clientAiRequest, request))
            {
                _clientAiRequest = null;
                if (kind is ClientAiRequestKind.QualityCheck or ClientAiRequestKind.QualityRevision) _qualityLoading = false;
                if (kind is ClientAiRequestKind.ConsistencyCheck or ClientAiRequestKind.ConsistencyRevision or ClientAiRequestKind.ConsistencyNavigation) _continuityBusy = false;
                AiCommandStatusService.Clear();
                if (!_clientAiDisposed)
                {
                    // Usage refresh is advisory. Its own quota-count notification
                    // must not invalidate a completed, still-owned checked result.
                    _clientAiUsageRefresh = request;
                    _clientAiUsageAccess = ClientAiAccess();
                    try { await RefreshPlanUsageAsync(); }
                    finally { if (ReferenceEquals(_clientAiUsageRefresh, request)) _clientAiUsageRefresh = null; }
                    if (!_clientAiDisposed) await InvokeAsync(StateHasChanged);
                }
            }
        }
    }
    private void CancelClientAiRequest() => CancelClientAiRequestCore(true);
    private void CancelClientAiRequestCore(bool showMessage)
    {
        if (!showMessage) _clientAiRequestMessage = null;
        if (_clientAiRequest is not { } request) return;
        _clientAiRequest = null;
        request.Cancellation.Cancel();
        if (request.Kind == ClientAiRequestKind.StyleReview) {
            DismissStyleReview();
            _styleReviewError = showMessage ? "Style review cancelled. Your writing is unchanged." : null;
        }
        if (request.Kind == ClientAiRequestKind.Writing) _pendingAiProposal = null;
        foreach (var proposal in request.Proposals)
        {
            _checkedProposals.Remove(proposal);
            _checkedWritingPreviews.Remove(proposal);
            _writingContexts.Remove(proposal);
            foreach (var key in _qualityHistoryProposals.Where(p => p.Value == proposal).Select(p => p.Key).ToArray()) _qualityHistoryProposals.Remove(key);
            foreach (var key in _continuityHistoryProposals.Where(p => p.Value == proposal).Select(p => p.Key).ToArray()) _continuityHistoryProposals.Remove(key);
            if (_pendingAiProposal?.ProposalId == proposal) _pendingAiProposal = null;
        }
        if (request.Kind is ClientAiRequestKind.QualityCheck or ClientAiRequestKind.QualityRevision)
        {
            _qualityLoading = false;
            CloseQualityProposal();
            _qualityStatus = showMessage ? "Quality request cancelled. Your writing is unchanged." : null;
            if (request.Kind == ClientAiRequestKind.QualityCheck)
            {
                _qualityCheckedSource = null;
                _qualityIssues.Clear();
            }
        }
        if (request.Kind is ClientAiRequestKind.ConsistencyCheck or ClientAiRequestKind.ConsistencyRevision or ClientAiRequestKind.ConsistencyNavigation)
        {
            _continuityBusy = false;
            CloseContinuityProposal();
            _continuityStatus = showMessage ? "Consistency request cancelled. Your writing is unchanged." : null;
            if (request.Kind == ClientAiRequestKind.ConsistencyCheck)
            {
                _continuityReport = null;
                _continuityCheckedSource = null;
                _continuitySectionSource = null; _continuityPages = []; _continuityPassages.Clear();
                _selectedContinuityIssueKey = null;
                _pendingContinuityHighlights = false;
            }
        }
        AiCommandStatusService.Clear();
        _clientAiRequestMessage = showMessage ? "AI request cancelled. Your writing is unchanged. A request already sent may still use your quota." : null;
    }
    private void TrackClientAiLease(CancellationToken ct, WebCheckedAi.Lease lease)
    {
        RequireClientAiRequest(ct);
        if (_clientAiRequest is { } request && request.Token == ct) request.Leases.Add(lease);
    }
    private void TrackClientAiProposal(CancellationToken ct, Guid proposal)
    {
        RequireClientAiRequest(ct);
        if (_clientAiRequest is { } request && request.Token == ct) request.Proposals.Add(proposal);
    }
    private bool IsOwnClientAiUsageNotification()
    {
        if (_clientAiUsageRefresh is not { } request || _clientAiUsageAccess != ClientAiAccess()
            || request.Leases.Count == 0 || request.Leases.Any(l => l.Account is null)) return false;
        try { foreach (var lease in request.Leases) Checked.RequireLease(lease); return true; }
        catch (InvalidOperationException) { return false; }
    }
}
