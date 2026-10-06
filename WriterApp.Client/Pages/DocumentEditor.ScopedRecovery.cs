namespace WriterApp.Client.Pages;
public partial class DocumentEditor
{
    private bool _scopedRecoveryBusy;
    private async Task<bool> SaveBeforeScopedRecovery(){
        if(_clientAiRequest is not null || _isProposalApplying && !_scopedRecoveryBusy || _webTranslationBusy || _webTranslationUncertainOperation is not null)return false;
        await FlushActiveEditorAsync("scoped-history");await FlushNotesSaveAsync();
        _pageEditor?.RequireSavedForAi();
        if(_sceneSaveInFlight || _sceneApplying)return false;
        if(_activeSection is { } section)await SaveSceneCardAsync(section.Id,false);
        return !_sceneSaveInFlight && (_sceneStatus is null || !_sceneStatus.Contains("failed",StringComparison.OrdinalIgnoreCase));
    }
    private async Task ScopedRecoveryBusyChanged(bool busy){
        if(busy && (_clientAiRequest is not null || _isProposalApplying || _webTranslationBusy || _sceneApplying))throw new InvalidOperationException("Finish the current AI action before recovery.");
        if(!busy && !_scopedRecoveryBusy)return;
        _scopedRecoveryBusy=busy;_isProposalApplying=busy;if(_pageEditor is not null)await _pageEditor.SetTranslationReadOnlyAsync(busy);
    }
    private async Task ReloadAfterScopedRecovery(){
        _pendingAiProposal=null;_sceneAiProposalId=null;_checkedProposals.Clear();_checkedWritingPreviews.Clear();_writingContexts.Clear();DismissRecommendedCopy();DismissStyleReview();
        ++_writingEpoch;++_webTranslationGeneration;_webTranslationPreview=null;
        await ReloadTranslatedDocumentAsync();
        // Loading changes navigation context and can release the editor lock. Hold it until the panel finishes.
        if(_pageEditor is not null)await _pageEditor.SetTranslationReadOnlyAsync(true);
        if(_activeSection is { } section)await LoadSceneCardAsync(section.Id);
        await LoadAiHistoryAsync();
    }
}
