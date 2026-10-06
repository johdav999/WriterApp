using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Canon;

namespace WriterApp.Device.Shared.Services;

public sealed class StoryReferenceUnavailableException(string message, int status) : InvalidOperationException(message)
{
    public int Status { get; } = status;
    public bool CanContinue => Status is 402 or 403 or 404 or 426 or 422 or >= 500;
}

public sealed class DeviceBibleApi(HttpClient http)
{
    public async Task<DeviceBibleSnapshot> ReadAsync(Guid id, CanonKind kind, string version, CancellationToken ct) =>
        await ReadResponse(await http.GetAsync($"api/documents/{id}/bibles/{kind.ToString().ToLowerInvariant()}/device?expectedDocumentVersion={Uri.EscapeDataString(version)}", ct), ct);
    public async Task<DeviceBibleSnapshot> RefreshAsync(Guid id, CanonKind kind, DeviceBibleRefreshRequest request, CancellationToken ct) =>
        await ReadResponse(await http.PostAsJsonAsync($"api/documents/{id}/bibles/{kind.ToString().ToLowerInvariant()}/device/refresh", request, ct), ct);
    private static async Task<DeviceBibleSnapshot> ReadResponse(HttpResponseMessage response, CancellationToken ct)
    {
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new StoryReferenceUnavailableException((int)response.StatusCode switch
                {
                    401 => "Sign in again to load canon.", 402 or 403 => "Your plan or account cannot access story canon.",
                    404 or 426 => "The document or revision-checked canon API is unavailable. Synchronize or update the backend.",
                    409 => "The manuscript or canon changed. Synchronize and load canon before retrying.",
                    422 => "Canon returned invalid structured data. Previous cached canon is preserved.",
                    _ => "Canon is temporarily unavailable. Previous cached canon is preserved."
                }, (int)response.StatusCode);
            // Bound the response before JSON deserialization; never cache arbitrary provider payloads.
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var bytes = new MemoryStream(); var buffer = new byte[8192]; int count;
            while ((count = await stream.ReadAsync(buffer, ct)) > 0)
            {
                if (bytes.Length + count > 8_000_000) throw new InvalidDataException("Canon response exceeds the supported size.");
                bytes.Write(buffer, 0, count);
            }
            return JsonSerializer.Deserialize<DeviceBibleSnapshot>(bytes.ToArray(), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidDataException("Empty canon response.");
        }
    }
}

