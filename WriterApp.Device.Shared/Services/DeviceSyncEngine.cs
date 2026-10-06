using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;

namespace WriterApp.Device.Shared.Services;

public sealed record DeviceSyncStatus(string Label, string? Error, DateTimeOffset? LastSynced, bool Conflict, bool Deleted);
public sealed record DeviceSyncConflictView(Guid LocalId, Guid? RemoteCopyId, bool Deleted);

/// <summary>Durable immutable requests plus a rescan of committed local revisions form the offline queue.</summary>
public sealed class DeviceSyncEngine : IDisposable
{
    private readonly FileLocalDocumentStore _store;
    private readonly DeviceSyncJournal _disk;
    private readonly IDeviceSyncApi _api;
    private readonly DeviceAccountService _account;
    private readonly DeviceConnectivity _network;
    private readonly LocalDocumentRepository _repository;
    private readonly string _backend;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _run, _scheduled;
    private bool _signedIn, _disposed;
    private long _generation;
    private SyncJournal? _journal;
    private string? _key;
    private IReadOnlyDictionary<Guid, DeviceSyncStatus> _statuses = new Dictionary<Guid, DeviceSyncStatus>();
    private DeviceSyncConflictView[] _conflicts = [];
    public bool IsRunning { get; private set; }
    public string Message { get; private set; } = "Sign in to synchronize. Local writing is always available.";
    public string? LastError { get; private set; }
    public event Action? Changed;

