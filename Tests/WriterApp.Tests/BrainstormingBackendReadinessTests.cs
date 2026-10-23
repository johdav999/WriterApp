using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingBackendReadinessTests
{
    [Fact]
    public async Task CapabilitiesBlockUnmigratedStorageAndBecomeAvailableAfterDatabaseUpdate()
    {
        await using var sql=new SqliteConnection("Data Source=:memory:");await sql.OpenAsync();
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(sql).Options);
        var controller=new BrainstormingCapabilitiesController(db:db){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
        async Task<BrainstormingCapabilities> Read()=>Assert.IsType<BrainstormingCapabilities>(Assert.IsType<OkObjectResult>((await controller.Get()).Result).Value);
        var missing=await Read();missing.Validate();Assert.False(missing.Sessions.IsAvailable);Assert.False(missing.Conversation.IsAvailable);
        Assert.Contains("database update",missing.Sessions.Message);Assert.Contains("unsent message",missing.Sessions.Message);
        var migrations=db.Database.GetMigrations().ToArray();
        var firstBrainstorm = Array.FindIndex(migrations, m => m.Contains("Brainstorming", StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(migrations[firstBrainstorm - 1]);
        Assert.False((await Read()).Conversation.IsAvailable);
        await db.Database.MigrateAsync();var ready=await Read();ready.Validate();Assert.True(ready.Sessions.IsAvailable);Assert.True(ready.Conversation.IsAvailable);
        Assert.False(ready.Dictation.IsAvailable);
    }

    private sealed class FailingHandler(HttpStatusCode status):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(status){Content=new StringContent("{\"code\":\"server.error\"}")});
    }
    [Theory][InlineData(500)][InlineData(503)]
    public async Task ServerStorageFailureRetainsExactOperationAndComposerWithoutGeneration(int status)
    {
        var root=Path.Combine(Path.GetTempPath(),"BrainstormingBackendFailure",Guid.NewGuid().ToString());var files=new LocalBrainstormingStore(root);
        var scope=BrainstormingContractTests.Fixture().Scope;var server=scope with{Space=BrainstormingIdentitySpace.Server,SessionId=Guid.NewGuid()};
        var session=BrainstormingPersistence.Empty(scope);var pending=new BrainstormingWrite(1,Guid.NewGuid(),0,BrainstormingPersistence.Empty(server) with{State=BrainstormingConversationState.Saved});
        var saved=await files.WriteAsync(new(1,1,session,"Retain Åsa 日本語 🧭",new(1,scope,server,1,DateTimeOffset.UtcNow),pending,null,[],[]),0,Guid.NewGuid());
        using var http=new HttpClient(new FailingHandler((HttpStatusCode)status)){BaseAddress=new("https://test.invalid/")};
        var remote=new BrainstormingHttpRemote(http,_=>new(()=>{}),_=>server);var workflow=new BrainstormingSessionService(files,remote);
        var identities=new BrainstormingIdentityMap(new Dictionary<Guid,Guid>{{scope.DocumentId,server.DocumentId}},new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>(),new Dictionary<Guid,Guid>());
        var error=await Assert.ThrowsAsync<HttpRequestException>(()=>workflow.SyncAsync(scope,identities,()=>{}));
        Assert.Equal((HttpStatusCode)status,error.StatusCode);Assert.Contains($"HTTP {status}",error.Message);Assert.Contains("database",error.Message);Assert.DoesNotContain("sign in",error.Message,StringComparison.OrdinalIgnoreCase);
        var retained=(await files.ReadAsync(scope))!;Assert.Equal(BrainstormingPersistence.Json(saved),BrainstormingPersistence.Json(retained));Assert.Null(retained.PendingGeneration);
    }
}
