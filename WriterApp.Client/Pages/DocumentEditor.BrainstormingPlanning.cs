using WriterApp.Shared.Brainstorming;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private async Task<BrainstormingPlanningSnapshot> CaptureBrainstormPlanning(BrainstormingScope scope) {
        var document=DocumentId;var identity=BrainstormSessions.Identity;RequireBrainstormKeepIdentity(scope,document,identity);
        await PrepareBrainstormDraftSend();await FlushActiveEditorAsync("brainstorming-planning-review");_pageEditor?.RequireSavedForAi();await FlushNotesSaveAsync();
        var cache=await BrainstormSessions.SyncAsync(scope);RequireBrainstormKeepIdentity(scope,document,identity);
        if(cache.Pending!=null || cache.PendingGeneration!=null || cache.DraftEdit!=null || cache.Conflicts.Count!=0)throw new InvalidOperationException("Settle retained session operations before planning review.");
        var source=await BrainstormSessions.Planning.CaptureAsync(cache.Mapping?.Server??throw new InvalidOperationException("Map the conversation first."));RequireBrainstormKeepIdentity(scope,document,identity);return source;
    }
    private async Task<BrainstormingCache> RunBrainstormPlanning(BrainstormingPlanningReview r,bool inspect,bool undo) {
        var scope=_brainstormContextScope??throw new InvalidOperationException("Open the original conversation.");var document=DocumentId;var identity=BrainstormSessions.Identity;
        if(_webTranslationBusy || IsSceneRoute || scope.DocumentId!=document || (await BrainstormSessions.ReadAsync(scope))?.Mapping?.Server!=r.Scope)throw new InvalidOperationException("Open the original manuscript and finish pending writing.");
        _webTranslationBusy=true;
        try {
            await FlushActiveEditorAsync("brainstorming-planning-save");_pageEditor?.RequireSavedForAi();await FlushNotesSaveAsync();RequireBrainstormKeepIdentity(scope,document,identity);
            if(undo)await SettleBrainstormConversationBeforeReplay(scope,document,identity);
            if(_pageEditor is not null)await _pageEditor.SetTranslationReadOnlyAsync(true);
            BrainstormingCache cache;
            if(inspect) {
                var receipt=undo?await BrainstormSessions.Planning.UndoAsync(r):await BrainstormSessions.Planning.InspectAsync(r);RequireBrainstormKeepIdentity(scope,document,identity);
                cache=await new BrainstormingPlanningWorkflow(BrainstormSessions).RememberAsync(scope,r,receipt);
            } else {
                var synced=await BrainstormSessions.SyncAsync(scope);if(synced.Pending!=null || synced.Conflicts.Count!=0)throw new InvalidOperationException("Settle the conversation before approving planning.");
                RequireBrainstormKeepIdentity(scope,document,identity);
                cache=await new BrainstormingPlanningWorkflow(BrainstormSessions).ApplyAsync(scope,r,review=>BrainstormSessions.Planning.ApplyAsync(review));
            }
            RequireBrainstormKeepIdentity(scope,document,identity);
            if(cache.PlanningOperations!.Single(p=>p.Review.OperationId==r.OperationId).Receipt?.State is BrainstormingApplicationState.Committed or BrainstormingApplicationState.Undone) {
                await ReloadBrainstormManuscriptAsync();cache=await BrainstormSessions.SyncAsync(scope,true);
            }
            RequireBrainstormKeepIdentity(scope,document,identity);return cache;
        }finally {_webTranslationBusy=false;if(_pageEditor is not null && DocumentId==document && identity==BrainstormSessions.Identity)await _pageEditor.SetTranslationReadOnlyAsync(false);}
    }
    private Task<BrainstormingCache> ApplyBrainstormPlanning(BrainstormingPlanningReview r)=>RunBrainstormPlanning(r,false,false);
    private Task<BrainstormingCache> InspectBrainstormPlanning(BrainstormingPlanningJournal j,bool undo)=>RunBrainstormPlanning(j.Review,true,undo);
}
