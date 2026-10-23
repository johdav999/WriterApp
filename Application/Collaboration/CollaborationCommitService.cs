using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed record CollaborationLeaseRequest(CollaborationServiceRequest Session, Guid WorkerId, long? Epoch = null);
public sealed record CollaborationLease(long Epoch, long ExpiresAtUnixMilliseconds, long CommittedSequence);
public sealed record CollaborationCommitRequest(CollaborationServiceRequest Session, Guid WorkerId, long Epoch,
    long ExpectedSequence, Guid UpdateId, byte[] Update, string Sha256);
public sealed record CollaborationCommitResult(CollaborationDurableReceipt Receipt, long CommittedSequence);
public sealed record CollaborationCheckpointRequest(CollaborationServiceRequest Session, Guid WorkerId, long Epoch,
    long ExpectedSequence, byte[] State, string Sha256);
public sealed class CollaborationCommitException(string code) : Exception(code)
{ public string Code { get; } = code; }

// Node validates Yjs/schema. This private boundary owns ordering, authorization and durable identity.
public sealed class CollaborationCommitService(AppDbContext db, CollaborationSessionService sessions,
    IOptionsMonitor<CollaborationSessionOptions> options, TimeProvider clock)
{
    public const int MaximumUpdateBytes = 512 * 1024;
    public const int LeaseMilliseconds = 30000;
    private long Now => clock.GetUtcNow().ToUnixTimeMilliseconds();
    private static CollaborationCommitException Deny(string code) => new("collaboration_" + code);

    private async Task<T> Transaction<T>(CollaborationServiceRequest request, Func<CollaborationSessionRecord, CollaborationPageRecord, Task<T>> work, CancellationToken ct)
    {
        if (!options.CurrentValue.CommitsEnabled || request.Acquire) throw Deny("commits_disabled");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var scope = await sessions.ResolveCommitScope(request, ct);
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (db.Database.IsSqlServer())
            {
                // Same document barrier as P2 content/lifecycle fences; session lock serializes revocation/refresh.
                await db.Documents.FromSqlInterpolated($"SELECT * FROM Documents WITH (UPDLOCK,HOLDLOCK) WHERE Id={scope.DocumentId}").AsNoTracking().SingleAsync(ct);
                await db.CollaborationSessions.FromSqlInterpolated($"SELECT * FROM CollaborationSessions WITH (UPDLOCK,HOLDLOCK) WHERE Id={scope.Id}").AsNoTracking().SingleAsync(ct);
                await db.CollaborationPages.FromSqlInterpolated($"SELECT * FROM CollaborationPages WITH (UPDLOCK,HOLDLOCK) WHERE PageId={scope.PageId} AND Generation={scope.Generation}").AsNoTracking().SingleAsync(ct);
            }
            else
            {
                // Acquire SQLite's writer lock before reading authority or sequence inside this transaction.
                await db.CollaborationPages.Where(p => p.PageId == scope.PageId && p.Generation == scope.Generation)
                    .ExecuteUpdateAsync(p => p.SetProperty(x => x.LeaseEpoch, x => x.LeaseEpoch), ct);
            }
            // Entitlement stores use tracked EF queries. Discard the preliminary read so the
            // transaction cannot reuse an entitlement cached before acquiring these barriers.
            db.ChangeTracker.Clear();
            scope = await sessions.Authorize(request, ct);
            if (!scope.CommitAllowed || !options.CurrentValue.CommitsEnabled) throw Deny("commits_disabled");
            var page = await db.CollaborationPages.SingleAsync(p => p.PageId == scope.PageId && p.Generation == scope.Generation, ct);
            T result = await work(scope, page);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    public Task<CollaborationLease> Lease(CollaborationLeaseRequest request, CancellationToken ct) => Transaction(request.Session, (session, page) =>
    {
        if (request.WorkerId == Guid.Empty) throw Deny("lease_invalid");
        string owner = request.WorkerId.ToString("D");
        if (request.Epoch.HasValue)
        {
            RequireLease(page, request.WorkerId, request.Epoch.Value);
        }
        else if (page.LeaseExpiresAtUnixMilliseconds > Now)
        {
            if (page.LeaseOwner != owner) throw Deny("lease_held");
        }
        else
        {
            page.LeaseEpoch = checked(page.LeaseEpoch + 1); page.LeaseOwner = owner;
        }
        page.LeaseExpiresAtUnixMilliseconds = Now + LeaseMilliseconds;
        return Task.FromResult(new CollaborationLease(page.LeaseEpoch, page.LeaseExpiresAtUnixMilliseconds, page.CommittedSequence));
    }, ct);

    public Task<CollaborationCommitResult> Commit(CollaborationCommitRequest request, CancellationToken ct)
    {
        RequireBytes(request.Update, request.Sha256, MaximumUpdateBytes);
        if (request.UpdateId == Guid.Empty || request.ExpectedSequence < 0 || request.ExpectedSequence >= 9007199254740991) throw Deny("commit_invalid");
        return Transaction(request.Session, async (session, page) =>
        {
            RequireLease(page, request.WorkerId, request.Epoch);
            var prior = await db.CollaborationReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.PageId == page.PageId && r.Generation == page.Generation
                && r.InstallationId == session.InstallationId && r.UpdateId == request.UpdateId, ct);
            if (prior is not null)
            {
                if (!prior.Sha256.Equals(request.Sha256, StringComparison.OrdinalIgnoreCase)) throw Deny("update_identity_reused");
                return new CollaborationCommitResult(Receipt(prior), page.CommittedSequence);
            }
            if (page.CommittedSequence != request.ExpectedSequence) throw Deny("sequence_changed");
            long sequence = ++page.CommittedSequence, now = Now;
            string hash = request.Sha256.ToLowerInvariant();
            db.CollaborationUpdates.Add(new() { PageId = page.PageId, Generation = page.Generation, Sequence = sequence,
                InstallationId = session.InstallationId, UpdateId = request.UpdateId, AuthorUserId = session.OwnerUserId,
                Sha256 = hash, Update = request.Update, CommittedAtUnixMilliseconds = now });
            var receipt = new CollaborationReceiptRecord { PageId = page.PageId, Generation = page.Generation,
                InstallationId = session.InstallationId, UpdateId = request.UpdateId, Sha256 = hash, CommitSequence = sequence, CommittedAtUnixMilliseconds = now };
            db.CollaborationReceipts.Add(receipt);
            db.CollaborationOutboxEvents.Add(new() { Id = Guid.NewGuid(), PageId = page.PageId, Generation = page.Generation, CommitSequence = sequence, CreatedAtUnixMilliseconds = now });
            return new CollaborationCommitResult(Receipt(receipt), sequence);
        }, ct);
    }

    public Task<long> Checkpoint(CollaborationCheckpointRequest request, CancellationToken ct)
    {
        RequireBytes(request.State, request.Sha256, 16 * 1024 * 1024);
        return Transaction(request.Session, async (session, page) =>
        {
            RequireLease(page, request.WorkerId, request.Epoch);
            if (page.CommittedSequence != request.ExpectedSequence) throw Deny("sequence_changed");
            var prior = await db.CollaborationSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.PageId == page.PageId && s.Generation == page.Generation && s.Sequence == request.ExpectedSequence, ct);
            if (prior is not null && !prior.Sha256.Equals(request.Sha256, StringComparison.OrdinalIgnoreCase)) throw Deny("snapshot_identity_reused");
            if (prior is null) db.CollaborationSnapshots.Add(new() { PageId = page.PageId, Generation = page.Generation, Sequence = page.CommittedSequence,
                State = request.State, Sha256 = request.Sha256.ToLowerInvariant(), CreatedAtUnixMilliseconds = Now });
            page.SnapshotSequence = page.CommittedSequence;
            // Receipts and outbox deliberately survive compaction; P7 consumes projection work separately.
            await db.CollaborationUpdates.Where(u => u.PageId == page.PageId && u.Generation == page.Generation && u.Sequence <= page.SnapshotSequence).ExecuteDeleteAsync(ct);
            return page.SnapshotSequence;
        }, ct);
    }

    private void RequireLease(CollaborationPageRecord page, Guid worker, long epoch)
    { if (worker == Guid.Empty || page.LeaseOwner != worker.ToString("D") || page.LeaseEpoch != epoch || page.LeaseExpiresAtUnixMilliseconds <= Now) throw Deny("lease_lost"); }
    private static void RequireBytes(byte[]? bytes, string? hash, int maximum)
    {
        if (bytes is null || bytes.Length == 0 || bytes.Length > maximum || hash is null || hash.Length != 64
            || !Convert.ToHexString(SHA256.HashData(bytes)).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw Deny("update_invalid");
    }
    private static CollaborationDurableReceipt Receipt(CollaborationReceiptRecord r) => new(r.PageId, r.Generation, r.InstallationId, r.UpdateId, r.Sha256, r.CommitSequence, r.CommittedAtUnixMilliseconds);
}
