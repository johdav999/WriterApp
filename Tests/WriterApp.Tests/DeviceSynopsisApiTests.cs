using System.Net;
using System.Net.Http.Json;
using WriterApp.Application.Synopsis;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;
public sealed class DeviceSynopsisApiTests
{
    private sealed class Http : HttpMessageHandler {
        public HttpStatusCode Status=HttpStatusCode.OK;public string Code="";public Uri? Url;public SynopsisAiRequestDto? Request;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct) {
            Url=r.RequestUri;Request=await r.Content!.ReadFromJsonAsync<SynopsisAiRequestDto>(cancellationToken:ct);
            return new(Status){Content=Status==HttpStatusCode.OK?JsonContent.Create(new SynopsisAiResponseDto("evaluate","Strengths: Clear",null,null,1,Guid.NewGuid(),Guid.NewGuid(),"v1",Request!.SourceSynopsis)):JsonContent.Create(new{code=Code})};
        }
    }
    [Theory][InlineData("evaluate")][InlineData("questions")][InlineData("suggest")]
    public async Task AdapterSendsPinnedSourceAndNotesToDedicatedWorkflow(string mode) {
        var handler=new Http();using var http=new HttpClient(handler){BaseAddress=new("https://test.invalid/")};var id=Guid.NewGuid();var source=new SyncSynopsis(Logline:"Saved source",Notes:"Author notes");
        var r=new SynopsisAiRequestDto(mode=="suggest"?"logline":null,"Coaching notes",1,"v1",source,Guid.NewGuid());
        await new DeviceAiApi(http).ExecuteSynopsisAsync(id,mode,r,default);Assert.Equal($"https://test.invalid/api/documents/{id}/synopsis/ai/{mode}",handler.Url!.AbsoluteUri);Assert.Equal(r,handler.Request);
    }
    [Theory][InlineData(409,"ai.stale_source",DeviceAiFailure.Invalid)][InlineData(402,"AI_QUOTA_EXCEEDED",DeviceAiFailure.Quota)][InlineData(403,"entitlement_denied",DeviceAiFailure.Upgrade)][InlineData(401,"",DeviceAiFailure.Authentication)]
    public async Task AdapterUsesExistingActionablePolicyErrors(int status,string code,DeviceAiFailure kind) {
        var handler=new Http{Status=(HttpStatusCode)status,Code=code};using var http=new HttpClient(handler){BaseAddress=new("https://test.invalid/")};
        var r=new SynopsisAiRequestDto(null,"",1,"v1",new(),Guid.NewGuid());var error=await Assert.ThrowsAsync<DeviceAiException>(()=>new DeviceAiApi(http).ExecuteSynopsisAsync(Guid.NewGuid(),"evaluate",r,default));Assert.Equal(kind,error.Kind);
    }
}
