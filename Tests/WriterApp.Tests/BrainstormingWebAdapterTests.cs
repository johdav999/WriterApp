using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using WriterApp.Application.Security;
using WriterApp.Client.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingWebAdapterTests
{
    // Interop fixture only: real browser transaction/reload behavior is checked by Tests/brainstorming-store.mjs.
    private sealed class JsFixture : IJSRuntime, IJSObjectReference
    {
        private readonly LocalBrainstormingStore store=new(Path.Combine(Path.GetTempPath(),"WriterApp.WebBrainstorming.Tests",Guid.NewGuid().ToString("N")));
        private readonly Dictionary<string,string> reviews=[];
        public bool Offline; public Action? AfterRead;
        private BrainstormingScope? scope;
        public ValueTask<T> InvokeAsync<T>(string name,object?[]? args)=>InvokeAsync<T>(name,default,args);
        public async ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args) {
            ct.ThrowIfCancellationRequested(); args ??=[]; object? result=null;
            switch(name) {
                case "import":result=this;break;
                case "isOffline":result=Offline;break;
                case "rememberIdentity": if(args[1] is string a)reviews[(string)args[0]!]=a;else reviews.Remove((string)args[0]!);break;
                case "reviewIdentity":result=reviews.GetValueOrDefault((string)args[0]!);break;
                case "read":result=scope is null?null:BrainstormingPersistence.Json(await store.ReadAsync(scope,ct));if(result is "null")result=null;AfterRead?.Invoke();break;
                case "write":
                    var value=JsonSerializer.Deserialize<BrainstormingCache>((string)args[3]!,BrainstormingValidation.Json)!;scope=value.Session.Scope;
                    var saved=await store.WriteAsync(value,(long)args[4]!, (Guid)args[5]!,ct);
                    result=JsonSerializer.SerializeToElement(new {conflict=false,json=BrainstormingPersistence.Json(saved)});break;
                case "list":result=(await store.ListAsync(scope!,ct:ct)).Select(BrainstormingPersistence.Json).ToArray();break;
                default:throw new InvalidOperationException("Unexpected fixture call: "+name);
            }
            return result is null?default!:(T)result;
        }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
    }
    private sealed class Handler(Func<HttpRequestMessage,Task<HttpResponseMessage>> send):HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>send(request);
    }
    [Fact]
    public async Task WebWorkflowRetainsFailedSendAndLateSignoutCannotAttachAnAcknowledgement()
    {
        var js=new JsFixture();AuthStateService? auth=null;int posts=0;bool signout=false;
        using var http=new HttpClient(new Handler(async request=>{
            if(request.RequestUri!.AbsolutePath=="/api/auth/me")return new(HttpStatusCode.OK){Content=JsonContent.Create(new AuthMeDto{IsAuthenticated=true,UserId="author"})};
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.NotFound);
            posts++;var value=await request.Content!.ReadFromJsonAsync<BrainstormingWrite>(BrainstormingValidation.Json);
            if(!signout)throw new HttpRequestException("Synthetic offline send");
            auth!.Reset();return new(HttpStatusCode.OK){Content=JsonContent.Create(new BrainstormingReceipt(1,value!.OperationId,value.Session.Scope,value.Session.Revision,BrainstormingPersistence.Hash(value),DateTimeOffset.UtcNow))};
        })){BaseAddress=new("https://test.invalid/")};
        auth=new(http,NullLogger<AuthStateService>.Instance,new(),new());await using var files=new WebBrainstormingStore(js);var adapter=new WebBrainstormingSessions(files,http,auth);
        var scope=await adapter.ScopeAsync(Guid.NewGuid(),null);await adapter.Sessions.CreateAsync(scope);await adapter.Sessions.SaveComposerAsync(scope,"Composer before failed send");
        await adapter.Sessions.AppendUnsentAsync(scope,"Unicode Åsa 日本語",Guid.NewGuid());await adapter.MapAsync(scope);
        await Assert.ThrowsAsync<HttpRequestException>(()=>adapter.SyncAsync(scope));
        var retained=(await files.ReadAsync(scope))!;Assert.NotNull(retained.Pending);Assert.Equal(BrainstormingTurnState.Unsent,Assert.Single(retained.Session.Turns).State);
        await adapter.Sessions.SaveComposerAsync(scope,"New composer after failure");signout=true;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>adapter.SyncAsync(scope));
        retained=(await files.ReadAsync(scope))!;Assert.Null(retained.Acknowledgement);Assert.NotNull(retained.Pending);Assert.Equal("New composer after failure",retained.Composer);Assert.Equal(2,posts);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>adapter.ReadAsync(scope));Assert.Null(await files.ReviewIdentityAsync(scope.BackendKey,default));
    }
    [Fact]
    public async Task WebReloadOfflineReviewNeverAuthorizesNetworkAndRejectsBackendOrResolvedOtherAccount()
    {
        var js=new JsFixture();bool offline=false;string user="author";
        using var http=new HttpClient(new Handler(_=>offline?throw new HttpRequestException("Synthetic disconnected auth bootstrap"):Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new AuthMeDto{IsAuthenticated=true,UserId=user})}))){BaseAddress=new("https://test.invalid/")};
        AuthStateService NewAuth()=>new(http,NullLogger<AuthStateService>.Instance,new(),new());
        await using var files=new WebBrainstormingStore(js);var adapter=new WebBrainstormingSessions(files,http,NewAuth());var scope=await adapter.ScopeAsync(Guid.NewGuid(),null);
        await adapter.Sessions.CreateAsync(scope);await adapter.Sessions.SaveComposerAsync(scope,"Reloaded unsent composer");offline=true;js.Offline=true;
        var reloaded=new WebBrainstormingSessions(files,http,NewAuth());Assert.Equal("Reloaded unsent composer",(await reloaded.ReadAsync(scope))!.Composer);
        await reloaded.Sessions.AppendUnsentAsync(scope,"Offline unsent turn",Guid.NewGuid());await reloaded.MapAsync(scope);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reloaded.SyncAsync(scope));Assert.NotNull((await files.ReadAsync(scope))!.Pending);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>reloaded.ReadAsync(scope with{BackendKey=new('C',64)}));
        offline=false;js.Offline=false;user="different-author";var other=new WebBrainstormingSessions(files,http,NewAuth());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>other.ReadAsync(scope));Assert.Equal(scope.AccountKey,(await files.ReadAsync(scope))!.Session.Scope.AccountKey);
    }
    [Fact]
    public async Task WebReadRejectsLateAccountChangeAfterStorageAndKeepsOriginalCache()
    {
        var js=new JsFixture();using var http=new HttpClient(new Handler(_=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new AuthMeDto{IsAuthenticated=true,UserId="author"})}))){BaseAddress=new("https://test.invalid/")};
        var auth=new AuthStateService(http,NullLogger<AuthStateService>.Instance,new(),new());await using var files=new WebBrainstormingStore(js);var adapter=new WebBrainstormingSessions(files,http,auth);
        var scope=await adapter.ScopeAsync(Guid.NewGuid(),null);await adapter.Sessions.CreateAsync(scope);js.AfterRead=auth.Reset;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>adapter.ReadAsync(scope));js.AfterRead=null;Assert.NotNull(await files.ReadAsync(scope));
    }
}
