using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public sealed partial class DeviceCoverStudio(HttpClient http, DeviceAccountService account, DeviceConnectivity network,
    DeviceHostOptions host, LocalDocumentRepository documents, LocalCoverStudioStore store)
{
    private sealed class AccountLease : IDisposable {
        private readonly DeviceAccountService account; private readonly CancellationTokenSource cancellation; private readonly long generation;
        public AccountLease(DeviceAccountService account, CancellationToken ct) { this.account=account; generation=account.Generation; cancellation=CancellationTokenSource.CreateLinkedTokenSource(ct); account.Changed+=Cancel; }
        private void Cancel() { if(account.Generation!=generation) cancellation.Cancel(); }
        public CancellationToken Token => cancellation.Token;
        public void Dispose() { account.Changed-=Cancel; cancellation.Dispose(); }
    }
    public string? Scope => account.IsSignedIn && account.AccountId is { } owner ? LocalBibleStore.ScopeKey(host.ApiBaseAddress,owner) : null;
    public bool CanGenerate => Scope is not null && network.IsOnline;
    private void Guard(string scope, long generation) { if(scope != Scope || generation != account.Generation) throw new DeviceSignInRequiredException(); }
    private static bool Matches(LocalDocument doc, LocalCoverDraft draft) => doc.DocumentId==draft.DocumentId && doc.Project?.ProjectId==draft.ProjectId
        && doc.ServerDocumentId==draft.CloudDocumentId && doc.Project.ServerProjectId==draft.CloudProjectId && doc.LocalRevision==draft.SourceRevision
        && doc.Project.MetadataRevision==draft.MetadataRevision && doc.ServerVersion==draft.DocumentVersion && doc.DeletedAtUtc is null && doc.SyncState!=LocalSyncState.Conflict;
    private async Task<LocalDocument> Latest(LocalDocument source, CancellationToken ct) {
        var current=await documents.LoadAsync(source.DocumentId,ct) ?? throw new IOException("Document unavailable.");
        if(current.LocalRevision!=source.LocalRevision || current.Project?.ProjectId!=source.Project?.ProjectId
            || current.Project?.MetadataRevision!=source.Project?.MetadataRevision || current.ServerDocumentId!=source.ServerDocumentId
            || current.Project?.ServerProjectId!=source.Project?.ServerProjectId || current.ServerVersion!=source.ServerVersion || current.DeletedAtUtc is not null || current.SyncState==LocalSyncState.Conflict)
            throw new InvalidOperationException("The project changed. Reload before using the cover studio.");
        return current;
    }
    public async Task<LocalCoverDraft?> CachedAsync(LocalDocument source, CancellationToken ct=default) {
        var scope=Scope; long generation=account.Generation; if(scope is null) return null;
        var draft=await store.ReadAsync(scope,source,ct); Guard(scope,generation); return draft;
    }
    public bool IsCurrent(LocalDocument source, LocalCoverDraft draft) => draft.Scope==Scope && Matches(source,draft);
    public async Task<LocalCoverDraft> GenerateAsync(LocalDocument source, CoverPrompt brief, CancellationToken ct=default) {
        using var lease=new AccountLease(account,ct); ct=lease.Token;
        string scope=Scope ?? throw new DeviceSignInRequiredException(); long generation=account.Generation;
        if(!network.IsOnline) throw new IOException("Connect to generate covers. Cached covers remain available offline.");
        var current=await Latest(source,ct); var p=current.Project;
        if(p?.ServerProjectId is null || current.ServerDocumentId is null || current.SyncState!=LocalSyncState.Synced || p.MetadataDirty
            || p.ServerMetadataRevision is null || string.IsNullOrWhiteSpace(current.ServerVersion)) throw new InvalidOperationException("Synchronize this project and saved document before generating covers.");
        var prompt=new CoverPrompt { ContractVersion=1, ProjectId=p.ServerProjectId, DocumentId=current.ServerDocumentId,
            ExpectedMetadataRevision=p.ServerMetadataRevision, ExpectedDocumentVersion=current.ServerVersion,
            Description=brief.Description, Genre=brief.Genre, Mood=brief.Mood, Style=brief.Style, ColorPalette=brief.ColorPalette };
        CoverStudioContract.ValidatePrompt(prompt); Guard(scope,generation);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var request=new HttpRequestMessage(HttpMethod.Post,"api/covers/generate") { Content=JsonContent.Create(prompt) };
        request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration,generation);
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token); Guard(scope,generation);
        if(!response.IsSuccessStatusCode) throw new IOException((int)response.StatusCode switch {
            401=>"Sign in again to generate covers.",402 or 403=>"Cover generation requires the Professional plan and available AI quota.",
            409=>"The cloud project changed. Synchronize before generating again.",429=>"The image quota or rate limit was reached. Try again later.",
            404 or 426=>"Update the backend to support revision-checked cover generation.",422=>"The image service returned unsupported or invalid media. Choose a local PNG or retry.",
            _=>"Cover generation is unavailable. The previous cover and cached concepts are preserved." });
        const int limit=16*1024*1024;
        if(response.Content.Headers.ContentLength>limit) throw new InvalidDataException("Cover response exceeds the safe limit.");
        await using var stream=await response.Content.ReadAsStreamAsync(timeout.Token); using var buffer=new MemoryStream(); byte[] block=new byte[16384];
        int read; while((read=await stream.ReadAsync(block,timeout.Token))>0) { if(buffer.Length+read>limit) throw new InvalidDataException("Cover response exceeds the safe limit."); await buffer.WriteAsync(block.AsMemory(0,read),timeout.Token); }
        var result=JsonSerializer.Deserialize<CoverGenerationResponse>(buffer.ToArray(),new JsonSerializerOptions(JsonSerializerDefaults.Web){MaxDepth=16}) ?? throw new InvalidDataException("Empty cover response.");
        if(result.ContractVersion!=1 || result.ProjectId!=prompt.ProjectId || result.DocumentId!=prompt.DocumentId
            || result.MetadataRevision!=prompt.ExpectedMetadataRevision || result.DocumentVersion!=prompt.ExpectedDocumentVersion)
            throw new InvalidDataException("The backend did not confirm the cover source. Update it and generate again.");
        var draft=new LocalCoverDraft(2,Guid.NewGuid(),scope,current.DocumentId,p.ProjectId,current.ServerDocumentId.Value,p.ServerProjectId.Value,
            current.LocalRevision,p.MetadataRevision,current.ServerVersion,p.ServerMetadataRevision.Value,DateTimeOffset.UtcNow,result.ImageUrls,0,result.Assets);
        LocalCoverStudioStore.Validate(draft); Guard(scope,generation); await Latest(source,timeout.Token);
        Guard(scope,generation); await store.WriteAsync(draft,timeout.Token); Guard(scope,generation); return draft;
    }
    public async Task<LocalCoverDraft> SelectAsync(LocalDocument source, LocalCoverDraft draft, int index, CancellationToken ct=default) {
        using var lease=new AccountLease(account,ct); ct=lease.Token;
        long generation=account.Generation; Guard(draft.Scope,generation); await Latest(source,ct);
        var next=draft with { Selected=index }; LocalCoverStudioStore.Validate(next); Guard(draft.Scope,generation); await store.WriteAsync(next,ct); Guard(draft.Scope,generation); return next;
    }
    public async Task<LocalDocument> SaveAsync(LocalDocument source, LocalCoverDraft draft, CancellationToken ct=default) {
        using var lease=new AccountLease(account,ct); ct=lease.Token;
        long generation=account.Generation; Guard(draft.Scope,generation); LocalCoverStudioStore.Validate(draft);
        var current=await documents.LoadAsync(source.DocumentId,ct) ?? throw new IOException("Document unavailable.");
        Guard(draft.Scope,generation);
        if(current.Project?.CoverChangeId==draft.Id && current.Project.CoverImageUrl==draft.Images[draft.Selected]) return current;
        if(!Matches(current,draft) || !IsCurrent(source,draft)) throw new InvalidOperationException("This preview is stale. Generate again; the saved cover is preserved.");
        if(draft.Edit is { } edit && network.IsOnline) {
            var resolved=await CoverCall<CoverAssetResponse>(HttpMethod.Post,"api/covers/edit-source",new CoverEditSourceRequest(edit.Request.Source.ProjectId,
                edit.Request.Source.DocumentId,edit.Request.Source.MetadataRevision,edit.Request.AssetId,edit.Request.Source.ReferenceHash),generation,ct);
            if(resolved.Source!=edit.Request.Source || resolved.Asset.ContentHash!=edit.Request.ContentHash)throw new InvalidOperationException("The cover edit source changed. Reopen before saving.");
            Guard(draft.Scope,generation);
            var proposed=edit.Proposed.Asset;
            var retained=await CoverCall<CoverAssetResponse>(HttpMethod.Post,"api/covers/edit-source",new CoverEditSourceRequest(edit.Request.Source.ProjectId,
                edit.Request.Source.DocumentId,edit.Request.Source.MetadataRevision,proposed.AssetId,CoverAssetContract.ReferenceHash(proposed.RemoteReference)),generation,ct);
            if(retained.Asset!=proposed || retained.Source.DocumentVersion!=edit.Request.Source.DocumentVersion)throw new InvalidOperationException("The proposed owned cover asset changed. Reopen before saving.");
            Guard(draft.Scope,generation);
        }
        return await documents.SetProjectCoverAsync(current,draft.Images[draft.Selected],draft.Id,ct:ct);
    }
}
