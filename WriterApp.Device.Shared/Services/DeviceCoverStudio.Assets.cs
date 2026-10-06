using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public sealed partial class DeviceCoverStudio
{
    public async Task<LocalCover?> CachedProjectCoverAsync(LocalDocument source,CancellationToken ct=default) {
        string? scope=Scope;long generation=account.Generation;if(scope is null)return null;
        var cached=await store.ReadAssetAsync(scope,source,ct);Guard(scope,generation);
        return cached is null ? null : new(cached.Asset.AssetId,source.DocumentId,source.Project!.ProjectId,"owned-project-cover.png",cached.Bytes,
            cached.Asset.RemoteReference,cached.Asset.ContentHash,scope);
    }
    public async Task<LocalCoverDraft> MaterializeProjectCoverAsync(LocalDocument source,CancellationToken ct=default) {
        using var lease=new AccountLease(account,ct);ct=lease.Token;
        string scope=Scope ?? throw new DeviceSignInRequiredException();long generation=account.Generation;
        if(!network.IsOnline)throw new IOException("Connect to cache the owned remote cover. Existing cached covers remain available offline.");
        var current=await Latest(source,ct);var project=current.Project;
        if(project?.ServerProjectId is not { } cloud || current.ServerDocumentId is not { } document || current.SyncState!=LocalSyncState.Synced
            || project.MetadataDirty || project.ServerMetadataRevision is not { } revision || current.ServerVersion is not { } version
            || project.CoverImageUrl is not { } reference || !reference.StartsWith("https://",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Synchronize the saved owned remote project cover before caching it.");
        var input=new CoverAssetSource(1,cloud,document,revision,version,CoverAssetContract.ReferenceHash(reference));
        CoverAssetContract.ValidateSource(input);Guard(scope,generation);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(TimeSpan.FromSeconds(40));
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/covers/assets/materialize"){Content=JsonContent.Create(input)};
        request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration,generation);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);Guard(scope,generation);
        if(!response.IsSuccessStatusCode)throw new IOException((int)response.StatusCode switch {
            401=>"Sign in again to cache the owned cover.",404 or 426=>"The owned cover or asset backend is unavailable. Restore the source or update the backend; existing cache is preserved.",
            409=>"The cloud cover or source changed. Synchronize and reopen before caching it.",410=>"The owned remote cover expired or was deleted. Keep the cached cover, regenerate or choose a local PNG.",
            422=>"This remote cover is unsupported or invalid. Only PNG from configured trusted provider storage can be cached; choose a local PNG.",
            _=>"Owned cover download was not acknowledged. Retry the same saved reference; previous cover/cache is preserved."});
        const int limit=3*1024*1024;
        if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("Owned cover response exceeds the safe limit.");
        await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var buffer=new MemoryStream();byte[] block=new byte[16384];int read;
        while((read=await stream.ReadAsync(block,timeout.Token))>0){if(buffer.Length+read>limit)throw new InvalidDataException("Owned cover response exceeds the safe limit.");await buffer.WriteAsync(block.AsMemory(0,read),timeout.Token);}
        var result=JsonSerializer.Deserialize<CoverAssetResponse>(buffer.ToArray(),new JsonSerializerOptions(JsonSerializerDefaults.Web){MaxDepth=16})
            ?? throw new InvalidDataException("Empty owned cover response.");
        if(result.Source!=input || result.Asset is null || result.Bytes is null || result.Asset.RemoteReference!=reference)throw new InvalidDataException("Owned cover response does not match its saved source.");
        CoverAssetContract.Validate(result.Asset,result.Bytes,cloud);
        string image="data:image/png;base64,"+Convert.ToBase64String(result.Bytes);
        var draft=new LocalCoverDraft(2,result.Asset.AssetId,scope,current.DocumentId,project.ProjectId,document,cloud,
            current.LocalRevision,project.MetadataRevision,version,revision,DateTimeOffset.UtcNow,[image],0,[result.Asset]);
        LocalCoverStudioStore.Validate(draft);Guard(scope,generation);await Latest(source,timeout.Token);
        await store.WriteAssetAsync(new(1,scope,project.ProjectId,cloud,result.Asset,result.Bytes),timeout.Token);Guard(scope,generation);
        await Latest(source,timeout.Token);await store.WriteAsync(draft,timeout.Token);Guard(scope,generation);return draft;
    }
}
