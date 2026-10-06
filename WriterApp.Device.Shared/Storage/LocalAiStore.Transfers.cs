using WriterApp.Shared;
using WriterApp.Controllers;

namespace WriterApp.Device.Shared.Storage;

public sealed record LocalPromptTransfer(int Version,string Scope,Guid Id,Guid LocalPromptId,long LocalRevision,DateTimeOffset CreatedAt,
    PromptTransferRequest Request,string Status="Pending",PromptTransferResponse? Result=null,PromptTransferConflict? Conflict=null);
public sealed record LocalPromptCloudCache(int Version,string Scope,DateTimeOffset RefreshedAt,IReadOnlyList<PromptPresetDto> Presets);

public sealed partial class LocalAiStore
{
    public Task<LocalPromptTransfer> QueuePresetCopyAsync(string scope,Guid localId,long revision,bool separate,CancellationToken ct=default)=>PresetWrite(async()=> {
        RequireScope(scope);var prompt=(await PresetsAsync(scope,ct)).SingleOrDefault(p=>p.Id==localId)??throw new InvalidOperationException("The local preset was deleted or belongs to another account.");
        if(prompt.Revision!=revision)throw new InvalidOperationException("The preset changed. Refresh before copying.");
        var transfers=await TransfersAsync(scope,ct);
        var pending=transfers.LastOrDefault(t=>t.LocalPromptId==localId&&t.Request.Action=="upsert"&&t.Status is "Pending" or "Conflict");if(pending is not null)return pending;
        var link=separate?null:transfers.LastOrDefault(t=>t.LocalPromptId==localId&&t.Status=="Completed"&&t.Request.Action=="upsert"&&!t.Result!.Deleted);
        var id=Guid.NewGuid();var t=new LocalPromptTransfer(1,scope,id,localId,revision,DateTimeOffset.UtcNow,new(1,id,link?.Request.PresetId??Guid.NewGuid(),"upsert",link?.Result?.SourceVersion,Definition(prompt)));
        ValidateTransfer(t);await Write(Path.Combine(TransferDir(scope),id.ToString("N")+".json"),t,ct);return t;
    },ct);
    public Task<LocalPromptTransfer> QueuePresetDeleteAsync(string scope,Guid cloudId,string version,CancellationToken ct=default)=>PresetWrite(async()=> {
        RequireScope(scope);var p=(await CachedPromptsAsync(scope,ct))?.Presets.SingleOrDefault(p=>p.Id==cloudId);
        if(p is null||p.Version!=version)throw new InvalidOperationException("Refresh the cloud preset before deleting.");
        var old=(await TransfersAsync(scope,ct)).LastOrDefault(t=>t.Request.PresetId==cloudId&&t.Request.Action=="delete"&&t.Status is "Pending" or "Conflict");if(old is not null)return old;
        var id=Guid.NewGuid();var t=new LocalPromptTransfer(1,scope,id,Guid.Empty,0,DateTimeOffset.UtcNow,new(1,id,cloudId,"delete",version,null));
        ValidateTransfer(t);await Write(Path.Combine(TransferDir(scope),id.ToString("N")+".json"),t,ct);return t;
    },ct);
    private string TransferDir(string scope){RequireScope(scope);return Path.Combine(root,"prompt-transfers",scope);}
    public async Task<IReadOnlyList<LocalPromptTransfer>> TransfersAsync(string scope,CancellationToken ct=default) {
        string dir=TransferDir(scope);if(!Directory.Exists(dir))return [];
        var result=new List<LocalPromptTransfer>();foreach(var path in Directory.EnumerateFiles(dir,"*.json")) {
            var t=await Read<LocalPromptTransfer>(path,ct);ValidateTransfer(t);
            if(t.Scope!=scope||Path.GetFileNameWithoutExtension(path)!=t.Id.ToString("N"))throw new InvalidDataException("Prompt transfer scope/identity mismatch.");result.Add(t);
        }return result.OrderBy(t=>t.CreatedAt).ToArray();
    }
    private static void ValidateTransfer(LocalPromptTransfer t) {
        RequireScope(t.Scope);
        if(t.Version!=1||t.Id==Guid.Empty||t.Request is null||t.Request.OperationId!=t.Id||t.Request.Version!=1||t.Request.PresetId==Guid.Empty
            ||t.Request.Action is not ("upsert" or "delete")||t.Request.ExpectedVersion?.Length>200||t.LocalRevision<0
            ||t.Status is not ("Pending" or "Conflict" or "Completed" or "Canceled"))throw new InvalidDataException("Invalid prompt transfer intent.");
        if(t.Request.Action=="upsert")ReusablePrompts.ValidateEnvelope(t.Request.Preset??throw new InvalidDataException("Missing transferred preset."));
        else if(t.Request.Preset is not null||string.IsNullOrWhiteSpace(t.Request.ExpectedVersion))throw new InvalidDataException("Delete requires a confirmed cloud version.");
        if(t.Status=="Completed" && (t.Result is null||t.Result.Version!=1||t.Result.OperationId!=t.Id||t.Result.PresetId!=t.Request.PresetId||t.Result.Deleted!=(t.Request.Action=="delete")))throw new InvalidDataException("Invalid prompt transfer receipt.");
        if(t.Status=="Completed"&&!t.Result!.Deleted&&(t.Result.Preset is null||string.IsNullOrWhiteSpace(t.Result.SourceVersion)||t.Result.Preset.Id!=t.Request.PresetId||t.Result.Preset.Version!=t.Result.SourceVersion
            ||ReusablePrompts.Canonical(WriterApp.Device.Shared.Services.DevicePromptLibrary.Definition(t.Result.Preset))!=ReusablePrompts.Canonical(t.Request.Preset)))throw new InvalidDataException("The receipt must retain the exact authored preset.");
        if(t.Status=="Conflict"&&(t.Conflict is null||t.Conflict.PresetId!=t.Request.PresetId||t.Conflict.Code!="preset.conflict"))throw new InvalidDataException("Invalid prompt conflict.");
    }
    public Task<LocalPromptTransfer> SaveTransferAsync(LocalPromptTransfer t,CancellationToken ct=default)=>PresetWrite(async()=> {
        ValidateTransfer(t);string path=Path.Combine(TransferDir(t.Scope),t.Id.ToString("N")+".json");
        if(File.Exists(path)) {
            var old=await Read<LocalPromptTransfer>(path,ct);ValidateTransfer(old);
            if(old.Scope!=t.Scope||old.Id!=t.Id||old.LocalPromptId!=t.LocalPromptId||old.LocalRevision!=t.LocalRevision||old.CreatedAt!=t.CreatedAt
                ||ReusablePrompts.Canonical(old.Request)!=ReusablePrompts.Canonical(t.Request)
                ||old.Result is not null&&ReusablePrompts.Canonical(old.Result)!=ReusablePrompts.Canonical(t.Result)
                ||old.Status is "Completed" or "Canceled" && old.Status!=t.Status)
                throw new InvalidDataException("Authored prompt transfer intent cannot be replaced.");
        }
        await Write(path,t,ct);return t;
    },ct);
    public async Task<LocalPromptCloudCache?> CachedPromptsAsync(string scope,CancellationToken ct=default) {
        RequireScope(scope);string path=Path.Combine(root,"prompt-cache",scope+".json");if(!File.Exists(path))return null;
        var cache=await Read<LocalPromptCloudCache>(path,ct);ValidateCache(cache);if(cache.Scope!=scope)throw new InvalidDataException("Prompt cache account mismatch.");return cache;
    }
    private static void ValidateCache(LocalPromptCloudCache cache) {
        RequireScope(cache.Scope);if(cache.Version!=1||cache.Presets is null||cache.Presets.Count>500||cache.Presets.Select(p=>p.Id).Distinct().Count()!=cache.Presets.Count)throw new InvalidDataException("Invalid prompt cloud cache.");
        foreach(var p in cache.Presets){if(p.Id==Guid.Empty||string.IsNullOrWhiteSpace(p.Version)||p.Version.Length>200)throw new InvalidDataException("Cloud presets need revision tokens. Update the backend.");ReusablePrompts.ValidateEnvelope(WriterApp.Device.Shared.Services.DevicePromptLibrary.Definition(p));}
    }
    public Task<LocalPromptCloudCache> SavePromptCacheAsync(LocalPromptCloudCache cache,CancellationToken ct=default)=>PresetWrite(async()=>{ValidateCache(cache);await Write(Path.Combine(root,"prompt-cache",cache.Scope+".json"),cache,ct);return cache;},ct);
}
