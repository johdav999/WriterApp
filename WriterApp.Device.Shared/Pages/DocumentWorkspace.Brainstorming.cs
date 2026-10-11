using Microsoft.AspNetCore.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Device.Shared.Pages;

public partial class DocumentWorkspace
{
    [Inject] private DeviceBrainstormingContext BrainstormContext { get; set; } = default!;
    [Inject] private DeviceBrainstormingSessions BrainstormSessions { get; set; } = default!;
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
    private async Task ToggleBrainstormAsync() {
        try {if(_brainstormOpen)await PrepareBrainstormDraftSend();if(_editor is not null)await _editor.FlushAsync();_brainstormVisited=true;_brainstormOpen=!_brainstormOpen;}
        catch(Exception e) when(e is IOException or InvalidDataException or BrainstormingConcurrencyException or InvalidOperationException or Microsoft.JSInterop.JSException) {_brainstormNavigationError="Pending writing is retained. "+e.Message;}
    }
    private Task<BrainstormingScope> CreateBrainstormScopeAsync() => BrainstormSessions.ScopeAsync(DocumentId);
    private void SelectBrainstormSession(BrainstormingScope? scope) {_brainstormGenerating=false;_brainstormContextScope=scope;_brainstormServerWriting=null;_brainstormCache=null;}
    private string BrainstormTargetLabel(BrainstormingTarget target) => _session?.Document.Sections.SingleOrDefault(s=>s.SectionId==(target.SectionId??target.BeforeSectionId)) is { } section ? $"{section.Title} · {(target.Kind==BrainstormingTargetKind.CreateSection?"new first section":section.Pages.SingleOrDefault(p=>p.PageId==target.PageId)?.Title??"Manuscript page")}" : "Original destination unavailable; refresh the outline.";
    private async Task NavigateBrainstormTargetAsync(BrainstormingTarget target) {
        _brainstormNavigationError=null;
        try {
            if(target.DocumentId!=DocumentId || _session is null)throw new InvalidDataException("This proposal belongs to another manuscript.");
            var page=target.PageId??_session.Document.Sections.SingleOrDefault(s=>s.SectionId==(target.SectionId??target.BeforeSectionId))?.Pages.FirstOrDefault()?.PageId;
            if(page is null || !_session.Document.Sections.SelectMany(s=>s.Pages).Any(p=>p.PageId==page))throw new InvalidDataException("The original proposal destination is unavailable. Its prose is retained.");
            await SwitchPageAsync(page);
        }catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException or Microsoft.JSInterop.JSException) {_brainstormNavigationError="Navigation stopped; pending writing is retained. "+e.Message;}
    }
    private BrainstormingScope? _brainstormContextScope;
    private string? _brainstormServerWriting;
    private string? CurrentBrainstormWritingVersion() => _session is null ? null : DeviceSyncMapping.Fingerprint(_session.Snapshot());
    private async Task BeforeBrainstormingSendAsync() {
        var editor=_editor;var session=_session;
        if(editor is null||session is null||PanelOperationsDisabled)throw new InvalidOperationException("Open the manuscript and resolve recovery first.");
        await editor.FlushAsync();
        if(_session!=session||_editor!=editor||session.IsDirty||_brainstormServerWriting!=DeviceSyncMapping.Fingerprint(session.Snapshot()))throw new InvalidDataException("Writing changed. Save, synchronize and review server context explicitly before sending.");
    }
    private async Task<BrainstormingPreparation> PrepareServerBrainstormingContextAsync(BrainstormingTargetQuery query,IReadOnlyList<BrainstormingContextChoice> choices,IReadOnlyList<Guid> supporting,int budget) {
        var session=_session;var editor=_editor;var page=_selectedPage;
        if(session is null||editor is null||PanelOperationsDisabled)throw new InvalidOperationException("Open the manuscript before reviewing server context.");await editor.FlushAsync();
        _brainstormServerWriting=null;
        if(!await FlushPlanningAsync())throw new InvalidDataException("Save the pending planning changes before preparing context. Your message is retained.");
        await BrainstormContext.SynchronizeForReviewAsync(session,()=>SaveAllAsync(),Sync);
        await editor.FlushAsync();
        if(_session!=session||_editor!=editor||_selectedPage!=page||session.IsDirty)throw new InvalidDataException("Writing changed during preparation. Your edits and message are retained; prepare again.");
        if(_brainstormContextScope?.AccountKey!=BrainstormSessions.ActiveAccountKey || _brainstormContextScope?.DocumentId!=DocumentId)_brainstormContextScope=await BrainstormSessions.ScopeAsync(DocumentId);
        var fingerprint=DeviceSyncMapping.Fingerprint(session.Snapshot());
        var result=await BrainstormContext.PrepareServerAsync(session,query,choices,supporting,budget,_brainstormContextScope);
        await editor.FlushAsync();if(_session!=session||_editor!=editor||_selectedPage!=page||DeviceSyncMapping.Fingerprint(session.Snapshot())!=fingerprint)throw new InvalidDataException("Writing changed during context review.");
        if(result.Context is not null){await BrainstormSessions.Sessions.RememberPreparationAsync(_brainstormContextScope,result);_brainstormServerWriting=fingerprint;}
        return result;
    }
    private async Task<BrainstormingSupportingPage> ListBrainstormingSupportingAsync(Guid? after) {
        var document = DocumentId; var local = await Documents.ListAsync();
        if (DocumentId != document) throw new OperationCanceledException("The active manuscript changed.");
        var ordered = local.Documents.Where(d => d.DocumentId != document && d.DeletedAtUtc is null && (after is null || d.DocumentId.CompareTo(after.Value) > 0)).OrderBy(d => d.DocumentId).Take(26).ToArray();
        return new(1, ordered.Take(25).Select(d => new BrainstormingSupportingDocument(d.DocumentId, d.Title, d.Kind)).ToArray(), ordered.Length > 25 ? ordered[24].DocumentId : null);
    }
    private async Task<BrainstormingPreparation> PrepareBrainstormingContextAsync(BrainstormingTargetQuery query, IReadOnlyList<BrainstormingContextChoice> choices, IReadOnlyList<Guid> supporting, int budget)
    {
        var session = _session; var editor = _editor; var page = _selectedPage;
        if (session is null || editor is null || PanelOperationsDisabled) throw new InvalidOperationException("Return to the manuscript editor and resolve recovery before reviewing story context.");
        await editor.FlushAsync(); // Capture DOM into the pending local session; never force a manuscript save here.
        if (_session != session || _editor != editor || _selectedPage != page) throw new OperationCanceledException("The active editor changed.");
        var capturedWriting = DeviceSyncMapping.Fingerprint(session.Snapshot());
        BrainstormingCache? cache = null;
        if (_brainstormContextScope?.AccountKey != BrainstormSessions.ActiveAccountKey) _brainstormContextScope = null;
        if (_brainstormContextScope?.DocumentId == session.Document.DocumentId) cache = await BrainstormSessions.Sessions.ReadAsync(_brainstormContextScope);
        var result = await BrainstormContext.PrepareAsync(session, query, choices, supporting, budget, cache?.Session.Scope, cache?.Session);
        await editor.FlushAsync(); // Recheck DOM events that arrived while the context pages were projected.
        if (_session != session || _editor != editor || _selectedPage != page) throw new OperationCanceledException("The active editor changed; writing is retained.");
        if (DeviceSyncMapping.Fingerprint(session.Snapshot()) != capturedWriting) throw new InvalidDataException("Pending editor writing changed during capture. Refresh context; no writing was discarded.");
        if (result.Context is not null) {
            cache ??= await BrainstormSessions.Sessions.CreateAsync(result.Context.Scope);
            await BrainstormSessions.Sessions.RememberPreparationAsync(cache.Session.Scope, result);
            _brainstormContextScope = cache.Session.Scope;
        }
        return result;
    }
}
