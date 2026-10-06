using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public sealed partial class DeviceCoverStudio
{
    private async Task<T> CoverCall<T>(HttpMethod method,string path,object? input,long generation,CancellationToken ct,int limit=4*1024*1024) {
        using var request=new HttpRequestMessage(method,path) {Content=input is null ? null : JsonContent.Create(input)};
        request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration,generation);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        if(!response.IsSuccessStatusCode)throw new IOException((int)response.StatusCode switch {
            401=>"Sign in again to edit covers.",402 or 403=>"Cover edits require image access and available AI quota.",429=>"Image quota or rate limit was reached.",
            409=>"The cover source changed. Synchronize and reopen before editing or saving.",404 or 426=>"The owned source or image-edit backend is unavailable. Regenerate or update the backend.",
            422=>"This operation or image result is unsupported or invalid. The previous cover is preserved.",_=>"Cover editing is unavailable. The previous cover and cached concepts are preserved."});
        if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Cover edit response exceeds the safe limit.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);using var buffer=new MemoryStream();byte[] block=new byte[16384];int read;
        while((read=await stream.ReadAsync(block,ct))>0) {if(buffer.Length+read>limit)throw new InvalidDataException("Cover edit response exceeds the safe limit.");await buffer.WriteAsync(block.AsMemory(0,read),ct);}
        ct.ThrowIfCancellationRequested();
        return JsonSerializer.Deserialize<T>(buffer.ToArray(),new JsonSerializerOptions(JsonSerializerDefaults.Web){MaxDepth=16}) ?? throw new InvalidDataException("Empty cover edit response.");
    }
    public async Task<CoverEditCapabilities> EditCapabilitiesAsync(CancellationToken ct=default) {
        using var lease=new AccountLease(account,ct); string scope=Scope ?? throw new DeviceSignInRequiredException();long generation=account.Generation;
        if(!network.IsOnline)throw new IOException("Connect to check image-edit availability.");
        var result=await CoverCall<CoverEditCapabilities>(HttpMethod.Get,"api/covers/edit-capabilities",null,generation,lease.Token,16_384);Guard(scope,generation);
        if(result.Version!=1 || result.Operations is null || result.Operations.Any(x=>!CoverEdits.Operations.Contains(x)))throw new InvalidDataException("Update the image-edit backend.");
        return result;
    }
    public async Task<LocalCoverDraft> EditAsync(LocalDocument source,LocalCoverDraft? draft,string operation,CoverPrompt brief,CancellationToken ct=default) {
        using var lease=new AccountLease(account,ct);ct=lease.Token;
        string scope=Scope ?? throw new DeviceSignInRequiredException();long generation=account.Generation;
        if(!network.IsOnline)throw new IOException("Connect to edit covers. Cached original/proposed concepts remain available offline.");
        var current=await Latest(source,ct);var project=current.Project;
        if(project?.ServerProjectId is not { } cloud || current.ServerDocumentId is not { } document || current.SyncState!=LocalSyncState.Synced
            || project.MetadataDirty || project.ServerMetadataRevision is not { } revision || current.ServerVersion is not { } version)
            throw new InvalidOperationException("Synchronize this project and document before editing covers.");
        if(draft is not null && !IsCurrent(current,draft))throw new InvalidOperationException("This concept is stale. Reopen or generate again.");
        var capabilities=await EditCapabilitiesAsync(ct);if(!capabilities.Operations.Contains(operation))throw new InvalidOperationException(capabilities.UnavailableReason ?? "This cover operation is unavailable.");
        string? image=draft?.Images[draft.Selected] ?? project.CoverImageUrl;
        var identity=draft?.Assets?[draft.Selected];
        if(image is null)throw new InvalidOperationException("Select an owned concept or saved project cover first.");
        if(draft is not null && identity is null)throw new InvalidOperationException("This older concept lacks an owned asset identity. Regenerate it before editing.");
        string reference=identity?.RemoteReference ?? image;
        var resolved=await CoverCall<CoverAssetResponse>(HttpMethod.Post,"api/covers/edit-source",new CoverEditSourceRequest(cloud,document,revision,identity?.AssetId,CoverAssetContract.ReferenceHash(reference)),generation,ct);
        Guard(scope,generation);CoverAssetContract.Validate(resolved.Asset,resolved.Bytes,cloud);
        if(resolved.Source!=new CoverAssetSource(1,cloud,document,revision,version,CoverAssetContract.ReferenceHash(reference))
            || identity is not null && resolved.Asset!=identity || image.StartsWith("data:",StringComparison.Ordinal) && !CoverStudioContract.ReadPng(image).SequenceEqual(resolved.Bytes))
            throw new InvalidDataException("The owned image edit source does not match the selected PNG.");
        var input=new CoverEditRequest(1,resolved.Source with{ReferenceHash=CoverAssetContract.ReferenceHash(resolved.Asset.RemoteReference)},resolved.Asset.AssetId,resolved.Asset.ContentHash,operation,brief);
        CoverEdits.Validate(input);await Latest(source,ct);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromMinutes(3));
        var result=await CoverCall<CoverEditResponse>(HttpMethod.Post,"api/covers/edit",input,generation,timeout.Token);
        Guard(scope,generation);CoverEdits.ValidateResponse(input,result);await Latest(source,timeout.Token);
        var next=new LocalCoverDraft(2,Guid.NewGuid(),scope,current.DocumentId,project.ProjectId,document,cloud,current.LocalRevision,project.MetadataRevision,
            version,revision,DateTimeOffset.UtcNow,["data:image/png;base64,"+Convert.ToBase64String(resolved.Bytes),"data:image/png;base64,"+Convert.ToBase64String(result.Proposed.Bytes)],0,[resolved.Asset,result.Proposed.Asset],result);
        LocalCoverStudioStore.Validate(next);ct.ThrowIfCancellationRequested();await store.WriteAsync(next,ct);Guard(scope,generation);return next;
    }
}
