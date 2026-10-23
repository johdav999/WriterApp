using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using WriterApp.Application.Collaboration;
using WriterApp.Data;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    [Fact]
    public async Task EmergencyDisableKeepsCommittedWritingAndLegacyFencesWhileStoppingNewAuthority()
    {
        await Convert(); await InviteMember("editor", CollaborationRole.Editor);
        var page = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page); var settings = new SessionOptions();
        var sessions = new CollaborationSessionService(_db, new EphemeralDataProtectionProvider(), settings, _plans, new WriterApp.Application.Security.DeletedUserIdentityService(_db), TimeProvider.System, Access);
        var request = new CollaborationSessionRequest(page.Generation, Guid.NewGuid(), 2, 1, page.SchemaFingerprint, true);
        var ticket = await sessions.Issue("owner", _document, _page, request, default); var scope = new CollaborationServiceRequest(ticket.Ticket, ticket.RoomName, Guid.NewGuid()); await sessions.Authorize(scope with {Acquire=true}, default);
        var commits = new CollaborationCommitService(_db, sessions, settings, TimeProvider.System); var worker = Guid.NewGuid(); var lease = await commits.Lease(new(scope,worker), default);
        var update = new CollaborationCommitRequest(scope,worker,lease.Epoch,0,Guid.NewGuid(),[0,0],CollaborationMigrationService.Hash([0,0]));
        var receipt = await commits.Commit(update, default); await new CollaborationProjectionService(_db,_codec).Drain(default);
        settings.CurrentValue.CommitsEnabled = false;
        await Assert.ThrowsAsync<CollaborationCommitException>(() => commits.Commit(update with {UpdateId=Guid.NewGuid(),ExpectedSequence=1},default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => sessions.Issue("owner",_document,_page,request,default));
        Assert.True((await sessions.Issue("owner",_document,_page,request with {RequestCommit=false},default)).ReadOnly);
        settings.CurrentValue.SessionsEnabled = false; _sharingSettings.CurrentValue.Enabled = false; _interactionOptions.Enabled = false; _options.Enabled = false;
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => sessions.Authorize(scope,default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Access.Require("editor",_document,CollaborationOperation.Read,default));
        var read = await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access); Assert.Equal(1,read.Pages.Single(p=>p.PageId==_page).Sequence);
        await Assert.ThrowsAnyAsync<Exception>(() => _db.Pages.Where(p=>p.Id==_page).ExecuteUpdateAsync(p=>p.SetProperty(x=>x.Content,"blocked emergency snapshot")));
        Assert.Equal(receipt.Receipt.UpdateId,(await _db.CollaborationReceipts.AsNoTracking().SingleAsync()).UpdateId);
        Assert.Equal(CollaborationMode.Collaborative,(await _db.Documents.AsNoTracking().SingleAsync()).CollaborationMode);
        Assert.True((await new CollaborationOperations(_db,_codec).Inspect(_document,default)).ConsistentReadReady);
    }

    [Fact]
    public async Task OperationsDetectCorruptLedgersProjectionsAndRecoveryWithoutChangingBytes()
    {
        await Convert(); var operations = new CollaborationOperations(_db, _codec);
        Assert.True((await operations.Inspect(_document, default)).ConsistentReadReady);
        var page = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page);
        await _db.CollaborationProjections.Where(p => p.PageId == _page).ExecuteUpdateAsync(p => p.SetProperty(x => x.Text, "wrong projection"));
        var corrupt = await operations.Inspect(_document, default); Assert.False(corrupt.IntegrityValid); Assert.Contains(corrupt.Findings, f => f.Code == "projection_content_mismatch");
        Assert.Equal("wrong projection", (await _db.CollaborationProjections.AsNoTracking().SingleAsync(p => p.PageId == _page)).Text);
        await new CollaborationProjectionService(_db, _codec).Project(_page, page.Generation, default);
        await _db.CollaborationPages.Where(p => p.PageId == _page).ExecuteUpdateAsync(p => p.SetProperty(x => x.ProjectionSequence, 0));
        await _db.CollaborationSnapshots.Where(p => p.PageId == _page).ExecuteUpdateAsync(p => p.SetProperty(x => x.Sha256, new string('0', 64)));
        Assert.Contains((await operations.Inspect(_document, default)).Findings, f => f.Code == "committed_history_invalid");
        var bytes = (await _db.CollaborationSnapshots.AsNoTracking().SingleAsync(p => p.PageId == _page)).State;
        await _db.CollaborationSnapshots.Where(p => p.PageId == _page).ExecuteUpdateAsync(p => p.SetProperty(x => x.Sha256, CollaborationMigrationService.Hash(bytes)));
        await _db.CollaborationPages.Where(p => p.PageId == _page).ExecuteUpdateAsync(p => p.SetProperty(x => x.CommittedSequence, 1));
        Assert.Contains((await operations.Inspect(_document, default)).Findings, f => f.Code == "committed_history_invalid");
        Assert.Equal(1, (await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page)).CommittedSequence);
    }

    [Fact]
    public async Task NativeDatabaseBackupRestoreRetainsWritingReceiptsCommentsAndFencesOldAuthority()
    {
        var support = await SupportingFixtures(); await Convert(); await InviteMember("editor", CollaborationRole.Editor);
        await Sharing.Supporting("owner", _document, new(support.Linked), default);
        var invitation = await Sharing.Invite("owner", _document, new(Guid.NewGuid(), CollaborationRole.Viewer, RecipientUserId: "pending"), default);
        var page = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page); var anchor = await AnchorFixture();
        var commentId = Guid.NewGuid(); await Comments.Create("owner", _document, _page, new(commentId, page.Generation, 0, 0, anchor.Anchor, "Restored comment"), default);
        var protection = new EphemeralDataProtectionProvider(); var settings = new SessionOptions();
        var sessions = new CollaborationSessionService(_db, protection, settings, _plans, new WriterApp.Application.Security.DeletedUserIdentityService(_db), TimeProvider.System, Access);
        var ticketRequest = new CollaborationSessionRequest(page.Generation, Guid.NewGuid(), 2, 1, page.SchemaFingerprint, true);
        var ticket = await sessions.Issue("owner", _document, _page, ticketRequest, default);
        var session = new CollaborationServiceRequest(ticket.Ticket, ticket.RoomName, Guid.NewGuid()); await sessions.Authorize(session with { Acquire = true }, default);
        var commits = new CollaborationCommitService(_db, sessions, settings, TimeProvider.System); var worker = Guid.NewGuid(); var lease = await commits.Lease(new(session, worker), default);
        var addition = await _codec.Seed("<p>Committed restore rehearsal writing</p>", "Html", default);
        var update = new CollaborationCommitRequest(session, worker, lease.Epoch, 0, Guid.NewGuid(), addition.State, addition.Sha256);
        var committed = await commits.Commit(update, default); await new CollaborationProjectionService(_db, _codec).Drain(default);
        byte[] snapshot = (await _db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == _page)).State;
        var canonical = await _codec.Project([snapshot, update.Update], default);
        await commits.Checkpoint(new(session, worker, lease.Epoch, 1, canonical.State, canonical.Sha256), default);
        var before = await new CollaborationOperations(_db, _codec).Inspect(_document, default); Assert.True(before.ConsistentReadReady); Assert.Equal(1, before.Receipts);
        await using var restored = await RestoreOwnedCopy();
        // The backup predates a later revocation. Restoring it cannot silently revive that grant.
        await Sharing.Change("owner", _document, "editor", new(1, CollaborationRole.Viewer, true), default);
        Assert.False((await restored.Db.CollaborationMembers.AsNoTracking().SingleAsync()).Revoked);
        Assert.True((await new CollaborationOperations(restored.Db, _codec).Inspect(_document, default)).ConsistentReadReady);
        var offline = new CollaborationOperations(restored.Db, _codec);
        Guid recoveryOperation = Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(() => offline.FenceRestoredCopy(recoveryOperation, false, new(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => offline.FenceRestoredCopy(recoveryOperation, true, new(Commits: true), default));
        var inspect = await OperationsCli(restored.Db, "inspect", "--document", _document.ToString()); Assert.Equal(0, inspect.Exit);
        using var inspection = JsonDocument.Parse(inspect.Output); string target = inspection.RootElement.GetProperty("target").GetString()!;
        var wrongTarget = await OperationsCli(restored.Db, "seal-restored-copy", "--expect-target", new string('0',64), "--writers-stopped", "true", "--operation", recoveryOperation.ToString()); Assert.Equal(4, wrongTarget.Exit);
        Assert.False((await restored.Db.CollaborationMembers.AsNoTracking().SingleAsync()).Revoked);
        var sealedCopy = await OperationsCli(restored.Db, "seal-restored-copy", "--expect-target", target, "--writers-stopped", "true", "--operation", recoveryOperation.ToString()); Assert.Equal(0, sealedCopy.Exit);
        restored.Db.ChangeTracker.Clear();
        string requiredEnvironment = (await restored.Db.CollaborationRecoveryStates.AsNoTracking().SingleAsync()).RequiredEnvironment;
        Assert.Equal(new CollaborationRestoreFence(0,0,0,0,0,requiredEnvironment), await offline.FenceRestoredCopy(recoveryOperation, true, new(), default));
        Assert.True((await restored.Db.CollaborationMembers.AsNoTracking().SingleAsync()).Revoked);
        Assert.True((await restored.Db.CollaborationInvitations.AsNoTracking().SingleAsync(i => i.Id == invitation.Invitation.Id)).Revoked);
        Assert.True((await restored.Db.CollaborationSupportingLinks.AsNoTracking().SingleAsync()).Revoked);
        var restoredAccess = new CollaborationAccessService(restored.Db, _sharingSettings, _plans, new WriterApp.Application.Security.DeletedUserIdentityService(restored.Db));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => restoredAccess.Require("editor", _document, CollaborationOperation.Read, default));
        var restoredSessions = new CollaborationSessionService(restored.Db, protection, settings, _plans, new WriterApp.Application.Security.DeletedUserIdentityService(restored.Db), TimeProvider.System, restoredAccess);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => restoredSessions.Authorize(session, default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => restoredSessions.Issue("owner", _document, _page, ticketRequest, default));
        settings.CurrentValue.Environment = requiredEnvironment;
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => restoredSessions.Issue("owner", _document, _page, ticketRequest with { Environment = "test" }, default));
        var fresh = await restoredSessions.Issue("owner", _document, _page, ticketRequest with { Environment = requiredEnvironment }, default); var freshScope = new CollaborationServiceRequest(fresh.Ticket, fresh.RoomName, Guid.NewGuid());
        await restoredSessions.Authorize(freshScope with { Acquire = true }, default);
        var restoredCommits = new CollaborationCommitService(restored.Db, restoredSessions, settings, TimeProvider.System);
        await Assert.ThrowsAsync<CollaborationCommitException>(() => restoredCommits.Commit(update with { Session = freshScope }, default));
        var newWorker = Guid.NewGuid(); var restoredLease = await restoredCommits.Lease(new(freshScope, newWorker), default);
        var replay = await restoredCommits.Commit(update with { Session = freshScope, WorkerId = newWorker, Epoch = restoredLease.Epoch }, default);
        Assert.Equal(committed.Receipt, replay.Receipt); Assert.Single(await restored.Db.CollaborationReceipts.ToArrayAsync()); Assert.Empty(await restored.Db.CollaborationUpdates.ToArrayAsync());
        Assert.Equal(snapshot, (await restored.Db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == _page && s.Sequence == 0)).State);
        Assert.Equal(canonical.State, (await restored.Db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == _page && s.Sequence == 1)).State);
        Assert.Contains("Committed restore rehearsal writing", (await CollaborationProjectionService.Read(restored.Db,"owner",_document,new(),default)).Pages.Single(p=>p.PageId==_page).Text);
        Assert.Equal("Restored comment", (await restored.Db.CollaborationComments.AsNoTracking().SingleAsync()).Body);
        Assert.True((await offline.Inspect(_document, default)).ConsistentReadReady);
        await Assert.ThrowsAnyAsync<Exception>(() => restored.Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(_database is null ? "20261018060230_CollaborationComments" : "20261018060233_CollaborationCommentsSqlServer"));
        Assert.Equal(CollaborationMode.Collaborative, (await restored.Db.Documents.AsNoTracking().SingleAsync(d => d.Id == _document)).CollaborationMode);
    }

    private async Task<(int Exit, string Output)> OperationsCli(AppDbContext database, string command, params string[] arguments)
    {
        string root = new Host().ContentRootPath;
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        string configuration = typeof(CollaborationConversionTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        start.ArgumentList.Add(Path.Combine(root, "Tools", "Collaboration.Operations", "bin", configuration, "net10.0", "Collaboration.Operations.dll")); start.ArgumentList.Add(command);
        foreach (string argument in new[] { "--provider", _database is null ? "sqlite" : "sqlserver", "--connection-env", "WRITERAPP_COLLAB_OPERATION_TEST_CONNECTION", "--root", root }.Concat(arguments)) start.ArgumentList.Add(argument);
        start.Environment["WRITERAPP_COLLAB_OPERATION_TEST_CONNECTION"] = database.Database.GetConnectionString();
        using var process = Process.Start(start)!; var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        string result = await output, diagnostic = await error; Assert.DoesNotContain(database.Database.GetConnectionString()!, result + diagnostic); return (process.ExitCode, result);
    }
    private sealed class OwnedRestoredCopy(AppDbContext db, string? directory, string? backup) : IAsyncDisposable
    {
        public AppDbContext Db { get; } = db;
        public async ValueTask DisposeAsync()
        {
            string name = Db.Database.GetDbConnection().Database;
            if (Db.Database.IsSqlServer() && name.StartsWith("WriterApp_CollaborationRestore_", StringComparison.Ordinal)) await Db.Database.EnsureDeletedAsync();
            await Db.DisposeAsync();
            if (backup is not null && Path.GetFileName(backup) == name + ".bak" && File.Exists(backup)) File.Delete(backup);
            if (directory is not null && Path.GetFullPath(directory).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(directory).StartsWith("WriterApp_CollaborationRestore_", StringComparison.Ordinal)) Directory.Delete(directory, true);
        }
    }
    private async Task<OwnedRestoredCopy> RestoreOwnedCopy()
    {
        string name = "WriterApp_CollaborationRestore_" + Guid.NewGuid().ToString("N");
        if (_database is null)
        {
            string directory = Path.Combine(Path.GetTempPath(), name); Directory.CreateDirectory(directory);
            using var backup = new SqliteConnection($"Data Source={Path.Combine(directory, "backup.db")};Pooling=False"); await backup.OpenAsync();
            ((SqliteConnection)_db.Database.GetDbConnection()).BackupDatabase(backup);
            string restoredPath = Path.Combine(directory, "restored.db"); using (var target = new SqliteConnection($"Data Source={restoredPath};Pooling=False")) { await target.OpenAsync(); backup.BackupDatabase(target); }
            return new(new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={restoredPath};Pooling=False").Options), directory, null);
        }
        var connection = new SqlConnectionStringBuilder(_db.Database.GetConnectionString()) { InitialCatalog = "master" };
        await using var sql = new SqlConnection(connection.ConnectionString); await sql.OpenAsync();
        async Task<string> Property(string property) { await using var cmd = sql.CreateCommand(); cmd.CommandText = $"SELECT CAST(SERVERPROPERTY('{property}') AS nvarchar(4000))"; return (string)(await cmd.ExecuteScalarAsync())!; }
        string backupPath = Path.Combine(await Property("InstanceDefaultBackupPath"), name + ".bak");
        string dataPath = Path.Combine(await Property("InstanceDefaultDataPath"), name + ".mdf"), logPath = Path.Combine(await Property("InstanceDefaultLogPath"), name + "_log.ldf");
        Assert.StartsWith("WriterApp_ConversionTests_", _database);
        await using (var cmd = sql.CreateCommand()) { cmd.CommandTimeout = 60; cmd.CommandText = $"BACKUP DATABASE [{_database}] TO DISK=@file WITH COPY_ONLY,CHECKSUM"; cmd.Parameters.AddWithValue("@file", backupPath); await cmd.ExecuteNonQueryAsync(); }
        await using (var cmd = sql.CreateCommand()) { cmd.CommandText = "RESTORE VERIFYONLY FROM DISK=@file WITH CHECKSUM"; cmd.Parameters.AddWithValue("@file", backupPath); await cmd.ExecuteNonQueryAsync(); }
        var logical = new Dictionary<string,string>();
        await using (var cmd = sql.CreateCommand()) { cmd.CommandText = "RESTORE FILELISTONLY FROM DISK=@file"; cmd.Parameters.AddWithValue("@file", backupPath); await using var reader = await cmd.ExecuteReaderAsync(); while (await reader.ReadAsync()) logical[(string)reader["Type"]] = (string)reader["LogicalName"]; }
        await using (var cmd = sql.CreateCommand())
        {
            cmd.CommandTimeout = 60; cmd.CommandText = $"IF DB_ID(@name) IS NOT NULL THROW 51072,'Owned restore target already exists',1; RESTORE DATABASE [{name}] FROM DISK=@file WITH MOVE @dataLogical TO @dataFile, MOVE @logLogical TO @logFile, CHECKSUM";
            cmd.Parameters.AddWithValue("@name", name); cmd.Parameters.AddWithValue("@file", backupPath); cmd.Parameters.AddWithValue("@dataLogical", logical["D"]); cmd.Parameters.AddWithValue("@dataFile", dataPath); cmd.Parameters.AddWithValue("@logLogical", logical["L"]); cmd.Parameters.AddWithValue("@logFile", logPath); await cmd.ExecuteNonQueryAsync();
        }
        connection.InitialCatalog = name;
        return new(new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(connection.ConnectionString).Options), null, backupPath);
    }
}
