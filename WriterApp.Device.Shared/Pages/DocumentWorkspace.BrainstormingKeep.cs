using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Device.Shared.Pages;

public partial class DocumentWorkspace
{
    [Inject] private LocalBrainstormingKeeps BrainstormKeeps { get; set; }=default!;
    [Inject] private IJSRuntime BrainstormKeepJS { get; set; }=default!;
    private async Task<BrainstormingKeepReview> PrepareBrainstormKeepAsync(Guid candidateId,bool replace,string title)
    {
        var scope=_brainstormContextScope??throw new InvalidOperationException("Open the original conversation.");var editor=_editor;var session=_session;
        if(editor is null || session is null || session.Document.DocumentId!=scope.DocumentId || PanelOperationsDisabled)throw new InvalidOperationException("Open the original manuscript and resolve recovery first.");
        if(!await SaveAllAsync(releaseEditor:false))throw new InvalidOperationException("Save pending manuscript writing before Keep.");
        try {
            var check=await BrainstormSessions.RequireCurrentAsync(scope);var cache=await BrainstormSessions.Sessions.ReadAsync(scope)??throw new InvalidDataException("Conversation unavailable.");
            if(cache.Pending is not null || cache.PendingGeneration is not null || cache.DraftEdit is not null || cache.Conflicts.Count!=0)throw new InvalidOperationException("Settle proposal/conversation recovery before Keep.");
            var candidate=cache.Session.Drafts.Single(d=>d.Id==candidateId);var target=cache.Session.Targets.Single(t=>t.Id==candidate.TargetId);
            await BrainstormSessions.RequireApplicationSourceAsync(scope,target,session.Document);check();
            int? from=null,to=null;
            if(replace) {
                if(_selectedPage!=target.PageId)throw new InvalidOperationException("Select the passage in the original manuscript page.");
                var capture=await editor.CaptureAiAsync(false,true,true);if(capture.SelectionStart>=capture.SelectionEnd)throw new InvalidOperationException("Select a manuscript passage first.");from=capture.SelectionStart;to=capture.SelectionEnd;
            }
            var review=BrainstormingKeepContract.Review(scope,candidate,target,LocalBrainstormingWriting.Capture(session.Document),from,to,title);
            if(!replace && target.Kind!=BrainstormingTargetKind.CreateSection)review=BrainstormingPlacements.ReviewAt(review,BrainstormingPlacements.Default(review),title);
            await ValidateBrainstormKeepEditorAsync(review);check();if(_session!=session || _editor!=editor)throw new OperationCanceledException("Manuscript changed during application review.");return review;
        } finally {await ReleaseContextEditorAsync();}
    }
    private async Task ValidateBrainstormKeepEditorAsync(BrainstormingKeepReview r) {
        await using var module=await BrainstormKeepJS.InvokeAsync<IJSObjectReference>("import","./_content/WriterApp.Device.Shared/editor/device-editor.js");
        var before=r.Kind==BrainstormingTargetKind.CreateSection?"":r.Before.Pages.Single(p=>p.Id==BrainstormingPlacements.Destination(r).PageId).Content;
        if(await module.InvokeAsync<string>("previewBrainstormingKeep",before,r.Candidate.EditorHtml??BrainstormingDraftContent.Html(r.Candidate.PlainText),r.AfterHtml,(int)r.Kind,r.From,r.To,r.ReplacedText,r.Kind==BrainstormingTargetKind.InsertInPage?BrainstormingPlacements.Boundary(r):0)!=r.AfterHtml)throw new InvalidDataException("The actual editor mapping changed.");
    }
    private async Task<BrainstormingCache> ApplyBrainstormKeepAsync(BrainstormingKeepReview r) {
        if(_brainstormContextScope!=r.Scope || _session?.Document.DocumentId!=r.Scope.DocumentId || !await BeforeContextWorkAsync())throw new InvalidOperationException("Save pending writing and resolve recovery before applying.");
        try {
            await ValidateBrainstormKeepEditorAsync(r);
            var flow=new BrainstormingKeepWorkflow(BrainstormSessions);
            var cache=await flow.ApplyAsync(r.Scope,r,review=>BrainstormKeeps.ResumeAsync(review));return cache;
        }finally{await AfterContextWorkAsync();await ReloadHistoryAsync();}
    }
    private async Task<BrainstormingCache> InspectBrainstormKeepAsync(BrainstormingKeepJournal journal) {
        if(_brainstormContextScope!=journal.Review.Scope || !await BeforeContextWorkAsync())throw new InvalidOperationException("Save pending writing before inspection.");
        try {var receipt=await BrainstormKeeps.InspectAsync(journal.Review);return await new BrainstormingKeepWorkflow(BrainstormSessions).RememberAsync(journal.Review.Scope,journal.Review,receipt);}
        finally {await AfterContextWorkAsync();await ReloadHistoryAsync();}
    }
    private async Task<BrainstormingCache> ChangeBrainstormKeepAsync(BrainstormingKeepJournal journal) {
        if(_brainstormContextScope!=journal.Review.Scope || !await BeforeContextWorkAsync())throw new InvalidOperationException("Save pending writing before Undo/Redo.");
        try{var receipt=await BrainstormKeeps.MoveAsync(journal.Review,journal.Receipt!.State==BrainstormingApplicationState.Undone);return await new BrainstormingKeepWorkflow(BrainstormSessions).RememberAsync(journal.Review.Scope,journal.Review,receipt);}
        finally{await AfterContextWorkAsync();await ReloadHistoryAsync();}
    }
    private async Task RecoverBrainstormKeepAsync(BrainstormingKeepJournal journal) {await BrainstormKeeps.RecoverAsync(journal.Review);_brainstormNavigationError="Original writing recovered as a separate local document. Current manuscript retained.";}
    private async Task ViewBrainstormApplicationAsync(BrainstormingTarget destination) {
        await NavigateBrainstormTargetAsync(destination);
        if(_brainstormNavigationError is null)_brainstormOpen=false;
    }
}
