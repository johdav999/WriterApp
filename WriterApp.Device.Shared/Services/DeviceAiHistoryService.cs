using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.UI.Shared;

namespace WriterApp.Device.Shared.Services;

/// <summary>Cloud inspection and delivery of durable local outcomes. Never applies or undoes cloud content.</summary>
public sealed class DeviceAiHistoryService : IDisposable
{
    private readonly HttpClient http;
    private readonly DeviceAccountService account;
    private readonly DeviceConnectivity connectivity;
    private readonly DeviceHostOptions host;
    private readonly LocalAiStore store;
    private readonly SemaphoreSlim _delivery = new(1,1);
    private readonly CancellationTokenSource _lifetime = new();
    private string? _errorScope;
    private string? _deliveryError;
    public string? DeliveryError => _errorScope == Scope ? _deliveryError : null;
    public DeviceAiHistoryService(HttpClient http, DeviceAccountService account, DeviceConnectivity connectivity,
        DeviceHostOptions host, LocalAiStore store, bool automaticDelivery = true)
    {
        this.http=http; this.account=account; this.connectivity=connectivity; this.host=host; this.store=store;
        if (automaticDelivery) store.HistoryChanged += HistorySaved;
    }
    private void HistorySaved(Guid document) { if (CanConnect && !_lifetime.IsCancellationRequested) _ = DeliverSaved(document); }
    private async Task DeliverSaved(Guid document)
    {
        string? scope=Scope;
        try { await DeliverAsync(document,_lifetime.Token); if(scope==Scope) {_errorScope=scope; _deliveryError=null;} }
        catch (Exception e) when (e is IOException or InvalidDataException or InvalidOperationException or HttpRequestException or JsonException or DeviceSignInRequiredException or OperationCanceledException)
        { if(scope==Scope) {_errorScope=scope; _deliveryError="Cloud reporting remains pending. " + e.Message.Split('\n')[0];} }
    }
    public void Dispose() { store.HistoryChanged -= HistorySaved; _lifetime.Cancel(); }
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { MaxDepth = 32, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public string? Scope => account.IsSignedIn && !string.IsNullOrWhiteSpace(account.AccountId) ? LocalBibleStore.ScopeKey(host.ApiBaseAddress, account.AccountId) : null;
    public bool CanConnect => Scope is not null && connectivity.IsOnline;
    private void RequireScope(string scope, long generation)
    {
        if (scope != Scope || generation != account.Generation) throw new DeviceSignInRequiredException();
    }
    private (string Scope, long Generation) Capture()
    {
        if (!connectivity.IsOnline) throw new IOException("Connect to load or report cloud history. Local history and saved writing remain available.");
        return (Scope ?? throw new DeviceSignInRequiredException(), account.Generation);
    }
    private async Task<T> Send<T>(HttpRequestMessage request, string scope, long generation, int limit, CancellationToken ct)
    {
        RequireScope(scope, generation); request.Options.Set(DeviceAuthenticatedHandler.ExpectedAccountGeneration, generation);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(60));
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        RequireScope(scope, generation);
        if (!response.IsSuccessStatusCode)
        {
            string message = (int)response.StatusCode switch
            {
                401 => "Sign in again to load or deliver cloud history.",
                403 => "Cloud history is unavailable for this account or plan. Local history remains available.",
                404 => "The proposal is unavailable or this backend needs the versioned history routes. Local recovery and pending events are retained.",
                409 => "Cloud history rejected this reporting sequence or identity. Pending events are retained; inspect history before retrying.",
                _ => "Cloud history could not be updated. Retry the retained events; saved local changes remain intact."
            };
            throw new IOException(message);
        }
        if (response.Content.Headers.ContentLength > limit) throw new InvalidDataException("Cloud history exceeds the safe response size. The previous cache and pending events are retained.");
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token); using var buffer = new MemoryStream(); byte[] block = new byte[16384];
        int read; while ((read = await stream.ReadAsync(block, timeout.Token)) > 0)
        { if (buffer.Length + read > limit) throw new InvalidDataException("Cloud history exceeds the safe response size."); await buffer.WriteAsync(block.AsMemory(0, read), timeout.Token); }
        RequireScope(scope, generation);
        return JsonSerializer.Deserialize<T>(buffer.ToArray(), Json) ?? throw new InvalidDataException("Cloud history response is empty.");
    }
    public async Task<LocalCloudHistoryCache?> CachedAsync(LocalDocument source, CancellationToken ct = default)
    {
        var scope = Scope; long generation = account.Generation;
        if (scope is null || source.ServerDocumentId is not { } cloud) return null;
        var cache = await store.CachedHistoryAsync(scope, source.DocumentId, cloud, ct); RequireScope(scope, generation); return cache;
    }
    public async Task<LocalCloudHistoryCache> RefreshAsync(LocalDocument source, CancellationToken ct = default)
    {
        var (scope, generation) = Capture(); var cloud = source.ServerDocumentId ?? throw new InvalidOperationException("Enable document cloud sync before loading its cloud history.");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/ai/actions/history/device?documentId={cloud}");
        var snapshot = await Send<DeviceCloudHistory>(request, scope, generation, 16 * 1024 * 1024, ct);
        DeviceAiHistoryContracts.Validate(snapshot, cloud); RequireScope(scope, generation);
        var cache = new LocalCloudHistoryCache(1, scope, source.DocumentId, snapshot); await store.SaveCloudHistoryAsync(cache, ct);
        RequireScope(scope, generation); return cache;
    }
    public async Task<int> DeliverAsync(Guid document, CancellationToken ct = default)
    {
        var (scope, generation) = Capture();
        await _delivery.WaitAsync(ct);
        try {
        RequireScope(scope,generation); int delivered = 0;
        foreach (var entry in (await store.HistoryAsync(document, ct)).Where(e => e.CloudOrigin?.Scope == scope).OrderBy(e => e.CreatedAt))
        {
            foreach (var evt in entry.Deliveries ?? [])
            {
                RequireScope(scope, generation); var report = LocalAiStore.HistoryReport(entry, evt);
                if (await store.HistoryReportedAsync(scope, report, ct)) continue;
                using var request = new HttpRequestMessage(HttpMethod.Post, "api/ai/actions/history/device/events") { Content = JsonContent.Create(report, options: Json) };
                var receipt = await Send<DeviceAiHistoryReceipt>(request, scope, generation, 65536, ct);
                RequireScope(scope, generation); await store.SaveHistoryReceiptAsync(scope, report, receipt, ct); RequireScope(scope, generation);
                if (++delivered == 200) return delivered;
            }
        }
        return delivered;
        } finally { _delivery.Release(); }
    }
    public async Task<int> PendingAsync(Guid document, CancellationToken ct = default)
    {
        var scope = Scope; long generation = account.Generation; if (scope is null) return 0;
        int pending = 0;
        foreach (var entry in (await store.HistoryAsync(document, ct)).Where(e => e.CloudOrigin?.Scope == scope))
            foreach (var evt in entry.Deliveries ?? []) if (!await store.HistoryReportedAsync(scope, LocalAiStore.HistoryReport(entry, evt), ct)) pending++;
        RequireScope(scope, generation); return pending;
    }
    public IReadOnlyList<AiHistoryItem> Combine(LocalDocument current, IReadOnlyList<AiHistoryItem> localItems,
        IReadOnlyList<LocalAiHistory> local, LocalCloudHistoryCache? cache, IReadOnlySet<Guid>? dismissed = null)
    {
        string? scope = Scope;
        var visible = local.Where(e => e.CloudOrigin is null || e.CloudOrigin.Scope == scope).ToArray();
        var items = localItems.Where(e => visible.Any(l => l.Id == e.Id)).Select(e => e with { Origin = "Local",
            Freshness = "Durable local recovery", CanRecover = true }).ToList();
        if (cache is null || cache.Scope != scope || cache.LocalDocumentId != current.DocumentId || cache.Snapshot.DocumentId != current.ServerDocumentId)
            return items.Where(e => dismissed?.Contains(e.Id) != true).OrderByDescending(e=>e.CreatedAt).Take(DeviceAiHistoryContracts.MaxEntries).ToArray();
        foreach (var cloud in cache.Snapshot.Entries)
        {
            string freshness = $"Cloud checked {cache.Snapshot.CheckedAt.ToLocalTime():g} · " + (cloud.SourceVersion == current.ServerVersion ? "matches last synchronized source" : "different or unknown source revision");
            if (current.SyncState != LocalSyncState.Synced) freshness += " · local changes are not yet synchronized";
            var matches = visible.Where(e => e.CloudOrigin?.ProposalId == cloud.ProposalId && e.CloudOrigin.DocumentId == cloud.DocumentId
                && e.CloudOrigin.SectionId == cloud.SectionId && e.CloudOrigin.PageId == cloud.PageId
                && e.CloudOrigin.ActionKey == cloud.ActionKey && e.CloudOrigin.SourceVersion == cloud.SourceVersion).ToArray();
            if (matches.Length > 0)
            {
                foreach (var match in matches)
                {
                    int index = items.FindIndex(e => e.Id == match.Id); if (index >= 0) items[index] = items[index] with { Origin = "Local + cloud", Freshness = freshness + $" · cloud {cloud.State.ToLowerInvariant()}" };
                }
                continue;
            }
            // No local identity/snapshots: comparison only. Never feed remote text into local undo or recovery.
            items.Add(new(cloud.ProposalId, cloud.ActionKey, cloud.Target ?? "Cloud proposal", cloud.State, cloud.CreatedAt, cloud.Original, cloud.Proposed,
                UnavailableReason: "Cloud-only entry: exact local target snapshots are unavailable. Inspect the comparison; undo and recovery require a retained local record.",
                Origin: "Cloud", Freshness: freshness, CanRecover: false));
        }
        return items.Where(e => dismissed?.Contains(e.Id) != true).OrderByDescending(e => e.CreatedAt).Take(DeviceAiHistoryContracts.MaxEntries).ToArray();
    }
}