    public DeviceSyncEngine(FileLocalDocumentStore store, DeviceSyncJournal disk, IDeviceSyncApi api,
        DeviceAccountService account, DeviceConnectivity network, LocalDocumentRepository repository,
        DeviceHostOptions host, TimeProvider? time = null)
    {
        _store = store; _disk = disk; _api = api; _account = account; _network = network; _repository = repository;
        _backend = host.ApiBaseAddress.AbsoluteUri; _time = time ?? TimeProvider.System;
        account.Changed += AccountChanged; network.Changed += NetworkChanged; repository.Changed += LocalChanged;
    }
    public void Start() => AccountChanged();
    public DeviceSyncStatus? Status(Guid id)
    {
        var status = _statuses.GetValueOrDefault(id);
        return status is not null && IsRunning && !status.Conflict && !status.Deleted && status.Error is null ? status with { Label = "Syncing" } : status;
    }
    public IReadOnlyList<DeviceSyncConflictView> Conflicts => _conflicts;
    public DeviceSyncDiagnosticSnapshot DiagnosticSnapshot => new(IsRunning, _statuses.Count,
        _statuses.Values.Count(item => item.Label == "Waiting to sync"),
        _statuses.Values.Count(item => item.Error is not null), _conflicts.Length,
        _statuses.Values.Select(item => item.LastSynced).Where(item => item.HasValue).Max());
    private void Publish()
    {
        _statuses = _journal?.Entries.ToDictionary(e => e.LocalId, e => new DeviceSyncStatus(
            e.Conflict is not null ? "Conflict — review copies" : e.Deleted ? "Deleted in cloud — local copy retained"
                : e.Error is not null ? "Sync needs attention" : e.Pending is not null ? "Waiting to sync" : "Cloud linked",
            e.Error, e.LastSynced, e.Conflict is not null, e.Deleted)) ?? [];
        _conflicts = _journal?.Entries.Where(e => e.Conflict is not null)
            .Select(e => new DeviceSyncConflictView(e.LocalId, e.Conflict!.Remote.Document is null ? null : e.Conflict.RemoteCopyId, e.Conflict.Remote.State.IsDeleted)).ToArray() ?? [];
        Changed?.Invoke();
    }
    private static void Stop(CancellationTokenSource? source) { try { source?.Cancel(); } catch (ObjectDisposedException) { } }
    private void AccountChanged()
    {
        bool changed = _signedIn != _account.IsSignedIn || _generation != _account.Generation;
        _signedIn = _account.IsSignedIn; _generation = _account.Generation;
        if (!changed) return; // Token acquisition also raises Changed; it must not schedule an endless sync loop.
        Stop(_run);
        if (!IsRunning) { _journal = null; _key = null; Publish(); }
        if (_signedIn)
        {
            Message = _network.IsOnline ? "Checking cloud synchronization…" : "Offline. Changes will synchronize after reconnection.";
            Changed?.Invoke(); Schedule();
        }
        else { Message = "Signed out. Local documents and pending changes are preserved."; Changed?.Invoke(); }
    }
    private void NetworkChanged() { if (_network.IsOnline) Schedule(); else { Stop(_run); Message = "Offline. Changes will synchronize after reconnection."; Changed?.Invoke(); } }
    private void LocalChanged() => Schedule();
    private void Schedule()
    {
        if (_disposed || !_account.IsSignedIn || !_network.IsOnline) return;
        Stop(_scheduled);
        var delay = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _scheduled = delay;
        _ = ScheduledAsync(delay);
    }
    private async Task ScheduledAsync(CancellationTokenSource delay)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(2), _time, delay.Token); await SyncAsync(delay.Token); }
        catch (OperationCanceledException) { }
        finally { if (_scheduled == delay) _scheduled = null; delay.Dispose(); }
    }
    public void Cancel() => Stop(_run);
    public Task SyncAsync(CancellationToken ct = default) => ExecuteAsync(null, ct);
    public Task EnableAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(async token =>
    {
        var doc = await _store.GetAsync(id, token) ?? throw new IOException("Document not found.");
        if (_journal!.Entries.Any(e => e.LocalId == id)) return;
        if (doc.ServerDocumentId is not null) throw new InvalidOperationException("This document is linked elsewhere. Duplicate it to upload to this account.");
        await _disk.BindAsync(id, _key!, token);
        _journal.Entries.Add(new() { LocalId = id, ServerId = Guid.NewGuid() });
        await PersistAsync(token);
        await UpdateMetadataAsync(_journal.Entries.Last(), token);
    }, ct);
    public Task DeleteAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(async token =>
    {
        var doc = await _store.GetAsync(id, token) ?? throw new IOException("Document not found.");
        var entry = _journal!.Entries.Single(e => e.LocalId == id);
        if (doc.DeletedAtUtc is null || entry.Conflict is not null || entry.Deleted) throw new InvalidOperationException("Resolve conflicts and move to Trash first.");
        entry.DeleteRequested = true;
        await PersistAsync(token);
    }, ct);
    public Task RetryAsync(Guid id, CancellationToken ct = default) => ExecuteAsync(async token =>
    {
        var entry = _journal!.Entries.Single(e => e.LocalId == id);
        // Only definitely rejected validation requests can be replaced. Ambiguous outcomes retain their operation ID.
        entry.Error = null; entry.TransientError = false;
        if (entry.Pending is not null && entry.Rejected)
        { entry.Pending = null; entry.Rejected = false; }
        await PersistAsync(token);
    }, ct);

    private async Task ExecuteAsync(Func<CancellationToken, Task>? before, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        using var run = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        _run = run;
        long generation = _account.Generation;
        bool signedInAtStart = _account.IsSignedIn;
        LastError = null;
        try
        {
            if (!_network.IsOnline)
            {
                if (before is not null && _key is not null)
                {
                    using var offlineLease = _disk.Acquire();
                    _journal = await _disk.LoadAsync(_key, run.Token);
                    await before(run.Token);
                    Message = "Offline. The operation is queued for the last verified account.";
                }
                else Message = before is null ? "Offline. Pending changes are saved on this device." : "Connect and verify the account once before enabling cloud operations.";
                return;
            }
            IsRunning = true; Message = "Checking cloud synchronization…"; Changed?.Invoke();
            string owner = await RetryNetworkAsync(() => _api.GetOwnerAsync(run.Token), run.Token);
            run.Token.ThrowIfCancellationRequested();
            using var lease = _disk.Acquire();
            _key = _disk.AccountKey(_backend, owner);
            _journal = await _disk.LoadAsync(_key, run.Token);
            Publish();
            foreach (var entry in _journal.Entries.Where(e => e.Conflict is not null)) await MaterializeRemoteAsync(entry, run.Token);
            if (before is not null) await before(run.Token);
            // Send durable work first: replay a lost acknowledgment before treating its own feed item as a conflict.
            foreach (var entry in _journal.Entries.ToArray()) await UploadAsync(entry, run.Token);
            bool moreChanges = false;
            for (int page = 0; page < 100; page++)
            {
                SyncChanges feed;
                try { feed = await RetryNetworkAsync(() => _api.ChangesAsync(_journal.Cursor, run.Token), run.Token); }
                catch (DeviceSyncApiException e) when (e.Status == 400 && _journal.Cursor is not null)
                { _journal.Cursor = null; await PersistAsync(run.Token); continue; }
                foreach (var change in feed.Changes)
                {
                    var entry = _journal.Entries.FirstOrDefault(e => e.ServerId == change.DocumentId);
                    if (entry is null)
                    {
                        if (change.IsDeleted) continue;
                        entry = new() { LocalId = Guid.NewGuid(), ServerId = change.DocumentId };
                        await _disk.BindAsync(entry.LocalId, _key!, run.Token);
                        _journal.Entries.Add(entry);
                        await PersistAsync(run.Token); // Stable local identity before creating a file.
                    }
                    if (entry.Version == change.Version || entry.Conflict is not null || entry.Pending is not null) continue;
                    var remote = await RetryNetworkAsync(() => _api.DownloadAsync(entry.ServerId, run.Token), run.Token);
                    await ReceiveAsync(entry, remote, run.Token);
                }
                _journal.Cursor = feed.Cursor;
                await PersistAsync(run.Token);
                moreChanges = feed.HasMore;
                if (!feed.HasMore) break;
            }
            Message = moreChanges ? "More cloud changes remain. Sync now to continue from the saved cursor."
                : _journal.Entries.Any(e => e.Conflict is not null || e.Error is not null)
                ? "Sync finished. Some documents need review; local writing is preserved." : "Synchronization complete.";
        }
        catch (OperationCanceledException) { Message = "Synchronization paused. Pending operations are preserved."; }
        catch (DeviceSignInRequiredException) { Failed("Sign in again to synchronize. Local writing is preserved."); }
        catch (DeviceIdentityUnavailableException) { Failed("Sign-in is unavailable. Retry when connected."); }
        catch (DeviceSyncApiException error) { Failed(error.Status switch { 401 => "Sign in again to synchronize.", 403 => "Cloud synchronization is unavailable for this account or plan. Local writing is preserved.", _ => "Synchronization stopped: " + error.Message }); }
        catch (HttpRequestException)
        {
            var backend = new Uri(_backend);
            Failed($"Cannot reach the configured backend at {backend.GetLeftPart(UriPartial.Authority)}. "
                + (backend.IsLoopback ? "Start the local API, then retry cloud sync." : "Check your connection, then retry cloud sync.")
                + " Local writing and queued operations are preserved.");
        }
        catch (IOException) { Failed("Cloud sync could not access local storage. Close other app instances and check storage access, then retry. Local writing and queued operations are preserved."); }
        catch (UnauthorizedAccessException) { Failed("Cloud sync cannot write to local storage. Check folder permissions, then retry. Local writing and queued operations are preserved."); }
        catch (JsonException) { Failed("Cloud sync data could not be read. Existing files and queued operations are preserved. Check the backend and local sync data before retrying."); }
        catch (InvalidOperationException error) { Failed("Synchronization stopped safely. " + error.Message); }
        finally
        {
            if (_account.Generation != generation || (signedInAtStart && !_account.IsSignedIn)) { _journal = null; _key = null; Publish(); }
            IsRunning = false; _run = null; _gate.Release(); Changed?.Invoke();
        }
    }

    private void Failed(string message) { LastError = message; Message = message; }

    private async Task UploadAsync(SyncEntry entry, CancellationToken ct)
    {
        if (entry.Deleted || entry.Conflict is not null || (entry.Error is not null && !entry.TransientError)) return;
        entry.Error = null; entry.TransientError = false;
        // Bound successive work when editing continues while a request is in flight.
        for (int step = 0; step < 4; step++)
        {
            var local = await _store.GetAsync(entry.LocalId, ct);
            if (local is null) return;
            string fingerprint = DeviceSyncMapping.WritingFingerprint(local);
            if (entry.Pending is null)
            {
                string? action = entry.Version is null ? "upload"
                    : entry.ServerTrashed != (local.DeletedAtUtc is not null) ? local.DeletedAtUtc is null ? "restore" : "trash"
                    : entry.DeleteRequested ? "delete"
                    : fingerprint != entry.BaseFingerprint && !entry.ServerTrashed
                        ? DeviceSyncMapping.ContentFingerprint(local) == entry.BaseContentFingerprint ? "rename" : "upload" : null;
                if (action is null)
                {
                    // Repair metadata from older clients or an interrupted acknowledgment even
                    // when the journal already proves there is no new writing to upload.
                    if (local.SyncState != LocalSyncState.Synced || local.Sections.Any(s =>
                        s.ServerSectionId is null || s.Pages.Any(p => p.ServerPageId is null)))
                        await UpdateMetadataAsync(entry, ct);
                    return;
                }
                entry.Pending = new(new(Guid.NewGuid(), entry.Version, action, action == "upload" ? DeviceSyncMapping.Upload(local) : null,
                    action == "rename" ? local.Title : null), fingerprint, DeviceSyncMapping.ContentFingerprint(local));
                await PersistAsync(ct); // Must succeed before any network mutation.
            }
            var pending = entry.Pending;
            try
            {
                var result = await RetryNetworkAsync(() => _api.MutateAsync(entry.ServerId, pending.Request, ct), ct);
                if (result.OperationId != pending.Request.OperationId || result.State.DocumentId != entry.ServerId) throw new JsonException("Mismatched sync acknowledgment.");
                entry.Version = result.State.Version; entry.ServerTrashed = result.State.IsTrashed; entry.Deleted = result.State.IsDeleted;
                entry.ProjectMetadataRevision = result.ProjectMetadataRevision;
                entry.PrimaryDocumentId = result.PrimaryDocumentId;
                // Trash/restore do not upload writing. Keep the old content base so newer content is sent afterward.
                if (pending.Request.Action is "upload" or "rename")
                { entry.BaseFingerprint = pending.Fingerprint; entry.BaseContentFingerprint = pending.ContentFingerprint; }
                entry.Pending = null; entry.LastSynced = _time.GetUtcNow();
                await PersistAsync(ct);
                await UpdateMetadataAsync(entry, ct);
                if (entry.Deleted) return;
            }
            catch (DeviceSyncApiException error) when (error.Status == 409 && error.Code is "version_conflict" or "project_metadata_conflict" or "document_deleted" or "document_trashed")
            {
                var remote = await RetryNetworkAsync(() => _api.DownloadAsync(entry.ServerId, ct), ct);
                // A sibling's shared metadata update can advance this document's version while
                // leaving exactly the writing and metadata we already intended to upload.
                if (error.Code == "version_conflict" && remote.Document is not null && !remote.State.IsTrashed && !remote.State.IsDeleted)
                {
                    var latest = await _store.GetAsync(entry.LocalId, ct);
                    if (latest is not null)
                    {
                        var cloud = DeviceSyncMapping.Download(remote, entry.LocalId, latest, _time.GetUtcNow());
                        if (DeviceSyncMapping.WritingFingerprint(cloud) == DeviceSyncMapping.WritingFingerprint(latest))
                        {
                            try
                            {
                                await _store.ApplySyncAsync(cloud, latest.LocalRevision, ct, projects: true);
                                entry.Version = remote.State.Version; entry.Pending = null; entry.LastSynced = _time.GetUtcNow();
                                entry.ProjectMetadataRevision = cloud.Project?.ServerMetadataRevision;
                                entry.PrimaryDocumentId = cloud.Project?.ServerPrimaryDocumentId;
                                entry.BaseFingerprint = DeviceSyncMapping.WritingFingerprint(cloud);
                                entry.BaseContentFingerprint = DeviceSyncMapping.ContentFingerprint(cloud);
                                await PersistAsync(ct); continue;
                            }
                            catch (LocalDocumentConflictException) { /* Preserve typing that changed during the comparison. */ }
                        }
                    }
                }
                await ConflictAsync(entry, remote, ct); return;
            }
            catch (DeviceSyncApiException error) when (!error.IsTransient && error.Status is not (401 or 403))
            {
                entry.Error = $"{error.Code}: {error.Message}";
                entry.Rejected = error.Status is 400 or 413 or 422;
                await PersistAsync(ct); return;
            }
            catch (Exception error) when (!ct.IsCancellationRequested && (error is HttpRequestException or TaskCanceledException || error is DeviceSyncApiException failure && failure.IsTransient))
            {
                entry.Error = "Connection interrupted. The same queued operation will be retried on reconnection or Sync now.";
                entry.TransientError = true;
                await PersistAsync(ct);
                throw;
            }
        }
    }
    private async Task ReceiveAsync(SyncEntry entry, SyncSnapshot remote, CancellationToken ct)
    {
        var local = await _store.GetAsync(entry.LocalId, ct);
        var downloaded = remote.Document is null ? null : DeviceSyncMapping.Download(remote, entry.LocalId, local, _time.GetUtcNow());
        bool alreadyMatches = local is not null && downloaded is not null && DeviceSyncMapping.WritingFingerprint(local) == DeviceSyncMapping.WritingFingerprint(downloaded);
        if (local is not null && (DeviceSyncMapping.RemovesProjectNodes(local, remote) || (!alreadyMatches && DeviceSyncMapping.WritingFingerprint(local) != entry.BaseFingerprint)
            || (local.DeletedAtUtc is not null) != entry.ServerTrashed || _repository.IsEditing(entry.LocalId)))
        { await ConflictAsync(entry, remote, ct); return; }
        if (remote.State.IsDeleted)
        {
            entry.Version = remote.State.Version; entry.Deleted = true; entry.LastSynced = _time.GetUtcNow();
            await PersistAsync(ct); await UpdateMetadataAsync(entry, ct); return;
        }
        try { await _store.ApplySyncAsync(downloaded!, local?.LocalRevision, ct, projects: true); }
        catch (LocalDocumentConflictException) { await ConflictAsync(entry, remote, ct); return; }
        downloaded = (await _store.GetAsync(entry.LocalId, ct))!;
        entry.Version = remote.State.Version; entry.ServerTrashed = remote.State.IsTrashed;
        entry.ProjectMetadataRevision = downloaded.Project?.ServerMetadataRevision;
        entry.PrimaryDocumentId = downloaded.Project?.ServerPrimaryDocumentId;
        entry.BaseFingerprint = DeviceSyncMapping.WritingFingerprint(downloaded);
        entry.BaseContentFingerprint = DeviceSyncMapping.ContentFingerprint(downloaded); entry.LastSynced = _time.GetUtcNow();
        await PersistAsync(ct);
    }
    private async Task ConflictAsync(SyncEntry entry, SyncSnapshot remote, CancellationToken ct)
    {
        entry.Conflict = new(remote, Guid.NewGuid(), Guid.NewGuid()); entry.Pending = null; entry.Error = null;
        await PersistAsync(ct); // Snapshot and copy identities survive interruption before file materialization.
        await MaterializeRemoteAsync(entry, ct);
        await UpdateMetadataAsync(entry, ct);
    }
    private async Task MaterializeRemoteAsync(SyncEntry entry, CancellationToken ct)
    {
        var conflict = entry.Conflict!;
        if (conflict.Remote.Document is null || await _store.GetAsync(conflict.RemoteCopyId, ct) is not null) return;
        var cloud = DeviceSyncMapping.Download(conflict.Remote, conflict.RemoteCopyId, null, _time.GetUtcNow());
        await _store.ApplySyncAsync(DeviceSyncMapping.Copy(cloud, conflict.RemoteCopyId, " (cloud conflict copy)"), null, ct, projects: true);
    }
    public Task ResolveAsync(Guid id, bool keepLocal, CancellationToken ct = default) => ExecuteAsync(async token =>
    {
        var entry = _journal!.Entries.Single(e => e.LocalId == id);
        var conflict = entry.Conflict ?? throw new InvalidOperationException("Conflict already resolved.");
        await MaterializeRemoteAsync(entry, token);
        var local = await _store.GetAsync(id, token) ?? throw new IOException("Local document is missing.");
        // Keep a durable independent copy of the latest local writing before either resolution.
        // A fresh backup on each resolution attempt also preserves edits made after an interrupted attempt.
        conflict = conflict with { LocalCopyId = Guid.NewGuid() };
        entry.Conflict = conflict;
        await PersistAsync(token);
        await _store.ApplySyncAsync(DeviceSyncMapping.Copy(local, conflict.LocalCopyId, " (local conflict copy)"), null, token, projects: true);
        if (!keepLocal && conflict.Remote.Document is not null)
        {
            var downloaded = DeviceSyncMapping.Download(conflict.Remote, id, local, _time.GetUtcNow());
            await _store.ApplySyncAsync(downloaded, local.LocalRevision, token, projects: true, overwriteProjectMetadata: true);
            downloaded = (await _store.GetAsync(id, token))!;
            entry.ProjectMetadataRevision = downloaded.Project?.ServerMetadataRevision;
            entry.PrimaryDocumentId = downloaded.Project?.ServerPrimaryDocumentId;
            entry.BaseFingerprint = DeviceSyncMapping.WritingFingerprint(downloaded);
            entry.BaseContentFingerprint = DeviceSyncMapping.ContentFingerprint(downloaded);
        }
        else
        {
            entry.BaseFingerprint = null;
            if (keepLocal && conflict.Remote.Document?.Project is { Version: >= 3 } project)
            {
                await _store.RebaseProjectSyncRevisionAsync(local, project.MetadataRevision, token);
                entry.ProjectMetadataRevision = project.MetadataRevision;
                entry.PrimaryDocumentId = local.Project?.ServerPrimaryDocumentId;
            }
        }
        entry.Version = conflict.Remote.State.Version; entry.ServerTrashed = conflict.Remote.State.IsTrashed;
        entry.Deleted = conflict.Remote.State.IsDeleted; entry.DeleteRequested = false; entry.Conflict = null;
        await PersistAsync(token); await UpdateMetadataAsync(entry, token);
        // Permanent deletions never recreate the old server ID. The retained independent copy can be explicitly uploaded.
    }, ct);
    private async Task UpdateMetadataAsync(SyncEntry entry, CancellationToken ct)
    {
        var local = await _store.GetAsync(entry.LocalId, ct);
        if (local is null) return;
        bool acknowledged = entry.Version is not null && !entry.Deleted && entry.Conflict is null
            && DeviceSyncMapping.WritingFingerprint(local) == entry.BaseFingerprint
            && (local.DeletedAtUtc is not null) == entry.ServerTrashed;
        var updated = local with { ServerDocumentId = entry.ServerId,
            // Upload sends these exact IDs. Bind them only when the current writing matches
            // the acknowledged request, so newly added targets never acquire unconfirmed IDs.
            Sections = acknowledged ? local.Sections.Select(s => s with {
                ServerSectionId = s.ServerSectionId ?? s.SectionId,
                Pages = s.Pages.Select(p => p with { ServerPageId = p.ServerPageId ?? p.PageId }).ToArray()
            }).ToArray() : local.Sections,
            ServerProjectId = local.Project is { } localProject ? localProject.ServerProjectId ?? localProject.ProjectId : local.ServerProjectId,
            Project = local.Project is { } p ? p with { ServerProjectId = p.ServerProjectId ?? p.ProjectId,
                ServerMetadataRevision = entry.ProjectMetadataRevision ?? p.ServerMetadataRevision,
                ServerPrimaryDocumentId = entry.PrimaryDocumentId ?? p.ServerPrimaryDocumentId,
                Nodes = p.Nodes.Select(n => n with { ServerNodeId = n.ServerNodeId ?? n.NodeId, Annotations = n.Annotations.Select(a => a with { ServerId = a.ServerId ?? a.LocalId }).ToArray() }).ToArray() } : null,
            ServerVersion = entry.Version, LastSyncedAtUtc = entry.LastSynced,
            SyncState = entry.Conflict is not null ? LocalSyncState.Conflict : DeviceSyncMapping.WritingFingerprint(local) == entry.BaseFingerprint
                && (local.DeletedAtUtc is not null) == entry.ServerTrashed ? LocalSyncState.Synced : LocalSyncState.PendingUpload };
        try { await _store.ApplySyncAsync(updated, local.LocalRevision, ct, projects: true); }
        catch (LocalDocumentConflictException) { /* A newer local save wins; next scan sees it. */ }
    }
    private async Task PersistAsync(CancellationToken ct)
    { await _disk.SaveAsync(_key!, _journal!, ct); Publish(); }
    private async Task<T> RetryNetworkAsync<T>(Func<Task<T>> call, CancellationToken ct)
    {
        for (int attempt = 0; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { var result = await call(); ct.ThrowIfCancellationRequested(); return result; }
            catch (Exception error) when (attempt < 2 && !ct.IsCancellationRequested &&
                (error is HttpRequestException || error is DeviceSyncApiException api && api.IsTransient || error is TaskCanceledException))
            { await Task.Delay(TimeSpan.FromSeconds(1 << attempt), _time, ct); }
        }
    }
    public void Dispose()
    {
        _disposed = true; _lifetime.Cancel(); Stop(_scheduled); Stop(_run);
        _account.Changed -= AccountChanged; _network.Changed -= NetworkChanged; _repository.Changed -= LocalChanged;
    }
}
