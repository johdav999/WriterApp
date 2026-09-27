using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed class DocumentSyncTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly EphemeralDataProtectionProvider _protection = new();
    private readonly Plans _plans = new();
    private AppDbContext _db = null!;
    private DocumentSyncService _sync = null!;
    private string? _sqlDatabase;
    public async Task InitializeAsync()
    {
        string? sqlServer = Environment.GetEnvironmentVariable("WRITERAPP_SYNC_SQLSERVER_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(sqlServer))
        {
            await _connection.OpenAsync();
            _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        }
        else
        {
            _sqlDatabase = "WriterApp_SyncTests_" + Guid.NewGuid().ToString("N");
            var connection = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
            { DataSource = sqlServer, InitialCatalog = _sqlDatabase, IntegratedSecurity = true, TrustServerCertificate = true };
            _db = new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(connection.ConnectionString).Options);
        }
        await _db.Database.EnsureCreatedAsync();
        foreach (string sql in DocumentSyncSchemaV1.Install(_sqlDatabase is not null)) await _db.Database.ExecuteSqlRawAsync(sql);
        _sync = new(_db, _plans, _protection, new ProjectDeletionService(_db, NullLogger<ProjectDeletionService>.Instance), new SearchIndexBackfillQueue());
    }
    public async Task DisposeAsync()
    {
        if (_sqlDatabase is not null && _db.Database.GetDbConnection().Database == _sqlDatabase
            && _sqlDatabase.StartsWith("WriterApp_SyncTests_", StringComparison.Ordinal)) await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync(); await _connection.DisposeAsync();
    }
    private static SyncMutation Create(string content = "<p>Local writing</p>") => new(Guid.NewGuid(), null, "upload",
        new("Book", "en", [new(Guid.NewGuid(), "Chapter", 0, null, "en", [new(Guid.NewGuid(), "Page", 0, content)])]));

    [Fact]
    public async Task InitialIncrementalUploadAndReceiptSurviveLostResponse()
    {
        Guid id = Guid.NewGuid(); var request = Create();
        var empty = await _sync.ChangesAsync("paid", null, 1);
        var first = await _sync.MutateAsync("paid", id, request);
        var replay = await _sync.MutateAsync("paid", id, request);
        Assert.Equal(first, replay);
        Assert.Equal(1, await _db.Documents.CountAsync());
        Assert.Equal(1, await _db.Projects.CountAsync());
        var changes = await _sync.ChangesAsync("paid", empty.Cursor, 1);
        Assert.Equal(first.State, Assert.Single(changes.Changes));
        var downloaded = await _sync.DownloadAsync("paid", id);
        Assert.Equal(request.Document!.Sections[0].Pages[0].Content, downloaded.Document!.Sections[0].Pages[0].Content);
        var rename = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), first.State.Version, "rename", Title: "Renamed"));
        Assert.NotEqual(first.State.Version, rename.State.Version);
        Assert.Equal(rename.State, Assert.Single((await _sync.ChangesAsync("paid", changes.Cursor, 50)).Changes));
        Assert.Equal(first, await _sync.MutateAsync("paid", id, request)); // Replay never reverts later edits.
        Assert.Equal("Renamed", (await _sync.DownloadAsync("paid", id)).Document!.Title);
    }

    [Fact]
    public async Task SameOperationWithDifferentBodyIsRejected()
    {
        var request = Create(); Guid id = Guid.NewGuid();
        await _sync.MutateAsync("paid", id, request);
        var error = await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, request with { Title = "different" }));
        Assert.Equal("operation_id_reused", error.Error.Code);
    }

    [Fact]
    public async Task TwoClientsPreserveWinnerAndReportStaleVersionWithCurrentMetadata()
    {
        Guid id = Guid.NewGuid(); var request = Create();
        var original = await _sync.MutateAsync("paid", id, request);
        var a = await _sync.DownloadAsync("paid", id); var b = await _sync.DownloadAsync("paid", id);
        var winner = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), a.State.Version, "rename", Title: "Device A"));
        var conflict = await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, new(Guid.NewGuid(), b.State.Version, "rename", Title: "Device B")));
        Assert.Equal(409, conflict.Status); Assert.Equal(winner.State, conflict.Error.Current);
        Assert.Equal("Device A", (await _sync.DownloadAsync("paid", id)).Document!.Title);
        Assert.NotEqual(original.State.Version, winner.State.Version);
    }

    [Fact]
    public async Task TrashRestorePermanentDeleteAndOldOfflineReplayCannotResurrect()
    {
        Guid id = Guid.NewGuid(); var request = Create();
        var initial = await _sync.MutateAsync("paid", id, request);
        var clientB = await _sync.ChangesAsync("paid", null, 50);
        var trash = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), initial.State.Version, "trash"));
        Assert.True(trash.State.IsTrashed);
        Assert.NotNull((await _sync.DownloadAsync("paid", id)).Document);
        var restored = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), trash.State.Version, "restore"));
        Assert.False(restored.State.IsTrashed);
        trash = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), restored.State.Version, "trash"));
        var delete = new SyncMutation(Guid.NewGuid(), trash.State.Version, "delete");
        var gone = await _sync.MutateAsync("paid", id, delete);
        Assert.True(gone.State.IsDeleted);
        Assert.Equal(gone, await _sync.MutateAsync("paid", id, delete));
        Assert.Equal(gone.State, Assert.Single((await _sync.ChangesAsync("paid", clientB.Cursor, 50)).Changes));
        Assert.Null((await _sync.DownloadAsync("paid", id)).Document);
        Assert.Empty(await _db.Pages.ToListAsync()); Assert.Empty(await _db.Sections.ToListAsync());
        var stale = await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, request with { OperationId = Guid.NewGuid() }));
        Assert.Equal("document_deleted", stale.Error.Code);
        Assert.Equal(initial, await _sync.MutateAsync("paid", id, request));
        Assert.Empty(await _db.Documents.ToListAsync());
    }

    [Fact]
    public async Task WebPageEditsAndBulkDeletesAppearInChangeFeed()
    {
        Guid id = Guid.NewGuid(); var request = Create();
        var created = await _sync.MutateAsync("paid", id, request);
        var cursor = (await _sync.ChangesAsync("paid", null, 50)).Cursor;
        _db.ChangeTracker.Clear();
        var page = await _db.Pages.SingleAsync(); page.Content = "<p>Web edit</p>";
        await _db.SaveChangesAsync();
        var changed = Assert.Single((await _sync.ChangesAsync("paid", cursor, 50)).Changes);
        Assert.NotEqual(created.State.Version, changed.Version);
        var conflict = await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, request with { OperationId = Guid.NewGuid(), ExpectedVersion = created.State.Version }));
        Assert.Equal("version_conflict", conflict.Error.Code);
        await _db.Pages.Where(p => p.DocumentId == id).ExecuteDeleteAsync();
        await _db.Sections.Where(s => s.DocumentId == id).ExecuteDeleteAsync();
        await _db.Documents.Where(d => d.Id == id).ExecuteDeleteAsync();
        Assert.True(Assert.Single((await _sync.ChangesAsync("paid", cursor, 50)).Changes).IsDeleted);
    }

    [Fact]
    public async Task OwnershipAndEntitlementAreCheckedBeforeReplayOrContentAccess()
    {
        Guid id = Guid.NewGuid(); var request = Create();
        await _sync.MutateAsync("paid", id, request);
        Assert.Equal(404, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.DownloadAsync("other", id))).Status);
        Assert.Empty((await _sync.ChangesAsync("other", null, 50)).Changes);
        Assert.Equal(404, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("other", id, request))).Status);
        Assert.Equal(401, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.ChangesAsync("", null, 50))).Status);
        _plans.Paid = false;
        Assert.Equal(403, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, request))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.DownloadAsync("paid", id))).Status);
    }

    [Fact]
    public async Task PagingAndOwnerBoundCursorDoNotSkipChanges()
    {
        Guid firstId = Guid.NewGuid(), secondId = Guid.NewGuid();
        var first = await _sync.MutateAsync("paid", firstId, Create());
        await _sync.MutateAsync("paid", secondId, Create());
        var page = await _sync.ChangesAsync("paid", null, 1);
        Assert.True(page.HasMore);
        await _sync.MutateAsync("paid", firstId, new(Guid.NewGuid(), first.State.Version, "rename", Title: "Moved in feed"));
        var next = await _sync.ChangesAsync("paid", page.Cursor, 100);
        Assert.Equal(2, next.Changes.Count);
        Assert.False(next.HasMore);
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.ChangesAsync("other", page.Cursor, 50))).Status);
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<p onclick='alert(1)'>text</p>")]
    [InlineData("<a href='javascript:alert(1)'>text</a>")]
    [InlineData("<img src='https://example.com'>")]
    public async Task UnsafeOrUnsupportedUploadsNeverMutateDatabase(string html)
    {
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", Guid.NewGuid(), Create(html)))).Status);
        Assert.Empty(await _db.Documents.ToListAsync());
    }

    [Fact]
    public async Task FailedWriteRollsBackContentFeedAndReceiptAndRetrySucceeds()
    {
        Guid id = Guid.NewGuid(); var request = Create();
        await _db.Database.ExecuteSqlRawAsync(_sqlDatabase is null
            ? "CREATE TRIGGER InjectFailure BEFORE INSERT ON DocumentSyncOperations BEGIN SELECT RAISE(ABORT,'injected write failure'); END;"
            : "CREATE TRIGGER InjectFailure ON DocumentSyncOperations INSTEAD OF INSERT AS BEGIN THROW 51001, 'injected write failure', 1; END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => _sync.MutateAsync("paid", id, request));
        Assert.Empty(await _db.Documents.AsNoTracking().ToListAsync());
        Assert.Empty((await _sync.ChangesAsync("paid", null, 50)).Changes);
        await _db.Database.ExecuteSqlRawAsync("DROP TRIGGER InjectFailure;");
        Assert.False((await _sync.MutateAsync("paid", id, request)).State.IsDeleted);
    }

    [Fact]
    public async Task SeparateConnectionsSerializeConcurrentStaleUpdates()
    {
        string path = Path.Combine(Path.GetTempPath(), "WriterApp_SyncConcurrency_" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
            await using var setup = new AppDbContext(options);
            await setup.Database.EnsureCreatedAsync();
            foreach (string sql in DocumentSyncSchemaV1.Install(false)) await setup.Database.ExecuteSqlRawAsync(sql);
            DocumentSyncService Service(AppDbContext db) => new(db, _plans, _protection,
                new ProjectDeletionService(db, NullLogger<ProjectDeletionService>.Instance), new SearchIndexBackfillQueue());
            Guid id = Guid.NewGuid();
            var initial = await Service(setup).MutateAsync("paid", id, Create());
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<int> WriteAsync(string title)
            {
                await using var db = new AppDbContext(options);
                await start.Task;
                try { await Service(db).MutateAsync("paid", id, new(Guid.NewGuid(), initial.State.Version, "rename", Title: title)); return 200; }
                catch (DocumentSyncException error) { return error.Status; }
            }
            Task<int> a = Task.Run(() => WriteAsync("A")), b = Task.Run(() => WriteAsync("B"));
            start.SetResult();
            Assert.Equal(new[] { 200, 409 }, (await Task.WhenAll(a, b)).Order().ToArray());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task MigrationBackfillsExistingDocuments()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        string previous = db.Database.GetMigrations().Reverse().Skip(1).First();
        await migrator.MigrateAsync(previous);
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectRecord { Id = Guid.NewGuid(), OwnerUserId = "paid", Title = "Existing", CreatedUtc = now, UpdatedUtc = now };
        var document = new DocumentRecord { Id = Guid.NewGuid(), ProjectId = project.Id, OwnerUserId = "paid", Title = "Existing", CreatedAt = now, UpdatedAt = now };
        db.Add(project); db.Add(document); await db.SaveChangesAsync();
        await db.Database.MigrateAsync();
        Assert.Equal(document.Id, (await db.DocumentSyncRecords.SingleAsync()).DocumentId);
        Assert.Equal("Existing", (await db.Documents.SingleAsync()).Title);
        Assert.True((await db.DocumentSyncRecords.SingleAsync()).Sequence > 0);
    }

    [Fact]
    public async Task LimitsDuplicateIdentitiesAndOmittedStructureAreRejected()
    {
        var huge = Create(new string('x', DocumentSyncService.MaxRequestBytes));
        Assert.Equal(413, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", Guid.NewGuid(), huge))).Status);
        var request = Create(); var section = request.Document!.Sections[0];
        var duplicate = request with { Document = request.Document with { Sections = [section, section] } };
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", Guid.NewGuid(), duplicate))).Status);
        Guid id = Guid.NewGuid(); var created = await _sync.MutateAsync("paid", id, request);
        var replacement = Create() with { ExpectedVersion = created.State.Version };
        Assert.Equal("structure_removal_not_supported", (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, replacement))).Error.Code);
        Assert.Equal(created.State, (await _sync.DownloadAsync("paid", id)).State);
    }

    [Fact]
    public async Task ControllerReturnsStructuredEntitlementAndConflictErrors()
    {
        var controller = new DocumentSyncController(_sync, new User()) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        Guid id = Guid.NewGuid(); var request = Create();
        Assert.IsType<OkObjectResult>(await controller.Mutate(id, request, default));
        var conflict = Assert.IsType<ObjectResult>(await controller.Mutate(id, request with { OperationId = Guid.NewGuid() }, default));
        Assert.Equal(409, conflict.StatusCode); Assert.Equal("version_conflict", Assert.IsType<SyncError>(conflict.Value).Code);
        _plans.Paid = false;
        var forbidden = Assert.IsType<ObjectResult>(await controller.Changes());
        Assert.Equal(403, forbidden.StatusCode); Assert.Equal("entitlement_required", Assert.IsType<SyncError>(forbidden.Value).Code);
    }

    private sealed class User : IUserIdResolver { public string ResolveUserId(System.Security.Claims.ClaimsPrincipal user) => "paid"; }
    private sealed class Plans : IUserEntitlementStore
    {
        public bool Paid { get; set; } = true;
        public Task<UserEntitlement> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserEntitlement { UserId = userId, PlanKey = Paid ? "standard" : "free", SubscriptionStatus = "active" });
    }
}
