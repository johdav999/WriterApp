using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
using Xunit;

namespace WriterApp.Tests;

public sealed class CoverAssetMigrationTests
{
    [Fact]
    public async Task SqliteUpgradePreservesRemoteReferenceMetadataWritingAndSyncAndCreatesNoUnverifiedCache() {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();var migrator=db.GetService<IMigrator>();await migrator.MigrateAsync("20261004064041_OwnedOnboardingDemo");
        var project=Guid.NewGuid();var document=Guid.NewGuid();var section=Guid.NewGuid();
        db.Projects.Add(new(){Id=project,OwnerUserId="author",CoverImageUrl=RemoteCoverNetwork.Reference});db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="author"});
        db.Sections.Add(new(){Id=section,DocumentId=document});db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=document,SectionId=section,Content="<p><strong>Åsa 日本語 🧭</strong></p>"});
        await db.SaveChangesAsync();var before=await db.DocumentSyncRecords.AsNoTracking().SingleAsync();var revision=(await db.Projects.SingleAsync()).MetadataRevision;
        await migrator.MigrateAsync();db.ChangeTracker.Clear();Assert.Empty(await db.CoverAssets.ToListAsync());
        var saved=await db.Projects.SingleAsync();Assert.Equal(RemoteCoverNetwork.Reference,saved.CoverImageUrl);Assert.Equal(revision,saved.MetadataRevision);
        Assert.Equal(before.Version,(await db.DocumentSyncRecords.SingleAsync()).Version);Assert.Equal("<p><strong>Åsa 日本語 🧭</strong></p>",(await db.Pages.SingleAsync()).Content);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public void SqlServerMigrationHasBoundedOwnerProvenanceAndImmutableRetryIndexWithoutExecutingDatabase() {
        using var db=new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>()
            .UseSqlServer("Server=localhost;Database=WriterAppScriptOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        string sql=db.GetService<IMigrator>().GenerateScript("20261004064042_OwnedOnboardingDemoSqlServer",null,MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [CoverAssets]",sql);Assert.Contains("[OwnerUserId] nvarchar(128)",sql);Assert.Contains("[RemoteReference] nvarchar(max)",sql);
        Assert.Equal(4096,db.Model.FindEntityType(typeof(WriterApp.Data.Documents.CoverAssetRecord))!.FindProperty("RemoteReference")!.GetMaxLength());
        Assert.Contains("[Bytes] varbinary(max)",sql);Assert.Contains("CREATE UNIQUE INDEX [IX_CoverAssets_OwnerUserId_ProjectId_ReferenceHash]",sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
