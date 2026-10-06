using WriterApp.Application.AI;
using System.Net.Http.Json;
using WriterApp.Shared;
using WriterApp.Shared.Canon;
using Microsoft.JSInterop;
using WriterApp.Application.Documents;
using WriterApp.Client.State;

namespace WriterApp.Client.Pages;
public partial class DocumentEditor
{
    private string? _continuityCoverage;
    private sealed record ContinuitySourcePreview(string Title, string Text, string Quote, string Url);
    private ContinuitySourcePreview? _continuitySourcePreview;
    private string ContinuityDecisionScope => WriterApp.Shared.DeviceAiHistoryContracts.Hash(new {
        _continuityCheckedSource!.Lease.Backend, _continuityCheckedSource.Lease.Source.AccountKey });
    private readonly HashSet<string> _intentionalContinuity = [];
    private string ContinuityDecisionKey(ContinuityIssue issue) => ConsistencyChecks.DecisionKey(
        Guid.Parse(issue.Evidence.SectionId!), issue.Evidence.Quote, issue.ComparisonEvidence, issue.Type);
    private bool IsContinuityIntentional(ContinuityIssue issue)
    {
        if (!Guid.TryParse(issue.Evidence.SectionId, out var section) || section == Guid.Empty || string.IsNullOrWhiteSpace(issue.Evidence.Quote)) return false;
        try { return _intentionalContinuity.Contains(ContinuityDecisionKey(issue)); }
        catch (InvalidDataException) { return false; }
    }
    private async Task LoadContinuityDecisionsAsync(CancellationToken ct = default)
    {
        RequireClientAiRequest(ct);
        _intentionalContinuity.Clear();
        if (_continuityReport?.Issues.Count is not > 0 || _continuityCheckedSource is null) return;
        var source = _continuityCheckedSource;
        string scope = ContinuityDecisionScope, document = DocumentId.ToString();
        try {
            await using var module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", ct, ["./_content/WriterApp.UI.Shared/consistency-decisions.js"]);
            foreach (var issue in _continuityReport.Issues) {
                if (!Guid.TryParse(issue.Evidence.SectionId, out var section) || section == Guid.Empty || string.IsNullOrWhiteSpace(issue.Evidence.Quote)) continue;
                string key = ContinuityDecisionKey(issue);
                bool intentional = await module.InvokeAsync<bool>("isIntentional", ct, [scope, document, key]);
                RequireClientAiRequest(ct);
                if (!ReferenceEquals(source, _continuityCheckedSource)) return;
                if (intentional) _intentionalContinuity.Add(key);
            }
        } catch (JSException) { RequireClientAiRequest(ct); _continuityCoverage = string.Join(" ", new[] { _continuityCoverage, "Findings are available. Saved intentional decisions could not be loaded in this browser." }.Where(s => !string.IsNullOrWhiteSpace(s))); }
    }
    private async Task MarkContinuityIntentionalAsync(ContinuityIssue issue)
    {
        if (_continuityBusy || _isApplyingContinuityProposal || _continuityCheckedSource is null) return;
        var source = _continuityCheckedSource;
        string scope = ContinuityDecisionScope, document = DocumentId.ToString();
        try {
            await RequireContinuitySourceAsync();
            string key = ContinuityDecisionKey(issue); bool intentional = !_intentionalContinuity.Contains(key);
            await using var module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/WriterApp.UI.Shared/consistency-decisions.js");
            await module.InvokeVoidAsync("setIntentional", scope, document, key, intentional);
            await RequireContinuitySourceAsync();
            if (!ReferenceEquals(source, _continuityCheckedSource)) throw new InvalidOperationException("The check changed. Run it again.");
            if (intentional) _intentionalContinuity.Add(key); else _intentionalContinuity.Remove(key);
            CloseContinuityProposal();
            _continuityStatus = intentional ? "Marked intentional in this browser. Your writing is unchanged." : "This finding will be checked again.";
            if (intentional) await OnClearContinuityHighlightsAsync();
            else await ApplyContinuityHighlightsAsync();
        } catch (Exception e) when (e is InvalidOperationException or InvalidDataException or JSException) { _continuityStatus = e.Message; }
    }
    private string ContinuityComparisonLocation(ConsistencyComparison comparison) =>
        _sections.FirstOrDefault(s => s.Id == comparison.SectionId)?.Title ?? "Source scene";
    private async Task ViewContinuityComparisonAsync(ConsistencyComparison comparison)
    {
        if (_continuityBusy || _continuityCheckedSource is null) return;
        var source = _continuityCheckedSource;
        try {
            await RequireContinuitySourceAsync();
            // Verify ownership and the exact quote before opening its scene.
            var sections = await Http.GetFromJsonAsync<SectionDto[]>($"api/documents/{DocumentId}/sections");
            if (sections?.Any(s => s.Id == comparison.SectionId) != true) throw new InvalidDataException("The source scene is unavailable.");
            var pages = await Http.GetFromJsonAsync<PageDto[]>($"api/sections/{comparison.SectionId}/pages");
            var matches = pages?.Where(p => PlainTextMapper.ToPlainText(p.Content).Contains(comparison.Quote, StringComparison.Ordinal)).ToArray();
            if (matches?.Length != 1)
                throw new InvalidDataException("The conflicting passage changed. Run the check again.");
            await RequireContinuitySourceAsync();
            if (!ReferenceEquals(source, _continuityCheckedSource)) throw new InvalidOperationException("The check changed. Run it again.");
            if (comparison.SectionId == _activeSection?.Id && _pageEditor is not null) {
                string plain = await _pageEditor.GetPlainTextAsync() ?? "";
                int start = plain.IndexOf(comparison.Quote, StringComparison.Ordinal);
                if (start >= 0) { await InvokePageCommandAsync("scrollToPosition", start); return; }
            }
            _continuitySourcePreview = new(sections!.Single(s => s.Id == comparison.SectionId).Title,
                PlainTextMapper.ToPlainText(matches[0].Content), comparison.Quote, $"/documents/{DocumentId}/sections/{comparison.SectionId}");
            await InvokeAsync(StateHasChanged);
        } catch (Exception e) when (e is InvalidOperationException or InvalidDataException or HttpRequestException) { _continuityStatus = e.Message; }
    }
    private async Task PrepareConsistencyReferencesAsync(CancellationToken ct = default)
    {
        RequireClientAiRequest(ct);
        // Save once before preparation; each refresh retains its existing checked commit boundary.
        await FlushActiveEditorAsync("consistency-check");
        _pageEditor?.RequireSavedForAi();
        await FlushNotesSaveAsync();
        RequireClientAiRequest(ct);
        var section = _activeSection?.Id ?? throw new InvalidOperationException("Choose a scene to check.");
        var document = DocumentId; int generation = _webTranslationGeneration;
        var missing = new List<string>();
        foreach (var kind in Enum.GetValues<CanonKind>())
        {
            string type = kind.ToString().ToLowerInvariant();
            _continuityStatus = $"Preparing {type} references…"; await InvokeAsync(StateHasChanged);
            var lease = await Checked.Capture(document, section, ct:ct);
            TrackClientAiLease(ct, lease);
            using var response = await Http.GetAsync($"api/documents/{document}/bibles/{type}/device?expectedDocumentVersion={Uri.EscapeDataString(lease.Source.DocumentVersion)}",ct);
            RequireClientAiRequest(ct);
            if (!response.IsSuccessStatusCode) {
                if ((int)response.StatusCode is 402 or 403 or 422 or >= 500) { missing.Add(type); continue; }
                throw new InvalidOperationException("Story references could not be loaded. Try checking again.");
            }
            var snapshot = await WebAiSources.Read<DeviceBibleSnapshot>(response.Content,ct);
            if (snapshot.ContractVersion != 1 || snapshot.DocumentId != document || snapshot.Kind != kind || snapshot.CheckedDocumentVersion != lease.Source.DocumentVersion)
                throw new InvalidDataException("Story reference response belongs to another source.");
            await Checked.Confirm(lease,ct);
            if (!snapshot.Exists || snapshot.SourceDocumentVersion != lease.Source.DocumentVersion)
            {
                using var updated = await PostCheckedCanonRefresh(type, false,ct);
                RequireClientAiRequest(ct);
                if (!updated.IsSuccessStatusCode)
                {
                    if ((int)updated.StatusCode is 402 or 403 or 422 or >= 500) missing.Add(type);
                    else throw new InvalidOperationException("Your draft or references changed while preparing the check. Run the check again.");
                }
                else if (await updated.Content.ReadFromJsonAsync<BibleSnapshotDto>(ct) is { } result) { RequireClientAiRequest(ct); SetBibleSnapshot(result); }
            }
            if (!TranslationContextCurrent(generation, document, lease.Backend) || _activeSection?.Id != section)
                throw new InvalidOperationException("The account or scene changed. Run the check again.");
        }
        RequireClientAiRequest(ct);
        _continuityCoverage = missing.Count == 0 ? null : $"Some references could not be prepared ({string.Join(", ", missing)}). This check uses your writing and the available references.";
        _continuityStatus = "Checking for contradictions…"; await InvokeAsync(StateHasChanged);
    }

    private async Task DiscardContinuitySuggestionAsync(ContinuityIssue issue)
    {
        if (_continuityBusy || _isApplyingContinuityProposal) return;
        RemoveContinuityIssueFromCurrentReport(issue);
        if (_pendingContinuityIssue is { } pending && GetContinuityIssueKey(pending) == GetContinuityIssueKey(issue))
            CloseContinuityProposal();
        _continuityStatus = "Suggestion discarded. Your writing is unchanged.";
        await OnClearContinuityHighlightsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private Task DiscardPendingContinuitySuggestionAsync() => _pendingContinuityIssue is { } issue
        ? DiscardContinuitySuggestionAsync(issue) : Task.CompletedTask;

    private Task<AiActionExecuteRequestDto> BindCheckedCanon(string key, AiActionExecuteRequestDto request, WebProposal context, CancellationToken ct) =>
        Checked.BindCanon(key, request, context.Lease, ct);
}
