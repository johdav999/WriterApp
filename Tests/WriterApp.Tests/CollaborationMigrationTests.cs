using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Application.Collaboration;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed class CollaborationMigrationTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private string? _database;
    private bool SqlServer => _database is not null;
    private string Previous => SqlServer ? "20261007164539_BrainstormingTranscriptionsSqlServer" : "20261007164538_BrainstormingTranscriptions";

    public async Task InitializeAsync()
    {
        string? server = Environment.GetEnvironmentVariable("WRITERAPP_SYNC_SQLSERVER_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server))
        {
            _db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
            await _db.Database.OpenConnectionAsync();
        }
        else
        {
            _database = "WriterApp_CollaborationMigrationTests_" + Guid.NewGuid().ToString("N");
            _db = new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(new SqlConnectionStringBuilder
            { DataSource = server, InitialCatalog = _database, IntegratedSecurity = true, TrustServerCertificate = true }.ConnectionString).Options);
        }
        await _db.GetService<IMigrator>().MigrateAsync(Previous);
        Guid project = Guid.NewGuid(), document = Guid.NewGuid(), section = Guid.NewGuid();
        _db.Projects.Add(new() { Id = project, OwnerUserId = "author" });
        _db.Documents.Add(new() { Id = document, ProjectId = project, OwnerUserId = "author" });
        _db.Sections.Add(new() { Id = section, DocumentId = document });
        _db.Pages.Add(new() { Id = Guid.NewGuid(), DocumentId = document, SectionId = section, Content = "<p><strong>Retained 日本語 🧭</strong></p>" });
        await LegacyMigrationSeed.SaveAsync(_db);
    }

    public async Task DisposeAsync()
    {
        if (_database is not null && _database.StartsWith("WriterApp_CollaborationMigrationTests_", StringComparison.Ordinal)
            && _db.Database.GetDbConnection().Database == _database) await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task RealUpgradeRetainsWritingSyncIdentityAndTriggersThenEnforcesFence()
    {
        var before = await _db.DocumentSyncRecords.AsNoTracking().SingleAsync();
        await _db.Database.MigrateAsync();
        Assert.False(_db.Database.HasPendingModelChanges());
        Assert.Equal(CollaborationMode.Legacy, (await _db.Documents.AsNoTracking().SingleAsync()).CollaborationMode);
        Assert.Equal("<p><strong>Retained 日本語 🧭</strong></p>", (await _db.Pages.AsNoTracking().SingleAsync()).Content);
        Assert.Equal(before.Version, (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version);
        Assert.Empty(await _db.CollaborationPages.ToArrayAsync());
        Assert.Empty(await _db.CollaborationMigrationJobs.ToArrayAsync());
        Assert.Empty(await _db.CollaborationSessions.ToArrayAsync());
        await _db.Pages.ExecuteUpdateAsync(s => s.SetProperty(p => p.Content, "still legacy"));
        Assert.NotEqual(before.Version, (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version);
        await _db.Documents.ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, CollaborationMode.Migrating));
        var error = await Assert.ThrowsAnyAsync<Exception>(() => _db.Pages.ExecuteUpdateAsync(s => s.SetProperty(p => p.Content, "blocked")));
        Assert.NotNull(CollaborationWriteException.FromStorage(error));
        await Assert.ThrowsAnyAsync<Exception>(() => _db.GetService<IMigrator>().MigrateAsync(Previous));
        Assert.Equal("still legacy", (await _db.Pages.AsNoTracking().SingleAsync()).Content);
        Assert.Equal(CollaborationMode.Migrating, (await _db.Documents.AsNoTracking().SingleAsync()).CollaborationMode);
    }

    [Fact]
    public async Task EmptyCollaborationStorageCanRollbackAndUpgradeWithoutLosingLegacySync()
    {
        await _db.Database.MigrateAsync();
        await _db.GetService<IMigrator>().MigrateAsync(Previous);
        var before = (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version;
        await _db.Pages.ExecuteUpdateAsync(s => s.SetProperty(p => p.Content, "saved after rollback"));
        Assert.NotEqual(before, (await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version);
        await _db.Database.MigrateAsync();
        Assert.Equal("saved after rollback", (await _db.Pages.AsNoTracking().SingleAsync()).Content);
        Assert.False(_db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void SqlServerIdempotentScriptUsesBatchSafeTriggersAndBoundedKeys()
    {
        using var db = new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>()
            .UseSqlServer("Server=unused;Database=ScriptOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        string script = db.GetService<IMigrator>().GenerateScript("20261007164539_BrainstormingTranscriptionsSqlServer", null, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [CollaborationReceipts]", script);
        Assert.Contains("CREATE TABLE [CollaborationSessions]", script);
        Assert.Contains("UPDLOCK,HOLDLOCK", script);
        Assert.Contains("EXEC(N'CREATE TRIGGER CollaborationFence_", script);
        Assert.Contains("[AuthorUserId] nvarchar(128)", script);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
