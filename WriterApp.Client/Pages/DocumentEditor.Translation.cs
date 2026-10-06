using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Shared;

namespace WriterApp.Client.Pages;

public partial class DocumentEditor
{
    private sealed record WebTranslationPreview(Guid ProposalId,Guid OperationId,string Backend,WebTranslationSource Source,
        TranslationStructure Original,TranslationStructure Translated,string SourceLanguage,
        IReadOnlyList<TranslationPageReview> Pages);
    private sealed record CapturedRuns(IReadOnlyList<TranslationRun> Runs);
    private WebTranslationPreview? _webTranslationPreview;
    private readonly List<WebTranslationReceipt> _webTranslationReceipts = new();
    private string? _webTranslationReceiptsBackend;
    private bool _webTranslationBusy;
    private Guid? _webTranslationUncertainOperation;
    private string? _webTranslationUncertainBackend;
    private string? _webTranslationError;
    private int _webTranslationGeneration;
    private CancellationTokenSource? _webTranslationCts;
    private Guid _webTranslationLoadedDocument;
    private static readonly JsonSerializerOptions TranslationJson = new(JsonSerializerDefaults.Web);
    private string TranslationEndpoint(Guid id) => $"api/documents/{id}/structured-translations";
    private bool TranslationContextCurrent(int generation,Guid id,string backend) =>
        generation==_webTranslationGeneration && DocumentId==id && backend==(Http.BaseAddress?.AbsoluteUri ?? "");
    private void RequireTranslationReceiptContext(WebTranslationReceipt receipt,string backend) {
        if(_webTranslationReceiptsBackend!=backend || receipt.SourceDocumentId!=DocumentId
            || !_webTranslationReceipts.Contains(receipt))
            throw new InvalidOperationException("The account, document or backend changed. Reload recovery from the original backend before using this approval.");
    }

