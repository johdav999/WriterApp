using System.Net;
using System.Net.Http.Json;
using WriterApp.Controllers;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public sealed partial class DevicePromptLibrary
{
    public string? Scope=>account.IsSignedIn&&account.AccountId is { } id&&http.BaseAddress is { } uri?LocalBibleStore.ScopeKey(uri,id):null;
    private string RequiredScope()=>Scope??throw new DeviceAiException(DeviceAiFailure.Authentication,"Sign in to transfer presets. Local authored presets remain available offline.");
    private void CheckScope(string scope,long generation,CancellationToken ct){Check(generation,ct);if(scope!=Scope)throw new InvalidOperationException("The account or backend changed. Reopen the current prompt library.");}
    public static PromptDefinition Definition(PromptPresetDto p)=>ReusablePrompts.FromCloud(p);
    public async Task<IReadOnlyList<PromptPresetDto>> RefreshPresetsAsync(CancellationToken ct) {
        string scope=RequiredScope();long generation=RequireAccount();using var message=TransferMessage(HttpMethod.Get,"api/ai/presets/transfer-library",generation);using var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,ct);await TransferError(response,ct);
        var values=await ReadTransferJson<PromptPresetDto[]>(response,ct,16*1024*1024)??throw new InvalidDataException("Empty cloud preset response.");
        CheckScope(scope,generation,ct);var cache=new LocalPromptCloudCache(1,scope,DateTimeOffset.UtcNow,values);await store.SavePromptCacheAsync(cache,ct);CheckScope(scope,generation,ct);return values;
    }
    public async Task<LocalAiPrompt> ImportPresetAsync(Guid id,CancellationToken ct) {
        string scope=RequiredScope();long generation=RequireAccount();var p=(await RefreshPresetsAsync(ct)).SingleOrDefault(p=>p.Id==id)??throw new InvalidOperationException("This cloud preset was deleted. Refresh the library.");
        CheckScope(scope,generation,ct);var local=await store.ImportPresetAsync(Definition(p),scope,p.Id,p.Version!,ct);CheckScope(scope,generation,ct);return local;
    }
    public async Task<LocalPromptTransfer> QueueCopyAsync(LocalAiPrompt prompt,bool separate,CancellationToken ct) {
        string scope=RequiredScope();long generation=RequireAccount();CheckScope(scope,generation,ct);
        var t=await store.QueuePresetCopyAsync(scope,prompt.Id,prompt.Revision,separate,ct);CheckScope(scope,generation,ct);return t;
    }
    public async Task<LocalPromptTransfer> QueueDeleteAsync(PromptPresetDto preset,CancellationToken ct) {
        string scope=RequiredScope();long generation=RequireAccount();CheckScope(scope,generation,ct);
        var t=await store.QueuePresetDeleteAsync(scope,preset.Id,preset.Version??"",ct);CheckScope(scope,generation,ct);return t;
    }
    public async Task<LocalPromptTransfer> SendAsync(Guid id,CancellationToken ct) {
        string scope=RequiredScope();long generation=RequireAccount();var transfer=(await store.TransfersAsync(scope,ct)).Single(t=>t.Id==id);
        if(transfer.Status!="Pending")throw new InvalidOperationException("Resolve the conflict or keep the completed transfer before sending again.");
        if(transfer.LocalPromptId!=Guid.Empty && !(await store.PresetsAsync(scope,ct)).Any(p=>p.Id==transfer.LocalPromptId))
            throw new InvalidOperationException("The local source was deleted. Cancel this transfer and refresh the cloud library; local deletion does not delete cloud copies.");
        CheckScope(scope,generation,ct);using var message=TransferMessage(HttpMethod.Post,"api/ai/presets/transfer",generation);message.Content=JsonContent.Create(transfer.Request);
        using var response=await http.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,ct);CheckScope(scope,generation,ct);
        if(response.StatusCode==HttpStatusCode.Conflict) {
            var conflict=await ReadTransferJson<PromptTransferConflict>(response,ct);
            if(conflict is null||conflict.Code!="preset.conflict"||conflict.PresetId!=transfer.Request.PresetId)throw new InvalidDataException("The transfer operation cannot be replayed with this content. Original intent is retained.");
            if(conflict.Current is { } current){ReusablePrompts.ValidateEnvelope(Definition(current));if(current.Id!=transfer.Request.PresetId||string.IsNullOrWhiteSpace(current.Version))throw new InvalidDataException("Invalid cloud conflict identity.");}
            CheckScope(scope,generation,ct);return await store.SaveTransferAsync(transfer with{Status="Conflict",Conflict=conflict},ct);
        }
        await TransferError(response,ct);var result=await ReadTransferJson<PromptTransferResponse>(response,ct)??throw new InvalidDataException("Missing transfer receipt.");
        if(result.Version!=1||result.OperationId!=id||result.PresetId!=transfer.Request.PresetId||result.Deleted!=(transfer.Request.Action=="delete")
            ||!result.Deleted&&(string.IsNullOrWhiteSpace(result.SourceVersion)||result.Preset is null||result.Preset.Id!=result.PresetId||result.Preset.Version!=result.SourceVersion
                ||ReusablePrompts.Canonical(Definition(result.Preset))!=ReusablePrompts.Canonical(transfer.Request.Preset)))
            throw new InvalidDataException("The backend did not confirm this exact authored preset. Original transfer intent is retained.");
        CheckScope(scope,generation,ct);return await store.SaveTransferAsync(transfer with{Status="Completed",Result=result},ct);
    }
    public async Task CancelTransferAsync(Guid id,CancellationToken ct) {
        string scope=RequiredScope();long generation=RequireAccount();var t=(await store.TransfersAsync(scope,ct)).Single(t=>t.Id==id);CheckScope(scope,generation,ct);
        if(t.Status is "Pending" or "Conflict")await store.SaveTransferAsync(t with{Status="Canceled"},ct);
    }
    public async Task<LocalPromptTransfer> KeepBothAsync(Guid id,CancellationToken ct) {
        string scope=RequiredScope();long generation=RequireAccount();var t=(await store.TransfersAsync(scope,ct)).Single(t=>t.Id==id);
        if(t.Status!="Conflict"||t.Request.Preset is null)throw new InvalidOperationException("Only an authored upload conflict can be copied separately.");
        // Deterministic fork identity makes interrupted conflict resolution itself restart-safe.
        Guid fork=new(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope+"/fork/"+id)).AsSpan(0,16));
        var request=t.Request with{OperationId=fork,PresetId=fork,ExpectedVersion=null};
        var existing=(await store.TransfersAsync(scope,ct)).SingleOrDefault(x=>x.Id==fork);
        CheckScope(scope,generation,ct);var copy=existing??await store.SaveTransferAsync(new(1,scope,fork,t.LocalPromptId,t.LocalRevision,t.CreatedAt,request),ct);
        await store.SaveTransferAsync(t with{Status="Canceled"},ct);return copy;
    }
    public async Task KeepCloudAsync(Guid id,bool import,CancellationToken ct) {
        string scope=RequiredScope();long generation=RequireAccount();var t=(await store.TransfersAsync(scope,ct)).Single(t=>t.Id==id);CheckScope(scope,generation,ct);
        if(t.Status!="Conflict")throw new InvalidOperationException("Refresh this conflict first.");
        if(import&&t.Conflict?.Current is { } current)await ImportPresetAsync(current.Id,ct);
        CheckScope(scope,generation,ct);await CancelTransferAsync(id,ct);
    }
    private static async Task<T?> ReadTransferJson<T>(HttpResponseMessage response,CancellationToken ct,int limit=128*1024) {
        if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("The cloud preset response is too large. Original data is retained.");
        await using var source=await response.Content.ReadAsStreamAsync(ct);using var bytes=new MemoryStream();var buffer=new byte[8192];int read;
        while((read=await source.ReadAsync(buffer,ct))!=0){if(bytes.Length+read>limit)throw new InvalidDataException("The cloud preset response is too large. Original data is retained.");await bytes.WriteAsync(buffer.AsMemory(0,read),ct);}
        bytes.Position=0;return await System.Text.Json.JsonSerializer.DeserializeAsync<T>(bytes,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web){MaxDepth=24,UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow},ct);
    }
    private static HttpRequestMessage TransferMessage(HttpMethod method,string route,long generation){var message=new HttpRequestMessage(method,route);message.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration,generation);return message;}
    private static async Task TransferError(HttpResponseMessage response,CancellationToken ct) {
        ct.ThrowIfCancellationRequested();if(response.IsSuccessStatusCode)return;
        if(response.StatusCode==HttpStatusCode.NotFound)throw new InvalidOperationException("Update the backend for safe versioned preset transfers, or refresh if the preset/project was removed. The queued copy is retained.");
        if(response.StatusCode==HttpStatusCode.Unauthorized)throw new DeviceSignInRequiredException();
        if(response.StatusCode==HttpStatusCode.BadRequest)throw new InvalidOperationException("Cloud preset data or this request does not meet the safe transfer contract. Originals are retained. Repair unsupported values or update the backend, then refresh; cancel a rejected queued intent before copying a repaired version.");
        throw new HttpRequestException("Cloud preset transfer is unavailable. The original and queued intent are retained; refresh or retry later.",null,response.StatusCode);
    }
}
