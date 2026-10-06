using WriterApp.Device.Shared.Storage;
using System.Net.Http.Json;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public sealed class DeviceOnboarding(LocalOnboardingStore store, ILocalDocumentStore documents,
    DeviceAccountService account, DeviceHostOptions host, HttpClient? http = null, DeviceSyncEngine? sync = null,
    DeviceConnectivity? network = null)
{
    public string Scope => LocalBibleStore.ScopeKey(host.ApiBaseAddress, account.IsSignedIn && !string.IsNullOrWhiteSpace(account.AccountId) ? account.AccountId : "device-guest");
    public Task<LocalOnboardingProgress> ReadAsync(CancellationToken ct = default) => store.ReadAsync(Scope, ct);
    private void RequireOnline(LocalOnboardingProgress source,long generation) {
        RequireScope(source);
        if(!account.IsSignedIn || account.Generation!=generation)throw new InvalidOperationException("Sign in to the original account and reopen the guide.");
        if(network?.IsOnline==false || http is null)throw new InvalidOperationException("Connect to refresh the server demo. Local guidance and writing remain available offline.");
    }
    private async Task<T> DemoResponse<T>(HttpResponseMessage response,CancellationToken ct) {
        using(response) {
            await response.Content.LoadIntoBufferAsync(16_384,ct);
            if(!response.IsSuccessStatusCode) {
                string message="The server demo was not acknowledged. Refresh or retry the same saved choice; local writing is preserved.";
                try { var json=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken:ct);
                    if(json.TryGetProperty("message",out var text))message=text.GetString() ?? message; } catch(System.Text.Json.JsonException) { }
                throw new InvalidOperationException(message);
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken:ct) ?? throw new InvalidDataException("Missing server demo response.");
        }
    }
    private Task<LocalOnboardingProgress> RememberDemo(LocalOnboardingProgress source,OnboardingDemoStatus status,CancellationToken ct) {
        if(status.Version!=1 || status.Revision is not {Length:64} || status.Reason is not {Length:<=200} || status.ActionKey!=OnboardingAiDemoRequest.ActionKey || status.Scope!="section"
            || status.Workspace is { } ids && (ids.ProjectId==Guid.Empty || ids.DocumentId==Guid.Empty || ids.SectionId==Guid.Empty || ids.SceneNodeId==Guid.Empty))
            throw new InvalidDataException("The backend returned an unsupported demo identity/policy. Update the backend.");
        if(source.Demo?.HasCompletedOnboarding==true)status=status with {HasCompletedOnboarding=true,Available=false,OnboardingStep=Math.Max(status.OnboardingStep,source.Demo.OnboardingStep)};
        return store.UpdateAsync(source,value => value with {Demo=status},ct);
    }
    public async Task<LocalOnboardingProgress> RefreshDemoAsync(LocalOnboardingProgress source,CancellationToken ct=default) {
        long generation=account.Generation;RequireOnline(source,generation);
        var status=await DemoResponse<OnboardingDemoStatus>(await http!.GetAsync("api/onboarding/demo/status",ct),ct);
        RequireOnline(source,generation);return await RememberDemo(source,status,ct);
    }
    public async Task<(LocalOnboardingProgress Progress,LocalDocument Document)> OpenServerDemoAsync(LocalOnboardingProgress source,CancellationToken ct=default) {
        long generation=account.Generation;RequireOnline(source,generation);
        if(source.DemoBootstrapId is null)source=await store.UpdateAsync(source,value => value with {DemoBootstrapId=Guid.NewGuid()},ct);
        RequireOnline(source,generation);
        var status=await DemoResponse<OnboardingDemoStatus>(await http!.PostAsJsonAsync("api/onboarding/demo/bootstrap",new OnboardingDemoBootstrapRequest(1,source.DemoBootstrapId!.Value,"Other"),ct),ct);
        RequireOnline(source,generation);
        source=await RememberDemo(source,status,ct);
        if(status.Workspace is not { } workspace)throw new InvalidOperationException("The server did not return a demo workspace.");
        if(source.DemoDocumentId is { } retained) {
            var previous=await documents.GetAsync(retained,ct);
            if(previous is null || previous.DeletedAtUtc is not null)throw new InvalidOperationException("The linked demo is missing or in Trash. Restore it; this guide will not recreate it.");
        }
        if(sync is null)throw new InvalidOperationException("The checked demo sync adapter is unavailable.");
        await sync.SyncAsync(ct);RequireOnline(source,generation);
        if(sync.LastError is not null)throw new InvalidOperationException(sync.LastError);
        var local=(await documents.ListAsync(LocalDocumentScope.All,ct)).Documents
            .SingleOrDefault(d => d.ServerDocumentId==workspace.DocumentId && sync.Status(d.DocumentId) is not null && (source.DemoDocumentId is null || source.DemoDocumentId==d.DocumentId));
        if(local is null || local.DeletedAtUtc is not null || sync.Status(local.DocumentId) is {Conflict:true} or {Deleted:true})
            throw new InvalidOperationException("The demo needs sync conflict/recovery review. Existing writing was preserved.");
        RequireOnline(source,generation);
        source=await store.UpdateAsync(source,value => value with {DemoDocumentId=local.DocumentId},ct);
        source=await ReconcileProgressAsync(source,false,ct);
        return(source,local);
    }
    public async Task<LocalOnboardingProgress> ReconcileProgressAsync(LocalOnboardingProgress source,bool complete,CancellationToken ct=default) {
        long generation=account.Generation;RequireOnline(source,generation);
        if(source.Demo is null)source=await RefreshDemoAsync(source,ct);
        if(source.PendingProgress is null)source=await store.UpdateAsync(source,value => value with {PendingProgress=new(1,Guid.NewGuid(),value.Demo!.Revision,complete ? 10 : value.Demo!.Workspace is null ? 0 : 2,complete)},ct);
        RequireOnline(source,generation);
        using var response=await http!.PostAsJsonAsync("api/onboarding/demo/progress",source.PendingProgress,ct);
        await response.Content.LoadIntoBufferAsync(16_384,ct);
        if(response.StatusCode==System.Net.HttpStatusCode.Conflict) {
            var error=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(cancellationToken:ct);
            if(error.TryGetProperty("code",out var code) && code.GetString()=="onboarding.progress_changed") {
                RequireOnline(source,generation);
                source=await store.UpdateAsync(source,value => value with {PendingProgress=null},ct);
                return await RefreshDemoAsync(source,ct);
            }
        }
        var status=await DemoResponse<OnboardingDemoStatus>(response,ct);
        RequireOnline(source,generation);
        if(status.Version!=1)throw new InvalidDataException("Unsupported onboarding progress acknowledgement.");
        source=await RefreshDemoAsync(source,ct);
        source=await store.UpdateAsync(source,value => value with {PendingProgress=null},ct);
        return source; // An older idempotent receipt cannot regress newer web completion.
    }
    public Task<LocalOnboardingProgress> ChangeAsync(LocalOnboardingProgress source, int step, string status, CancellationToken ct = default) {
        RequireScope(source);
        return store.UpdateAsync(source, value => value with { Step = step, Status = status }, ct);
    }
    public async Task<LocalOnboardingProgress> ContinuePracticeAsync(LocalOnboardingProgress source, Guid documentId, CancellationToken ct = default) {
        RequireScope(source);
        var document = await documents.GetAsync(documentId, ct);
        if (document is null || document.DeletedAtUtc is not null || document.ExtensionData is null
            || !document.ExtensionData.TryGetValue("desktopAiPracticeGuide", out var marker) || marker.ValueKind != System.Text.Json.JsonValueKind.String
            || !Guid.TryParse(marker.GetString(), out var id) || id == Guid.Empty)
            throw new InvalidOperationException("Choose the labelled practice project. Existing manuscripts cannot be used as seeded tutorial content.");
        RequireScope(source);
        return await store.UpdateAsync(source, value => value with { PracticeDocumentId = documentId, PracticeCreated = true, Status = "Active", Step = 1 }, ct);
    }
    public async Task<(LocalOnboardingProgress Progress, LocalDocument Document)> OpenPracticeAsync(LocalOnboardingProgress source, CancellationToken ct = default) {
        RequireScope(source);
        var generation = account.Generation;
        if (source.PracticeDocumentId is null)
            source = await store.UpdateAsync(source, value => value with { PracticeDocumentId = Guid.NewGuid() }, ct);
        RequireScope(source);
        if (account.Generation != generation) throw new InvalidOperationException("Account changed. Reopen the practice guide.");
        // Reserve a stable ID durably before creation. Retry after a crash opens the same document.
        var document = source.PracticeCreated
            ? await documents.GetAsync(source.PracticeDocumentId!.Value, ct) ?? throw new InvalidOperationException("The practice project is missing. Restore it from your backup; the guide will not seed it again.")
            : await documents.CreateOnboardingPracticeAsync(source.PracticeDocumentId!.Value, source.Id, ct);
        if (document.DeletedAtUtc is not null) throw new InvalidOperationException("The practice project is in Trash. Restore it from the library; the guide will not overwrite it.");
        RequireScope(source);
        if (account.Generation != generation) throw new InvalidOperationException("Account changed. Reopen the practice guide.");
        if (!source.PracticeCreated) source = await store.UpdateAsync(source, value => value with { PracticeCreated = true }, ct);
        return (source, document);
    }
    private void RequireScope(LocalOnboardingProgress source) {
        if (source.Scope != Scope) throw new InvalidOperationException("Account or backend changed. Reopen the practice guide.");
    }
}
