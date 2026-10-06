using System.Net.Http.Json;
using Microsoft.JSInterop;
using WriterApp.Application.Documents;
using WriterApp.Client.State;
using WriterApp.Client.Components.Editor;
using WriterApp.Shared;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private WebTranslationSource? _continuitySectionSource;
    private IReadOnlyList<ConsistencyPageSource> _continuityPages = [];
    private sealed record ContinuityPageBinding(ConsistencyPrimaryPassage? Passage, string? Error);
    private readonly Dictionary<string, ContinuityPageBinding> _continuityPassages = [];
    private Guid? _continuityReviewPage;
    private async Task<WebTranslationSource> ReadConsistencySectionAsync(Guid section, CancellationToken ct)
    {
        using var response = await Http.GetAsync($"{TranslationEndpoint(DocumentId)}/source?scope=section&sectionId={section}&purpose=consistency", ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Checked section pages are unavailable. Save and update the backend before checking consistency.");
        var source = await WebAiSources.Read<WebTranslationSource>(response.Content, ct);
        if (source.Version != 1 || source.DocumentId != DocumentId || source.Scope != "section"
            || source.Sections.Count != 1 || source.Sections[0].Id != section || source.Fingerprint?.Length != 64)
            throw new InvalidDataException("Consistency source has the wrong section or identity.");
        return source;
    }
    private async Task<string> CaptureConsistencyPagesAsync(CancellationToken ct)
    {
        var section = _activeSection?.Id ?? throw new InvalidOperationException("Choose a section.");
        var lease = await Checked.Capture(DocumentId, section, _activePage?.Id, IsSceneRoute ? SceneNodeId : null, ct);
        TrackClientAiLease(ct, lease);
        var source = await ReadConsistencySectionAsync(section, ct);
        if (source.AccountKey != lease.Source.AccountKey || source.DocumentVersion != lease.Source.DocumentVersion)
            throw new InvalidDataException("Consistency source revisions do not match. Save and check again.");
        var pages = source.Sections[0].Pages.OrderBy(p => p.OrderIndex).ThenBy(p => p.Id)
            .Select(p => new ConsistencyPageSource(p.Id, p.Content, PlainTextMapper.ToPlainText(p.Content))).ToArray();
        if (IsSceneRoute)
        {
            // Scene content is a separate persistence target; never save a different page to that scene node.
            string html = _pageEditor is null ? _activePage?.Content ?? "" : await _pageEditor.GetContentAsync(ct);
            pages = [new(_activePage?.Id ?? throw new InvalidOperationException("Choose a saved scene."), html, PlainTextMapper.ToPlainText(html))];
        }
        await Checked.Confirm(lease, ct); RequireClientAiRequest(ct);
        if (section != _activeSection?.Id) throw new InvalidOperationException("The section changed. Check again.");
        if (_pageEditor is not null && pages.SingleOrDefault(p => p.PageId == _activePage?.Id) is { } active
            && await _pageEditor.GetContentAsync(ct) != active.Html)
            throw new InvalidOperationException("The active page has unsaved writing. Save it before checking.");
        string text = ConsistencyPagePassages.Text(pages);
        _continuitySectionSource = source; _continuityPages = pages;
        return text;
    }
    private void BindContinuityPassages()
    {
        _continuityPassages.Clear();
        foreach (var issue in _continuityReport?.Issues ?? [])
        {
            try
            {
                if (issue.Evidence is null || issue.Anchor is null || !Guid.TryParse(issue.Evidence.SectionId, out var section)
                    || section != _activeSection?.Id) throw new InvalidDataException("The finding refers to a different section. Check again.");
                _continuityPassages[GetContinuityIssueKey(issue)] = new(ConsistencyPagePassages.Resolve(_continuityPages,
                    issue.Evidence.Quote, issue.Anchor.PlainTextStart, issue.Anchor.PlainTextLength), null);
            }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
            { _continuityPassages[GetContinuityIssueKey(issue)] = new(null, e.Message); }
        }
    }
    private ConsistencyPrimaryPassage ContinuityPassage(ContinuityIssue issue) =>
        _continuityPassages.TryGetValue(GetContinuityIssueKey(issue), out var binding) && binding.Passage is { } passage
            ? passage : throw new InvalidOperationException(binding?.Error ?? "Run a new checked consistency report before navigating this passage.");
    private string ContinuityPrimaryLocation(ContinuityIssue issue) =>
        _continuityPassages.TryGetValue(GetContinuityIssueKey(issue), out var binding) && binding.Passage is { } passage
            ? _continuitySectionSource?.Sections.SelectMany(s => s.Pages).SingleOrDefault(p => p.Id == passage.PageId)?.Title ?? "Checked scene writing"
            : "Primary passage unavailable";
    private string? ContinuityPrimaryError(ContinuityIssue issue) =>
        _continuityPassages.TryGetValue(GetContinuityIssueKey(issue), out var binding) ? binding.Error : "Run a checked report to locate this passage.";

    private async Task RequireContinuitySourceAsync(CancellationToken ct = default)
    {
        RequireClientAiRequest(ct);
        var context = _continuityCheckedSource ?? throw new InvalidOperationException("Run a new checked consistency report.");
        var source = _continuitySectionSource ?? throw new InvalidOperationException("This report has no checked section pages. Run it again.");
        void Check()
        {
            RequireClientAiRequest(ct); Checked.RequireLease(context.Lease); WebAiSources.RequireTime(context.Created);
            if (!ReferenceEquals(context, _continuityCheckedSource) || !TranslationContextCurrent(context.Generation, source.DocumentId, context.Lease.Backend)
                || _activeSection?.Id != source.Sections[0].Id || !_continuityPages.Any(p => p.PageId == _activePage?.Id))
                throw new InvalidOperationException("The account, section or page changed. Run the consistency check again.");
        }
        Check();
        _pageEditor?.RequireSavedForAi();
        var active = _continuityPages.Single(p => p.PageId == _activePage!.Id);
        if (_pageEditor is not null && (_pageEditor.Page?.Id != active.PageId || await _pageEditor.GetContentAsync(ct) != active.Html))
            throw new InvalidOperationException("The writing changed after analysis. Save and run the consistency check again.");
        await Checked.Confirm(context.Lease, ct);
        var current = await ReadConsistencySectionAsync(source.Sections[0].Id, ct);
        SameTranslationSource(current, source); Check();
        if (_pageEditor is not null && await _pageEditor.GetContentAsync(ct) != active.Html)
            throw new InvalidOperationException("Editor writing changed during the source check. Run consistency again.");
    }
    private async Task<ConsistencyPrimaryPassage> OpenContinuityPassageAsync(ContinuityIssue issue, CancellationToken ct)
    {
        var passage = ContinuityPassage(issue);
        await FlushActiveEditorAsync("consistency-navigation"); _pageEditor?.RequireSavedForAi();
        await RequireContinuitySourceAsync(ct);
        if (_activePage?.Id != passage.PageId)
        {
            if (IsSceneRoute) throw new InvalidOperationException("Open this section's manuscript page to review that passage.");
            var source = _continuityCheckedSource!;
            var pages = await Http.GetFromJsonAsync<PageDto[]>($"api/sections/{_activeSection!.Id}/pages", ct);
            await RequireContinuitySourceAsync(ct);
            var target = pages?.SingleOrDefault(p => p.Id == passage.PageId)
                ?? throw new InvalidOperationException("The checked page was moved or deleted. Run the check again.");
            _pagesBySection[_activeSection.Id] = pages!.OrderBy(p => p.OrderIndex).ThenBy(p => p.Id).ToList();
            _activePage = target;
            if (_clientAiRequest is { } request && request.Token == ct) request.Page = target.Id;
            _pendingAiProposal = null; InvalidateStructuredTranslation(preserveConsistency: true, keepClientAiRequest: true);
            // This controlled navigation changes the editor page, not the checked section source/account.
            _continuityCheckedSource = source with { Generation = _webTranslationGeneration };
            _qualityHasRunOnce = false; ResetVersionStatusTracking();
            await InvokeAsync(StateHasChanged);
            await WaitForConsistencyPageEditorAsync(target.Id, ct);
            await RequireContinuitySourceAsync(ct);
            await LoadPageVersionsAsync(); await LoadAnnotationsAsync(); await LoadQualityIssuesAsync();
        }
        await RequireContinuitySourceAsync(ct);
        return passage;
    }
    protected virtual async Task WaitForConsistencyPageEditorAsync(Guid pageId, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            while (_pageEditor?.Page?.Id != pageId || await _pageEditor.GetPlainTextAsync(timeout.Token) is null)
            { RequireClientAiRequest(ct); await Task.Delay(25, timeout.Token); }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { throw new InvalidOperationException("The checked page opened but its editor is not ready. Try Jump again; writing is unchanged."); }
    }
    private async Task<WebAiSource> ContinuitySaveSourceAsync()
    {
        await RequireContinuitySourceAsync();
        var source = _continuityCheckedSource!;
        var current = await Checked.Capture(DocumentId, _activeSection!.Id, _activePage!.Id, IsSceneRoute ? SceneNodeId : null);
        WebAiSources.RequireCurrent(current.Source with { PageId = source.Lease.Source.PageId }, source.Lease.Source);
        await RequireContinuitySourceAsync();
        return current.Source;
    }
}
