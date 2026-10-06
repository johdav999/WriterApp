using System.Net.Http.Json;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Client.Services;
using WriterApp.Shared;

namespace WriterApp.Client.Pages;
public partial class DocumentEditor
{
    [Microsoft.AspNetCore.Components.Inject] public WebCheckedAi CheckedAi { get; set; } = default!;
    private WebCheckedAi Checked => CheckedAi ?? new WebCheckedAi(Http);
    private sealed record WebProposal(WebCheckedAi.Lease Lease,int Generation,Guid? Page,string? Html,DateTimeOffset Created);
    private readonly Dictionary<Guid,WebProposal> _checkedProposals=new();
    private string _qualityStyleGoal = WriterApp.Shared.StyleQualityReview.DefaultGoal;
    private Task QualityStyleGoalChanged(string goal) {
        if (StyleReviewBusy) return Task.CompletedTask;
        DismissStyleReview();
        _qualityStyleGoal = WriterApp.Shared.StyleQualityReview.Goal(goal).Key;
        // An AI candidate generated for another goal must be reviewed again.
        for (int i = 0; i < _qualityIssues.Count; i++)
            if (_qualityHistoryProposals.ContainsKey(_qualityIssues[i].IssueKey))
                _qualityIssues[i] = _qualityIssues[i] with { Fix = null };
        _qualityHistoryProposals.Clear();
        return Task.CompletedTask;
    }
    private WebProposal? _qualityCheckedSource;
    private WebProposal? _continuityCheckedSource;
    private WebCheckedAi.Lease? _sceneApprovalLease;
    private string? _sceneReviewedSnapshot;
    private string? _sceneSavedFingerprint;
    private string? _sceneSavedSnapshot;
    private async Task<WebProposal> CaptureCheckedRequest(AiActionExecuteRequestDto request,CancellationToken ct)
    {
        RequireClientAiRequest(ct);
        await FlushActiveEditorAsync("checked-ai-request");
        RequireClientAiRequest(ct);
        _pageEditor?.RequireSavedForAi();
        if(_notesEditVersion>_notesSavedVersion)await FlushNotesSaveAsync();
        RequireClientAiRequest(ct);
        if(_notesSaveInFlight || _notesSaveQueued || _notesError is not null)throw new InvalidOperationException("Finish saving notes before AI generation.");
        var id=request.DocumentId ?? throw new InvalidOperationException("Choose a saved manuscript.");
        int generation=_webTranslationGeneration;
        string? html=_pageEditor is null ? null : await _pageEditor.GetContentAsync(ct);
        var lease=await Checked.Capture(id,request.SectionId,request.PageId,IsSceneRoute ? SceneNodeId : null,ct);
        TrackClientAiLease(ct, lease);
        if(!TranslationContextCurrent(generation,id,lease.Backend) || request.SectionId!=_activeSection?.Id)
            throw new InvalidOperationException("AI target changed while saving. Generate again.");
        return new(lease,generation,_activePage?.Id,html,DateTimeOffset.UtcNow);
    }
    private async Task<HttpResponseMessage> PostCheckedCanonRefresh(string type,bool rebuild,CancellationToken ct = default)
    {
        RequireClientAiRequest(ct);
        await FlushActiveEditorAsync("canon-refresh");await FlushNotesSaveAsync();
        RequireClientAiRequest(ct);
        int generation=_webTranslationGeneration;
        var lease=await Checked.Capture(DocumentId,_activeSection!.Id,ct:ct);
        using var snapshotResponse=await Http.GetAsync($"api/documents/{DocumentId}/bibles/{type}/device?expectedDocumentVersion={Uri.EscapeDataString(lease.Source.DocumentVersion)}",ct);
        if(!snapshotResponse.IsSuccessStatusCode)throw new InvalidOperationException("Checked canon capability unavailable. Save and update the backend.");
        var snapshot=await WebAiSources.Read<WriterApp.Shared.Canon.DeviceBibleSnapshot>(snapshotResponse.Content,ct);
        if(snapshot.ContractVersion!=1 || snapshot.DocumentId!=DocumentId || snapshot.CheckedDocumentVersion!=lease.Source.DocumentVersion)throw new InvalidDataException("Unchecked canon snapshot.");
        await Checked.Confirm(lease,ct);
        var request=new WriterApp.Shared.Canon.DeviceBibleRefreshRequest(lease.Source.DocumentVersion,snapshot.SnapshotVersion,rebuild,_activeSection.Id,lease.Source);
        var response=await Http.PostAsJsonAsync($"api/documents/{DocumentId}/bibles/{type}/device/refresh",request,ct);
        try {
            RequireClientAiRequest(ct);
            Checked.RequireLease(lease);
            if(!TranslationContextCurrent(generation,lease.Source.DocumentId,lease.Backend))throw new InvalidOperationException("Account or manuscript changed during canon refresh.");
            if(response.IsSuccessStatusCode) {
                var result=await WebAiSources.Read<WriterApp.Shared.Canon.DeviceBibleSnapshot>(response.Content,ct);
                RequireClientAiRequest(ct);
                if(result.ContractVersion!=1 || result.DocumentId!=lease.Source.DocumentId || result.Kind!=snapshot.Kind || result.SourceDocumentVersion!=lease.Source.DocumentVersion)
                    throw new InvalidDataException("Unchecked canon refresh response.");
                _=WriterApp.Shared.Canon.CanonContent.Parse(result.Kind,result.ContentJson);
                response.Content.Dispose();response.Content=JsonContent.Create(new BibleSnapshotDto(type,1,result.ContentJson,result.RefreshedAt,result.ChangedSections,result.ChangedSections,0,0,0,0,0));
            }
            return response;
        } catch { response.Dispose();throw; }
    }
    private async Task RequireCheckedProposal(WebProposal context,CancellationToken ct = default)
    {
        RequireClientAiRequest(ct);
        WebAiSources.RequireTime(context.Created);
        if(!TranslationContextCurrent(context.Generation,context.Lease.Source.DocumentId,context.Lease.Backend)
            || context.Lease.Source.SectionId!=_activeSection?.Id || context.Page!=_activePage?.Id)
            throw new InvalidOperationException("The account, section or page changed. Generate again.");
        if(context.Html is not null && _pageEditor is not null && context.Html!=await _pageEditor.GetContentAsync(ct))
            throw new InvalidOperationException("Editor writing changed. Save and generate again.");
        await Checked.Confirm(context.Lease,ct);
        RequireClientAiRequest(ct);
        if(!TranslationContextCurrent(context.Generation,context.Lease.Source.DocumentId,context.Lease.Backend) || context.Lease.Source.SectionId!=_activeSection?.Id || context.Page!=_activePage?.Id
            || context.Html is not null && _pageEditor is not null && context.Html!=await _pageEditor.GetContentAsync(ct))
            throw new InvalidOperationException("Editor target changed during the source check. Generate again.");
    }
    private async Task<bool> ValidateCheckedApproval(PendingAiProposal pending)
    {
        try {
            if(!_checkedProposals.TryGetValue(pending.ProposalId,out var context)) throw new InvalidOperationException("This legacy proposal has no checked source. Generate and review again before Apply.");
            await RequireCheckedProposal(context);
            if(!_checkedWritingPreviews.ContainsKey(pending.ProposalId) && pending.ActionKey is not ("translate.section" or "translate.document")
                && ResolveAiApplyMode(pending.Context?.Scope,pending.ActionKey,pending.Context?.AppendAtEnd==true)=="section")
                throw new InvalidOperationException("This section preview has no supported rich multi-page application contract. Use a checked section writing preset or a selection action.");
            if(_pageEditor is not null && !_checkedWritingPreviews.ContainsKey(pending.ProposalId) && pending.ActionKey is not ("translate.section" or "translate.document"))
            {
                _pageEditor.SetAiSaveSource(context.Lease.Source);
                _pageEditor.SetAiHistoryProposal(pending.ProposalId,context.Lease);
            }
            return true;
        } catch(Exception e) { _pendingAiProposal=pending with { ErrorMessage=e.Message }; await InvokeAsync(StateHasChanged);return false; }
    }
    private void RestoreCheckedScene(string json)
    {
        var card=System.Text.Json.JsonSerializer.Deserialize<SectionSceneCardProposalDto>(json,JsonOptions)!;
        _sceneSummary=card.Summary ?? "";_sceneCardMetadataStatus=card.Status ?? "Draft";
        _sceneNarrativeRole=card.NarrativeRole ?? "";_sceneNarrativeIntent=card.NarrativeIntent ?? "";
        _sceneEmotionalBeat=card.EmotionalBeat ?? "";_sceneKeyEvents=card.KeyEvents ?? "";_sceneOpenQuestions=card.OpenQuestions ?? "";
        _scenePovCharacterId=card.PovCharacterId ?? "";_scenePlaceId=card.PlaceId ?? "";_sceneTimelineEventId=card.TimelineEventId ?? "";_sceneTimeRef=card.TimeRef ?? "";
        _sceneTagsText=string.Join(", ",card.Tags ?? []);_sceneSubplotTagsText=string.Join(", ",card.SubplotTags ?? []);_sceneReferencesJson=SerializeSceneReferences(card.References);
    }
    private async Task<HttpResponseMessage> CheckedHistoryMove(string mode) {
        var lease=await Checked.Capture(DocumentId,_activeSection!.Id,_activePage?.Id,IsSceneRoute ? SceneNodeId : null);
        using var message=new HttpRequestMessage(HttpMethod.Post,"api/ai/actions/history/web/move") {Content=JsonContent.Create(new WebAiHistoryMoveRequest(lease.Source,mode=="undo"?"Undone":"Redone"))};
        var response=await Http.SendAsync(message);
        try {
            if(response.IsSuccessStatusCode && response.StatusCode!=System.Net.HttpStatusCode.NoContent) {
                var move=await WebAiSources.Read<WebAiHistoryMove>(response.Content);
                var intent=move.Intent;WebAiHistoryContracts.Validate(intent);
                if(intent.Source!=lease.Source || intent.Outcome!=(mode=="undo"?"Undone":"Redone") || _pageEditor is null || await _pageEditor.GetContentAsync()!=intent.BeforeContent)
                    throw new InvalidDataException("This history result has no current checked source. Use editor undo or original-copy recovery.");
                await Checked.Confirm(lease);
                // Persist browser/server intent before changing editor content; Move itself changes nothing.
                await HistoryOutbox.Prepare(intent,lease);
                _pageEditor.SetAiHistoryMove(intent,lease);
                var result=new AiActionUndoRedoResponseDto(intent.ProposalId,intent.AfterContent!,intent.BeforeContent,lease.Source);
                response.Content.Dispose();response.Content=JsonContent.Create(result);await response.Content.LoadIntoBufferAsync();
            }
            return response;
        } catch { response.Dispose();throw; }
    }
}
