using Microsoft.AspNetCore.Components;
using WriterApp.Client.Services;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    [Inject] private WebBrainstormingContext BrainstormContext { get; set; } = default!;
    [Inject] private WebBrainstormingSessions BrainstormSessions { get; set; } = default!;
    [Inject] private IBrainstormingCapabilityService BrainstormCapabilities { get; set; } = default!;
    private bool _brainstormOpen;
    private bool _brainstormVisited;
    private bool _brainstormGenerating;
    private WriterApp.UI.Shared.BrainstormingProposalPreview? _brainstormProposal;
    private WriterApp.UI.Shared.EditorWorkspace? _brainstormEditorWorkspace;
    private Task ShowBrainstormDraft() => _brainstormEditorWorkspace?.ShowDraftAsync() ?? Task.CompletedTask;
    private Task PrepareBrainstormDraftSend() => _brainstormProposal?.FlushAndCheckpointAsync() ?? Task.CompletedTask;
    private BrainstormingCache? _brainstormCache;
    private Guid? SelectedBrainstormDraftId => _brainstormCache is null ? null : BrainstormingDrafts.Candidate(_brainstormCache)?.Id;
    private string? _brainstormNavigationError;
    private Guid? _brainstormPageToOpen;
    private async Task ToggleBrainstorm() {
        try {
            if(IsSceneRoute) {
                await FlushNotesSaveAsync();await FlushActiveEditorAsync("brainstorming-open-manuscript");
                Navigation.NavigateTo($"documents/{DocumentId}/sections/{SectionId}?brainstorm-manuscript=true",forceLoad:true);return;
            }
            if(_brainstormOpen)await PrepareBrainstormDraftSend();_brainstormVisited=true;_brainstormOpen=!_brainstormOpen;
        }
        catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException or Microsoft.JSInterop.JSException or BrainstormingConcurrencyException) {_brainstormNavigationError="Proposal review remains open; pending edits are retained. "+e.Message;}
    }
    private async Task<BrainstormingScope> CreateBrainstormScope() {
        var document=DocumentId;var identity=BrainstormSessions.Identity;var source=await BrainstormContext.ReadAsync(document,null);
        if(DocumentId!=document || identity!=BrainstormSessions.Identity)throw new OperationCanceledException("Manuscript or account changed while opening Brainstorm.");
        return await BrainstormSessions.ScopeAsync(document,source.Outline.ProjectId);
    }
    private void SelectBrainstormSession(BrainstormingScope? scope) { _brainstormGenerating=false;_brainstormContextScope=scope;_brainstormCache=null; }
    private string BrainstormTargetLabel(BrainstormingTarget target) => _sections.SingleOrDefault(s=>s.Id==(target.SectionId??target.BeforeSectionId)) is { } section ? $"{section.Title} · {(target.Kind==BrainstormingTargetKind.CreateSection?"new first section":GetPages(section.Id).SingleOrDefault(p=>p.Id==target.PageId)?.Title??"Manuscript page")}" : "Original destination unavailable; refresh the outline.";
    private async Task NavigateBrainstormTarget(BrainstormingTarget target) {
        _brainstormNavigationError=null;
        try {
            if(target.DocumentId!=DocumentId || (target.SectionId??target.BeforeSectionId) is not { } section || !_sections.Any(s=>s.Id==section))throw new InvalidDataException("The original proposal destination is no longer available. Its prose is retained.");
            if(section==_activeSection?.Id && target.PageId is { } page)await SelectManuscriptPageAsync(new ChangeEventArgs{Value=page.ToString()});
            else {_brainstormPageToOpen=target.PageId;await OnSectionSelected(section);}
        }catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException or System.Net.Http.HttpRequestException or Microsoft.JSInterop.JSException) {_brainstormNavigationError="Navigation stopped; pending writing is retained. "+e.Message;}
    }
    private async Task OpenBrainstormPageAfterNavigation() {
        if(_brainstormPageToOpen is not { } page || _activeSection is null || !GetPages(_activeSection.Id).Any(p=>p.Id==page))return;
        _brainstormPageToOpen=null;await SelectManuscriptPageAsync(new ChangeEventArgs{Value=page.ToString()});StateHasChanged();
    }
    private BrainstormingScope? _brainstormContextScope;
    private async Task BeforeBrainstormingSend() {
        var scope=_brainstormContextScope??throw new InvalidOperationException("Prepare story context first.");var document=DocumentId;var editor=_pageEditor;var page=_activePage?.Id;
        if(document!=scope.DocumentId || editor is null || page is null || IsSceneRoute)throw new OperationCanceledException("The active manuscript changed.");
        var source=await BrainstormContext.ReadAsync(document);var html=await editor.GetContentAsync();
        if(DocumentId!=document || _pageEditor!=editor || _activePage?.Id!=page || source.Pages.Single(p=>p.Id==page).Content!=html)throw new InvalidDataException("Save pending writing and refresh context before sending. No older text was substituted.");
        await BrainstormSessions.MapAsync(scope);
    }
    private async Task<BrainstormingSupportingPage> ListBrainstormingSupportingAsync(Guid? after) {
        var document = DocumentId; var page = await BrainstormContext.ListSupportingAsync(document, after);
        if (DocumentId != document) throw new OperationCanceledException("The active manuscript changed.");
        return page with { Items = page.Items.Where(d => d.Id != document).ToArray() };
    }
    private async Task<BrainstormingPreparation> PrepareBrainstormingContext(BrainstormingTargetQuery query, IReadOnlyList<BrainstormingContextChoice> choices, IReadOnlyList<Guid> supporting, int budget)
    {
        var document = DocumentId; var editor = _pageEditor; var page = _activePage?.Id;
        if (editor is null || page is null || IsSceneRoute) {
            if (IsSceneRoute) return new(1, new(1, null, "Open the linked manuscript section to prepare chapter prose. Separate scene content is not a manuscript insertion destination.", [], [], []), null, []);
            throw new InvalidOperationException("Open a manuscript page before preparing context.");
        }
        BrainstormingCache? cache = null;
        if (_brainstormContextScope?.AccountKey != BrainstormSessions.ActiveAccountKey) _brainstormContextScope = null;
        if (_brainstormContextScope?.DocumentId == document) cache = await BrainstormSessions.Sessions.ReadAsync(_brainstormContextScope);
        async Task<(Guid PageId, string Html)?> Pending(CancellationToken ct) {
            if (DocumentId != document || _pageEditor != editor || _activePage?.Id != page) throw new OperationCanceledException("The editor destination changed. Refresh context.");
            return (page.Value, await editor.GetContentAsync(ct));
        }
        var result = await BrainstormContext.PrepareAsync(document, query, choices, supporting, budget, Pending, cache?.Session.Scope, cache?.Session);
        if (result.Context is not null) {
            cache ??= await BrainstormSessions.Sessions.CreateAsync(result.Context.Scope);
            await BrainstormSessions.Sessions.RememberPreparationAsync(cache.Session.Scope, result);
            _brainstormContextScope = cache.Session.Scope;
        }
        await Pending(default); return result;
    }
    private async Task<BrainstormingPreparation> PrepareSavedBrainstormingContext(BrainstormingTargetQuery query, IReadOnlyList<BrainstormingContextChoice> choices, IReadOnlyList<Guid> supporting, int budget)
    {
        var document=DocumentId; var editor=_pageEditor; var page=_activePage?.Id;
        if (editor is null || page is null || IsSceneRoute) throw new InvalidOperationException("Open a manuscript page before preparing context.");
        await FlushActiveEditorAsync("brainstorming-context");
        editor.RequireSavedForAi();
        await FlushNotesSaveAsync();
        if (DocumentId!=document || _pageEditor!=editor || _activePage?.Id!=page)
            throw new OperationCanceledException("The editor destination changed while saving story context.");
        return await PrepareBrainstormingContext(query,choices,supporting,budget);
    }
}
