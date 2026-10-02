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

public sealed partial class DocumentSyncTests : IAsyncLifetime
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
        foreach (string sql in ProjectSyncSchemaV2.Install(_sqlDatabase is not null)) await _db.Database.ExecuteSqlRawAsync(sql);
        foreach (string sql in PlanningSyncSchemaV3.Install(_sqlDatabase is not null)) await _db.Database.ExecuteSqlRawAsync(sql);
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

    [Theory]
    [MemberData(nameof(DeviceContentCompatibilityTests.Supported), MemberType = typeof(DeviceContentCompatibilityTests))]
    public async Task SharedCompatibilityContentRoundTripsThroughRealBackend(string html)
    {
        Guid id = Guid.NewGuid();
        await _sync.MutateAsync("paid", id, Create(html));
        var downloaded = await _sync.DownloadAsync("paid", id);
        Assert.Equal(html, downloaded.Document!.Sections[0].Pages[0].Content);
        var local = WriterApp.Device.Shared.Services.DeviceSyncMapping.Download(downloaded, Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var upload = WriterApp.Device.Shared.Services.DeviceSyncMapping.Upload(local);
        await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), downloaded.State.Version, "upload", upload));
        Assert.Equal(html, (await _sync.DownloadAsync("paid", id)).Document!.Sections[0].Pages[0].Content);
    }

    [Theory]
    [InlineData("<table class=\"future\"><tr><td>Original 日本語</td></tr></table>")]
    [InlineData("<p><img src=\"/original.png\" data-future=\"keep\"></p>")]
    [InlineData("{\"type\":\"doc\",\"futureMetadata\":true}")]
    public async Task ExistingRichOrLegacySourcePassesThroughWithoutSanitization(string source)
    {
        Guid id = Guid.NewGuid();
        await _sync.MutateAsync("paid", id, Create());
        var page = await _db.Set<PageRecord>().SingleAsync();
        page.Content = source; // Existing web/legacy content, outside device upload restrictions.
        await _db.SaveChangesAsync();
        var original = await _sync.DownloadAsync("paid", id);
        var local = WriterApp.Device.Shared.Services.DeviceSyncMapping.Download(original, Guid.NewGuid(), null, DateTimeOffset.UtcNow);
        var upload = WriterApp.Device.Shared.Services.DeviceSyncMapping.Upload(local) with { Title = "Metadata edit" };
        await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), original.State.Version, "upload", upload));
        var preserved = await _sync.DownloadAsync("paid", id);
        Assert.Equal(source, preserved.Document!.Sections[0].Pages[0].Content);
        var changed = upload with { Sections = upload.Sections.Select(s => s with
            { Pages = s.Pages.Select(p => p with { Content = source + " changed" }).ToArray() }).ToArray() };
        await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, new(Guid.NewGuid(), preserved.State.Version, "upload", changed)));
        Assert.Equal(source, (await _sync.DownloadAsync("paid", id)).Document!.Sections[0].Pages[0].Content);
        // Knowing another document's page ID/source does not permit introducing it in a new upload.
        await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", Guid.NewGuid(), new(Guid.NewGuid(), null, "upload", upload)));
    }

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
    [InlineData("<img src='javascript:bad()'>")]
    public async Task UnsafeOrUnsupportedUploadsNeverMutateDatabase(string html)
    {
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", Guid.NewGuid(), Create(html)))).Status);
        Assert.Empty(await _db.Documents.ToListAsync());
    }

    [Fact]
    public async Task DesktopToolbarRichHtmlSyncsWithoutDroppingFormatting()
    {
        const string html = "<p style=\"text-align: center; margin-left: 2em;\" data-indent-level=\"1\">Aligned</p><table style=\"min-width: 25px;\"><colgroup><col style=\"min-width: 25px;\"></colgroup><tbody><tr><th colspan=\"1\" rowspan=\"1\"><p>Header</p></th></tr></tbody></table><img src=\"data:image/png;base64,iVBORw0KGgo=\" alt=\"Embedded\">";
        Guid id = Guid.NewGuid();
        await _sync.MutateAsync("paid", id, Create(html));
        var snapshot = await _sync.DownloadAsync("paid", id);
        Assert.Equal(html, snapshot.Document!.Sections[0].Pages[0].Content);
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
        string previous = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("_AddDocumentSynchronization")).Last();
        await migrator.MigrateAsync(previous);
        var now = DateTimeOffset.UtcNow;
        var project = new ProjectRecord { Id = Guid.NewGuid(), OwnerUserId = "paid", Title = "Existing", CreatedUtc = now, UpdatedUtc = now };
        var document = new DocumentRecord { Id = Guid.NewGuid(), ProjectId = project.Id, OwnerUserId = "paid", Title = "Existing", CreatedAt = now, UpdatedAt = now };
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Projects (Id,OwnerUserId,Title,CreatedUtc,UpdatedUtc) VALUES ({project.Id},{project.OwnerUserId},{project.Title},{now},{now})");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Documents (Id,ProjectId,OwnerUserId,Title,DocumentKind,CreatedAt,UpdatedAt,CreatedAtUnixSeconds,UpdatedAtUnixSeconds,IsArchived) VALUES ({document.Id},{project.Id},{document.OwnerUserId},{document.Title},{0},{now},{now},{0},{0},{false})");
        Guid legacyNodeId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ProjectNodes (Id,ProjectId,NodeType,Title,OrderIndex,WordCountCache,UpdatedUtc) VALUES ({legacyNodeId},{project.Id},{1},{"Legacy chapter"},{0},{0},{now})");
        await db.Database.MigrateAsync();
        Assert.Equal(document.Id, (await db.DocumentSyncRecords.SingleAsync()).DocumentId);
        Assert.Equal("Existing", (await db.Documents.SingleAsync()).Title);
        Assert.Equal(document.Id, (await db.Projects.SingleAsync()).PrimaryDocumentId);
        var retainedNode = await db.ProjectNodes.SingleAsync();
        Assert.Equal(legacyNodeId, retainedNode.Id);
        Assert.Equal(document.Id, retainedNode.DocumentId);
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

    [Fact]
    public async Task ExplicitMovesRemovalAndRestoreKeepServerIdentitiesAndContent()
    {
        Guid id = Guid.NewGuid();
        var request = Create("<p>Move and recover 日本語</p>");
        var first = request.Document!.Sections[0];
        Guid movedId = first.Pages[0].Id;
        var second = new SyncSection(Guid.NewGuid(), "Second", 10, null, "en", [new(Guid.NewGuid(), "Second page", 5, "<p>Second</p>")]);
        first = first with { Pages = first.Pages.Append(new SyncPage(Guid.NewGuid(), "Keep first", 10, "<p>Keep</p>")).ToArray() };
        request = request with { Document = request.Document with { Sections = [first, second] } };
        var created = await _sync.MutateAsync("paid", id, request);
        var move = request.Document! with { Sections = [first with { Pages = [first.Pages[1]] }, second with { Pages = second.Pages.Append(first.Pages[0] with { OrderIndex = 20 }).ToArray() }],
            Structure = new(true, [], []) };
        var moved = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), created.State.Version, "upload", move));
        Assert.Equal(second.Id, (await _db.Pages.SingleAsync(p => p.Id == movedId)).SectionId);
        _db.ChangeTracker.Clear();
        Assert.Equal(movedId, (await _sync.DownloadAsync("paid", id)).Document!.Sections[1].Pages[1].Id);
        var remove = move with { Sections = [move.Sections[0], second], Structure = new(true, [], [movedId]) };
        var removalRequest = new SyncMutation(Guid.NewGuid(), moved.State.Version, "upload", remove);
        var removed = await _sync.MutateAsync("paid", id, removalRequest);
        Assert.False(await _db.Pages.AnyAsync(p => p.Id == movedId));
        Assert.Equal(removed, await _sync.MutateAsync("paid", id, removalRequest)); // Same receipt on replay.
        var restored = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), removed.State.Version, "upload", move));
        Assert.Equal("<p>Move and recover 日本語</p>", (await _sync.DownloadAsync("paid", id)).Document!.Sections[1].Pages[1].Content);
        var removeSection = move with { Sections = [move.Sections[1]], Structure = new(true, [first.Id], [first.Pages[1].Id]) };
        var sectionRemoved = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), restored.State.Version, "upload", removeSection));
        Assert.False(await _db.Sections.AnyAsync(s => s.Id == first.Id));
        Assert.True(await _db.Pages.AnyAsync(p => p.Id == movedId));
        await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), sectionRemoved.State.Version, "upload", move));
        _db.ChangeTracker.Clear();
        Assert.Equal(first.Id, (await _sync.DownloadAsync("paid", id)).Document!.Sections[0].Id);
    }

    [Theory]
    [InlineData("page-note", "structure_has_web_metadata")]
    [InlineData("section-note", "structure_has_web_metadata")]
    [InlineData("translation", "structure_has_web_metadata")]
    [InlineData("rich", "structure_has_protected_content")]
    [InlineData("legacy", "structure_has_protected_content")]
    public async Task ExplicitRemovalRetainsWebOnlyMetadataAndProtectedSource(string feature, string code)
    {
        Guid id = Guid.NewGuid(); var request = Create();
        var first = request.Document!.Sections[0];
        var second = new SyncSection(Guid.NewGuid(), "Keep", 1, null, null, [new(Guid.NewGuid(), "Keep", 0, "<p>Keep</p>")]);
        request = request with { Document = request.Document with { Sections = [first, second] } };
        await _sync.MutateAsync("paid", id, request);
        if (feature == "page-note") _db.PageNotes.Add(new() { PageId = first.Pages[0].Id, Notes = "Unknown to devices", UpdatedAt = DateTimeOffset.UtcNow });
        if (feature == "section-note") _db.SectionNotes.Add(new() { SectionId = first.Id, NotesText = "Unknown to devices", UpdatedAtUtc = DateTimeOffset.UtcNow });
        if (feature == "translation") (await _db.Sections.SingleAsync(s => s.Id == first.Id)).TranslationGroupId = Guid.NewGuid();
        if (feature is "rich" or "legacy") (await _db.Pages.SingleAsync(p => p.Id == first.Pages[0].Id)).Content = feature == "rich" ? "<table class=\"future\"><tr><td>Original</td></tr></table>" : "{\"future\":true}";
        await _db.SaveChangesAsync();
        var snapshot = await _sync.DownloadAsync("paid", id);
        var remove = request.Document! with { Sections = [second], Structure = new(true, [first.Id], [first.Pages[0].Id]) };
        var error = await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, new(Guid.NewGuid(), snapshot.State.Version, "upload", remove)));
        Assert.Equal(422, error.Status); Assert.Equal(code, error.Error.Code);
        _db.ChangeTracker.Clear();
        var retained = await _sync.DownloadAsync("paid", id);
        Assert.Equal(snapshot.State, retained.State);
        Assert.Equal(snapshot.Document!.Sections[0].Pages[0].Content, retained.Document!.Sections[0].Pages[0].Content);
        Assert.Equal(2, retained.Document.Sections.Count);
    }

    [Fact]
    public async Task StructureRequestsRequireExplicitMissingIdsAndExpectedVersion()
    {
        Guid id = Guid.NewGuid(); var request = Create();
        var created = await _sync.MutateAsync("paid", id, request);
        var replacement = Create().Document! with { Structure = new(true, [], []) };
        Assert.Equal("structure_removal_not_supported", (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, new(Guid.NewGuid(), created.State.Version, "upload", replacement)))).Error.Code);
        var invalid = request.Document! with { Structure = new(true, [request.Document.Sections[0].Id], []) };
        Assert.Equal(400, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, new(Guid.NewGuid(), created.State.Version, "upload", invalid)))).Status);
        var edit = request.Document with { Sections = [request.Document.Sections[0] with { Pages = [request.Document.Sections[0].Pages[0] with { Content = "<p>Remote edit wins its version</p>" }] }] };
        var edited = await _sync.MutateAsync("paid", id, new(Guid.NewGuid(), created.State.Version, "upload", edit));
        replacement = replacement with { Structure = new(true, [request.Document.Sections[0].Id], [request.Document.Sections[0].Pages[0].Id]) };
        Assert.Equal(409, (await Assert.ThrowsAsync<DocumentSyncException>(() => _sync.MutateAsync("paid", id, new(Guid.NewGuid(), created.State.Version, "upload", replacement)))).Status);
        Assert.Equal(edited.State, (await _sync.DownloadAsync("paid", id)).State);
        Assert.Equal("<p>Remote edit wins its version</p>", (await _sync.DownloadAsync("paid", id)).Document!.Sections[0].Pages[0].Content);
    }

    private sealed class User : IUserIdResolver { public string ResolveUserId(System.Security.Claims.ClaimsPrincipal user) => "paid"; }
    private sealed class Plans : IUserEntitlementStore
    {
        public bool Paid { get; set; } = true;
        public Task<UserEntitlement> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(new UserEntitlement { UserId = userId, PlanKey = Paid ? "standard" : "free", SubscriptionStatus = "active" });
    }
}
