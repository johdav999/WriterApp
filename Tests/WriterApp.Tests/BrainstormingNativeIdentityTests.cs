using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using WriterApp.Application.Security;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class BrainstormingConversationAdapterTests
{
    [Fact]
    public async Task NativeDiscussionRecoveryPreservesOriginalOperationsAndIgnoresForeignNamespacesAndTombstones()
    {
        await using var f=new BrainstormingConversationTests.Fixture();await f.Start(true);using var host=await Host(f);
        var account=new DeviceAccountService(new Identity("author.native-tenant"));await account.SignInAsync();
        using var handler=new DeviceAuthenticatedHandler(account,new("https://test.invalid/")){InnerHandler=host.GetTestServer().CreateHandler()};
        using var http=new HttpClient(handler){BaseAddress=new("https://test.invalid/")};http.DefaultRequestHeaders.Add("X-Test-Identity","author");
        var root=Path.Combine(Path.GetTempPath(),"BrainstormingNativeRecovery",Guid.NewGuid().ToString());
        var repository=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"documents")));var doc=await repository.CreateAsync("Recovery");
        var cacheRoot=Path.Combine(root,"sessions");var files=new LocalBrainstormingStore(cacheRoot);
        var oldScope=new BrainstormingScope(1,Guid.NewGuid(),doc.DocumentId,null,BrainstormingIdentitySpace.Local,
            BrainstormingValidation.Fingerprint(http.BaseAddress.AbsoluteUri),BrainstormingValidation.Fingerprint(account.AccountId!),null);
        var rich=BrainstormingPersistence.ReScope(BrainstormingContractTests.Fixture(),oldScope);
        rich=rich with {Revision=1,State=BrainstormingConversationState.PendingSave,
            Targets=rich.Targets.Select(t=>t with{DocumentId=doc.DocumentId}).ToArray(),
            Contexts=rich.Contexts.Select(c=>c with{Sources=c.Sources.Select(s=>s with{DocumentId=doc.DocumentId}).ToArray()}).ToArray()};
        var server=oldScope with{Space=BrainstormingIdentitySpace.Server,DocumentId=f.Data.Document};
        var pending=new BrainstormingWrite(1,Guid.NewGuid(),0,BrainstormingPersistence.Empty(server) with{State=BrainstormingConversationState.Saved});
        var original=await files.WriteAsync(new(1,1,rich,"My unsent Åsa 日本語 🧭 message",new(1,oldScope,server,1,DateTimeOffset.UtcNow),pending,null,[],[],PendingGeneration:f.Send with{Scope=server}),0,Guid.NewGuid());
        var unknownScope=oldScope with{SessionId=Guid.NewGuid()};var unknown=BrainstormingPersistence.ReScope(rich,unknownScope);
        unknown=unknown with{Drafts=[],Approvals=[],Applications=[],State=BrainstormingConversationState.LocalOnly,
            Requests=unknown.Requests.Select(r=>r with{State=BrainstormingGenerationState.Unknown}).ToArray(),
            Turns=unknown.Turns.Select(t=>t.Role==BrainstormingRole.Assistant?t with{State=BrainstormingTurnState.Incomplete}:t).ToArray()};
        await files.WriteAsync(new(1,1,unknown,"Uncertain generation",null,null,null,[],[]),0,Guid.NewGuid());
        foreach(var excluded in new[]{oldScope with{AccountKey=BrainstormingValidation.Fingerprint("other-native"),SessionId=Guid.NewGuid()},
            oldScope with{BackendKey=BrainstormingValidation.Fingerprint("https://other.invalid/"),SessionId=Guid.NewGuid()},oldScope with{SessionId=Guid.NewGuid()}})
            await files.WriteAsync(new(1,1,BrainstormingPersistence.Empty(excluded) with{State=excluded.AccountKey==oldScope.AccountKey && excluded.BackendKey==oldScope.BackendKey?BrainstormingConversationState.Deleted:BrainstormingConversationState.LocalOnly},"Excluded",null,null,null,[],[]),0,Guid.NewGuid());
        var originals=Directory.GetFiles(cacheRoot,"*.json",SearchOption.AllDirectories).ToDictionary(p=>p,File.ReadAllBytes);
        var network=new DeviceConnectivity();var options=new DeviceHostOptions("Recovery",http.BaseAddress);
        var sessions=new DeviceBrainstormingSessions(files,account,options,network,repository,http);
        var canonical=await sessions.ScopeAsync(doc.DocumentId,oldScope.SessionId);Assert.Equal(BrainstormingValidation.Fingerprint("author"),canonical.AccountKey);
        var recovered=(await sessions.ReadAsync(canonical))!;Assert.Equal(original.Composer,recovered.Composer);
        Assert.Equal(original.Session.Turns.Select(t=>t.Text),recovered.Session.Turns.Select(t=>t.Text));
        Assert.Equal(original.Session.Drafts.Select(d=>d.EditorHtml),recovered.Session.Drafts.Select(d=>d.EditorHtml));
        Assert.All(recovered.Session.Requests,r=>Assert.Equal(BrainstormingGenerationState.Completed,r.State));
        var interrupted=(await sessions.ReadAsync(unknownScope with{AccountKey=canonical.AccountKey}))!;
        Assert.All(interrupted.Session.Requests,r=>Assert.Equal(BrainstormingGenerationState.Interrupted,r.State));
        Assert.Null(recovered.Mapping);Assert.Null(recovered.Pending);Assert.Null(recovered.Acknowledgement);Assert.Null(recovered.PendingGeneration);
        Assert.Null(recovered.Keeps);Assert.Null(recovered.PlanningOperations);Assert.Null(recovered.Recovery);Assert.Empty(recovered.Session.Approvals);Assert.Empty(recovered.Session.Applications);
        Assert.Equal(2,(await sessions.ListAsync(canonical)).Count);Assert.Equal(0,f.Provider.Calls);
        foreach(var (path,bytes) in originals)Assert.Equal(bytes,await File.ReadAllBytesAsync(path));
        var edited=await sessions.Sessions.SaveComposerAsync(canonical,"Edited after recovery");
        await sessions.ScopeAsync(doc.DocumentId,oldScope.SessionId);Assert.Equal("Edited after recovery",(await sessions.ReadAsync(canonical))!.Composer);
        network.SetOnline(false);
        var reopened=new DeviceBrainstormingSessions(new LocalBrainstormingStore(cacheRoot),account,options,network,repository,http);
        Assert.Equal(canonical,await reopened.ScopeAsync(doc.DocumentId,oldScope.SessionId));Assert.Equal(edited.Composer,(await reopened.ReadAsync(canonical))!.Composer);
        await Assert.ThrowsAsync<HttpRequestException>(()=>reopened.Conversation.ReadStoryAsync(server with{AccountKey=canonical.AccountKey}));
        await account.SignOutAsync();await Assert.ThrowsAsync<OperationCanceledException>(()=>sessions.ReadAsync(canonical));
        foreach(var (path,bytes) in originals)Assert.Equal(bytes,await File.ReadAllBytesAsync(path));
    }

    private sealed class ProfileHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> reply):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>reply(request,ct);
    }
    [Theory][InlineData("unauthenticated")][InlineData("missing-owner")][InlineData("account-change")][InlineData("forbidden")]
    public async Task NativeCanonicalVerificationRejectsUntrustedOrChangedProfileWithoutRecovery(string failure)
    {
        var root=Path.Combine(Path.GetTempPath(),"BrainstormingNativeGuard",Guid.NewGuid().ToString());
        var repository=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"documents")));var doc=await repository.CreateAsync("Guard");
        var account=new DeviceAccountService(new Identity("native"));await account.SignInAsync();
        using var handler=new ProfileHandler(async (_,_)=>{
            if(failure=="account-change")await account.SignOutAsync();
            return new(failure=="forbidden"?HttpStatusCode.Forbidden:HttpStatusCode.OK){Content=JsonContent.Create(new AuthMeDto{IsAuthenticated=failure!="unauthenticated",UserId=failure=="missing-owner"?null:"author"})};
        });
        using var http=new HttpClient(handler){BaseAddress=new("https://test.invalid/")};var cacheRoot=Path.Combine(root,"sessions");
        var sessions=new DeviceBrainstormingSessions(new LocalBrainstormingStore(cacheRoot),account,new("Guard",http.BaseAddress),new(),repository,http);
        if(failure=="account-change")await Assert.ThrowsAsync<OperationCanceledException>(()=>sessions.ScopeAsync(doc.DocumentId));
        else if(failure=="forbidden")await Assert.ThrowsAsync<HttpRequestException>(()=>sessions.ScopeAsync(doc.DocumentId));
        else await Assert.ThrowsAsync<InvalidDataException>(()=>sessions.ScopeAsync(doc.DocumentId));
        Assert.False(Directory.Exists(Path.Combine(cacheRoot,"backend-accounts")));Assert.Null(sessions.ActiveAccountKey);
    }
}
