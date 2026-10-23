using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private string? _database;
    private readonly Guid _document = Guid.NewGuid(), _page = Guid.NewGuid(), _other = Guid.NewGuid();
    private readonly Plans _plans = new();
    private readonly CollaborationConversionOptions _options = new() { Enabled = true };
    private ICollaborationCodec _codec = null!;
    private string _version = "";
    private sealed class Host : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = FindRoot();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        private static string FindRoot() { var p = new DirectoryInfo(AppContext.BaseDirectory); while (p is not null && !File.Exists(Path.Combine(p.FullName, "BlazorApp.csproj"))) p = p.Parent; return p!.FullName; }
    }
    private sealed class Plans : IUserEntitlementStore
    {
        public bool Paid = true;
        public HashSet<string> UnpaidUsers = [];
        public Task<UserEntitlement> GetOrCreateAsync(string id, CancellationToken ct = default) => Task.FromResult(new UserEntitlement { UserId = id, PlanKey = Paid && !UnpaidUsers.Contains(id) ? "standard" : "free", SubscriptionStatus = "active" });
    }
    private sealed class SessionOptions : IOptionsMonitor<CollaborationSessionOptions>
    {
        public CollaborationSessionOptions CurrentValue { get; } = new() { SessionsEnabled = true, CommitsEnabled = true, Environment = "test", ServiceKey = new string('s', 64), WebSocketUrl = "ws://127.0.0.1:1234" };
        public CollaborationSessionOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<CollaborationSessionOptions, string?> listener) => null;
    }
    private sealed class FaultCodec(ICollaborationCodec inner) : ICollaborationCodec
    {
        public int Calls;
        public async Task<CollaborationCodecResult> Seed(string content, string format, CancellationToken ct)
        { if (++Calls == 2) throw new IOException("owned migration interruption"); return await inner.Seed(content, format, ct); }
        public Task<CollaborationCodecResult> Project(byte[][] updates, CancellationToken ct) => inner.Project(updates, ct);
    }
    private sealed class RaceCodec(ICollaborationCodec inner, Func<Task> mutate) : ICollaborationCodec
    {
        private bool _changed;
        public async Task<CollaborationCodecResult> Seed(string content, string format, CancellationToken ct)
        { if (!_changed) { _changed = true; await mutate(); } return await inner.Seed(content, format, ct); }
        public Task<CollaborationCodecResult> Project(byte[][] updates, CancellationToken ct) => inner.Project(updates, ct);
    }
    private CollaborationMigrationService Migration(ICollaborationCodec? codec = null) => new(_db, codec ?? _codec, Options.Create(_options), _plans, new DeletedUserIdentityService(_db), TimeProvider.System);
    public async Task InitializeAsync()
    {
        string? server = Environment.GetEnvironmentVariable("WRITERAPP_SYNC_SQLSERVER_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server))
        { _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options); await _db.Database.OpenConnectionAsync(); }
        else
        {
            _database = "WriterApp_ConversionTests_" + Guid.NewGuid().ToString("N");
            _db = new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(new SqlConnectionStringBuilder
            { DataSource = server, InitialCatalog = _database, IntegratedSecurity = true, TrustServerCertificate = true }.ConnectionString).Options);
        }
        await _db.Database.MigrateAsync();
        _codec = new CollaborationCodec(Options.Create(_options), new Host());
        Guid project = Guid.NewGuid(), section = Guid.NewGuid();
        _db.Projects.Add(new() { Id = project, OwnerUserId = "owner" });
        _db.Documents.Add(new() { Id = _document, OwnerUserId = "owner", ProjectId = project, Title = "Synthetic manuscript" });
        _db.Sections.Add(new() { Id = section, DocumentId = _document });
        _db.Pages.AddRange(new() { Id = _page, SectionId = section, DocumentId = _document, Content = "<h2 style=\"text-align:center\" data-indent-level=\"2\">日本語 🧭</h2><p><strong>Bold</strong> and <a href=\"https://example.test\">link</a></p>" },
            new() { Id = _other, SectionId = section, DocumentId = _document, OrderIndex = 1, Content = "<p>Other page</p>" });
        await _db.SaveChangesAsync(); _db.ChangeTracker.Clear();
        _version = (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version;
    }
    public async Task DisposeAsync()
    {
        if (_database is not null && _database.StartsWith("WriterApp_ConversionTests_", StringComparison.Ordinal) && _db.Database.GetDbConnection().Database == _database) await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }
    private async Task<Guid> Convert()
    { var id = Guid.NewGuid(); await Migration().Begin("owner", _document, new(id, _version, true), default); await Migration().Resume("owner", _document, id, default); return id; }

    [Fact]
    public async Task InterruptedMultiPageConversionResumesExactlyOnceAndNeverChangesLegacySource()
    {
        var job = Guid.NewGuid(); var migration = Migration(new FaultCodec(_codec));
        var original = await _db.Pages.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Content).ToArrayAsync();
        await Migration().Begin("owner", _document, new(job, _version, true), default);
        await Assert.ThrowsAsync<IOException>(() => migration.Resume("owner", _document, job, default));
        var first = await _db.CollaborationSnapshots.AsNoTracking().SingleAsync();
        Assert.Equal(CollaborationMode.Migrating, (await _db.Documents.AsNoTracking().SingleAsync()).CollaborationMode);
        await Assert.ThrowsAnyAsync<Exception>(() => _db.Pages.ExecuteUpdateAsync(p => p.SetProperty(x => x.Content, "stale overwrite")));
        await Migration().Resume("owner", _document, job, default);
        await Migration().Resume("owner", _document, job, default);
        Assert.Equal(2, await _db.CollaborationPages.CountAsync());
        Assert.Equal(2, await _db.CollaborationSnapshots.CountAsync());
        Assert.Equal(first.State, (await _db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == first.PageId)).State);
        Assert.Equal(original, await _db.Pages.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Content).ToArrayAsync());
        var snapshot = await CollaborationProjectionService.Read(_db, "owner", _document, new(), default);
        Assert.Equal(2, snapshot.Pages.Length); Assert.Contains("日本語 🧭", snapshot.Pages.Single(p => p.PageId == _page).Text);
        Assert.False(_db.Database.HasPendingModelChanges());
    }
    [Fact]
    public async Task StaleVersionWrongOwnerUnpaidAndDisabledMigrationCannotFenceWriting()
    {
        await Assert.ThrowsAsync<CollaborationCommitException>(() => Migration().Begin("owner", _document, new(Guid.NewGuid(), "stale", true), default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Migration().Begin("other", _document, new(Guid.NewGuid(), _version, true), default));
        _plans.Paid = false;
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Migration().Begin("owner", _document, new(Guid.NewGuid(), _version, true), default));
        _plans.Paid = true; _options.Enabled = false;
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Migration().Begin("owner", _document, new(Guid.NewGuid(), _version, true), default));
        Assert.Empty(await _db.CollaborationMigrationJobs.ToArrayAsync());
        Assert.Equal(CollaborationMode.Legacy, (await _db.Documents.AsNoTracking().SingleAsync()).CollaborationMode);
    }
    [Fact]
    public async Task UnsupportedSourceAndConflictingSceneDoNotFenceTheOriginalManuscript()
    {
        await _db.Pages.Where(p => p.Id == _page).ExecuteUpdateAsync(p => p.SetProperty(x => x.Content, "<p><span>Unsupported source</span></p>"));
        _version = (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version;
        await Assert.ThrowsAsync<CollaborationCommitException>(() => Migration().Begin("owner", _document, new(Guid.NewGuid(), _version, true), default));
        Assert.Equal(CollaborationMode.Legacy, (await _db.Documents.AsNoTracking().SingleAsync()).CollaborationMode);
        await _db.Pages.Where(p => p.Id == _page).ExecuteUpdateAsync(p => p.SetProperty(x => x.Content, "<p>Original page</p>"));
        var doc = await _db.Documents.AsNoTracking().SingleAsync(); var section = await _db.Sections.AsNoTracking().SingleAsync();
        var node = Guid.NewGuid(); _db.ProjectNodes.Add(new() { Id = node, ProjectId = doc.ProjectId, DocumentId = doc.Id, LinkedSectionId = section.Id });
        _db.SceneContents.Add(new() { SceneNodeId = node, ContentJson = "<p>Independent scene writing</p>" }); await _db.SaveChangesAsync();
        _version = (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version;
        var denied = await Assert.ThrowsAsync<CollaborationCommitException>(() => Migration().Begin("owner", _document, new(Guid.NewGuid(), _version, true), default));
        Assert.Equal("collaboration_scene_page_reconciliation_required", denied.Code);
        Assert.Empty(await _db.CollaborationMigrationJobs.ToArrayAsync());
    }
    [Fact]
    public async Task ConcurrentLegacyWriteDuringPreflightKeepsTheNewSourceAndRefusesConversion()
    {
        var codec = new RaceCodec(_codec, () => _db.Pages.Where(p => p.Id == _other).ExecuteUpdateAsync(p => p.SetProperty(x => x.Content, "<p>Concurrent retained writing</p>")));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => Migration(codec).Begin("owner", _document, new(Guid.NewGuid(), _version, true), default));
        Assert.Equal(CollaborationMode.Legacy, (await _db.Documents.AsNoTracking().SingleAsync()).CollaborationMode);
        Assert.Contains("Concurrent retained", (await _db.Pages.AsNoTracking().SingleAsync(p => p.Id == _other)).Content);
        Assert.Empty(await _db.CollaborationMigrationJobs.ToArrayAsync());
    }
    [Fact]
    public async Task ProjectionFailureAndLagCannotBlessOldContentThenRetryPublishesAtomically()
    {
        await Convert(); var page = await _db.CollaborationPages.SingleAsync(p => p.PageId == _page);
        var seed = await _codec.Seed("<p>Committed replacement fixture</p>", "Html", default);
        // Simulate one validated committed prefix; the transport commit/receipt transaction has its own real-process suite.
        page.CommittedSequence = 1; page.SnapshotSequence = 1;
        _db.CollaborationSnapshots.Add(new() { PageId = page.PageId, Generation = page.Generation, Sequence = 1, State = seed.State, Sha256 = seed.Sha256 });
        _db.CollaborationOutboxEvents.Add(new() { Id = Guid.NewGuid(), PageId = page.PageId, Generation = page.Generation, CommitSequence = 1 });
        await _db.SaveChangesAsync();
        var pending = await Assert.ThrowsAsync<CollaborationCommitException>(() => CollaborationProjectionService.Read(_db, "owner", _document, new(), default));
        Assert.Equal("collaboration_projection_pending", pending.Code);
        await _db.CollaborationSnapshots.Where(s => s.PageId == page.PageId && s.Sequence == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.Sha256, new string('0', 64)));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => new CollaborationProjectionService(_db, _codec).Project(page.PageId, page.Generation, default));
        Assert.Null((await _db.CollaborationOutboxEvents.AsNoTracking().SingleAsync()).CompletedAtUnixMilliseconds);
        await _db.CollaborationSnapshots.Where(s => s.PageId == page.PageId && s.Sequence == 1).ExecuteUpdateAsync(s => s.SetProperty(x => x.Sha256, seed.Sha256));
        Assert.True(await new CollaborationProjectionService(_db, _codec).Project(page.PageId, page.Generation, default));
        var read = await CollaborationProjectionService.Read(_db, "owner", _document, new(), default);
        Assert.Contains("Committed replacement", read.Pages.Single(p => p.PageId == _page).Html);
        Assert.NotNull((await _db.CollaborationOutboxEvents.AsNoTracking().SingleAsync()).CompletedAtUnixMilliseconds);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => CollaborationProjectionService.Read(_db, "other", _document, new(), default));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => CollaborationProjectionService.Read(_db, "owner", _document, new(99), default));
    }
    [Fact]
    public async Task RealCommitReceiptAndOutboxSurviveProjectionRollbackAndCompaction()
    {
        await Convert(); var state = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page);
        var settings = new SessionOptions();
        var sessions = new CollaborationSessionService(_db, new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), settings, _plans, new DeletedUserIdentityService(_db), TimeProvider.System);
        var issued = await sessions.Issue("owner", _document, _page, new(state.Generation, Guid.NewGuid(), 2, 1, state.SchemaFingerprint, true), default);
        var request = new CollaborationServiceRequest(issued.Ticket, issued.RoomName, Guid.NewGuid());
        await sessions.Authorize(request with { Acquire = true }, default);
        var commits = new CollaborationCommitService(_db, sessions, settings, TimeProvider.System); var worker = Guid.NewGuid();
        var lease = await commits.Lease(new(request, worker), default);
        var addition = await _codec.Seed("<p>Remote addition fixture</p>", "Html", default);
        var commit = new CollaborationCommitRequest(request, worker, lease.Epoch, 0, Guid.NewGuid(), addition.State, addition.Sha256);
        var receipt = await commits.Commit(commit, default);
        Assert.Equal(1, receipt.Receipt.CommitSequence);
        string installFault = _database is null
            ? "CREATE TRIGGER OwnedProjectionFault BEFORE UPDATE ON CollaborationProjections BEGIN SELECT RAISE(ABORT,'owned_projection_failure'); END;"
            : "CREATE TRIGGER OwnedProjectionFault ON CollaborationProjections AFTER UPDATE AS THROW 51001, 'owned_projection_failure', 1;";
        await _db.Database.ExecuteSqlRawAsync(installFault);
        var projector = new CollaborationProjectionService(_db, _codec);
        await Assert.ThrowsAnyAsync<Exception>(() => projector.Project(_page, state.Generation, default));
        Assert.Null((await _db.CollaborationOutboxEvents.AsNoTracking().SingleAsync()).CompletedAtUnixMilliseconds);
        Assert.Equal(0, (await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page)).ProjectionSequence);
        await _db.Database.ExecuteSqlRawAsync("DROP TRIGGER OwnedProjectionFault"); _db.ChangeTracker.Clear();
        Assert.Equal(1, await projector.Drain(default));
        var snapshot = await CollaborationProjectionService.Read(_db, "owner", _document, new(), default);
        Assert.Contains("Remote addition fixture", snapshot.Pages.Single(p => p.PageId == _page).Text);
        Assert.Equal(receipt, await commits.Commit(commit, default));
        var basis = await _db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == _page && s.Sequence == 0);
        var merged = await _codec.Project([basis.State, addition.State], default);
        await commits.Checkpoint(new(request, worker, lease.Epoch, 1, merged.State, merged.Sha256), default);
        Assert.Empty(await _db.CollaborationUpdates.ToArrayAsync()); Assert.Single(await _db.CollaborationReceipts.ToArrayAsync());
        Assert.True(await projector.Project(_page, state.Generation, default));
        Assert.Contains("Remote addition fixture", (await CollaborationProjectionService.Read(_db, "owner", _document, new(), default)).Pages.Single(p => p.PageId == _page).Text);
    }
    [Fact]
    public async Task TombstoneIsIdempotentFencesGenerationAndRetainsRecoveryAndReceipts()
    {
        await Convert(); var page = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page);
        var command = new CollaborationDeletePage(Guid.NewGuid(), 1, _page, page.Generation);
        Assert.Equal(2, await CollaborationProjectionService.DeletePage(_db, "owner", _document, command, default));
        Assert.Equal(2, await CollaborationProjectionService.DeletePage(_db, "owner", _document, command, default));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => CollaborationProjectionService.DeletePage(_db, "owner", _document, command with { Generation = Guid.NewGuid() }, default));
        var read = await CollaborationProjectionService.Read(_db, "owner", _document, new(2), default);
        Assert.Single(read.Pages); Assert.Equal(_other, read.Pages[0].PageId);
        Assert.Equal(2, await _db.CollaborationSnapshots.CountAsync()); Assert.NotEmpty((await _db.CollaborationMigrationJobs.SingleAsync()).RecoverySnapshot);
        await Assert.ThrowsAnyAsync<Exception>(() => _db.Documents.ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, CollaborationMode.Legacy)));
    }
}