    private async Task<WebTranslationSource> TranslationSource(Guid id,string scope,Guid? section,CancellationToken ct = default) {
        using var response=await Http.GetAsync($"{TranslationEndpoint(id)}/source?scope={scope}&sectionId={section}",ct);
        if(!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadApiErrorMessageAsync(response,
            "This backend cannot safely apply structured translations. Update its checked translation capability and migrations; preview/copy remains available for legacy proposals."));
        return await response.Content.ReadFromJsonAsync<WebTranslationSource>(ct) ?? throw new InvalidDataException("Missing checked source.");
    }
    private static void SameTranslationSource(WebTranslationSource current,WebTranslationSource original) {
        if(JsonSerializer.Serialize(current with { DocumentVersion="ack" },TranslationJson)!=JsonSerializer.Serialize(original with { DocumentVersion="ack" },TranslationJson))
            throw new InvalidOperationException("Writing, planning or account changed. Generate a new checked translation.");
    }
    private async Task ConfirmActiveTranslationSave(WebTranslationSource source,CancellationToken ct = default) {
        if(_pageEditor is not null && _activePage is not null) {
            var saved=source.Sections.SelectMany(s => s.Pages).SingleOrDefault(p => p.Id==_activePage.Id);
            if(saved is not null && await _pageEditor.GetContentAsync(ct)!=saved.Content)
                throw new InvalidOperationException("The editor has unsaved changes. Retry saving before translating or applying.");
        }
    }
    private async Task ExecuteStructuredTranslationAsync(AiActionOption action)
    {
        if(_webTranslationBusy || _activeSection is null) return;
        _webTranslationBusy=true;_webTranslationError=null;
        _webTranslationCts?.Dispose();_webTranslationCts=new();
        int generation=++_webTranslationGeneration;Guid id=DocumentId;Guid section=_activeSection.Id;
        string backend=Http.BaseAddress?.AbsoluteUri ?? "";
        string sourceLanguage=_translateSourceLanguage,targetLanguage=_translateTargetLanguage,style=_translateStyle;
        try {
            await FlushActiveEditorAsync("translation-capture");
            await FlushNotesSaveAsync();
            if(!TranslationContextCurrent(generation,id,backend) || _activeSection?.Id!=section) return;
            string scope=action.ActionKey=="translate.section" ? "section" : "document";
            var source=await TranslationSource(id,scope,section);
            await ConfirmActiveTranslationSave(source);
            if(!TranslationContextCurrent(generation,id,backend) || _activeSection?.Id!=section) return;
            var sections=new List<TranslationSection>();
            foreach(var s in source.Sections) {
                var pages=new List<TranslationPage>();
                foreach(var p in s.Pages) {
                    var capture=await JSRuntime.InvokeAsync<CapturedRuns>("tiptapEditor.captureTranslation",p.Content,"Html");
                    if(!TranslationContextCurrent(generation,id,backend) || _activeSection?.Id!=section) return;
                    pages.Add(new(p.Id,capture.Runs));
                }
                sections.Add(new(s.Id,pages));
            }
            var original=new TranslationStructure(1,id,scope,targetLanguage,sections);
            string json=TranslationStructures.Serialize(original);
            var parameters=new Dictionary<string,object?> { [TranslationStructures.Parameter]=json,
                ["web_translation_source"]=JsonSerializer.Serialize(source,TranslationJson),
                ["target_language"]=targetLanguage,["source_language"]=sourceLanguage,["style"]=style };
            var request=new AiActionExecuteRequestDto(id,section,source.Sections.First().Pages.First().Id,
                null,null,null,null,null,parameters,source.DocumentVersion);
            using var response=await PostAiActionAsync(action.ActionKey,request,commandLabel:action.Label,cancellationToken:_webTranslationCts.Token);
            if(!response.IsSuccessStatusCode) {
                if(await TryHandleEntitlementDeniedAsync(response,"ai.actions","Upgrade to continue using AI features."))
                    throw new InvalidOperationException(_entitlementUserMessage);
                if(await TryHandlePlanUpgradeRequiredAsync(response)) return;
                if(await TryHandleAiQuotaExceededAsync(response)) throw new InvalidOperationException(_aiQuotaMessage);
                throw new InvalidOperationException(await ReadApiErrorMessageAsync(response,"Translation failed."));
            }
            var result=await response.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>() ?? throw new InvalidDataException("Missing translation result.");
            if(!TranslationContextCurrent(generation,id,backend) || _activeSection?.Id!=section) return;
            if(result.SourceDocumentVersion!=source.DocumentVersion || result.ActionKey!=action.ActionKey
                || result.ProposalId==Guid.Empty || DateTimeOffset.UtcNow-result.CreatedUtc>TimeSpan.FromHours(1))
                throw new InvalidDataException("The backend returned an unchecked or expired translation. Request a checked preview.");
            var translated=TranslationStructures.Result(result.ProposedText ?? "",original);
            SameTranslationSource(await TranslationSource(id,scope,section),source);
            await ConfirmActiveTranslationSave(source);
            if(!TranslationContextCurrent(generation,id,backend) || _activeSection?.Id!=section) return;
            var reviews=new List<TranslationPageReview>();
            for(int s=0;s<source.Sections.Count;s++)for(int p=0;p<source.Sections[s].Pages.Count;p++) {
                var page=source.Sections[s].Pages[p];
                // Exercise exactly the shared shipped schema used by the desktop; output is inert.
                _=await JSRuntime.InvokeAsync<string>("tiptapEditor.previewTranslation",page.Content,"Html",translated.Sections[s].Pages[p].Runs);
                if(!TranslationContextCurrent(generation,id,backend) || _activeSection?.Id!=section) return;
                reviews.Add(new(source.Sections[s].Title+" / "+page.Title,
                    string.Join("\n",original.Sections[s].Pages[p].Runs.Select(r => r.Text)),
                    string.Join("\n",translated.Sections[s].Pages[p].Runs.Select(r => r.Text))));
            }
            _webTranslationPreview=new(result.ProposalId,Guid.NewGuid(),backend,source,original,translated,sourceLanguage,reviews);
            _pendingAiProposal=new(result.ProposalId,action.ActionKey,action.Label,string.Join("\n\n",reviews.Select(r => r.Original)),
                string.Join("\n\n",reviews.Select(r => r.Proposed)),result.ChangesSummary,null,result.CreatedUtc);
            _translationApplyMode="replace";
        } catch(Exception e) { if(TranslationContextCurrent(generation,id,backend)) _webTranslationError=e.Message; }
        finally { _webTranslationBusy=false;await InvokeAsync(StateHasChanged); }
    }

