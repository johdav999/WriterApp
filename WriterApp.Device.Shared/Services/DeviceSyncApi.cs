using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Application.Security;
using WriterApp.Shared.Sync;

namespace WriterApp.Device.Shared.Services;

public sealed class DeviceSyncApiException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public bool IsTransient => Status is 408 or 429 or >= 500;
}

public interface IDeviceSyncApi
{
    Task<string> GetOwnerAsync(CancellationToken ct);
    Task<SyncChanges> ChangesAsync(string? cursor, CancellationToken ct);
    Task<SyncSnapshot> DownloadAsync(Guid id, CancellationToken ct);
    Task<SyncMutationResult> MutateAsync(Guid id, SyncMutation request, CancellationToken ct);
}

public sealed class DeviceSyncApi(HttpClient client, WriterApp.Device.Shared.Storage.LocalOnboardingStore? onboarding = null,
    DeviceHostOptions? host = null) : IDeviceSyncApi
{
    private const string Root = "api/sync/v4/documents";
    private Guid? _demoDocument;
    private string ActiveRoot => _demoDocument is null ? Root : "api/onboarding/demo/documents";
    public async Task<string> GetOwnerAsync(CancellationToken ct)
    {
        var profile = await ReadAsync<AuthMeDto>(await client.GetAsync("api/auth/me", ct), ct);
        if (!profile.IsAuthenticated || string.IsNullOrWhiteSpace(profile.UserId)) throw new DeviceSignInRequiredException();
        _demoDocument=null;
        if (!profile.IsPaidAccessActive) {
            var choice=onboarding is null || host is null ? null
                : await onboarding.ReadAsync(WriterApp.Device.Shared.Storage.LocalBibleStore.ScopeKey(host.ApiBaseAddress,profile.UserId),ct);
            if(choice?.DemoBootstrapId is null)
                throw new DeviceSyncApiException(403,"demo_choice_required","Choose Create or reopen online demo in the guide before synchronizing its writing. Local writing is preserved.");
            WriterApp.Shared.OnboardingDemoStatus? status=null;
            try { status=await ReadAsync<WriterApp.Shared.OnboardingDemoStatus>(await client.GetAsync("api/onboarding/demo/status",ct),ct); }
            catch(DeviceSyncApiException) { }
            if(status is not { Version:1,Workspace: { } demo })
                throw new DeviceSyncApiException(403, "entitlement_required", "An active paid plan or an explicitly created server demo is required. Local writing is preserved.");
            _demoDocument=demo.DocumentId;
        }
        return profile.UserId;
    }
    public async Task<SyncChanges> ChangesAsync(string? cursor, CancellationToken ct) =>
        await ReadAsync<SyncChanges>(await client.GetAsync(ActiveRoot + "/changes?limit=100" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), ct), ct);
    public async Task<SyncSnapshot> DownloadAsync(Guid id, CancellationToken ct) =>
        await ReadAsync<SyncSnapshot>(await client.GetAsync($"{Route(id)}/{id}", ct), ct);
    public async Task<SyncMutationResult> MutateAsync(Guid id, SyncMutation request, CancellationToken ct) =>
        await ReadAsync<SyncMutationResult>(await client.PostAsJsonAsync($"{Route(id)}/{id}/operations", request, ct), ct);
    private string Route(Guid id) {
        if(_demoDocument is { } demo && id!=demo)throw new DeviceSyncApiException(403,"entitlement_required","Only the server-owned demo may synchronize without a paid plan.");
        return ActiveRoot;
    }
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                SyncError? error = null;
                try { error = await response.Content.ReadFromJsonAsync<SyncError>(cancellationToken: ct); }
                catch (JsonException) { }
                if ((int)response.StatusCode == 404 && error is null)
                    throw new DeviceSyncApiException(426, "project_sync_backend_required", "This backend does not support multi-document sync v4. Local changes and queued operations are preserved until the backend is upgraded.");
                throw new DeviceSyncApiException((int)response.StatusCode, error?.Code ?? "http_error", error?.Message ?? "The sync request was rejected.");
            }
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct) ?? throw new JsonException("Empty synchronization response.");
        }
    }
}

// The Windows host updates this from MAUI connectivity events. Other hosts can supply their adapter later.
public sealed class DeviceConnectivity
{
    public bool IsOnline { get; private set; } = true;
    public event Action? Changed;
    public void SetOnline(bool value) { if (IsOnline == value) return; IsOnline = value; Changed?.Invoke(); }
}
