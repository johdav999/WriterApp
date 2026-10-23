using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Collaboration;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Security;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationSessionTests
{
    private CollaborationCommitService Commits(AppDbContext db) => new(db, Service(db), _settings, _clock);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private async Task<CollaborationServiceRequest> CommitSession(bool writable = true)
    {
        _settings.CurrentValue.CommitsEnabled = true;
        await using var db = Open();
        var ticket = await Service(db).Issue("owner", _document, _page, Request with { RequestCommit = writable }, default);
        var scope = new CollaborationServiceRequest(ticket.Ticket, ticket.RoomName, Guid.NewGuid());
        await Service(db).Authorize(scope with { Acquire = true }, default); return scope;
    }
    private static CollaborationCommitRequest Update(CollaborationServiceRequest session, Guid worker, long epoch, long sequence = 0) =>
        new(session, worker, epoch, sequence, Guid.NewGuid(), [0, 0], Hash([0, 0]));

    [Fact]
    public async Task CommitAtomicallyStoresLedgerReceiptSequenceAndOutboxAndSurvivesCompaction()
    {
        var session = await CommitSession(); var worker = Guid.NewGuid();
        CollaborationCommitRequest update; CollaborationCommitResult result;
        await using (var db = Open())
        {
            var service = Commits(db); var lease = await service.Lease(new(session, worker), default);
            update = Update(session, worker, lease.Epoch); result = await service.Commit(update, default);
            Assert.Equal(1, result.Receipt.CommitSequence);
            Assert.Equal((await db.CollaborationSessions.SingleAsync()).InstallationId, result.Receipt.InstallationId);
            Assert.Single(await db.CollaborationUpdates.ToArrayAsync()); Assert.Single(await db.CollaborationReceipts.ToArrayAsync());
            Assert.Single(await db.CollaborationOutboxEvents.ToArrayAsync());
            Assert.Equal(result, await service.Commit(update, default));
            await service.Checkpoint(new(session, worker, lease.Epoch, 1, [0, 0], Hash([0, 0])), default);
        }
        await using (var db = Open())
        {
            Assert.Empty(await db.CollaborationUpdates.ToArrayAsync()); Assert.Single(await db.CollaborationReceipts.ToArrayAsync());
            Assert.Single(await db.CollaborationOutboxEvents.ToArrayAsync());
            Assert.Equal(result, await Commits(db).Commit(update, default));
            var scope = await Service(db).Authorize(session, default);
            var bootstrap = await Service(db).Bootstrap(scope, default);
            Assert.Equal(1, bootstrap.CommittedSequence); Assert.Equal(1, bootstrap.SnapshotSequence); Assert.Single(bootstrap.Updates);
            Assert.Equal("<p>Legacy retained</p>", (await db.Pages.SingleAsync()).Content);
        }
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("predecessor")]
    [InlineData("worker")]
    [InlineData("epoch")]
    [InlineData("disabled")]
    [InlineData("revoked")]
    [InlineData("expired")]
    public async Task InvalidOrStaleCommitNeverAdvancesSequence(string change)
    {
        var session = await CommitSession(); var worker = Guid.NewGuid();
        await using var db = Open(); var service = Commits(db); var lease = await service.Lease(new(session, worker), default);
        var update = Update(session, worker, lease.Epoch);
        switch (change)
        {
            case "hash": update = update with { Sha256 = new string('a', 64) }; break;
            case "size": byte[] large = new byte[CollaborationCommitService.MaximumUpdateBytes + 1]; update = update with { Update = large, Sha256 = Hash(large) }; break;
            case "predecessor": update = update with { ExpectedSequence = 10 }; break;
            case "worker": update = update with { WorkerId = Guid.NewGuid() }; break;
            case "epoch": update = update with { Epoch = lease.Epoch + 1 }; break;
            case "disabled": _settings.CurrentValue.CommitsEnabled = false; break;
            case "revoked": await Service(db).Revoke("owner", _document, _page, (await db.CollaborationSessions.SingleAsync()).Id, default); break;
            case "expired": _clock.Advance(TimeSpan.FromSeconds(31)); break;
        }
        await Assert.ThrowsAnyAsync<Exception>(() => service.Commit(update, default));
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.CollaborationPages.SingleAsync()).CommittedSequence);
        Assert.Empty(await db.CollaborationUpdates.ToArrayAsync()); Assert.Empty(await db.CollaborationReceipts.ToArrayAsync()); Assert.Empty(await db.CollaborationOutboxEvents.ToArrayAsync());
    }

    [Fact]
    public async Task SameIdentityWithDifferentBytesCannotAcquireAnotherReceipt()
    {
        var session = await CommitSession(); var worker = Guid.NewGuid(); await using var db = Open(); var service = Commits(db);
        var lease = await service.Lease(new(session, worker), default); var update = Update(session, worker, lease.Epoch);
        await service.Commit(update, default);
        var error = await Assert.ThrowsAsync<CollaborationCommitException>(() => service.Commit(update with { Update = [1, 0], Sha256 = Hash([1, 0]) }, default));
        Assert.Equal("collaboration_update_identity_reused", error.Code);
        Assert.Single(await db.CollaborationReceipts.ToArrayAsync()); Assert.Equal(1, (await db.CollaborationPages.AsNoTracking().SingleAsync()).CommittedSequence);
    }

    [Fact]
    public async Task LeaseTakeoverFencesPreviousWorkerAndCannotRenewAnExpiredEpoch()
    {
        var session = await CommitSession(); var first = Guid.NewGuid(); var next = Guid.NewGuid(); await using var db = Open(); var service = Commits(db);
        var lease = await service.Lease(new(session, first), default);
        await Assert.ThrowsAsync<CollaborationCommitException>(() => service.Lease(new(session, next), default));
        _clock.Advance(TimeSpan.FromSeconds(31));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => service.Lease(new(session, first, lease.Epoch), default));
        var takeover = await service.Lease(new(session, next), default); Assert.True(takeover.Epoch > lease.Epoch);
        await Assert.ThrowsAsync<CollaborationCommitException>(() => service.Commit(Update(session, first, lease.Epoch), default));
        Assert.Equal(1, (await service.Commit(Update(session, next, takeover.Epoch), default)).CommittedSequence);
    }

    [Fact]
    public async Task ReadOnlySessionCannotBePromotedByTurningOnCommitConfiguration()
    {
        var session = await CommitSession(false); await using var db = Open();
        await Assert.ThrowsAsync<CollaborationCommitException>(() => Commits(db).Lease(new(session, Guid.NewGuid()), default));
        _settings.CurrentValue.CommitsEnabled = false;
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Service(db).Issue("owner", _document, _page, Request with { RequestCommit = true }, default));
    }

    [Fact]
    public async Task CompetingCommitsFromDifferentBackendContextsCannotForkSequence()
    {
        var session = await CommitSession(); var worker = Guid.NewGuid();
        await using var first = Open(); await using var second = Open();
        var lease = await Commits(first).Lease(new(session, worker), default);
        var results = await Task.WhenAll(Attempt(Commits(first), Update(session, worker, lease.Epoch)), Attempt(Commits(second), Update(session, worker, lease.Epoch)));
        Assert.Single(results, r => r);
        await using var verify = Open(); Assert.Single(await verify.CollaborationUpdates.ToArrayAsync()); Assert.Single(await verify.CollaborationReceipts.ToArrayAsync()); Assert.Single(await verify.CollaborationOutboxEvents.ToArrayAsync());
        static async Task<bool> Attempt(CollaborationCommitService service, CollaborationCommitRequest update)
        { try { await service.Commit(update, default); return true; } catch (CollaborationCommitException e) when (e.Code == "collaboration_sequence_changed") { return false; } }
    }

    [Fact]
    public async Task DatabaseFailureRollsBackAllCommitRowsAndRetryUsesOriginalIdentity()
    {
        var session = await CommitSession(); var worker = Guid.NewGuid(); await using var db = Open(); var service = Commits(db);
        var lease = await service.Lease(new(session, worker), default); var update = Update(session, worker, lease.Epoch);
        string trigger = db.Database.IsSqlServer()
            ? "CREATE TRIGGER CommitTestRejectOutbox ON CollaborationOutboxEvents AFTER INSERT AS BEGIN THROW 51199, 'intentional test rollback', 1; END"
            : "CREATE TRIGGER CommitTestRejectOutbox BEFORE INSERT ON CollaborationOutboxEvents BEGIN SELECT RAISE(ABORT,'intentional test rollback'); END;";
        await db.Database.ExecuteSqlRawAsync(trigger);
        await Assert.ThrowsAnyAsync<Exception>(() => service.Commit(update, default));
        db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.CollaborationPages.SingleAsync()).CommittedSequence);
        Assert.Empty(await db.CollaborationUpdates.ToArrayAsync()); Assert.Empty(await db.CollaborationReceipts.ToArrayAsync()); Assert.Empty(await db.CollaborationOutboxEvents.ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER CommitTestRejectOutbox");
        Assert.Equal(1, (await service.Commit(update, default)).Receipt.CommitSequence);
    }

    [Fact]
    public async Task SessionOwnerAndInstallationAreTheOnlySourceOfCommitAttribution()
    {
        var firstSession = await CommitSession(); var secondSession = await CommitSession(); var worker = Guid.NewGuid();
        await using var db = Open(); var service = Commits(db); var lease = await service.Lease(new(firstSession, worker), default);
        var update = Update(firstSession, worker, lease.Epoch); var first = await service.Commit(update, default);
        var second = await service.Commit(update with { Session = secondSession, ExpectedSequence = 1 }, default);
        Assert.NotEqual(first.Receipt.InstallationId, second.Receipt.InstallationId);
        Assert.Equal(first.Receipt.UpdateId, second.Receipt.UpdateId); Assert.Equal(2, second.Receipt.CommitSequence);
        Assert.All(await db.CollaborationUpdates.ToArrayAsync(), row => Assert.Equal("owner", row.AuthorUserId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TransactionBoundaryFailureHasOneRecoverableDurableOutcome(bool afterCommit)
    {
        var session = await CommitSession(); var worker = Guid.NewGuid(); CollaborationLease lease;
        await using (var setup = Open()) lease = await Commits(setup).Lease(new(session, worker), default);
        var interceptor = new CommitBoundaryFault(afterCommit);
        await using AppDbContext db = _sql is null
            ? new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(_sqlite!).AddInterceptors(interceptor).Options)
            : new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>(_sql).AddInterceptors(interceptor).Options);
        var update = Update(session, worker, lease.Epoch);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Commits(db).Commit(update, default));
        await using (var inspect = Open())
        {
            int expected = afterCommit ? 1 : 0;
            Assert.Equal(expected, await inspect.CollaborationUpdates.CountAsync()); Assert.Equal(expected, await inspect.CollaborationReceipts.CountAsync());
            Assert.Equal(expected, await inspect.CollaborationOutboxEvents.CountAsync()); Assert.Equal(expected, (await inspect.CollaborationPages.SingleAsync()).CommittedSequence);
        }
        var recovered = await Commits(db).Commit(update, default); Assert.Equal(1, recovered.Receipt.CommitSequence);
        Assert.Single(await db.CollaborationReceipts.ToArrayAsync());
    }

    private sealed class CommitBoundaryFault(bool afterCommit) : DbTransactionInterceptor
    {
        private bool _armed = true;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (_armed && !afterCommit) { _armed = false; throw new InvalidOperationException("intentional pre-commit fault after SaveChanges"); }
            return ValueTask.FromResult(result);
        }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (_armed && afterCommit) { _armed = false; throw new InvalidOperationException("intentional lost transaction acknowledgement"); }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task CommitRechecksEntitlementInsideTheBarrierWithoutUsingTrackedPreflightState()
    {
        var session = await CommitSession(); var worker = Guid.NewGuid(); await using var db = Open();
        var lease = await Commits(db).Lease(new(session, worker), default);
        db.UserEntitlements.Add(new() { UserId = "owner", PlanKey = "standard", SubscriptionStatus = "active" }); await db.SaveChangesAsync();
        // Keep the active row tracked while another context withdraws access. The
        // identity-only lock lookup must not reuse this stale policy at commit.
        {
            await using var other = Open(); await other.UserEntitlements.ExecuteUpdateAsync(x => x.SetProperty(e => e.PlanKey, "free"));
        }
        var tracking = new TrackingPlans(db);
        var authorizer = new CollaborationSessionService(db, _protection, _settings, tracking, new DeletedUserIdentityService(db), _clock);
        var commits = new CollaborationCommitService(db, authorizer, _settings, _clock);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => commits.Commit(Update(session, worker, lease.Epoch), default));
        Assert.Empty(await db.CollaborationUpdates.ToArrayAsync()); Assert.Empty(await db.CollaborationReceipts.ToArrayAsync());
    }

    private sealed class TrackingPlans(AppDbContext db) : IUserEntitlementStore
    {
        public async Task<UserEntitlement> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default)
        {
            Assert.NotNull(db.Database.CurrentTransaction);
            return await db.UserEntitlements.SingleAsync(x => x.UserId == userId, cancellationToken);
        }
    }

    [Fact]
    public async Task FailedCheckpointRestoresLedgerAndPointerWithoutLosingReceipts()
    {
        var session = await CommitSession(); var worker = Guid.NewGuid(); await using var db = Open(); var service = Commits(db);
        var lease = await service.Lease(new(session, worker), default); var update = Update(session, worker, lease.Epoch);
        await service.Commit(update, default);
        string trigger = db.Database.IsSqlServer()
            ? "CREATE TRIGGER CommitTestRejectSnapshot ON CollaborationSnapshots AFTER INSERT AS BEGIN THROW 51199, 'intentional snapshot rollback', 1; END"
            : "CREATE TRIGGER CommitTestRejectSnapshot BEFORE INSERT ON CollaborationSnapshots BEGIN SELECT RAISE(ABORT,'intentional snapshot rollback'); END;";
        await db.Database.ExecuteSqlRawAsync(trigger);
        var checkpoint = new CollaborationCheckpointRequest(session, worker, lease.Epoch, 1, [0, 0], Hash([0, 0]));
        await Assert.ThrowsAnyAsync<Exception>(() => service.Checkpoint(checkpoint, default));
        db.ChangeTracker.Clear();
        var page = await db.CollaborationPages.SingleAsync(); Assert.Equal(0, page.SnapshotSequence); Assert.Equal(1, page.CommittedSequence);
        Assert.Single(await db.CollaborationUpdates.ToArrayAsync()); Assert.Single(await db.CollaborationReceipts.ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER CommitTestRejectSnapshot");
        Assert.Equal(1, await service.Checkpoint(checkpoint, default));
        Assert.Empty(await db.CollaborationUpdates.ToArrayAsync()); Assert.Single(await db.CollaborationReceipts.ToArrayAsync());
        await Assert.ThrowsAsync<CollaborationCommitException>(() => service.Checkpoint(checkpoint with { ExpectedSequence = 0 }, default));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => service.Checkpoint(checkpoint with { State = [1, 0], Sha256 = Hash([1, 0]) }, default));
    }

    [Fact]
    public async Task RealWebSocketCommitReceiptsRecoveryCompactionAndNoLegacyWrites()
    {
        _clock.RealTime = true;
        _settings.CurrentValue.CommitsEnabled = true;
        var start = new ProcessStartInfo("node") { WorkingDirectory = FindRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("WriterApp.Collaboration/tests/schema-fixture.mjs");
        using var fixture = Process.Start(start)!;
        string json = await fixture.StandardOutput.ReadToEndAsync(); string error = await fixture.StandardError.ReadToEndAsync();
        await fixture.WaitForExitAsync(); Assert.True(fixture.ExitCode == 0, error);
        using var metadata = JsonDocument.Parse(json); string fingerprint = metadata.RootElement.GetProperty("support").GetProperty("schemaFingerprint").GetString()!;
        byte[] seed = Convert.FromBase64String(metadata.RootElement.GetProperty("state").GetString()!);
        await using (var db = Open())
        {
            await db.CollaborationPages.ExecuteUpdateAsync(p => p.SetProperty(x => x.SchemaFingerprint, fingerprint));
            await db.CollaborationSnapshots.ExecuteUpdateAsync(p => p.SetProperty(x => x.State, seed).SetProperty(x => x.Sha256, Hash(seed)));
        }
        await RunProbe("commit-probe.mjs", Request with { SchemaFingerprint = fingerprint, RequestCommit = true });
        await using var verify = Open();
        var page = await verify.CollaborationPages.SingleAsync();
        Assert.True(page.CommittedSequence >= 3); Assert.True(page.SnapshotSequence >= 1);
        Assert.Equal(page.CommittedSequence, await verify.CollaborationReceipts.CountAsync());
        Assert.Equal(page.CommittedSequence, await verify.CollaborationOutboxEvents.CountAsync());
        Assert.Equal("<p>Legacy retained</p>", (await verify.Pages.SingleAsync()).Content);
    }
}