public sealed class DeviceBibleService(DeviceBibleApi api, LocalBibleStore store, LocalDocumentRepository documents,
    DeviceAccountService account, DeviceConnectivity connectivity, DeviceHostOptions host)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string Scope() => account.IsSignedIn && !string.IsNullOrWhiteSpace(account.AccountId)
        ? LocalBibleStore.ScopeKey(host.ApiBaseAddress, account.AccountId)
        : throw new DeviceSignInRequiredException();
    public bool CanView => account.IsSignedIn && !string.IsNullOrWhiteSpace(account.AccountId);
    public string CurrentScope => Scope();
    public async Task<DeviceCanonContext> PrepareConsistencyAsync(LocalDocument source, Guid section,
        Func<CanonKind, Task>? progress, CancellationToken ct)
    {
        // Reuse matching references. Preparing an unchanged draft must not repeat AI extraction.
        var context = await ContextAsync(source, ct);
        foreach (var kind in Enum.GetValues<CanonKind>())
        {
            if (context.Snapshots.ContainsKey(kind)) continue;
            if (progress is not null) await progress(kind);
            try { await LoadAsync(source, kind, true, false, section, ct); }
            catch (StoryReferenceUnavailableException e) when (e.CanContinue) { /* The report discloses omitted references. */ }
            catch (HttpRequestException) { /* Other available references and original writing remain usable. */ }
        }
        LocalConsistencyRevisions.RequireUnchanged(await documents.LoadAsync(source.DocumentId, ct)
            ?? throw new IOException("Manuscript unavailable."), source);
        return await ContextAsync(source, ct);
    }
    public Task<LocalBibleCache?> CachedAsync(LocalDocument document, CanonKind kind, CancellationToken ct = default) =>
        store.ReadAsync(Scope(), document, kind, ct);
    public async Task<LocalBibleCache> LoadAsync(LocalDocument source, CanonKind kind, bool refresh, bool rebuild, Guid? sectionId, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            string scope = Scope(); long generation = account.Generation;
            void Check() { ct.ThrowIfCancellationRequested(); if (Scope() != scope || account.Generation != generation) throw new DeviceSignInRequiredException(); }
            if (!connectivity.IsOnline) throw new InvalidOperationException("Connect to refresh canon. Cached canon remains available.");
            if (source.DeletedAtUtc is not null || source.ServerDocumentId is null || source.ServerVersion is null || source.SyncState != LocalSyncState.Synced)
                throw new InvalidOperationException("Save and synchronize this manuscript before loading or refreshing canon.");
            var previous = await store.ReadAsync(scope, source, kind, ct);
            var pending = new LocalBibleCache(1, scope, source.DocumentId, source.ServerDocumentId.Value, kind,
                previous?.Snapshot, previous?.CheckedAt ?? DateTimeOffset.UtcNow, "Refreshing");
            Check(); await store.SaveAsync(pending, ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(90));
            try
            {
                // Read first on every attempt. Interrupted refresh is reconciled instead of replaying a stale create/update.
                var response = await api.ReadAsync(source.ServerDocumentId.Value, kind, source.ServerVersion, timeout.Token);
                ValidateResponse(response, source, kind); Check();
                if (refresh)
                {
                    Guid? serverSection = sectionId is { } localSection
                        ? source.Sections.Single(s => s.SectionId == localSection).ServerSectionId ?? localSection : null;
                    response = await api.RefreshAsync(source.ServerDocumentId.Value, kind,
                        new(source.ServerVersion, response.SnapshotVersion, rebuild || !response.Exists, serverSection), timeout.Token);
                    ValidateResponse(response, source, kind); Check();
                    if (response.SourceDocumentVersion != source.ServerVersion)
                        throw new InvalidDataException("Refresh did not confirm the analyzed manuscript revision. Previous cache is preserved.");
                }
                var latest = await documents.LoadAsync(source.DocumentId, timeout.Token)
                    ?? throw new InvalidOperationException("Manuscript unavailable.");
                LocalConsistencyRevisions.RequireUnchanged(latest, source); Check();
                var saved = pending with { Snapshot = response, CheckedAt = DateTimeOffset.UtcNow, RefreshState = "Ready" };
                await store.SaveAsync(saved, timeout.Token); Check(); return saved;
            }
            catch
            {
                // Keep the previous snapshot even when remote work completed before cancellation.
                await store.SaveAsync(pending with { RefreshState = "Interrupted" }, CancellationToken.None);
                throw;
            }
        }
        finally { _gate.Release(); }
    }
    private static void ValidateResponse(DeviceBibleSnapshot result, LocalDocument document, CanonKind kind)
    {
        if (result.ContractVersion != 1 || result.DocumentId != document.ServerDocumentId || result.Kind != kind
            || result.CheckedDocumentVersion != document.ServerVersion || string.IsNullOrWhiteSpace(result.SnapshotVersion)
            || (result.SourceDocumentVersion is not null && result.SourceDocumentVersion != document.ServerVersion))
            throw new InvalidDataException("Canon response has the wrong identity or revision. Update the backend or run again.");
        _ = CanonContent.Parse(kind, result.ContentJson);
    }
    // Consistency uses only source-matched snapshots from the current account/backend scope.
    public async Task<DeviceCanonContext> ContextAsync(LocalDocument document, CancellationToken ct = default)
    {
        var scope = Scope(); var generation = account.Generation;
        var snapshots = new Dictionary<CanonKind, DeviceBibleSnapshot>();
        foreach (var kind in Enum.GetValues<CanonKind>())
            if ((await store.ReadAsync(scope, document, kind, ct)) is { RefreshState: "Ready", Snapshot: { Exists: true } snapshot }
                && snapshot.SourceDocumentVersion == document.ServerVersion && document.SyncState == LocalSyncState.Synced)
                snapshots.Add(kind, snapshot);
        if (Scope() != scope || account.Generation != generation) throw new DeviceSignInRequiredException();
        return new(document.ServerDocumentId ?? Guid.Empty, document.ServerVersion ?? "", snapshots);
    }
}