    private async Task ApplyStructuredTranslationAsync(PendingAiProposal pending)
    {
        var preview=_webTranslationPreview;
        if(_webTranslationBusy || preview?.ProposalId!=pending.ProposalId) return;
        if(preview.Backend!=(Http.BaseAddress?.AbsoluteUri ?? "")) {
            _webTranslationError="The backend changed. Request a new checked preview.";
            return;
        }
        _webTranslationBusy=true;_webTranslationError=null;
        int generation=_webTranslationGeneration;Guid id=DocumentId;
        try {
            if(_pageEditor is not null) await _pageEditor.SetTranslationReadOnlyAsync(true);
            await FlushActiveEditorAsync("translation-approve");
            var current=await TranslationSource(id,preview.Source.Scope,preview.Source.Sections.First().Id);
            SameTranslationSource(current,preview.Source);await ConfirmActiveTranslationSave(current);
            if(!TranslationContextCurrent(generation,preview.Source.DocumentId,preview.Backend)) return;
            var approval=new WebTranslationApproval(preview.OperationId,pending.ProposalId,preview.Source,preview.Original,
                preview.Translated,WritingOutline.Consumes(pending.ActionKey) ? "replace" : _translationApplyMode,preview.SourceLanguage,WritingOutline.Consumes(pending.ActionKey));
            await PrepareAggregateHistory(pending,preview);
            using var response=await Http.PostAsJsonAsync($"{TranslationEndpoint(id)}/approve",approval);
            if(!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadApiErrorMessageAsync(response,"Approval was not acknowledged. Reload the translation recovery list before retrying."));
            var receipt=await response.Content.ReadFromJsonAsync<WebTranslationReceipt>() ?? throw new InvalidDataException("Missing durable approval receipt.");
            if(!TranslationContextCurrent(generation,id,preview.Backend)) return;
            if(receipt.OperationId!=preview.OperationId || receipt.ProposalId!=pending.ProposalId || receipt.SourceDocumentId!=id
                || receipt.State is not ("Approved" or "Committed")) throw new InvalidDataException("The approval receipt does not match this reviewed translation. Reload recovery before retrying.");
            _webTranslationReceiptsBackend=preview.Backend;
            _webTranslationReceipts.RemoveAll(r => r.OperationId==receipt.OperationId);_webTranslationReceipts.Insert(0,receipt);
            await CommitStructuredTranslationAsync(receipt,generation,id,preview.Backend);
        } catch(Exception e) { if(TranslationContextCurrent(generation,id,preview.Backend)) _webTranslationError=e.Message; }
        finally {
            if(_pageEditor is not null && _webTranslationUncertainOperation is null) await _pageEditor.SetTranslationReadOnlyAsync(false);
            _webTranslationBusy=false;await InvokeAsync(StateHasChanged);
        }
    }
    private async Task CommitStructuredTranslationAsync(WebTranslationReceipt receipt,int generation,Guid id,string backend) {
        if(!TranslationContextCurrent(generation,id,backend)) return;
        _webTranslationUncertainOperation=receipt.OperationId;
        _webTranslationUncertainBackend=backend;
        using var response=await Http.PostAsync($"{TranslationEndpoint(id)}/operations/{receipt.OperationId}/commit",null);
        if(!TranslationContextCurrent(generation,id,backend)) return;
        if(!response.IsSuccessStatusCode) {
            if(response.StatusCode is System.Net.HttpStatusCode.Conflict or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.NotFound)
                { _webTranslationUncertainOperation=null;_webTranslationUncertainBackend=null; }
            throw new InvalidOperationException(await ReadApiErrorMessageAsync(response,
                "Save was not acknowledged. Writing is paused until recovery confirms the outcome. Reload recovery or finish the same approved operation."));
        }
        var saved=await response.Content.ReadFromJsonAsync<WebTranslationReceipt>();
        if(saved is null || saved.OperationId!=receipt.OperationId || saved.ProposalId!=receipt.ProposalId
            || saved.SourceDocumentId!=id || saved.State!="Committed") throw new InvalidDataException("Persistence was not confirmed. Reconcile the approved operation.");
        if(!TranslationContextCurrent(generation,id,backend)) return;
        _webTranslationUncertainOperation=null;_webTranslationUncertainBackend=null;
        _webTranslationReceipts.RemoveAll(r => r.OperationId==saved.OperationId);_webTranslationReceipts.Insert(0,saved);
        _pendingAiProposal=null;_webTranslationPreview=null;
        UpdateAiHistoryAppliedState(saved.ProposalId,DateTimeOffset.UtcNow);
        await RefreshHistoryDelivery(true);
        // The aggregate receipt is authoritative. Never send its snapshots to legacy page Undo.
        if(saved.ResultDocumentId!=id) Navigation.NavigateTo($"/documents/{saved.ResultDocumentId}");
        else {
            await ReloadTranslatedDocumentAsync();
        }
    }
    protected virtual async Task ReloadTranslatedDocumentAsync() {
        await LoadDocumentAsync();
        if(_pageEditor is not null && _activePage is not null) await _pageEditor.SetContentAsync(_activePage.Content,markDirty:false);
    }
    private async Task RefreshTranslationRecoveryAsync() {
        Guid id=DocumentId;int generation=_webTranslationGeneration;string backend=Http.BaseAddress?.AbsoluteUri ?? "";
        if(_webTranslationUncertainOperation is not null && _webTranslationUncertainBackend!=backend
            || _webTranslationPreview is not null && _webTranslationPreview.Backend!=backend) {
            _webTranslationError="The backend changed. Return to the original backend and reload recovery to confirm this translation's outcome.";
            return;
        }
        try {
            var rows=await Http.GetFromJsonAsync<List<WebTranslationReceipt>>($"{TranslationEndpoint(id)}/operations");
            if(TranslationContextCurrent(generation,id,backend)) {
                if(rows?.Any(r => r.SourceDocumentId!=id || r.OperationId==Guid.Empty || r.ProposalId==Guid.Empty
                    || r.State is not ("Approved" or "Committed"))==true) throw new InvalidDataException("Invalid recovery receipt.");
                _webTranslationReceiptsBackend=backend;
                _webTranslationReceipts.Clear();_webTranslationReceipts.AddRange(rows ?? []);
                var confirmed=rows?.FirstOrDefault(r => r.State=="Committed" &&
                    (r.OperationId==_webTranslationUncertainOperation && _webTranslationUncertainBackend==backend
                        || r.ProposalId==_pendingAiProposal?.ProposalId && _webTranslationPreview?.Backend==backend));
                if(confirmed is not null) {
                    _webTranslationUncertainOperation=null;_webTranslationUncertainBackend=null;_pendingAiProposal=null;_webTranslationPreview=null;_webTranslationError=null;
                    await ReloadTranslatedDocumentAsync();
                    if(TranslationContextCurrent(generation,id,backend) && _pageEditor is not null) await _pageEditor.SetTranslationReadOnlyAsync(false);
                }
            }
        } catch { /* Older backends still allow ordinary writing/selection and legacy preview. */ }
    }
    private async Task ResumeTranslationAsync(WebTranslationReceipt receipt) {
        if(_webTranslationBusy) return;_webTranslationBusy=true;_webTranslationError=null;
        Guid id=DocumentId;int generation=_webTranslationGeneration;string backend=Http.BaseAddress?.AbsoluteUri ?? "";
        try {
            RequireTranslationReceiptContext(receipt,backend);
            if(_pageEditor is not null) await _pageEditor.SetTranslationReadOnlyAsync(true);
            await FlushActiveEditorAsync("translation-resume");
            await CommitStructuredTranslationAsync(receipt,generation,id,backend);
        }
        catch(Exception e) { if(TranslationContextCurrent(generation,id,backend)) _webTranslationError=e.Message; }
        finally {
            if(_pageEditor is not null && _webTranslationUncertainOperation is null) await _pageEditor.SetTranslationReadOnlyAsync(false);
            _webTranslationBusy=false;
        }
    }
    private async Task RecoverTranslationOriginalAsync(WebTranslationReceipt receipt) {
        if(_webTranslationBusy) return;_webTranslationBusy=true;_webTranslationError=null;
        Guid id=DocumentId;int generation=_webTranslationGeneration;string backend=Http.BaseAddress?.AbsoluteUri ?? "";
        try {
            RequireTranslationReceiptContext(receipt,backend);
            using var response=await Http.PostAsync($"{TranslationEndpoint(id)}/operations/{receipt.OperationId}/original",null);
            if(!response.IsSuccessStatusCode) throw new InvalidOperationException(await ReadApiErrorMessageAsync(response,"Original-copy recovery failed. Your writing is unchanged."));
            if(TranslationContextCurrent(generation,id,backend)) Navigation.NavigateTo($"/documents/{receipt.OriginalCopyId}");
        } catch(Exception e) { if(TranslationContextCurrent(generation,id,backend)) _webTranslationError=e.Message; }
        finally { _webTranslationBusy=false; }
    }
    private void InvalidateStructuredTranslation(bool preserveConsistency = false, bool keepClientAiRequest = false) {
        DismissStyleReview();
        DismissRecommendedCopy();
        // Only a checked, controlled page navigation retains its owning request.
        if (!keepClientAiRequest) CancelClientAiRequestCore(false);
        _continuitySourcePreview = null;
        _historyDelivery=[];_qualityHistoryProposals.Clear();
        if (!preserveConsistency) { _intentionalContinuity.Clear(); _continuityHistoryProposals.Clear(); }
        foreach (var id in _checkedProposals.Keys.Where(id => !preserveConsistency || !_continuityHistoryProposals.ContainsValue(id)).ToArray())
            _checkedProposals.Remove(id);
        _checkedWritingPreviews.Clear();
        ++_writingEpoch;_writingContexts.Clear();
        if(_pendingAiProposal is { } writing && WritingOutline.Consumes(writing.ActionKey))_pendingAiProposal=null;
        _webTranslationCts?.Cancel();
        _webTranslationUncertainOperation=null;_webTranslationUncertainBackend=null;
        if(_pageEditor is not null) _= _pageEditor.SetTranslationReadOnlyAsync(false);
        ++_webTranslationGeneration;_webTranslationPreview=null;_webTranslationReceipts.Clear();_webTranslationReceiptsBackend=null;_webTranslationError=null;
        if(_pendingAiProposal?.ActionKey is "translate.section" or "translate.document") _pendingAiProposal=null;
    }
    private void CancelStructuredTranslation() {
        var uncertain=_webTranslationUncertainOperation;
        var uncertainBackend=_webTranslationUncertainBackend;
        InvalidateStructuredTranslation();
        _webTranslationUncertainOperation=uncertain;
        _webTranslationUncertainBackend=uncertainBackend;
        if(uncertain is not null && _pageEditor is not null) _= _pageEditor.SetTranslationReadOnlyAsync(true);
        _webTranslationError="Cancelled. If a save was already approved, reload recovery to confirm its outcome before retrying.";
    }
}
