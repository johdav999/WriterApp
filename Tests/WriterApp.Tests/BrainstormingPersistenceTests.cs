using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingPersistenceTests
{
    private static string Root() => Path.Combine(Path.GetTempPath(), "WriterApp.Brainstorming.Tests", Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task RichLocalProposalAncestryManualEditingAndEnabledSourcesSurviveRestart()
    {
        var s = BrainstormingContractTests.Fixture(); var original = s.Drafts[0];
        var manual = original with { Id=Guid.NewGuid(), ParentDraftId=original.Id, Revision=2, ManuallyEdited=true,
            PlainText="Hon tvivlade. Åsa 日本語 🧭", EditorHtml="<p><strong>Hon tvivlade.</strong> Åsa 日本語 🧭</p>" };
        s=s with { Drafts=[original,manual], EnabledSources=[new(s.Scope.DocumentId,BrainstormingSourceKind.StoryNote,"private-note")], State=BrainstormingConversationState.LocalOnly };
        var root=Root();var store=new LocalBrainstormingStore(root);var operation=Guid.NewGuid();
        var saved=await store.WriteAsync(new(1,1,s,"Unsent after reload",null,null,null,[],[]),0,operation);
        var reopened=(await new LocalBrainstormingStore(root).ReadAsync(s.Scope))!;
        Assert.Equal(BrainstormingPersistence.Json(saved),BrainstormingPersistence.Json(reopened));
        Assert.Equal(original.Id,reopened.Session.Drafts[1].ParentDraftId);Assert.Equal(manual.EditorHtml,reopened.Session.Drafts[1].EditorHtml);
        // Export a validated, synthetic contract envelope for the real IndexedDB browser harness.
        if (Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_FIXTURE") is {Length:>0} path) {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);await File.WriteAllTextAsync(path,BrainstormingPersistence.Json(saved));
        }
    }
    internal sealed class Database : IAsyncDisposable
    {
        public readonly SqliteConnection Connection = new("Data Source=:memory:");
        public DbContextOptions<AppDbContext> Options => new DbContextOptionsBuilder<AppDbContext>().UseSqlite(Connection).Options;
        public readonly Guid Document = Guid.NewGuid(), Project = Guid.NewGuid(), Section = Guid.NewGuid(), Page = Guid.NewGuid();
        public readonly string Backend = BrainstormingValidation.Fingerprint("https://test.invalid/");
        public async Task Start(bool migrations = false) {
            await Connection.OpenAsync(); await using var db = new AppDbContext(Options);
            if (migrations) await db.Database.MigrateAsync(); else await db.Database.EnsureCreatedAsync();
            db.Projects.Add(new() { Id = Project, OwnerUserId = "author", Title = "Backing project" });
            db.Documents.Add(new() { Id = Document, ProjectId = Project, OwnerUserId = "author", Title = "Standalone manuscript" });
            db.Sections.Add(new() { Id = Section, DocumentId = Document, Title = "Section" });
            db.Pages.Add(new() { Id = Page, DocumentId = Document, SectionId = Section, Content = "<p><strong>Åsa 日本語 🧭</strong></p>" });
            await db.SaveChangesAsync();
        }
        public BrainstormingScope Scope(Guid? id = null) => new(1, id ?? Guid.NewGuid(), Document, null, BrainstormingIdentitySpace.Server,
            Backend, BrainstormingValidation.Fingerprint("author"), null);
        public BrainstormingSession Empty(Guid? id = null) => BrainstormingPersistence.Empty(Scope(id)) with { State = BrainstormingConversationState.Saved };
        public BrainstormingSession Rich() {
            var source = BrainstormingContractTests.Fixture(); var scope = Scope();
            var rebased = BrainstormingPersistence.ReScope(source, scope);
            return rebased with { Targets = rebased.Targets.Select(t => t with { DocumentId = Document, SectionId = Section, PageId = Page }).ToArray(),
                Contexts = rebased.Contexts.Select(c => c with { Sources = c.Sources.Select(s => s with { DocumentId = Document, SectionId = Section, PageId = Page }).ToArray() }).ToArray() };
        }
        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
    [Fact]
    public async Task ServerRestartDeduplicatesExactWritesAndPreservesAllInertProposalState()
    {
        await using var f = new Database(); await f.Start(); var session = f.Rich(); var write = new BrainstormingWrite(1, Guid.NewGuid(), 0, session);
        BrainstormingReceipt receipt; string writing;
        await using (var db = new AppDbContext(f.Options)) {
            writing = (await db.Pages.SingleAsync()).Content; receipt = await new BrainstormingSessions(db, "author", f.Backend).WriteAsync(write, default);
        }
        await using var reopened = new AppDbContext(f.Options); var service = new BrainstormingSessions(reopened, "author", f.Backend);
        Assert.Equal(receipt, await service.WriteAsync(write, default));
        var read = await service.ReadAsync(f.Document, session.Scope.SessionId, default);
        Assert.Equal(BrainstormingPersistence.Json(session), BrainstormingPersistence.Json(read));
        Assert.Equal(receipt, await service.InspectAsync(f.Document, session.Scope.SessionId, write.OperationId, default));
        Assert.Single(await reopened.BrainstormingSessionOperations.ToListAsync()); Assert.Equal(writing, (await reopened.Pages.SingleAsync()).Content);
        Assert.Empty(await reopened.AiActionHistoryEntries.ToListAsync()); Assert.Empty(await reopened.UsageEvents.ToListAsync());
        await Assert.ThrowsAsync<InvalidDataException>(() => service.WriteAsync(write with { Session = session with { EnabledSources = [new(f.Document, BrainstormingSourceKind.StoryNote, "notes")] } }, default));
    }
    [Theory]
    [InlineData("account")][InlineData("backend")][InlineData("document")][InlineData("source")][InlineData("page")]
    public async Task ServerRefusesWrongOwnedScopeOrReferences(string failure)
    {
        await using var f = new Database(); await f.Start(); var session = f.Rich();
        session = failure switch {
            "account" => BrainstormingPersistence.ReScope(session, session.Scope with { AccountKey = BrainstormingValidation.Fingerprint("other") }),
            "backend" => BrainstormingPersistence.ReScope(session, session.Scope with { BackendKey = new('A',64) }),
            "document" => BrainstormingPersistence.ReScope(session, session.Scope with { DocumentId = Guid.NewGuid() }) with { Targets = [], Contexts = [], Turns = [], Requests = [], Drafts = [], Planning = [], Approvals = [], Applications = [] },
            "source" => session with { EnabledSources = [new(Guid.NewGuid(), BrainstormingSourceKind.SupportingDocument, null)] },
            _ => session with { Targets = [session.Targets[0] with { PageId = Guid.NewGuid() }] }
        };
        await using var db = new AppDbContext(f.Options);
        await Assert.ThrowsAsync<BrainstormingAccessException>(() => new BrainstormingSessions(db, "author", f.Backend).WriteAsync(new(1, Guid.NewGuid(), 0, session), default));
        Assert.Empty(await db.BrainstormingSessions.ToListAsync()); Assert.Contains("Åsa", (await db.Pages.SingleAsync()).Content);
    }
    [Fact]
    public async Task StaleRevisionTombstoneAndAcceptedHistoryCannotBeOverwritten()
    {
        await using var f = new Database(); await f.Start(); var session = f.Rich();
        await using var db = new AppDbContext(f.Options); var service = new BrainstormingSessions(db, "author", f.Backend);
        var first = new BrainstormingWrite(1, Guid.NewGuid(), 0, session); await service.WriteAsync(first, default);
        var next = session with { Revision = 2, UpdatedAt = DateTimeOffset.UtcNow };
        await Assert.ThrowsAsync<InvalidDataException>(() => service.WriteAsync(new(1, Guid.NewGuid(), 1, next with { Turns = [next.Turns[0] with { Text = "Rewrite accepted message" }, next.Turns[1]] }), default));
        var deleted = next with { State = BrainstormingConversationState.Deleted }; await service.WriteAsync(new(1, Guid.NewGuid(), 1, deleted), default);
        var error = await Assert.ThrowsAsync<BrainstormingConcurrencyException>(() => service.WriteAsync(new(1, Guid.NewGuid(), 1, next), default));
        Assert.Equal(BrainstormingConversationState.Deleted, error.Current!.State);
        await Assert.ThrowsAsync<BrainstormingConcurrencyException>(() => service.WriteAsync(new(1, Guid.NewGuid(), 2, next with { Revision = 3 }), default));
        Assert.Equal(2, (await db.BrainstormingSessions.SingleAsync()).Revision); Assert.Equal(2, await db.BrainstormingSessionOperations.CountAsync());
    }
    [Fact]
    public async Task SessionReadRechecksRevocationAndStorageCannotClaimACommit()
    {
        await using var f = new Database(); await f.Start(); var session = f.Rich(); await using var db = new AppDbContext(f.Options);
        var service = new BrainstormingSessions(db, "author", f.Backend);
        var committed = session with { Applications = [session.Applications[0] with { State = BrainstormingApplicationState.Committed,
            ReceiptId = Guid.NewGuid(), HistoryOperationId = Guid.NewGuid(), CommittedSource = session.Targets[0].Source }] };
        await Assert.ThrowsAsync<InvalidDataException>(() => service.WriteAsync(new(1, Guid.NewGuid(), 0, committed), default));
        db.ChangeTracker.Clear(); await service.WriteAsync(new(1, Guid.NewGuid(), 0, session), default);
        db.Documents.Single().OwnerUserId = "another-owner"; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<BrainstormingAccessException>(() => service.ReadAsync(f.Document, session.Scope.SessionId, default));
        Assert.Single(await db.BrainstormingSessions.ToListAsync());
    }
    [Fact]
    public async Task ListingUsesBoundedStableOwnedPagesIncludingTombstones()
    {
        await using var f = new Database(); await f.Start(); await using var db = new AppDbContext(f.Options);
        var service = new BrainstormingSessions(db, "author", f.Backend);
        for (int i = 0; i < 6; i++) await service.WriteAsync(new(1, Guid.NewGuid(), 0, f.Empty()), default);
        var first = await service.ListAsync(f.Document, null, 2, default); var second = await service.ListAsync(f.Document, first.NextAfter, 2, default);
        Assert.Equal(2, first.Items.Count); Assert.Equal(2, second.Items.Count); Assert.Empty(first.Items.Select(s => s.Scope.SessionId).Intersect(second.Items.Select(s => s.Scope.SessionId)));
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ListAsync(f.Document, null, 51, default));
        await Assert.ThrowsAsync<BrainstormingAccessException>(() => service.ListAsync(f.Document, Guid.NewGuid(), 2, default));
    }
    [Fact]
    public async Task AtomicLocalRestartRetainsComposerReceiptsUnsentOrderAndStaleBranches()
    {
        var root = Root(); var scope = BrainstormingContractTests.Fixture().Scope; var store = new LocalBrainstormingStore(root);
        var value = new BrainstormingCache(1, 1, BrainstormingPersistence.Empty(scope), "Unsent Åsa 👩🏽‍💻", null, null, null, [], []);
        var operation = Guid.NewGuid(); var saved = await store.WriteAsync(value, 0, operation);
        var changed = await store.WriteAsync(saved with { Composer = "Saved composer" }, saved.CacheRevision, Guid.NewGuid());
        var reopened = new LocalBrainstormingStore(root); Assert.Equal("Saved composer", (await reopened.ReadAsync(scope))!.Composer);
        Assert.Equal(changed.CacheRevision, (await reopened.WriteAsync(value, 0, operation)).CacheRevision);
        await Assert.ThrowsAsync<BrainstormingConcurrencyException>(() => reopened.WriteAsync(saved with { Composer = "Concurrent tab composer" }, saved.CacheRevision, Guid.NewGuid()));
        Assert.Equal("Concurrent tab composer", Assert.Single(await reopened.ReadRejectedAsync(scope)).Proposed.Composer);
        Assert.Equal("Saved composer", (await reopened.ReadAsync(scope))!.Composer);
        await File.WriteAllTextAsync(Path.Combine(root, "abandoned.tmp"), "{incomplete}"); Assert.Single(await reopened.ListAsync(scope));
        Assert.Equal(await store.LocalOwnerAsync(), await reopened.LocalOwnerAsync());
    }
    [Theory][InlineData("fault")][InlineData("cancel")][InlineData("future")]
    public async Task InterruptedOrUnsupportedLocalFilesArePreserved(string failure)
    {
        var root = Root(); var scope = BrainstormingContractTests.Fixture().Scope; var original = new LocalBrainstormingStore(root);
        var value = await original.WriteAsync(new(1,1,BrainstormingPersistence.Empty(scope),"Keep me",null,null,null,[],[]),0,Guid.NewGuid());
        var file = Path.Combine(root, BrainstormingPersistence.Partition(scope), scope.DocumentId.ToString("N"), scope.SessionId.ToString("N") + ".json");
        var before = await File.ReadAllBytesAsync(file);
        if (failure == "future") {
            await File.WriteAllTextAsync(file, BrainstormingPersistence.Json(value with { Version = 99 })); before = await File.ReadAllBytesAsync(file);
            await Assert.ThrowsAsync<InvalidDataException>(() => original.WriteAsync(value, value.CacheRevision, Guid.NewGuid()));
        } else {
            var failing = new LocalBrainstormingStore(root, new AtomicDocumentWriter(_ => throw new IOException("Synthetic interrupted replacement")));
            if (failure == "fault") await Assert.ThrowsAsync<IOException>(() => failing.WriteAsync(value with { Composer = "New" },value.CacheRevision,Guid.NewGuid()));
            else await Assert.ThrowsAnyAsync<OperationCanceledException>(() => original.WriteAsync(value with { Composer = "New" },value.CacheRevision,Guid.NewGuid(),new(true)));
        }
        Assert.Equal(before, await File.ReadAllBytesAsync(file));
    }
    [Fact]
    public async Task SqliteMigrationAddsOnlyEmptySessionStorageAndPreservesAuthoredContent()
    {
        await using var f = new Database(); await f.Connection.OpenAsync(); await using var db = new AppDbContext(f.Options);
        var migrator = db.GetService<IMigrator>(); await migrator.MigrateAsync("20261006100000_WebScopedPlanningRecovery");
        db.Projects.Add(new() { Id=f.Project,OwnerUserId="author" }); db.Documents.Add(new() { Id=f.Document,ProjectId=f.Project,OwnerUserId="author" });
        db.Sections.Add(new() {Id=f.Section,DocumentId=f.Document}); db.Pages.Add(new(){Id=f.Page,DocumentId=f.Document,SectionId=f.Section,Content="<p><strong>Åsa 日本語 🧭</strong></p>"});
        await LegacyMigrationSeed.SaveAsync(db); var version = (await db.DocumentSyncRecords.SingleAsync()).Version;
        await migrator.MigrateAsync(); db.ChangeTracker.Clear(); Assert.Empty(await db.BrainstormingSessions.ToArrayAsync());
        Assert.Empty(await db.BrainstormingSessionOperations.ToArrayAsync()); Assert.Equal(version,(await db.DocumentSyncRecords.SingleAsync()).Version);
        Assert.Equal("<p><strong>Åsa 日本語 🧭</strong></p>",(await db.Pages.SingleAsync()).Content); Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public void SqlServerMigrationScriptHasScopedRevisionAndUniqueOperationReceipts()
    {
        using var db = new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer("Server=localhost;Database=BrainstormingScriptOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        var sql = db.GetService<IMigrator>().GenerateScript("20261006100001_WebScopedPlanningRecoverySqlServer", null, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [BrainstormingSessions]",sql); Assert.Contains("[OwnerUserId] nvarchar(128)",sql);
        Assert.Contains("CREATE TABLE [BrainstormingGenerations]",sql);
        Assert.Contains("CREATE TABLE [BrainstormingTranscriptions]",sql);Assert.DoesNotContain("[Wav]",sql);Assert.DoesNotContain("[Audio]",sql);
        Assert.Contains("PRIMARY KEY ([SessionId], [OperationId])",sql); Assert.False(db.Database.HasPendingModelChanges());
    }
}
