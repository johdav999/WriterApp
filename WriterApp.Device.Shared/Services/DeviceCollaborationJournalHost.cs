using System.Net.Http.Json;
using Microsoft.JSInterop;
using WriterApp.Application.Security;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Device.Shared.Services;

// Registered on desktop/iOS/Android through their common service setup; product editor wiring remains gated.
public sealed class DeviceCollaborationJournalHost(DeviceCollaborationJournal disk, DeviceAccountService account, DeviceHostOptions host, HttpClient client)
{
    // Explicit private-draft recovery works after a cold offline start without granting a network identity.
    public async Task<DeviceCollaborationJournalBinding> OpenLocalRecoveryAsync(CollaborationJournalScope scope, CancellationToken ct = default)
    {
        scope = DeviceCollaborationJournal.Normalize(scope); long generation = account.Generation;
        if (account.IsSignedIn || scope.Backend != host.ApiBaseAddress.AbsoluteUri.TrimEnd('/') + "/") throw new DeviceSignInRequiredException();
        if (await disk.LoadAsync(scope, ct) is null) throw new InvalidDataException("A known local generation is required for offline recovery.");
        if (account.Generation != generation || account.IsSignedIn) throw new OperationCanceledException("collaboration_account_changed");
        return new(disk, account, client, scope, generation, "", recoveryOnly: true);
    }
    public async Task<DeviceCollaborationJournalBinding> OpenAsync(CollaborationJournalScope scope, bool offline = false, CancellationToken ct = default)
    {
        scope = DeviceCollaborationJournal.Normalize(scope);
        if (scope.Backend != host.ApiBaseAddress.AbsoluteUri.TrimEnd('/') + "/" || !account.IsSignedIn || string.IsNullOrWhiteSpace(account.AccountId))
            throw new DeviceSignInRequiredException();
        long generation = account.Generation; string accountId = account.AccountId;
        if (!offline)
        {
            var me = await client.GetFromJsonAsync<AuthMeDto>("api/auth/me", ct);
            if (me is null || !me.IsAuthenticated || me.UserId != scope.OwnerId) throw new DeviceSignInRequiredException();
        }
        if (account.Generation != generation || account.AccountId != accountId || !account.IsSignedIn) throw new OperationCanceledException("collaboration_account_changed");
        if (!await disk.AccountBindingAsync(scope, accountId, !offline, ct)) throw new DeviceSignInRequiredException();
        if (account.Generation != generation || account.AccountId != accountId || !account.IsSignedIn) throw new OperationCanceledException("collaboration_account_changed");
        return new(disk, account, client, scope, generation, accountId);
    }
}

public sealed class DeviceCollaborationTicket
{
    public Guid SessionId { get; init; }
    public long Version { get; init; }
    public string Ticket { get; init; } = "";
    public string RoomName { get; init; } = "";
    public string WebSocketUrl { get; init; } = "";
    public long ExpiresAtUnixMilliseconds { get; init; }
    public bool ReadOnly { get; init; }
    public override string ToString() => "[redacted collaboration ticket]";
}

public sealed class DeviceCollaborationJournalBinding : IDisposable
{
    private readonly DeviceCollaborationJournal _disk;
    private readonly DeviceAccountService _account;
    private readonly HttpClient _client;
    private readonly long _generation;
    private readonly string _accountId;
    private readonly bool _recoveryOnly;
    private bool _disposed;
    public CollaborationJournalScope Scope { get; }
    public event Action? Invalidated;
    internal DeviceCollaborationJournalBinding(DeviceCollaborationJournal disk, DeviceAccountService account, HttpClient client, CollaborationJournalScope scope, long generation, string accountId, bool recoveryOnly = false)
    { _disk = disk; _account = account; _client = client; Scope = scope; _generation = generation; _accountId = accountId; _recoveryOnly = recoveryOnly; _account.Changed += Changed; }
    [JSInvokable] public bool IsCurrent() => !_disposed && _account.Generation == _generation && (_recoveryOnly ? !_account.IsSignedIn : _account.IsSignedIn && _account.AccountId == _accountId);
    private void RequireCurrent() { if (!IsCurrent()) throw new OperationCanceledException("collaboration_account_changed"); }
    private void Changed() { if (!IsCurrent()) Invalidated?.Invoke(); }
    private async Task<T> Disk<T>(Func<Task<T>> operation)
    {
        RequireCurrent();
        // Already-admitted writes finish only in their immutable old scope, even if sign-out occurs mid-flush.
        T result = await operation(); RequireCurrent(); return result;
    }
    [JSInvokable] public Task<CollaborationJournalPage?> Load() => Disk(() => _disk.LoadAsync(Scope));
    [JSInvokable] public Task<CollaborationJournalPage> Initialize(string state, string hash) => Disk(() => _disk.InitializeAsync(Scope, state, hash));
    [JSInvokable] public async Task<CollaborationJournalPage> Save(CollaborationJournalMutation request)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(DeviceCollaborationJournalBinding));
        // A transaction captured by the old editor may finish in its private old scope after sign-out.
        // Its result cannot reach a new account; receipts, reads and transport still require current identity.
        var result = await _disk.SaveAsync(Scope, request); RequireCurrent(); return result;
    }
    [JSInvokable] public Task<CollaborationJournalPage> Acknowledge(CollaborationDurableReceipt receipt) => Disk(() => _disk.AcknowledgeAsync(Scope, receipt));
    [JSInvokable] public Task<CollaborationJournalPage> Recovery(string? reason) => Disk(() => _disk.RecoveryAsync(Scope, reason));
    [JSInvokable] public async Task<DeviceCollaborationTicket> GetTicket(Guid installationId)
    {
        RequireCurrent();
        if (_recoveryOnly) throw new DeviceSignInRequiredException();
        var me = await _client.GetFromJsonAsync<AuthMeDto>("api/auth/me"); RequireCurrent();
        if (me is null || !me.IsAuthenticated || me.UserId != Scope.OwnerId) throw new DeviceSignInRequiredException();
        using var response = await _client.PostAsJsonAsync($"api/documents/{Scope.DocumentId}/pages/{Scope.PageId}/collaboration/sessions", new {
            Scope.Generation, Scope.Environment, installationId, Scope.Support.ContentContractVersion, Scope.Support.ProtocolVersion, Scope.Support.SchemaFingerprint, Scope.MembershipRevision, requestCommit = true });
        RequireCurrent(); response.EnsureSuccessStatusCode();
        var ticket = await response.Content.ReadFromJsonAsync<DeviceCollaborationTicket>() ?? throw new InvalidDataException("Missing session ticket.");
        RequireCurrent(); return ticket;
    }
    public void Dispose() { if (_disposed) return; _disposed = true; _account.Changed -= Changed; Invalidated?.Invoke(); }
}
