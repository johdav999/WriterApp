using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
using WriterApp.Data.Documents;
using WriterApp.Shared.Collaboration;

namespace WriterApp.Application.Collaboration;

public sealed record CollaborationMigrationRequest(Guid OperationId, string ExpectedVersion, bool SynchronizedAndConflictsResolved);
public sealed record CollaborationMigrationProgress(Guid JobId, CollaborationMode Mode, int ReadyPages, int TotalPages, string? FailureCode);

// No mode downgrade or legacy content bypass exists. Recovery retains the exact source and immutable page seeds.
public sealed class CollaborationMigrationService(AppDbContext db, ICollaborationCodec codec,
    IOptions<CollaborationConversionOptions> options, IUserEntitlementStore entitlements,
    IDeletedUserIdentityService deletedUsers, TimeProvider clock)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record SourcePage(Guid Id, Guid SectionId, string Title, int OrderIndex, string Content);
    private sealed record Source(Guid DocumentId, Guid ProjectId, string Title, string OwnerUserId,
        JsonElement Sections, JsonElement Outline, SourcePage[] Pages, SourceScene[] Scenes, JsonElement Metadata);
    private sealed record SourceScene(Guid Id, Guid? SectionId, string Content);
    private static CollaborationCommitException Deny(string code) => new("collaboration_" + code);
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private async Task<DocumentRecord> Owner(string owner, Guid id, CancellationToken ct)
    {
        if (!options.Value.Enabled || await deletedUsers.IsDeletedAsync(owner, ct)
            || !EntitlementAccessEvaluator.Evaluate(await entitlements.GetOrCreateAsync(owner, ct)).IsPaidAccessActive) throw new CollaborationSessionDeniedException();
        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == id && d.OwnerUserId == owner, ct);
        if (document is null || document.DeletedAtUtc is not null || document.IsArchived || document.DocumentKind != DocumentKind.Manuscript) throw new CollaborationSessionDeniedException();
        return document;
    }
    internal static async Task Lock(AppDbContext db, Guid id, CancellationToken ct)
    {
        if (db.Database.IsSqlServer()) await db.Documents.FromSqlInterpolated($"SELECT * FROM Documents WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").AsNoTracking().ToArrayAsync(ct);
        else await db.CollaborationMigrationJobs.Where(j => j.DocumentId == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.FailureCode, j => j.FailureCode), ct);
        db.ChangeTracker.Clear();
    }
    private async Task<byte[]> Capture(DocumentRecord d, CancellationToken ct)
    {
        var sections = await db.Sections.AsNoTracking().Where(s => s.DocumentId == d.Id).OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Title, s.OrderIndex, s.NarrativePurpose, s.LanguageCode, s.TranslationGroupId }).ToArrayAsync(ct);
        var outline = await db.DocumentOutlineNodes.AsNoTracking().Where(n => n.DocumentId == d.Id).OrderBy(n => n.Id).ToArrayAsync(ct);
        var pages = await db.Pages.AsNoTracking().Where(p => p.DocumentId == d.Id).OrderBy(p => p.Id)
            .Select(p => new SourcePage(p.Id, p.SectionId, p.Title, p.OrderIndex, p.Content)).ToArrayAsync(ct);
        var nodes = await db.ProjectNodes.IgnoreQueryFilters().AsNoTracking().Where(n => n.DocumentId == d.Id || n.DocumentId == null && n.ProjectId == d.ProjectId).OrderBy(n => n.Id).ToArrayAsync(ct);
        var nodeIds = nodes.Select(n => n.Id).ToArray();
        var sceneRows = await db.SceneContents.AsNoTracking().Where(s => nodeIds.Contains(s.SceneNodeId)).OrderBy(s => s.SceneNodeId).ToArrayAsync(ct);
        var scenes = sceneRows.Select(s => new SourceScene(s.SceneNodeId, nodes.Single(n => n.Id == s.SceneNodeId).LinkedSectionId, s.ContentJson)).ToArray();
        var synopsis = await db.DocumentSynopses.AsNoTracking().SingleOrDefaultAsync(s => s.DocumentId == d.Id, ct);
        if (pages.Length is 0 or > 1000 || sections.Length == 0 || pages.Any(p => !sections.Any(s => s.Id == p.SectionId))) throw Deny("source_invalid");
        return JsonSerializer.SerializeToUtf8Bytes(new Source(d.Id, d.ProjectId, d.Title, d.OwnerUserId,
            JsonSerializer.SerializeToElement(sections, Json), JsonSerializer.SerializeToElement(outline, Json), pages, scenes,
            JsonSerializer.SerializeToElement(new { d.LanguageCode, d.TranslationGroupId, d.CreatedAt, d.UpdatedAt, d.DocumentKind, synopsis, nodes }, Json)), Json);
    }
    private static string Format(string content) => content.TrimStart().StartsWith('{') ? "LegacyJson" : content.TrimStart().StartsWith('<') || content.Length == 0 ? "Html" : "LegacyText";
    private async Task RequireReconciledScenes(byte[] bytes, CancellationToken ct)
    {
        var source = JsonSerializer.Deserialize<Source>(bytes, Json)!;
        // SceneContents and Pages used to be independent writers. Never choose one over conflicting prose.
        foreach (var group in source.Scenes.Where(s => s.Content.Length > 0).GroupBy(s => s.SectionId))
        {
            if (group.Key is null || group.Count() != 1) throw Deny("scene_page_reconciliation_required");
            var scene = group.Single(); var sceneDoc = await codec.Seed(scene.Content, Format(scene.Content), ct);
            var content = new List<JsonElement>();
            foreach (var page in source.Pages.Where(p => p.SectionId == group.Key).OrderBy(p => p.OrderIndex).ThenBy(p => p.Id))
                content.AddRange((await codec.Seed(page.Content, Format(page.Content), ct)).Json.GetProperty("content").EnumerateArray().Select(c => c.Clone()));
            if (!JsonElement.DeepEquals(sceneDoc.Json.GetProperty("content"), JsonSerializer.SerializeToElement(content, Json)))
                throw Deny("scene_page_reconciliation_required");
        }
    }
    public async Task<CollaborationMigrationProgress> Begin(string owner, Guid id, CollaborationMigrationRequest request, CancellationToken ct)
    {
        if (request.OperationId == Guid.Empty || !request.SynchronizedAndConflictsResolved || string.IsNullOrWhiteSpace(request.ExpectedVersion) || request.ExpectedVersion.Length > 128) throw Deny("migration_preconditions");
        db.ChangeTracker.Clear();
        var preflightDocument = await Owner(owner, id, ct);
        byte[]? preflight = null;
        if (!await db.CollaborationMigrationJobs.AsNoTracking().AnyAsync(j => j.DocumentId == id, ct))
        {
            preflight = await Capture(preflightDocument, ct);
            if (preflight.Length > 32 * 1024 * 1024) throw Deny("source_limit");
            // No database lock is held while parsing rich source. The transaction rechecks its exact hash and version.
            foreach (var page in JsonSerializer.Deserialize<Source>(preflight, Json)!.Pages) await codec.Seed(page.Content, Format(page.Content), ct);
            await RequireReconciledScenes(preflight, ct);
        }
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await Lock(db, id, ct); var d = await Owner(owner, id, ct);
            var prior = await db.CollaborationMigrationJobs.SingleOrDefaultAsync(j => j.DocumentId == id, ct);
            if (prior is not null)
            {
                if (prior.Id != request.OperationId || prior.SourceVersion != request.ExpectedVersion) throw Deny("migration_identity_reused");
                return await Progress(d, prior, ct);
            }
            if (d.CollaborationMode != CollaborationMode.Legacy || await db.CollaborationPages.AnyAsync(p => p.DocumentId == id, ct)) throw Deny("migration_state_invalid");
            var version = await db.DocumentSyncRecords.AsNoTracking().Where(s => s.DocumentId == id && !s.IsDeleted && !s.IsTrashed).Select(s => s.Version).SingleOrDefaultAsync(ct);
            if (version != request.ExpectedVersion) throw Deny("source_version_changed");
            var source = await Capture(d, ct);
            if (preflight is null || Hash(source) != Hash(preflight)) throw Deny("source_changed");
            var job = new CollaborationMigrationJobRecord { Id = request.OperationId, DocumentId = id, SourceVersion = version,
                SourceSha256 = Hash(source), RecoverySnapshot = source, CreatedAtUnixMilliseconds = clock.GetUtcNow().ToUnixTimeMilliseconds() };
            db.CollaborationMigrationJobs.Add(job);
            d.CollaborationMode = CollaborationMode.Migrating; d.CollaborationMigrationState = CollaborationMigrationState.Preparing;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Progress(d, job, ct);
        });
    }
    public async Task<CollaborationMigrationProgress> Resume(string owner, Guid id, Guid jobId, CancellationToken ct)
    {
        db.ChangeTracker.Clear(); var d = await Owner(owner, id, ct);
        var job = await db.CollaborationMigrationJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == jobId && j.DocumentId == id, ct) ?? throw Deny("migration_unknown");
        if (Hash(job.RecoverySnapshot) != job.SourceSha256) throw Deny("recovery_corrupt");
        if (job.CompletedAtUnixMilliseconds is not null) return await Progress(d, job, ct);
        var source = JsonSerializer.Deserialize<Source>(job.RecoverySnapshot, Json) ?? throw Deny("recovery_corrupt");
        try
        {
            foreach (var page in source.Pages)
            {
                if (await db.CollaborationPages.AsNoTracking().AnyAsync(p => p.PageId == page.Id, ct)) continue;
                var seed = await codec.Seed(page.Content, Format(page.Content), ct);
                await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                    await Lock(db, id, ct); var current = await Owner(owner, id, ct);
                    if (current.CollaborationMode != CollaborationMode.Migrating || Hash(await Capture(current, ct)) != job.SourceSha256) throw Deny("source_changed");
                    if (await db.CollaborationPages.AnyAsync(p => p.PageId == page.Id, ct)) return;
                    var generation = Guid.NewGuid();
                    db.CollaborationPages.Add(new() { DocumentId = id, PageId = page.Id, Generation = generation, ContentContractVersion = seed.Support.ContentContractVersion,
                        ProtocolVersion = seed.Support.ProtocolVersion, SchemaFingerprint = seed.Support.SchemaFingerprint });
                    db.CollaborationSnapshots.Add(new() { PageId = page.Id, Generation = generation, State = seed.State, Sha256 = seed.Sha256, CreatedAtUnixMilliseconds = clock.GetUtcNow().ToUnixTimeMilliseconds() });
                    db.CollaborationProjections.Add(new() { PageId = page.Id, Generation = generation, Html = seed.Html, Json = seed.Json.GetRawText(), Text = seed.Text, StateSha256 = seed.Sha256 });
                    current.CollaborationMigrationState = CollaborationMigrationState.Converting;
                    await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
                });
            }
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                await Lock(db, id, ct); var current = await Owner(owner, id, ct);
                var saved = await db.CollaborationMigrationJobs.SingleAsync(j => j.Id == jobId && j.DocumentId == id, ct);
                if (saved.CompletedAtUnixMilliseconds is not null) return await Progress(current, saved, ct);
                var states = await db.CollaborationPages.AsNoTracking().Where(p => p.DocumentId == id).ToArrayAsync(ct);
                if (current.CollaborationMode != CollaborationMode.Migrating || Hash(await Capture(current, ct)) != job.SourceSha256
                    || states.Length != source.Pages.Length || states.Select(p => p.SchemaFingerprint).Distinct().Count() != 1
                    || states.Any(p => p.IsDeleted || p.CommittedSequence != 0 || p.ProtocolVersion != 1 || p.ContentContractVersion != 2 || !source.Pages.Any(s => s.Id == p.PageId))) throw Deny("migration_incomplete");
                foreach (var state in states)
                {
                    var snapshot = await db.CollaborationSnapshots.AsNoTracking().SingleOrDefaultAsync(s => s.PageId == state.PageId && s.Generation == state.Generation && s.Sequence == 0, ct);
                    if (snapshot is null || Hash(snapshot.State) != snapshot.Sha256
                        || !await db.CollaborationProjections.AnyAsync(p => p.PageId == state.PageId && p.Generation == state.Generation && p.Sequence == 0 && p.StateSha256 == snapshot.Sha256, ct)) throw Deny("migration_incomplete");
                }
                current.CollaborationMode = CollaborationMode.Collaborative; current.CollaborationMigrationState = CollaborationMigrationState.Completed;
                saved.CompletedAtUnixMilliseconds = clock.GetUtcNow().ToUnixTimeMilliseconds(); saved.FailureCode = null;
                db.CollaborationManifests.Add(new() { DocumentId = id });
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Progress(current, saved, ct);
            });
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            db.ChangeTracker.Clear();
            await db.CollaborationMigrationJobs.Where(j => j.Id == jobId && j.CompletedAtUnixMilliseconds == null).ExecuteUpdateAsync(s => s.SetProperty(j => j.FailureCode, "collaboration_conversion_failed"), ct);
            throw;
        }
    }
    private async Task<CollaborationMigrationProgress> Progress(DocumentRecord d, CollaborationMigrationJobRecord job, CancellationToken ct) =>
        new(job.Id, d.CollaborationMode, await db.CollaborationPages.CountAsync(p => p.DocumentId == d.Id, ct),
            await db.Pages.CountAsync(p => p.DocumentId == d.Id, ct), job.FailureCode);
}
