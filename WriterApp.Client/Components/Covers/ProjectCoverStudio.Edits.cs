using System.Security.Claims;
using System.Text.Json;
using Microsoft.JSInterop;
using WriterApp.Shared;

namespace WriterApp.Client.Components.Covers;

public partial class ProjectCoverStudio
{
    private readonly Dictionary<string,CoverAssetIdentity> _conceptAssets=new();
    private CoverEditCapabilities? _editCapabilities;
    private CoverEditPreview? _editPreview;
    private CoverEditSaveReceipt? _coverRecovery;
    private bool HasEditRecovery=>_coverRecovery?.State=="committed";
    private static string EditedImage(CoverEditPreview preview)=>"data:image/png;base64,"+Convert.ToBase64String(preview.Edit.Proposed.Bytes);
    private async Task<string> CoverScopeAsync() {
        var user=await Authentication.GetAsync();
        string? owner=user.UserId;
        if(!user.IsAuthenticated || string.IsNullOrWhiteSpace(owner))throw new InvalidOperationException("Sign in to reopen or edit owned cover concepts.");
        return CoverAssetContract.ReferenceHash(CoverApiClient.Backend+"|"+owner);
    }
    private static string PreviewKey(string scope,Guid project)=>"prosa.cover-edit.v1."+scope+"."+project.ToString("N");
    private async Task CacheEditAsync(CoverEditPreview preview,CancellationToken ct) {
        CoverEditPreviews.Validate(preview,await CoverScopeAsync(),Project!.Id);
        string json=JsonSerializer.Serialize(preview);
        if(json.Length>12*1024*1024)throw new InvalidDataException("Cover review cache exceeds the safe limit.");
        await JS.InvokeVoidAsync("localStorage.setItem",ct,PreviewKey(preview.Scope,Project.Id),json);
        ct.ThrowIfCancellationRequested();
    }
    protected override async Task OnAfterRenderAsync(bool firstRender) {if(firstRender && Project is not null) {await RefreshEditsAsync();StateHasChanged();}}
    private void CancelEdit()=>_request?.Cancel();
    private async Task DismissEditAsync() {
        if(_isGenerating || _isSavingCover)return;
        if(_editPreview?.SaveApproved==true && _editPreview.Receipt is null) {_coverError="Check the approved save before dismissing its recovery evidence.";return;}
        _editPreview=null;_generatedImageUrls.Clear();_selectedImageUrl=_projectCoverImageUrl;_saveStatus="Preview dismissed. The saved cover is unchanged; cached review and prior-cover recovery are retained for reopening.";
    }
    private async Task RefreshEditsAsync() {
        if(Project is null || _isGenerating || _isSavingCover)return;
        long epoch=_epoch;Guid project=Project.Id;string backend=CoverApiClient.Backend;
        using var ct=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);_request=ct;_isGenerating=true;_coverError=null;
        try {
            string scope=await CoverScopeAsync();
            string? json=await JS.InvokeAsync<string?>("localStorage.getItem",ct.Token,PreviewKey(scope,project));
            if(json is not null) {
                if(json.Length>12*1024*1024)throw new InvalidDataException("Cover review cache exceeds the safe limit.");
                var preview=JsonSerializer.Deserialize<CoverEditPreview>(json,new JsonSerializerOptions{MaxDepth=16}) ?? throw new InvalidDataException("Malformed cover review cache.");
                CoverEditPreviews.Validate(preview,scope,project);
                if(CurrentEditContext(epoch,project,backend) && scope==await CoverScopeAsync()) {InstallEdit(preview);if(preview.Receipt is { } receipt)_coverRecovery=receipt;_saveStatus="Cached original/proposed review reopened. Check an approved save before retrying after a lost acknowledgement.";}
            }
            var capabilities=await CoverApiClient.EditCapabilitiesAsync(ct.Token);
            if(capabilities.Version!=1 || capabilities.Operations is null || capabilities.Operations.Any(x=>!CoverEdits.Operations.Contains(x)))throw new InvalidDataException("Update the image-edit backend.");
            if(CurrentEditContext(epoch,project,backend))_editCapabilities=capabilities;
            var recovery=await CoverApiClient.EditCallAsync<CoverEditRecoveryResponse>(HttpMethod.Get,$"api/covers/edit-recovery/{project}",null,ct.Token,6*1024*1024);
            if(CurrentEditContext(epoch,project,backend) && recovery.Receipt is {Version:1} latest && latest.ProjectId==project)_coverRecovery=latest;
        } catch(Exception e) {if(CurrentEditContext(epoch,project,backend)) {_coverError=e.Message;_editCapabilities=null;}}
        finally {_isGenerating=false;if(_request==ct)_request=null;}
    }
    private bool CurrentEditContext(long epoch,Guid project,string backend)=>epoch==_epoch && Project?.Id==project && CoverApiClient.Backend==backend && !_lifetime.IsCancellationRequested;
    private void InstallEdit(CoverEditPreview preview) {
        _editPreview=preview;_generatedImageUrls.Clear();_generatedImageUrls.Add(preview.Original);_generatedImageUrls.Add(EditedImage(preview));
        _selectedImageUrl=_generatedImageUrls[preview.Selected];
    }
    private async Task EditAsync(string operation) {
        if(Project is null || _isGenerating || _isSavingCover || !_selectedImageAvailable)return;
        if(await RedirectToUpgradeIfLockedAsync())return;
        if(_editPreview is {SaveApproved:true,Receipt:null}) {_coverError="Reconcile the approved save before starting another edit.";return;}
        var project=Project.Id;var document=Project.PrimaryDocumentId ?? Project.DocumentId;var revision=_metadataRevision;
        if(document is null) {_coverError="Choose a saved manuscript first.";return;}
        long epoch=_epoch;string backend=CoverApiClient.Backend,selected=_selectedImageUrl!;
        using var ct=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);ct.CancelAfter(TimeSpan.FromMinutes(3));_request=ct;_isGenerating=true;_coverError=null;_saveStatus=null;
        try {
            string scope=await CoverScopeAsync();var capabilities=await CoverApiClient.EditCapabilitiesAsync(ct.Token);
            if(capabilities.Version!=1 || capabilities.Operations?.Contains(operation)!=true)throw new InvalidOperationException(capabilities.UnavailableReason ?? "This operation is unavailable.");
            _conceptAssets.TryGetValue(selected,out var asset);
            if(_editPreview is { } prior)asset=prior.Selected==0 ? prior.OriginalAsset : prior.Edit.Proposed.Asset;
            // Original review sources retain their own identity even after dismissing other generated concepts.
            Guid? assetId=asset?.AssetId;
            string reference=asset?.RemoteReference ?? selected;
            var source=await CoverApiClient.EditSourceAsync(new(project,document.Value,revision,assetId,CoverAssetContract.ReferenceHash(reference)),ct.Token);
            CoverAssetContract.Validate(source.Asset,source.Bytes,project);
            if(source.Source.ProjectId!=project || source.Source.DocumentId!=document || source.Source.MetadataRevision!=revision || source.Source.ReferenceHash!=CoverAssetContract.ReferenceHash(reference)
                || asset is not null && asset!=source.Asset || selected.StartsWith("data:",StringComparison.Ordinal) && !CoverStudioContract.ReadPng(selected).SequenceEqual(source.Bytes))throw new InvalidDataException("The owned edit source does not match the selected PNG.");
            var input=new CoverEditRequest(1,source.Source with{ReferenceHash=CoverAssetContract.ReferenceHash(source.Asset.RemoteReference)},source.Asset.AssetId,source.Asset.ContentHash,operation,BuildPrompt());
            var result=await CoverApiClient.EditAsync(input,ct.Token);ct.Token.ThrowIfCancellationRequested();
            if(!CurrentEditContext(epoch,project,backend) || scope!=await CoverScopeAsync() || revision!=_metadataRevision || selected!=_selectedImageUrl)return;
            var preview=new CoverEditPreview(1,scope,result,"data:image/png;base64,"+Convert.ToBase64String(source.Bytes),0,Guid.NewGuid(),OriginalAsset:source.Asset);
            await CacheEditAsync(preview,ct.Token);
            if(!CurrentEditContext(epoch,project,backend))return;InstallEdit(preview);_saveStatus="Review original and proposed. Select a concept, then explicitly save.";
        } catch(Exception e){if(CurrentEditContext(epoch,project,backend))_coverError=e.Message;}
        finally {_isGenerating=false;if(_request==ct)_request=null;}
    }
    private async Task SelectEditAsync(int selected) {
        if(_editPreview is not { } preview || _isGenerating || _isSavingCover || preview.SaveApproved || selected is <0 or >1)return;
        long epoch=_epoch;Guid project=Project!.Id;string backend=CoverApiClient.Backend;
        try {var next=preview with{Selected=selected};await CacheEditAsync(next,_lifetime.Token);if(!CurrentEditContext(epoch,project,backend) || next.Scope!=await CoverScopeAsync())return;InstallEdit(next);_saveStatus="Concept selected. The saved cover is unchanged.";}
        catch(Exception e){if(CurrentEditContext(epoch,project,backend))_coverError=e.Message;}
    }
    private async Task SaveEditAsync() {
        if(_editPreview is not { } preview || Project is null || _isGenerating || _isSavingCover)return;
        long epoch=_epoch;Guid project=Project.Id;string backend=CoverApiClient.Backend;
        using var ct=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);_request=ct;_isSavingCover=true;_coverError=null;
        try {
            if(preview.Edit.Request.Source.MetadataRevision!=_metadataRevision)throw new InvalidOperationException("The cover review is stale. Reopen before saving.");
            var approved=preview with{SaveApproved=true};await CacheEditAsync(approved,ct.Token);_editPreview=approved;
            var receipt=await CoverApiClient.SaveEditAsync(new(preview.OperationId,preview.Edit,preview.Selected==0 ? preview.Edit.Request.AssetId : preview.Edit.Proposed.Asset.AssetId),ct.Token);
            if(CurrentEditContext(epoch,project,backend))await AcceptReceiptAsync(approved,receipt,ct.Token);
        }catch(Exception e){if(CurrentEditContext(epoch,project,backend))_coverError=e.Message+" Check approved save to reconcile before retrying; recovery evidence is retained.";}
        finally{_isSavingCover=false;if(_request==ct)_request=null;}
    }
    private async Task AcceptReceiptAsync(CoverEditPreview preview,CoverEditSaveReceipt receipt,System.Threading.CancellationToken ct) {
        var next=preview with{Receipt=receipt};CoverEditPreviews.Validate(next,await CoverScopeAsync(),Project!.Id);
        if(receipt.State=="approved") {_saveStatus="Approval retained; project save is not confirmed. Retry Save after resolving connectivity/source.";_editPreview=next;return;}
        await CacheEditAsync(next,ct);_editPreview=next;_coverRecovery=receipt;_projectCoverImageUrl=receipt.CoverImageUrl;_metadataRevision=receipt.MetadataRevision;
        _saveStatus=receipt.State=="restored" ? "Previous project cover restored with confirmed persistence." : "Cover save confirmed. Previous cover is retained for recovery.";
    }
    private async Task ReconcileEditAsync()=>await ReceiptActionAsync(false);
    private async Task RestoreEditAsync() {
        if(_coverRecovery is not {State:"committed"} recovery || Project is null || _isGenerating || _isSavingCover)return;
        long epoch=_epoch;Guid project=Project.Id;string backend=CoverApiClient.Backend;
        using var ct=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);_request=ct;_isSavingCover=true;_coverError=null;
        try {
            var receipt=await CoverApiClient.RestoreEditAsync(recovery.OperationId,_metadataRevision,ct.Token);
            if(!CurrentEditContext(epoch,project,backend))return;
            if(receipt.Version!=1 || receipt.ProjectId!=project || receipt.OperationId!=recovery.OperationId || receipt.State!="restored")throw new InvalidDataException("Invalid original-cover recovery acknowledgement.");
            if(_editPreview is { } preview && preview.OperationId==receipt.OperationId)await AcceptReceiptAsync(preview,receipt,ct.Token);
            else {_coverRecovery=receipt;_projectCoverImageUrl=receipt.CoverImageUrl;_metadataRevision=receipt.MetadataRevision;_saveStatus="Previous project cover restored with confirmed persistence.";}
        }catch(Exception e){if(CurrentEditContext(epoch,project,backend))_coverError=e.Message;}
        finally{_isSavingCover=false;if(_request==ct)_request=null;}
    }
    private async Task ReceiptActionAsync(bool restore) {
        if(_editPreview is not {SaveApproved:true} preview || Project is null || _isGenerating || _isSavingCover)return;
        long epoch=_epoch;Guid project=Project.Id;string backend=CoverApiClient.Backend;
        using var ct=System.Threading.CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);_request=ct;_isSavingCover=true;_coverError=null;
        try {var receipt=restore ? await CoverApiClient.RestoreEditAsync(preview.OperationId,_metadataRevision,ct.Token) : await CoverApiClient.ReadEditSaveAsync(preview.OperationId,ct.Token);
            if(CurrentEditContext(epoch,project,backend))await AcceptReceiptAsync(preview,receipt,ct.Token);}
        catch(Exception e){if(CurrentEditContext(epoch,project,backend))_coverError=e.Message;}
        finally{_isSavingCover=false;if(_request==ct)_request=null;}
    }
}
