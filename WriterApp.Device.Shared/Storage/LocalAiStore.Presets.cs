using WriterApp.Shared;

namespace WriterApp.Device.Shared.Storage;

public sealed partial class LocalAiStore
{
    private readonly SemaphoreSlim _presets = new(1,1);
    public static PromptDefinition Definition(LocalAiPrompt p)=>new(p.Name,p.Category,p.Kind,p.BuiltinActionId,
        p.Kind=="custom"?p.Instruction:null,p.Parameters??new(),p.Scope,p.Pinned,p.ProjectId);
    private string PresetPath(Guid id)=>Path.Combine(root,"prompts",id.ToString("N")+".json");
    public async Task<IReadOnlyList<LocalAiPrompt>> PresetsAsync(string? scope,CancellationToken ct=default) {
        string dir=Path.Combine(root,"prompts");if(!Directory.Exists(dir))return [];
        var result=new List<LocalAiPrompt>();foreach(var path in Directory.EnumerateFiles(dir,"*.json")) {
            var p=await Read<LocalAiPrompt>(path,ct);Validate(p);
            if(Path.GetFileNameWithoutExtension(path)!=p.Id.ToString("N"))throw new InvalidDataException("Preset file identity mismatch.");
            if(p.DeletedAt is null && (p.OriginScope is null || scope is not null && p.OriginScope==scope))result.Add(p);
        }return result.OrderByDescending(p=>p.Pinned).ThenBy(p=>p.Category).ThenBy(p=>p.Name).ToArray();
    }
    private async Task<FileStream> PresetLock(CancellationToken ct) {
        Directory.CreateDirectory(root);var start=DateTimeOffset.UtcNow;
        while(true){ct.ThrowIfCancellationRequested();try{return new FileStream(Path.Combine(root,".presets.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException)when(DateTimeOffset.UtcNow-start<TimeSpan.FromSeconds(5)){await Task.Delay(25,ct);}}
    }
    private async Task<T> PresetWrite<T>(Func<Task<T>> action,CancellationToken ct) {
        await _presets.WaitAsync(ct);try{await using var file=await PresetLock(ct);return await action();}finally{_presets.Release();}
    }
    public Task<LocalAiPrompt> SavePresetAsync(PromptDefinition value,Guid? id,long? expectedRevision,string? scope,CancellationToken ct=default)=>PresetWrite(async()=> {
        ReusablePrompts.ValidateEnvelope(value);ReusablePrompts.ValidateForRun(value,requireTokens:false);
        LocalAiPrompt? old=null;if(id is { } existing) {
            old=await Read<LocalAiPrompt>(PresetPath(existing),ct);Validate(old);
            if(old.DeletedAt is not null||old.Revision!=expectedRevision||old.OriginScope is not null&&old.OriginScope!=scope)
                throw new InvalidOperationException("This preset changed or belongs to another account. Refresh before saving; your draft is retained.");
        }
        if(value.Pinned&&! (old?.Pinned??false)&&(await PresetsAsync(scope,ct)).Count(p=>p.Pinned)>=3)throw new InvalidOperationException("You can pin up to 3 presets. Unpin one first.");
        var now=DateTimeOffset.UtcNow;var p=new LocalAiPrompt(2,id??Guid.NewGuid(),value.Name.Trim(),value.Template??"",old?.CreatedAt??now,
            value.Category,value.Kind,value.ActionKey,ReusablePrompts.Parse(ReusablePrompts.Serialize(value)).Parameters,value.Scope,value.Pinned,(old?.Revision??0)+1,now,
            OriginScope:old?.OriginScope,OriginCloudId:old?.OriginCloudId,OriginVersion:old?.OriginVersion,ProjectId:value.ProjectId);
        Validate(p);await Write(PresetPath(p.Id),p,ct);return p;
    },ct);
    public Task<LocalAiPrompt> PinPresetAsync(Guid id,long expected,string? scope,CancellationToken ct=default)=>PresetWrite(async()=> {
        var p=await Read<LocalAiPrompt>(PresetPath(id),ct);Validate(p);
        if(p.DeletedAt is not null||p.Revision!=expected||p.OriginScope is not null&&p.OriginScope!=scope)throw new InvalidOperationException("Preset changed. Refresh before pinning.");
        if(!p.Pinned&&(await PresetsAsync(scope,ct)).Count(x=>x.Pinned)>=3)throw new InvalidOperationException("You can pin up to 3 presets.");
        p=p with{Version=2,Pinned=!p.Pinned,Revision=p.Revision+1,UpdatedAt=DateTimeOffset.UtcNow};Validate(p);await Write(PresetPath(id),p,ct);return p;
    },ct);
    public Task<bool> DeletePresetAsync(Guid id,long expected,string? scope,CancellationToken ct=default)=>PresetWrite(async()=> {
        var p=await Read<LocalAiPrompt>(PresetPath(id),ct);Validate(p);
        if(p.Revision!=expected||p.OriginScope is not null&&p.OriginScope!=scope)throw new InvalidOperationException("Preset changed. Refresh before deleting.");
        if(p.DeletedAt is not null)return true;
        p=p with{Version=2,DeletedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow,Revision=p.Revision+1,Pinned=false};Validate(p);await Write(PresetPath(id),p,ct);return true;
    },ct);
    public Task<LocalAiPrompt> ImportPresetAsync(PromptDefinition value,string scope,Guid cloudId,string cloudVersion,CancellationToken ct=default)=>PresetWrite(async()=> {
        ReusablePrompts.ValidateEnvelope(value);RequireScope(scope);
        // One immutable cloud revision has one local copy, even if the import confirmation was lost.
        var hash=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(scope+"/"+cloudId+"/"+cloudVersion));var id=new Guid(hash.AsSpan(0,16));
        if(File.Exists(PresetPath(id))){var existing=await Read<LocalAiPrompt>(PresetPath(id),ct);Validate(existing);if(existing.OriginScope!=scope||existing.OriginCloudId!=cloudId||existing.OriginVersion!=cloudVersion)throw new InvalidDataException("Preset import identity mismatch.");if(existing.DeletedAt is not null)throw new InvalidOperationException("This cloud revision was deleted locally. Create a new local preset or import a changed cloud version; a retry cannot revive the deleted copy.");return existing;}
        if(value.Pinned&&(await PresetsAsync(scope,ct)).Count(p=>p.Pinned)>=3)throw new InvalidOperationException("Unpin a preset before importing this pinned cloud preset. Its pin is retained.");
        var now=DateTimeOffset.UtcNow;bool pin=value.Pinned;
        var p=new LocalAiPrompt(2,id,value.Name,value.Template??"",now,value.Category,value.Kind,value.ActionKey,ReusablePrompts.Parse(ReusablePrompts.Serialize(value)).Parameters,value.Scope,pin,1,now,
            OriginScope:scope,OriginCloudId:cloudId,OriginVersion:cloudVersion,ProjectId:value.ProjectId);Validate(p);await Write(PresetPath(id),p,ct);return p;
    },ct);
    private static void RequireScope(string scope){if(scope.Length!=64||scope.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Invalid prompt account scope.");}
}
