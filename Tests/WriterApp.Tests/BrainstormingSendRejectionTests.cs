using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class BrainstormingConversationAdapterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedSendRetainsActualReasonAcrossInspectionAndRestartWithoutDispatch(bool quota)
    {
        await using var f = new BrainstormingConversationTests.Fixture(); await f.Start(true);
        f.Provider.Allowed = quota; f.Provider.QuotaAllowed = !quota;
        using var host = await Host(f); using var http = host.GetTestClient(); http.BaseAddress = new("https://test.invalid/");
        http.DefaultRequestHeaders.Add("X-Test-Identity", "author");
        var transport = new BrainstormingConversationHttp(http, _ => new(() => { }));
        var root = Path.Combine(Path.GetTempPath(), "BrainstormRejectedSend", Guid.NewGuid().ToString("N"));
        var store = new LocalBrainstormingStore(root); var local = f.Send.Scope with { Space = BrainstormingIdentitySpace.Local };
        var session = (await new WriterApp.Application.AI.BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,f.Send.Scope.SessionId,default))!;
        const string instruction = "Keep this exact unsent message Åsa 日本語.";
        var cache = new BrainstormingCache(1,1,BrainstormingPersistence.ReScope(session,local),instruction,
            new(1,local,f.Send.Scope,1,DateTimeOffset.UtcNow),null,
            new(1,Guid.NewGuid(),f.Send.Scope,session.Revision,new('A',64),DateTimeOffset.UtcNow),[],[]);
        await store.WriteAsync(cache,0,Guid.NewGuid());
        var workflow = new BrainstormingConversationWorkflow(store,transport,async (s,p,ct) => (await store.ReadAsync(s,ct))!);
        var rejected = await Assert.ThrowsAsync<BrainstormingSendRejectedException>(async () => {
            await foreach(var item in workflow.SendAsync(local,instruction,null)) { }
        });
        Assert.Equal(quota ? "Quota blocked." : "AI is not authorized.",rejected.Message);
        var pending = (await store.ReadAsync(local))!;
        Assert.NotNull(pending.PendingGeneration); Assert.Contains(rejected.Message,pending.LastGeneration!.Message);
        var inspected = await workflow.InspectAsync(local);
        Assert.Null(inspected.PendingGeneration); Assert.Equal(instruction,inspected.Composer);
        Assert.Contains(rejected.Message,inspected.LastGeneration!.Message);
        var reopened = (await new LocalBrainstormingStore(root).ReadAsync(local))!;
        Assert.Equal(BrainstormingPersistence.Json(inspected.LastGeneration),BrainstormingPersistence.Json(reopened.LastGeneration));
        Assert.Equal(0,f.Provider.Calls); Assert.Equal(0,f.Provider.Charges);
        Assert.Empty(await f.Db.BrainstormingGenerations.ToArrayAsync());
    }
}

public sealed class BrainstormingSendRejectionTransportTests
{
    private sealed class RejectionHandler(Guid? operation) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) {Content=JsonContent.Create(new {
                code="ai.disabled",message="AI is not enabled for your plan.",accepted=false,operationId=operation
            })});
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task RejectionMustMatchExactSendOperation(bool matching)
    {
        var session=BrainstormingContractTests.Fixture();var scope=session.Scope with {Space=BrainstormingIdentitySpace.Server};
        var send=new BrainstormingSend(1,scope,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,session.Contexts[0].Id,session.Targets[0].Id,null,"Explore.");
        using var http=new HttpClient(new RejectionHandler(matching?send.OperationId:Guid.NewGuid())) {BaseAddress=new("https://test.invalid/")};
        var remote=new BrainstormingConversationHttp(http,_=>new(()=>{}));
        var error=await Record.ExceptionAsync(async()=>{await foreach(var item in remote.SendAsync(send)) { }});
        Assert.IsAssignableFrom<HttpRequestException>(error);
        Assert.Equal(matching,error is BrainstormingSendRejectedException);
    }
}
