using Microsoft.JSInterop;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private async Task ReloadBrainstormManuscriptAsync() {
        if(!_collaborative){await ReloadTranslatedDocumentAsync();return;}
        // Keep the conversation, planning review and proposal controls mounted.
        // A full document loader replaces the workspace and loses their view state.
        await RefreshSharedStructureAsync();
        if(_activeSection is null || _activePage is null)return;
        _activePage=GetPages(_activeSection.Id).SingleOrDefault(p=>p.Id==_activePage.Id)??GetPages(_activeSection.Id).FirstOrDefault();
        _synopsisPreviewCacheDocumentId=null;_synopsisPreviewCacheHtml=null;
        if(_pageEditor is not null && _activePage is not null)await _pageEditor.ReloadSharedCommittedAsync();
    }
    private bool _brainstormKeepUnknown;
    private async Task<BrainstormingKeepReview> PrepareBrainstormKeep(Guid candidateId,bool replace,string title)
    {
        var local=_brainstormContextScope??throw new InvalidOperationException("Open the original conversation first.");var document=DocumentId;
        if(local.DocumentId!=document || IsSceneRoute)throw new InvalidOperationException("Open the original manuscript page; scene content is a separate target.");
        await FlushActiveEditorAsync("brainstorming-keep-review");_pageEditor?.RequireSavedForAi();await FlushNotesSaveAsync();
        var cache=await BrainstormSessions.SyncAsync(local);var candidate=cache.Session.Drafts.Single(d=>d.Id==candidateId);var target=cache.Session.Targets.Single(t=>t.Id==candidate.TargetId);
        if(cache.PendingGeneration is not null || cache.DraftEdit is not null || cache.Conflicts.Count!=0 || cache.Pending is not null)throw new InvalidOperationException("Settle the retained proposal or conversation operation before Keep.");
        var source=await BrainstormContext.ReadAsync(document,local.ProjectId);
        if(DocumentId!=document || _brainstormContextScope!=local || source.Stamp!=target.Source)throw new InvalidOperationException("The original source or destination changed. Refresh and explicitly review a new proposal.");
        int? from=null,to=null;
        if(replace) {
            if(_activePage?.Id!=target.PageId || _pageEditor is null)throw new InvalidOperationException("Select the passage in the proposal's original manuscript page.");
            var capture=await _pageEditor.CaptureStyleQualityAsync(false,default);if(string.IsNullOrEmpty(capture.Text))throw new InvalidOperationException("Select a manuscript passage to replace.");
            if(capture.Html!=source.Pages.Single(p=>p.Id==target.PageId).Content)throw new InvalidOperationException("Save the selected manuscript page first.");
            from=capture.From;to=from+capture.Text.Length;
        }
        var server=cache.Mapping?.Server??throw new InvalidOperationException("Map this conversation first.");
        var review=BrainstormingKeepContract.Review(server,candidate with {Scope=server},target,BrainstormingKeepContract.Writing(source),from,to,title);
        if(!replace && target.Kind!=BrainstormingTargetKind.CreateSection)review=BrainstormingPlacements.ReviewAt(review,BrainstormingPlacements.Default(review),title);
        await ValidateBrainstormKeepEditor(review);return review;
    }
    private async Task ValidateBrainstormKeepEditor(BrainstormingKeepReview r) {
        var before=r.Kind==BrainstormingTargetKind.CreateSection?"":r.Before.Pages.Single(p=>p.Id==BrainstormingPlacements.Destination(r).PageId).Content;
        var result=await JSRuntime.InvokeAsync<string>("tiptapEditor.previewBrainstormingKeep",before,r.Candidate.EditorHtml??BrainstormingDraftContent.Html(r.Candidate.PlainText),r.AfterHtml,(int)r.Kind,r.From,r.To,r.ReplacedText,r.Kind==BrainstormingTargetKind.InsertInPage?BrainstormingPlacements.Boundary(r):0);
        if(result!=r.AfterHtml)throw new InvalidDataException("The actual editor did not confirm the reviewed rich mapping.");
    }
    private async Task<BrainstormingCache> ApplyBrainstormKeep(BrainstormingKeepReview r) {
        var local=_brainstormContextScope??throw new InvalidOperationException("Open the original conversation.");var document=DocumentId;var identity=BrainstormSessions.Identity;
        if(r.Scope!= (await BrainstormSessions.Sessions.ReadAsync(local))?.Mapping?.Server || local.DocumentId!=document || IsSceneRoute)throw new InvalidOperationException("Application ownership or manuscript changed.");
        if(_webTranslationBusy)throw new InvalidOperationException("Finish the current writing operation first.");
        _webTranslationBusy=true;
        try {
            await FlushActiveEditorAsync("brainstorming-keep-apply");_pageEditor?.RequireSavedForAi();if(_pageEditor is not null)await _pageEditor.SetTranslationReadOnlyAsync(true);
            await ValidateBrainstormKeepEditor(r);
            _brainstormKeepUnknown=true;
            var result=await new BrainstormingKeepWorkflow(BrainstormSessions).ApplyAsync(local,r,review=>BrainstormSessions.Keeps.ApplyAsync(review));
            RequireBrainstormKeepIdentity(local,document,identity);
            if(result.Keeps!.Single(k=>k.Review.OperationId==r.OperationId).Receipt?.State==BrainstormingApplicationState.Committed)await ReloadBrainstormManuscriptAsync();
            _brainstormKeepUnknown=false;
            return await BrainstormSessions.SyncAsync(local,true);
        }finally {_webTranslationBusy=false;if(_pageEditor is not null && !_brainstormKeepUnknown && DocumentId==document && identity==BrainstormSessions.Identity)await _pageEditor.SetTranslationReadOnlyAsync(false);}
    }
    private void RequireBrainstormKeepIdentity(BrainstormingScope scope,Guid document,string? identity) {
        if(DocumentId!=document || _brainstormContextScope!=scope || identity!=BrainstormSessions.Identity)throw new OperationCanceledException("Application identity changed. Inspect in the original account.");
    }
    private async Task SettleBrainstormConversationBeforeReplay(BrainstormingScope scope,Guid document,string? identity) {
        // Reopening prepares a new private context. Publish that retained local
        // revision before Undo/Redo advances the server conversation clock.
        await PrepareBrainstormDraftSend();
        var cache=await BrainstormSessions.SyncAsync(scope);
        RequireBrainstormKeepIdentity(scope,document,identity);
        if(cache.Pending is not null || cache.PendingGeneration is not null || cache.DraftEdit is not null || cache.Conflicts.Count!=0)
            throw new InvalidOperationException("Settle retained conversation operations before changing an approved save.");
    }
    private async Task<BrainstormingCache> InspectOrChangeBrainstormKeep(BrainstormingKeepJournal journal,bool change) {
        var scope=_brainstormContextScope??throw new InvalidOperationException("Open the original conversation.");var document=DocumentId;var identity=BrainstormSessions.Identity;
        if(_webTranslationBusy || IsSceneRoute || scope.DocumentId!=document || (await BrainstormSessions.Sessions.ReadAsync(scope))?.Mapping?.Server!=journal.Review.Scope)throw new InvalidOperationException("Open the original manuscript and finish the current writing operation.");
        _webTranslationBusy=true;
        try {
            await FlushActiveEditorAsync("brainstorming-inspect-history");_pageEditor?.RequireSavedForAi();await FlushNotesSaveAsync();RequireBrainstormKeepIdentity(scope,document,identity);
            // Inspection must remain available for an uncertain operation; only
            // an explicit undo/redo needs to settle new local conversation changes.
            if(change)await SettleBrainstormConversationBeforeReplay(scope,document,identity);
            if(_pageEditor is not null)await _pageEditor.SetTranslationReadOnlyAsync(true);
            _brainstormKeepUnknown=true;
            var receipt=change?await BrainstormSessions.Keeps.MoveAsync(journal.Review,new(Guid.NewGuid(),journal.Receipt!.Sequence,journal.Receipt.State==BrainstormingApplicationState.Undone)):await BrainstormSessions.Keeps.InspectAsync(journal.Review);
            RequireBrainstormKeepIdentity(scope,document,identity);
            var cache=await new BrainstormingKeepWorkflow(BrainstormSessions).RememberAsync(scope,journal.Review,receipt);
            if(receipt.State is BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone){await ReloadBrainstormManuscriptAsync();cache=await BrainstormSessions.SyncAsync(scope,true);}
            RequireBrainstormKeepIdentity(scope,document,identity);_brainstormKeepUnknown=receipt.State==BrainstormingApplicationState.Unknown;return cache;
        }finally {_webTranslationBusy=false;if(_pageEditor is not null && !_brainstormKeepUnknown && DocumentId==document && identity==BrainstormSessions.Identity)await _pageEditor.SetTranslationReadOnlyAsync(false);}
    }
    private async Task<BrainstormingCache> InspectBrainstormKeep(BrainstormingKeepJournal journal) {
        return await InspectOrChangeBrainstormKeep(journal,false);
    }
    private async Task<BrainstormingCache> ChangeBrainstormKeep(BrainstormingKeepJournal journal) {
        return await InspectOrChangeBrainstormKeep(journal,true);
    }
    private async Task RecoverBrainstormKeep(BrainstormingKeepJournal journal) {
        var scope=_brainstormContextScope??throw new InvalidOperationException("Open the original conversation.");var document=DocumentId;var identity=BrainstormSessions.Identity;
        var id=await BrainstormSessions.Keeps.RecoverAsync(journal.Review);RequireBrainstormKeepIdentity(scope,document,identity);
        _brainstormNavigationError="Original writing recovered as a separate document: "+id;
    }
    private async Task ViewBrainstormApplication(BrainstormingTarget destination) {
        await NavigateBrainstormTarget(destination);
        if(_brainstormNavigationError is null)_brainstormOpen=false;
    }
}
