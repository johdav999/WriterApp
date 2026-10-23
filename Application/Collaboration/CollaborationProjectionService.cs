using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed record CollaborationReadPage(Guid PageId, Guid SectionId, int OrderIndex, string Title,
    Guid Generation, long Sequence, string Html, string Text, string Json);
public sealed record CollaborationReadSection(Guid SectionId, string Title, int OrderIndex, string Html);
public sealed record CollaborationReadSnapshot(Guid DocumentId, long StructureRevision, CollaborationReadPage[] Pages, CollaborationReadSection[] Sections, string? Title = null);
public sealed record CollaborationDeletePage(Guid OperationId, long ExpectedStructureRevision, Guid PageId, Guid Generation);

public sealed class CollaborationProjectionService(AppDbContext db, ICollaborationCodec codec, WriterApp.Application.Search.ISearchIndexService? search = null,
    ILogger<CollaborationProjectionService>? logger = null)
{
    // Capture one committed prefix; decode outside the DB lock and publish monotonically within its generation.
    public async Task<bool> Project(Guid pageId, Guid generation, CancellationToken ct)
    {
        var captured = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var page = await db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == pageId && p.Generation == generation, ct);
            var snapshot = await db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == pageId && s.Generation == generation && s.Sequence == page.SnapshotSequence, ct);
            var query = db.CollaborationUpdates.AsNoTracking().Where(u => u.PageId == pageId && u.Generation == generation && u.Sequence > snapshot.Sequence);
            if (snapshot.State.Length > 16 * 1024 * 1024 || snapshot.State.Length + await query.SumAsync(u => (long)u.Update.Length, ct) > 16 * 1024 * 1024)
                throw new CollaborationCommitException("collaboration_projection_source_limit");
            var tail = await query.OrderBy(u => u.Sequence).ToArrayAsync(ct);
            if (page.IsDeleted || tail.Length > 4096 || tail.Length != page.CommittedSequence - snapshot.Sequence
                || CollaborationMigrationService.Hash(snapshot.State) != snapshot.Sha256.ToLowerInvariant()
                || tail.Where((u, i) => u.Sequence != snapshot.Sequence + i + 1 || CollaborationMigrationService.Hash(u.Update) != u.Sha256.ToLowerInvariant()).Any())
                throw new CollaborationCommitException("collaboration_projection_source_invalid");
            await tx.CommitAsync(ct);
            return (Page: page, Updates: new[] { snapshot.State }.Concat(tail.Select(u => u.Update)).ToArray());
        });
        var result = await codec.Project(captured.Updates, ct);
        if (result.Support.SchemaFingerprint != captured.Page.SchemaFingerprint || result.Support.ProtocolVersion != captured.Page.ProtocolVersion
            || result.Support.ContentContractVersion != captured.Page.ContentContractVersion) throw new CollaborationCommitException("collaboration_upgrade_required");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await CollaborationMigrationService.Lock(db, captured.Page.DocumentId, ct);
            var page = await db.CollaborationPages.SingleAsync(p => p.PageId == pageId && p.Generation == generation, ct);
            if (page.IsDeleted || page.CommittedSequence < captured.Page.CommittedSequence) return false;
            var projection = await db.CollaborationProjections.SingleOrDefaultAsync(p => p.PageId == pageId && p.Generation == generation, ct);
            if (projection is null) { projection = new() { PageId = pageId, Generation = generation }; db.CollaborationProjections.Add(projection); }
            if (projection.Sequence > page.CommittedSequence) throw new CollaborationCommitException("collaboration_projection_invalid");
            if (projection.Sequence > captured.Page.CommittedSequence) return true;
            projection.Sequence = captured.Page.CommittedSequence; projection.Html = result.Html; projection.Json = result.Json.GetRawText(); projection.Text = result.Text; projection.StateSha256 = result.Sha256;
            page.ProjectionSequence = projection.Sequence;
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await db.CollaborationOutboxEvents.Where(e => e.PageId == pageId && e.Generation == generation && e.CommitSequence <= projection.Sequence && e.CompletedAtUnixMilliseconds == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.CompletedAtUnixMilliseconds, now).SetProperty(e => e.Attempts, e => e.Attempts + 1), ct);
            await db.SaveChangesAsync(ct);
            if (search is not null)
            {
                var legacy = await db.Pages.AsNoTracking().SingleAsync(p => p.Id == pageId, ct);
                legacy.Content = result.Html; legacy.UpdatedAt = DateTimeOffset.UtcNow;
                await search.UpsertPageAsync(legacy, ct);
            }
            await tx.CommitAsync(ct); return true;
        });
    }
    public async Task<int> Drain(CancellationToken ct)
    {
        var work = await db.CollaborationOutboxEvents.AsNoTracking().Where(e => e.CompletedAtUnixMilliseconds == null)
            .GroupBy(e => new { e.PageId, e.Generation }).Select(g => new { g.Key.PageId, g.Key.Generation, Attempts = g.Max(e => e.Attempts), Created = g.Min(e => e.CreatedAtUnixMilliseconds) })
            .OrderBy(e => e.Attempts).ThenBy(e => e.Created).Take(64).ToArrayAsync(ct);
        int completed = 0;
        foreach (var item in work)
        {
            try { if (await Project(item.PageId, item.Generation, ct)) completed++; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch
            {
                db.ChangeTracker.Clear();
                await db.CollaborationOutboxEvents.Where(e => e.PageId == item.PageId && e.Generation == item.Generation && e.CompletedAtUnixMilliseconds == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(e => e.Attempts, e => e.Attempts < int.MaxValue ? e.Attempts + 1 : e.Attempts), ct);
                logger?.LogWarning("Collaboration projection retained for retry; no writing discarded.");
            }
        }
        return completed;
    }
    public static Task<CollaborationReadSnapshot> Read(AppDbContext db, string owner, Guid id, CollaborationReadRequest request, CancellationToken ct,
        CollaborationAccessService? access = null, CollaborationOperation operation = CollaborationOperation.Read) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            if (access is not null) await access.Require(owner, id, operation, ct);
            var document = await db.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id && (access != null || d.OwnerUserId == owner) && d.DeletedAtUtc == null && !d.IsArchived, ct);
            if (document is null || await db.DeletedUserIdentities.AnyAsync(u => u.UserId == owner, ct)) throw new CollaborationSessionDeniedException();
            if (document.CollaborationMode != CollaborationMode.Collaborative) throw new CollaborationCommitException("collaboration_migration_incomplete");
            var manifest = await db.CollaborationManifests.AsNoTracking().SingleOrDefaultAsync(m => m.DocumentId == id, ct) ?? throw new CollaborationCommitException("collaboration_projection_pending");
            if (request.StructureRevision is { } revision && revision != manifest.StructureRevision) throw new CollaborationCommitException("collaboration_structure_changed");
            var states = await db.CollaborationPages.AsNoTracking().Where(p => p.DocumentId == id && !p.IsDeleted).ToArrayAsync(ct);
            if (states.Length > 1000 || await db.CollaborationProjections.Where(p => db.CollaborationPages.Any(s => s.DocumentId == id && s.PageId == p.PageId && s.Generation == p.Generation && !s.IsDeleted))
                .SumAsync(p => (long)p.Html.Length + p.Json.Length + p.Text.Length, ct) > 32 * 1024 * 1024)
                throw new CollaborationCommitException("collaboration_read_limit");
            if (request.Required is { } required && (required.Length != states.Length || required.Select(r => r.PageId).Distinct().Count() != required.Length
                || required.Any(r => r.Sequence < 0 || !states.Any(s => s.PageId == r.PageId && s.Generation == r.Generation && s.CommittedSequence >= r.Sequence))))
                throw new CollaborationCommitException("collaboration_watermark_invalid");
            var pages = await (from p in db.Pages.AsNoTracking() join s in db.CollaborationPages.AsNoTracking() on p.Id equals s.PageId
                join projection in db.CollaborationProjections.AsNoTracking() on new { s.PageId, s.Generation } equals new { projection.PageId, projection.Generation }
                where p.DocumentId == id && s.DocumentId == id && !s.IsDeleted
                orderby p.SectionId, p.OrderIndex, p.Id
                select new { Page = p, State = s, Projection = projection }).ToArrayAsync(ct);
            // A latest consistent snapshot is stronger than a requested lower bound. Never mix stale pages.
            if (pages.Length != states.Length || pages.Any(p => p.Projection.Sequence != p.State.CommittedSequence || p.State.ProjectionSequence != p.Projection.Sequence))
                throw new CollaborationCommitException("collaboration_projection_pending");
            var sections = await db.Sections.AsNoTracking().Where(s => s.DocumentId == id && db.Pages.Any(p=>p.SectionId==s.Id && db.CollaborationPages.Any(c=>c.PageId==p.Id && !c.IsDeleted))).OrderBy(s => s.OrderIndex).ThenBy(s => s.Id).ToArrayAsync(ct);
            var sectionOrder = sections.Select((s, index) => (s.Id, index)).ToDictionary(x => x.Id, x => x.index);
            var readPages = pages.OrderBy(p => sectionOrder[p.Page.SectionId]).ThenBy(p => p.Page.OrderIndex).ThenBy(p => p.Page.Id)
                .Select(p => new CollaborationReadPage(p.Page.Id, p.Page.SectionId, p.Page.OrderIndex, p.Page.Title, p.State.Generation, p.Projection.Sequence, p.Projection.Html, p.Projection.Text, p.Projection.Json)).ToArray();
            var result = new CollaborationReadSnapshot(id, manifest.StructureRevision, readPages,
                sections.Select(s => new CollaborationReadSection(s.Id, s.Title, s.OrderIndex, string.Join("\n", readPages.Where(p => p.SectionId == s.Id).Select(p => p.Html)))).ToArray(), document.Title);
            if (tx is not null) await tx.CommitAsync(ct); return result;
        });

    // Minimum safe lifecycle command: a tombstone fences active and offline sessions, retains all recovery bytes.
    public static Task<long> DeletePage(AppDbContext db, string owner, Guid id, CollaborationDeletePage request, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            if (request.OperationId == Guid.Empty || request.ExpectedStructureRevision < 1) throw new CollaborationCommitException("collaboration_command_invalid");
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await CollaborationMigrationService.Lock(db, id, ct);
            if (!await db.Documents.AnyAsync(d => d.Id == id && d.OwnerUserId == owner && d.CollaborationMode == CollaborationMode.Collaborative && !d.IsArchived && d.DeletedAtUtc == null, ct)) throw new CollaborationSessionDeniedException();
            string hash = CollaborationMigrationService.Hash(JsonSerializer.SerializeToUtf8Bytes(new { id, request }, CollaborationMigrationService.Json));
            var prior = await db.CollaborationCommands.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.OperationId, ct);
            if (prior is not null)
            {
                if (prior.DocumentId != id || prior.RequestSha256 != hash) throw new CollaborationCommitException("collaboration_command_identity_reused");
                return prior.StructureRevision;
            }
            var manifest = await db.CollaborationManifests.SingleAsync(m => m.DocumentId == id, ct);
            if (manifest.StructureRevision != request.ExpectedStructureRevision) throw new CollaborationCommitException("collaboration_structure_changed");
            var page = await db.CollaborationPages.SingleOrDefaultAsync(p => p.DocumentId == id && p.PageId == request.PageId && p.Generation == request.Generation && !p.IsDeleted, ct)
                ?? throw new CollaborationCommitException("collaboration_generation_changed");
            page.IsDeleted = true; page.LeaseEpoch++; page.LeaseOwner = null; page.LeaseExpiresAtUnixMilliseconds = 0;
            manifest.StructureRevision++;
            string indexedPage = IdNorm.Norm(request.PageId);
            await db.SearchIndexEntries.Where(e => e.PageId == indexedPage).ExecuteDeleteAsync(ct);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            await db.CollaborationOutboxEvents.Where(e => e.PageId == request.PageId && e.Generation == request.Generation && e.CompletedAtUnixMilliseconds == null)
                .ExecuteUpdateAsync(s => s.SetProperty(e => e.CompletedAtUnixMilliseconds, now), ct);
            await db.CollaborationSessions.Where(s => s.DocumentId == id && s.PageId == request.PageId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Revoked, true), ct);
            db.CollaborationCommands.Add(new() { Id = request.OperationId, DocumentId = id, RequestSha256 = hash, StructureRevision = manifest.StructureRevision });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return manifest.StructureRevision;
        });
}

public sealed class CollaborationProjectionWorker(IServiceScopeFactory scopes, ILogger<CollaborationProjectionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CollaborationConversionOptions>>().Value.Enabled)
                    await scope.ServiceProvider.GetRequiredService<CollaborationProjectionService>().Drain(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { logger.LogWarning("Collaboration projection work pending; committed writing remains retained."); }
            try { await Task.Delay(1000, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
