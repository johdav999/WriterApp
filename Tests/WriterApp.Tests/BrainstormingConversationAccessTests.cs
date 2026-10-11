using System.Net;
using System.Text;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingConversationAccessTests
{
    public static IEnumerable<object[]> Revocations()
    {
        foreach(var action in new[]{"story","context","stream","inspect","stop"})
            foreach(var status in new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden,HttpStatusCode.NotFound})
                yield return new object[]{action,status};
    }

    private sealed class ResponseHandler(HttpStatusCode status,string body):HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body,Encoding.UTF8,"application/json")});
        }
    }

    private static BrainstormingSend Send()
    {
        var session=BrainstormingContractTests.Fixture();
        return new(1,session.Scope with{Space=BrainstormingIdentitySpace.Server},Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),session.Revision,session.Contexts[0].Id,session.Targets[0].Id,null,"Explore without changing writing.");
    }

    private static async Task Invoke(BrainstormingConversationHttp transport,BrainstormingSend send,string action)
    {
        switch(action)
        {
            case "story":await transport.ReadStoryAsync(send.Scope);break;
            case "context":await transport.PrepareAsync(new(1,send.Scope,new(SectionId:send.Scope.DocumentId),[],[],60_000));break;
            case "stream":await foreach(var item in transport.SendAsync(send)){}break;
            case "inspect":await transport.InspectAsync(send.Scope,send.OperationId);break;
            case "stop":await transport.StopAsync(send.Scope,send.OperationId);break;
            default:throw new ArgumentOutOfRangeException(nameof(action));
        }
    }

    [Theory][MemberData(nameof(Revocations))]
    public async Task AccessLossRevokesReviewWithoutReplayOrFalseOperationAbsence(string action,HttpStatusCode status)
    {
        using var handler=new ResponseHandler(status,"{\"code\":\"brainstorming.access_revoked\"}");
        using var http=new HttpClient(handler){BaseAddress=new("https://test.invalid/")};
        var revoked=0;var transport=new BrainstormingConversationHttp(http,_=>new(()=>{},AccessRevoked:()=>revoked++));
        var error=await Record.ExceptionAsync(()=>Invoke(transport,Send(),action));
        Assert.True(error is HttpRequestException or OperationCanceledException);
        Assert.Equal(1,revoked);Assert.Equal(1,handler.Requests);
    }

    [Theory][InlineData("story")][InlineData("context")][InlineData("stream")][InlineData("stop")]
    public async Task MissingBackendWithoutRevocationCodeDoesNotClearOwnedReview(string action)
    {
        using var handler=new ResponseHandler(HttpStatusCode.NotFound,"{}");
        using var http=new HttpClient(handler){BaseAddress=new("https://test.invalid/")};
        var revoked=0;var transport=new BrainstormingConversationHttp(http,_=>new(()=>{},AccessRevoked:()=>revoked++));
        await Assert.ThrowsAsync<HttpRequestException>(()=>Invoke(transport,Send(),action));
        Assert.Equal(0,revoked);Assert.Equal(1,handler.Requests);
    }
}
