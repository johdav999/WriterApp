using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Data;
using Xunit;

namespace WriterApp.Tests;

public sealed class OnboardingDemoMigrationTests
{
    [Fact]
    public async Task SqliteUpgradePreservesExistingWritingAndSyncAndDoesNotAuthorizeLegacyMetadata() {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();var migrator=db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261003183221_WebTranslationOperations");
        var project=Guid.NewGuid();var document=Guid.NewGuid();var section=Guid.NewGuid();
        db.Projects.Add(new(){Id=project,OwnerUserId="author"});db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="author"});
        db.Sections.Add(new(){Id=section,DocumentId=document});db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=document,SectionId=section,Content="<p><strong>Åsa 日本語 🧭</strong></p>"});
        await db.SaveChangesAsync();var before=await db.DocumentSyncRecords.AsNoTracking().SingleAsync(x=>x.DocumentId==document);
        await migrator.MigrateAsync();db.ChangeTracker.Clear();
        Assert.Empty(await db.OnboardingDemoWorkspaces.ToListAsync());Assert.Empty(await db.OnboardingProgressOperations.ToListAsync());
        Assert.Equal("<p><strong>Åsa 日本語 🧭</strong></p>",(await db.Pages.SingleAsync()).Content);
        Assert.Equal(before.Version,(await db.DocumentSyncRecords.SingleAsync(x=>x.DocumentId==document)).Version);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public void SqlServerScriptHasOwnerSingletonUniqueDocumentAndOwnedProgressReceiptWithoutExecutingDatabase() {
        using var db=new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>()
            .UseSqlServer("Server=localhost;Database=WriterAppScriptOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        string sql=db.GetService<IMigrator>().GenerateScript("20261003183237_WebTranslationOperationsSqlServer",null,MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [OnboardingDemoWorkspaces]",sql);Assert.Contains("[OwnerUserId] nvarchar(128)",sql);
        Assert.Contains("CREATE UNIQUE INDEX [IX_OnboardingDemoWorkspaces_DocumentId]",sql);
        Assert.Contains("PRIMARY KEY ([OwnerUserId], [OperationId])",sql);Assert.False(db.Database.HasPendingModelChanges());
    }
}
