using System.Data;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed record CollaborationIntegrityFinding(string Code, Guid? PageId = null, bool Warning = false);
public sealed record CollaborationIntegrityReport(Guid DocumentId, bool IntegrityValid, bool ConsistentReadReady,
    int Generations, long Receipts, long PendingProjections, CollaborationIntegrityFinding[] Findings);
public sealed record CollaborationRestoreFence(int Sessions, int Leases, int Members, int Invitations, int SupportingLinks, string RequiredEnvironment);
public sealed record CollaborationStoppedFeatures(bool Sessions = false, bool Commits = false, bool Sharing = false, bool Interactions = false, bool Conversion = false);

// Offline operations only; deliberately not registered as an HTTP endpoint or background writer.
public sealed class CollaborationOperations(AppDbContext db, ICollaborationCodec codec)
{
    public Task<CollaborationIntegrityReport> Inspect(Guid documentId, CancellationToken ct) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var findings = new List<CollaborationIntegrityFinding>();
        void Error(string code, Guid? page = null) => findings.Add(new(code, page));
        var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null) return new CollaborationIntegrityReport(documentId, false, false, 0, 0, 0, [new("document_missing")]);
        if (document.CollaborationMode != CollaborationMode.Collaborative) Error("migration_incomplete");
        var recovery = await db.CollaborationRecoveryStates.AsNoTracking().SingleOrDefaultAsync(ct);
        if (recovery is not null && (recovery.OperationId == Guid.Empty || recovery.RequiredEnvironment != "restore-" + recovery.OperationId.ToString("N"))) Error("recovery_environment_invalid");
        var manifest = await db.CollaborationManifests.AsNoTracking().SingleOrDefaultAsync(m => m.DocumentId == documentId, ct);
        if (manifest is null || manifest.StructureRevision < 1) Error("manifest_missing_or_invalid");
        var job = await db.CollaborationMigrationJobs.AsNoTracking().SingleOrDefaultAsync(j => j.DocumentId == documentId, ct);
        if (job is null || job.CompletedAtUnixMilliseconds is null || CollaborationMigrationService.Hash(job.RecoverySnapshot) != job.SourceSha256.ToLowerInvariant()) Error("migration_recovery_invalid");
        var pages = await db.CollaborationPages.AsNoTracking().Where(p => p.DocumentId == documentId).Take(1001).ToArrayAsync(ct);
        if (pages.Length > 1000) return new CollaborationIntegrityReport(documentId, false, false, pages.Length, 0, 0, [new("generation_limit")]);
        var sources = await db.Pages.AsNoTracking().Where(p => p.DocumentId == documentId).Select(p => p.Id).ToArrayAsync(ct);
        if (sources.Any(id => !pages.Any(p => p.PageId == id)) || pages.Any(p => !sources.Contains(p.PageId))
            || pages.Where(p => !p.IsDeleted).GroupBy(p => p.PageId).Any(g => g.Count() != 1)) Error("page_topology_invalid");
        long receipts = 0, pending = 0;
        foreach (var page in pages)
        {
            if (page.CommittedSequence < 0 || page.CommittedSequence > 9007199254740991 || page.SnapshotSequence < 0 || page.SnapshotSequence > page.CommittedSequence
                || page.ProjectionSequence < 0 || page.ProjectionSequence > page.CommittedSequence) { Error("sequence_invalid", page.PageId); continue; }
            var snapshot = await db.CollaborationSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.PageId == page.PageId && s.Generation == page.Generation && s.Sequence == page.SnapshotSequence, ct);
            var tailQuery = db.CollaborationUpdates.AsNoTracking().Where(u => u.PageId == page.PageId && u.Generation == page.Generation);
            if (snapshot is null || snapshot.State.Length > 16 * 1024 * 1024 || snapshot.State.Length + await tailQuery.SumAsync(u => (long)u.Update.Length, ct) > 16 * 1024 * 1024)
            { Error("snapshot_missing_or_oversized", page.PageId); continue; }
            var tail = await tailQuery.OrderBy(u => u.Sequence).Take(4097).ToArrayAsync(ct);
            if (tail.Length > 4096 || tail.Length != page.CommittedSequence - snapshot.Sequence || CollaborationMigrationService.Hash(snapshot.State) != snapshot.Sha256.ToLowerInvariant()
                || tail.Where((u, i) => u.Sequence != snapshot.Sequence + i + 1 || CollaborationMigrationService.Hash(u.Update) != u.Sha256.ToLowerInvariant()).Any())
            { Error("committed_history_invalid", page.PageId); continue; }
            var receiptQuery = db.CollaborationReceipts.AsNoTracking().Where(r => r.PageId == page.PageId && r.Generation == page.Generation);
            long count = await receiptQuery.LongCountAsync(ct); receipts += count;
            if (count != page.CommittedSequence || await receiptQuery.AnyAsync(r => r.CommitSequence < 1 || r.CommitSequence > page.CommittedSequence, ct)) Error("receipt_coverage_invalid", page.PageId);
            var outbox = db.CollaborationOutboxEvents.AsNoTracking().Where(e => e.PageId == page.PageId && e.Generation == page.Generation);
            if (await outbox.LongCountAsync(ct) != page.CommittedSequence || await outbox.AnyAsync(e => e.CommitSequence < 1 || e.CommitSequence > page.CommittedSequence, ct)) Error("outbox_coverage_invalid", page.PageId);
            pending += await outbox.LongCountAsync(e => e.CompletedAtUnixMilliseconds == null, ct);
            foreach (var update in tail)
                if (!await receiptQuery.AnyAsync(r => r.CommitSequence == update.Sequence && r.UpdateId == update.UpdateId && r.InstallationId == update.InstallationId && r.Sha256 == update.Sha256 && r.CommittedAtUnixMilliseconds == update.CommittedAtUnixMilliseconds, ct)) Error("update_receipt_mismatch", page.PageId);
            try
            {
                byte[][] updates = [snapshot.State, .. tail.Select(t => t.Update)];
                var projected = await codec.Project(updates, ct);
                if (projected.Support.SchemaFingerprint != page.SchemaFingerprint || projected.Support.ProtocolVersion != page.ProtocolVersion || projected.Support.ContentContractVersion != page.ContentContractVersion) Error("schema_mismatch", page.PageId);
                var projection = await db.CollaborationProjections.AsNoTracking().SingleOrDefaultAsync(p => p.PageId == page.PageId && p.Generation == page.Generation, ct);
                if (!page.IsDeleted)
                {
                    if (projection is null || projection.Sequence != page.CommittedSequence || page.ProjectionSequence != projection.Sequence) findings.Add(new("projection_pending", page.PageId, true));
                    else if (projection.StateSha256 != projected.Sha256 || projection.Html != projected.Html || projection.Json != projected.Json.GetRawText() || projection.Text != projected.Text) Error("projection_content_mismatch", page.PageId);
                }
                var comments = await db.CollaborationComments.AsNoTracking().Where(t => t.PageId == page.PageId && t.Generation == page.Generation).Take(201).ToArrayAsync(ct);
                if (comments.Length > 200 || comments.Any(t => t.DocumentId != documentId || t.Revision < 1 || string.IsNullOrWhiteSpace(t.Body) || t.Body.Length > 4000 || (t.Start is null) != (t.End is null))) Error("comments_invalid", page.PageId);
                else
                {
                    var anchors = comments.Where(t => t.Start is not null).Select(t => new CollaborationAnchor(t.Start!, t.End!)).ToArray();
                    if (anchors.Length > 0) await codec.Anchors(updates, anchors, ct, projected.Support);
                }
            }
            catch (CollaborationCommitException) { Error("binary_or_anchor_invalid", page.PageId); }
        }
        await transaction.CommitAsync(ct);
        bool valid = findings.All(f => f.Warning);
        return new CollaborationIntegrityReport(documentId, valid, valid && findings.Count == 0, pages.Length, receipts, pending, findings.ToArray());
    });

    // Must run on the restored copy with ALL application/Node writers stopped. The acknowledgment
    // is an operator precondition, not process discovery or proof that another host has stopped.
    // Restored sharing may predate revocation: revoke it conservatively rather than revive access.
    public Task<CollaborationRestoreFence> FenceRestoredCopy(Guid operationId, bool writersStopped, CollaborationStoppedFeatures features, CancellationToken ct)
    {
        if (operationId == Guid.Empty || !writersStopped || features.Sessions || features.Commits || features.Sharing || features.Interactions || features.Conversion)
            throw new InvalidOperationException("collaboration_restore_requires_stopped_writers_and_features");
        return db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var documents = await db.Documents.Where(d => d.CollaborationMode != CollaborationMode.Legacy).OrderBy(d => d.Id).Select(d => d.Id).ToArrayAsync(ct);
            foreach (var id in documents) await CollaborationMigrationService.Lock(db, id, ct);
            var recovery = await db.CollaborationRecoveryStates.SingleOrDefaultAsync(ct);
            if (recovery?.OperationId == operationId) { await tx.CommitAsync(ct); return new CollaborationRestoreFence(0,0,0,0,0,recovery.RequiredEnvironment); }
            if (recovery is null) { recovery = new(); db.CollaborationRecoveryStates.Add(recovery); }
            recovery.OperationId = operationId; recovery.RequiredEnvironment = "restore-" + operationId.ToString("N"); recovery.SealedAtUnixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            int sessions = await db.CollaborationSessions.Where(s => !s.Revoked).ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true).SetProperty(x => x.Version, x => x.Version + 1).SetProperty(x => x.ConnectionId, (Guid?)null), ct);
            int leases = await db.CollaborationPages.Where(p => p.LeaseOwner != null || p.LeaseExpiresAtUnixMilliseconds != 0)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseExpiresAtUnixMilliseconds, 0).SetProperty(x => x.LeaseEpoch, x => x.LeaseEpoch + 1), ct);
            int members = await db.CollaborationMembers.Where(m => !m.Revoked).ExecuteUpdateAsync(m => m.SetProperty(x => x.Revoked, true).SetProperty(x => x.Revision, x => x.Revision + 1), ct);
            int invitations = await db.CollaborationInvitations.Where(i => !i.Revoked).ExecuteUpdateAsync(i => i.SetProperty(x => x.Revoked, true).SetProperty(x => x.Revision, x => x.Revision + 1), ct);
            int supporting = await db.CollaborationSupportingLinks.Where(l => !l.Revoked).ExecuteUpdateAsync(l => l.SetProperty(x => x.Revoked, true).SetProperty(x => x.Revision, x => x.Revision + 1), ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new CollaborationRestoreFence(sessions, leases, members, invitations, supporting, recovery.RequiredEnvironment);
        });
    }
}
