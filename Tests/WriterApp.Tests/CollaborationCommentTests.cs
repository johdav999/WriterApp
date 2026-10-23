using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    private readonly CollaborationInteractionOptions _interactionOptions = new() { Enabled = true };
    private CollaborationCommentService Comments => new(_db, Access, _codec, Options.Create(_interactionOptions), TimeProvider.System);
    private async Task<(CollaborationAnchor Anchor, byte[] Deletion)> AnchorFixture()
    {
        var start = new ProcessStartInfo("node") { WorkingDirectory = new Host().ContentRootPath, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("WriterApp.Collaboration/tests/anchor-fixture.mjs");
        using var process = Process.Start(start)!;
        byte[] state = (await _db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == _page)).State;
        await process.StandardInput.WriteAsync(JsonSerializer.Serialize(new { state })); process.StandardInput.Close();
        string output = await process.StandardOutput.ReadToEndAsync(), error = await process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync(); Assert.True(process.ExitCode == 0, error);
        using var result = JsonDocument.Parse(output);
        return (result.RootElement.GetProperty("anchor").Deserialize<CollaborationAnchor>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!, result.RootElement.GetProperty("deletion").GetBytesFromBase64());
    }
    [Fact]
    public async Task CommentsAreScopedIdentityBoundRevisionedIdempotentAndDetachedAfterDeletion()
    {
        await Convert(); var member = await InviteMember("editor", CollaborationRole.Editor); await InviteMember("viewer", CollaborationRole.Viewer); await InviteMember("other-editor", CollaborationRole.Editor);
        var page = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page); var fixture = await AnchorFixture();
        var create = new CollaborationCreateComment(Guid.NewGuid(), page.Generation, member.Revision, 0, fixture.Anchor, "<script>Plain comment 日本語</script>");
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Comments.Create("viewer", _document, _page, create, default));
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Comments.Create("stranger", _document, _page, create, default));
        Assert.Equal(1, await Comments.Create("editor", _document, _page, create, default));
        Assert.Equal(1, await Comments.Create("editor", _document, _page, create, default));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => Comments.Create("editor", _document, _page, create with { Body = "altered" }, default));
        var snapshot = await Comments.Read("viewer", _document, _page, page.Generation, default); var thread = Assert.Single(snapshot.Threads);
        Assert.Equal("editor", thread.AuthorId); Assert.False(thread.Detached); Assert.Equal(create.Body, Assert.Single(thread.Messages).Body);
        var reply = new CollaborationCommentMutation(Guid.NewGuid(), page.Generation, 0, 1, "reply", "Owner reply");
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Comments.Change("other-editor", _document, _page, thread.Id, new(Guid.NewGuid(), page.Generation, 1, 1, "resolve"), default));
        Assert.Equal(2, await Comments.Change("owner", _document, _page, thread.Id, reply, default)); Assert.Equal(2, await Comments.Change("owner", _document, _page, thread.Id, reply, default));
        await Assert.ThrowsAsync<CollaborationCommitException>(() => Comments.Change("editor", _document, _page, thread.Id, new(Guid.NewGuid(), page.Generation, 1, 1, "resolve"), default));
        Assert.Equal(3, await Comments.Change("editor", _document, _page, thread.Id, new(Guid.NewGuid(), page.Generation, 1, 2, "resolve"), default));
        Assert.Equal(4, await Comments.Change("owner", _document, _page, thread.Id, new(Guid.NewGuid(), page.Generation, 0, 3, "reopen"), default));
        var updated = await _codec.Project([(await _db.CollaborationSnapshots.AsNoTracking().SingleAsync(s => s.PageId == _page)).State, fixture.Deletion], default);
        // Install a valid committed snapshot under the normal page barrier, simulating deletion/compaction.
        await using (var tx = await _db.Database.BeginTransactionAsync())
        {
            await CollaborationMigrationService.Lock(_db, _document, default);
            _db.CollaborationSnapshots.Add(new() { PageId = _page, Generation = page.Generation, Sequence = 1, State = updated.State, Sha256 = updated.Sha256 });
            await _db.CollaborationPages.Where(p => p.PageId == _page).ExecuteUpdateAsync(p => p.SetProperty(x => x.CommittedSequence, 1).SetProperty(x => x.SnapshotSequence, 1)); await _db.SaveChangesAsync(); await tx.CommitAsync();
        }
        Assert.True(Assert.Single((await Comments.Read("owner", _document, _page, page.Generation, default)).Threads).Detached);
        await Assert.ThrowsAsync<CollaborationCommitException>(() => Comments.Create("owner", _document, _page, create with { OperationId = Guid.NewGuid(), MembershipRevision = 0, ExpectedSequence = 1 }, default));
        await Sharing.Change("owner", _document, "editor", new(1, CollaborationRole.Viewer, true), default);
        await Assert.ThrowsAsync<CollaborationSessionDeniedException>(() => Comments.Read("editor", _document, _page, page.Generation, default));
        long structure = (await _db.CollaborationManifests.AsNoTracking().SingleAsync()).StructureRevision;
        await CollaborationProjectionService.DeletePage(_db, "owner", _document, new(Guid.NewGuid(), structure, _page, page.Generation), default);
        Assert.True(Assert.Single((await Comments.Read("owner", _document, _page, page.Generation, default)).Threads).Detached);
        Assert.Single(await _db.CollaborationComments.ToArrayAsync()); Assert.Single(await _db.CollaborationCommentMessages.ToArrayAsync());
        Assert.Equal(4, await _db.CollaborationCommentOperations.CountAsync());
    }
    [Fact]
    public async Task CommentHttpRoutesRejectViewersStaleScopesAndUnsafeBodiesAndFeatureDisable()
    {
        await Convert(); await InviteMember("editor", CollaborationRole.Editor); await InviteMember("viewer", CollaborationRole.Viewer);
        var page = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page); var fixture = await AnchorFixture();
        using var api = Api(sharing: true); using var client = api.CreateClient(); client.DefaultRequestHeaders.Add("X-Test-Owner", "editor");
        string path = $"/api/documents/{_document}/pages/{_page}/collaboration/comments";
        var create = new CollaborationCreateComment(Guid.NewGuid(), page.Generation, 1, 0, fixture.Anchor, "HTTP comment");
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, create)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, create with { OperationId = Guid.NewGuid(), ExpectedSequence = 10 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, create with { OperationId = Guid.NewGuid(), Anchor = new("AAAA", "AAAA") })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, create with { OperationId = Guid.NewGuid(), Body = "\u0001" })).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-Owner"); client.DefaultRequestHeaders.Add("X-Test-Owner", "viewer");
        var response = await client.PostAsJsonAsync(path + "/read", new CollaborationCommentRead(page.Generation)); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, create)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/import-legacy", new CollaborationCommentRead(page.Generation))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/read", new CollaborationCommentRead(Guid.NewGuid()))).StatusCode);
        _interactionOptions.Enabled = false;
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/read", new CollaborationCommentRead(page.Generation))).StatusCode);
    }
    [Fact]
    public async Task LegacyCommentsImportDetachedWithoutChangingOriginalAndPreventDestructiveRollback()
    {
        Guid annotation = Guid.NewGuid();
        _db.PageAnnotations.Add(new() { Id = annotation, DocumentId = _document, PageId = _page, AuthorUserId = "owner", Content = "Retained legacy note", AnchorFrom = 1, AnchorTo = 20, AnchorText = "Old passage", CreatedAt = DateTimeOffset.UtcNow }); await _db.SaveChangesAsync();
        await Convert(); var page = await _db.CollaborationPages.AsNoTracking().SingleAsync(p => p.PageId == _page);
        Assert.Equal(1, await Comments.ImportLegacy("owner", _document, _page, page.Generation, default));
        Assert.Equal(0, await Comments.ImportLegacy("owner", _document, _page, page.Generation, default));
        var thread = Assert.Single((await Comments.Read("owner", _document, _page, page.Generation, default)).Threads); Assert.True(thread.Detached); Assert.Null(thread.Anchor);
        Assert.Equal("Old passage", (await _db.PageAnnotations.AsNoTracking().SingleAsync()).AnchorText);
        await Assert.ThrowsAnyAsync<Exception>(() => _db.GetService<IMigrator>().MigrateAsync(_database is null ? "20261017200441_CollaborationSharing" : "20261017200449_CollaborationSharingSqlServer"));
        Assert.Single(await _db.CollaborationComments.ToArrayAsync());
    }
}
