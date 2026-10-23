using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed class CollaborationJournalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp_CollaborationJournal_" + Guid.NewGuid().ToString("N"));
    private readonly CollaborationJournalScope _scope = new("http://localhost:5188/", "owner-a", "test", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new(2, 1, new string('a', 64)));
    private DeviceCollaborationJournal Disk => new(_root);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes);
    private static CollaborationPendingEnvelope Envelope(CollaborationJournalPage page, byte[]? bytes = null) => new("commitUpdate", 1,
        page.Scope.DocumentId, page.Scope.PageId, page.Scope.Generation, page.InstallationId, Guid.NewGuid(), B64(bytes ?? [0, 0]), Hash(bytes ?? [0, 0]));
    private Task<CollaborationJournalPage> Init(CollaborationJournalScope? scope = null) => Disk.InitializeAsync(scope ?? _scope, B64([0, 0]), Hash([0, 0]));
    private static CollaborationDurableReceipt Receipt(CollaborationPendingEnvelope e) => new(e.PageId, e.Generation, e.InstallationId, e.UpdateId, e.Sha256, 1, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    public void Dispose()
    {
        // This fixture only owns its generated directory.
        if (Directory.Exists(_root) && Path.GetFileName(_root).StartsWith("WriterApp_CollaborationJournal_", StringComparison.Ordinal)) Directory.Delete(_root, true);
    }

    [Fact]
    public async Task RegrantedMembershipGetsSeparateJournalAndPreservesOriginalPendingWork()
    {
        var original = _scope with { MembershipRevision = 1 }; var page = await Init(original); var envelope = Envelope(page);
        await Disk.SaveAsync(original, new(0, page.State, page.StateSha256, envelope));
        var regranted = original with { MembershipRevision = 3 };
        Assert.NotEqual(DeviceCollaborationJournal.Key(original), DeviceCollaborationJournal.Key(regranted));
        Assert.Null(await Disk.LoadAsync(regranted)); Assert.Empty((await Init(regranted)).Pending);
        Assert.Equal(envelope, Assert.Single((await Disk.LoadAsync(original))!.Pending));
        Assert.Equal(DeviceCollaborationJournal.Key(_scope), DeviceCollaborationJournal.Key(_scope with { MembershipRevision = 0 }));
        Assert.Throws<InvalidDataException>(() => DeviceCollaborationJournal.Key(original with { MembershipRevision = -1 }));
    }

    [Fact]
    public async Task CheckpointAndImmutableOutboxRecoverTogetherAndInitializationNeverReseeds()
    {
        var initial = await Init(); var envelope = Envelope(initial);
        var saved = await Disk.SaveAsync(_scope, new(0, B64([1, 0]), Hash([1, 0]), envelope));
        Assert.Equal(1, saved.Revision); Assert.Single(saved.Pending);
        var reopened = await Disk.LoadAsync(_scope); Assert.Equal(saved.State, reopened!.State); Assert.Equal(envelope, Assert.Single(reopened.Pending));
        var retry = await Disk.SaveAsync(_scope, new(0, B64([1, 0]), Hash([1, 0]), envelope)); Assert.Equal(1, retry.Revision);
        Assert.Equal(saved.State, (await Disk.InitializeAsync(_scope, B64([2, 0]), Hash([2, 0]))).State);
        Assert.Equal(initial.InstallationId, (await Init(_scope with { PageId = Guid.NewGuid() })).InstallationId);
        await Assert.ThrowsAsync<InvalidDataException>(() => Disk.SaveAsync(_scope, new(1, B64([1, 0]), Hash([1, 0]), envelope with { Update = B64([2, 0]), Sha256 = Hash([2, 0]) })));
    }

    [Fact]
    public async Task ReceiptMustMatchBeforeItAtomicallyClearsPendingAndSurvivesRestart()
    {
        var page = await Init(); var envelope = Envelope(page); await Disk.SaveAsync(_scope, new(0, page.State, page.StateSha256, envelope));
        var receipt = Receipt(envelope);
        foreach (var invalid in new[] { receipt with { Sha256 = new string('b', 64) }, receipt with { Generation = Guid.NewGuid() }, receipt with { InstallationId = Guid.NewGuid() }, receipt with { CommitSequence = 0 } })
            await Assert.ThrowsAsync<InvalidDataException>(() => Disk.AcknowledgeAsync(_scope, invalid));
        Assert.Single((await Disk.LoadAsync(_scope))!.Pending);
        var accepted = await Disk.AcknowledgeAsync(_scope, receipt); Assert.Empty(accepted.Pending); Assert.Equal(receipt, Assert.Single(accepted.Receipts));
        Assert.Equal(accepted.Revision, (await Disk.AcknowledgeAsync(_scope, receipt)).Revision);
        var reopened = await Disk.LoadAsync(_scope); Assert.Empty(reopened!.Pending); Assert.Equal(receipt, Assert.Single(reopened.Receipts));
        await Assert.ThrowsAsync<InvalidDataException>(() => Disk.AcknowledgeAsync(_scope, receipt with { CommitSequence = 2 }));
    }

    [Fact]
    public async Task FailedAtomicReplacementPreservesPreviousCheckpointAndPendingSet()
    {
        var page = await Init(); var envelope = Envelope(page);
        var broken = new DeviceCollaborationJournal(_root, new AtomicDocumentWriter(_ => throw new IOException("intentional durable staging failure")));
        await Assert.ThrowsAsync<IOException>(() => broken.SaveAsync(_scope, new(0, B64([1, 0]), Hash([1, 0]), envelope)));
        var original = await Disk.LoadAsync(_scope); Assert.Equal(page.State, original!.State); Assert.Empty(original.Pending); Assert.Equal(0, original.Revision);
        await Disk.SaveAsync(_scope, new(0, B64([1, 0]), Hash([1, 0]), envelope));
        await Assert.ThrowsAsync<IOException>(() => broken.AcknowledgeAsync(_scope, Receipt(envelope)));
        Assert.Single((await Disk.LoadAsync(_scope))!.Pending);
    }

    [Fact]
    public async Task IndependentWritersCannotOverwriteARevisionAndFailureLeavesItsSourceIntact()
    {
        var page = await Init();
        async Task<bool> Attempt(DeviceCollaborationJournal journal)
        { try { await journal.SaveAsync(_scope, new(0, page.State, page.StateSha256, Envelope(page))); return true; } catch (InvalidOperationException) { return false; } }
        var results = await Task.WhenAll(Attempt(Disk), Attempt(Disk)); Assert.Single(results, value => value);
        Assert.Single((await Disk.LoadAsync(_scope))!.Pending);
    }

    [Fact]
    public async Task AccountBackendEnvironmentAndGenerationAreSeparateAndRecoveryStaysPrivate()
    {
        var page = await Init(); await Disk.SaveAsync(_scope, new(0, page.State, page.StateSha256, Envelope(page)));
        foreach (var other in new[] { _scope with { OwnerId = "owner-b" }, _scope with { Backend = "http://localhost:5999/" }, _scope with { Environment = "prod" }, _scope with { Generation = Guid.NewGuid() } })
        { Assert.Null(await Disk.LoadAsync(other)); await Init(other); }
        Assert.Single(await Disk.ListRecoveryAsync(_scope)); Assert.Empty(await Disk.ListRecoveryAsync(_scope with { OwnerId = "owner-b" }));
        var before = await File.ReadAllBytesAsync(Path.Combine(_root, DeviceCollaborationJournal.Key(_scope) + ".json"));
        await Assert.ThrowsAsync<InvalidDataException>(() => Disk.LoadAsync(_scope with { Support = new(2, 1, new string('f', 64)) }));
        Assert.Equal(before, await File.ReadAllBytesAsync(Path.Combine(_root, DeviceCollaborationJournal.Key(_scope) + ".json")));
        var blocked = await Disk.RecoveryAsync(_scope, "generation_changed"); Assert.Equal("generation_changed", blocked.RecoveryReason); Assert.Single(blocked.Pending);
    }

    [Fact]
    public async Task CorruptedAggregateIsNotLoadedOrReplacedByASeed()
    {
        await Init(); string path = Path.Combine(_root, DeviceCollaborationJournal.Key(_scope) + ".json");
        var bytes = await File.ReadAllBytesAsync(path); bytes[bytes.Length / 2] ^= 1; await File.WriteAllBytesAsync(path, bytes);
        await Assert.ThrowsAnyAsync<Exception>(() => Disk.LoadAsync(_scope));
        await Assert.ThrowsAnyAsync<Exception>(() => Init()); Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
    }

    [Fact]
    public async Task ColdOfflineRecoveryOpensOnlyAKnownPrivateGenerationAndCannotAcquireTickets()
    {
        await Init(); var identity = new Identity(); var account = new DeviceAccountService(identity);
        using var client = new HttpClient(new Handler(() => "owner-a")) { BaseAddress = new Uri(_scope.Backend) };
        var host = new DeviceCollaborationJournalHost(Disk, account, new("Test", client.BaseAddress), client);
        using var recovery = await host.OpenLocalRecoveryAsync(_scope); Assert.NotNull(await recovery.Load());
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => recovery.GetTicket(Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidDataException>(() => host.OpenLocalRecoveryAsync(_scope with { Generation = Guid.NewGuid() }));
        await account.SignInAsync(default); Assert.False(recovery.IsCurrent());
        await Assert.ThrowsAsync<OperationCanceledException>(() => recovery.Load());
    }

    [Fact]
    public async Task NativeBindingUsesVerifiedServerOwnerAndFencesLateCallbacksOnAccountChange()
    {
        var identity = new Identity(); var account = new DeviceAccountService(identity); await account.SignInAsync(default);
        string owner = "owner-a";
        using var client = new HttpClient(new Handler(() => owner)) { BaseAddress = new Uri(_scope.Backend) };
        var host = new DeviceCollaborationJournalHost(Disk, account, new("Test", client.BaseAddress), client);
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => host.OpenAsync(_scope, offline: true));
        using var binding = await host.OpenAsync(_scope); var initial = await binding.Initialize(B64([0, 0]), Hash([0, 0]));
        using var offline = await host.OpenAsync(_scope, offline: true); Assert.True(offline.IsCurrent());
        int invalidations = 0; binding.Invalidated += () => invalidations++;
        var captured = Envelope(initial); await account.SignOutAsync(); Assert.True(invalidations > 0); Assert.False(binding.IsCurrent());
        // An already-captured old edit can finish privately; its result and receipts cannot enter the new account.
        await Assert.ThrowsAsync<OperationCanceledException>(() => binding.Save(new(0, initial.State, initial.StateSha256, captured)));
        await Assert.ThrowsAsync<OperationCanceledException>(() => binding.Acknowledge(Receipt(captured)));
        Assert.Single((await Disk.LoadAsync(_scope))!.Pending);
        identity.Account = "identity-b"; owner = "owner-b"; await account.SignInAsync(default);
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => host.OpenAsync(_scope));
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => host.OpenAsync(_scope, offline: true));
        using var next = await host.OpenAsync(_scope with { OwnerId = owner }); Assert.Null(await next.Load());
    }
    private sealed class Identity : IDeviceIdentityClient
    {
        public string Account = "identity-a";
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken) => Task.FromResult<DeviceAccessToken?>(new("secret-never-journaled", DateTimeOffset.UtcNow.AddHours(1), "Writer", Account));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class Handler(Func<string> owner) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { isAuthenticated = true, isPaidAccessActive = true, userId = owner() }), Encoding.UTF8, "application/json") });
    }
}
