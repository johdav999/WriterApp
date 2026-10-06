using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Client.Services;
using WriterApp.Client.State;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    [Fact]
    public async Task WebHistoryMalformedCachedConfirmationCannotManufactureCloudSuccess() {
        await using var f=new TranslationFixture();await f.Start();var intent=await HistoryIntent(f);var storage=new HistoryMemoryModule();var lease=new WebCheckedAi.Lease(intent.Source,f.Http.BaseAddress!.AbsoluteUri,null);
        await using var outbox=new WebAiHistoryOutbox(f.Http,storage,new WebCheckedAi(f.Http));await outbox.Prepare(intent,lease);
        storage.CorruptConfirmation(lease.Backend+"|"+lease.Source.AccountKey,intent.OperationId);
        await Assert.ThrowsAsync<InvalidDataException>(()=>outbox.List(lease));await using var db=new AppDbContext(f.Options);Assert.Null((await db.WebAiHistoryOperations.SingleAsync()).ReportedAt);Assert.Equal(intent.BeforeContent,(await db.Pages.SingleAsync(p=>p.Id==f.Pages[0])).Content);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WebHistorySceneContentUsesItsOwnedSceneIdentityAndNeverReplaysAnAggregateOrForeignPage(bool laterEdit) {
        await using var f=new TranslationFixture();await f.Start();await using(var db=new AppDbContext(f.Options)){(await db.ProjectNodes.SingleAsync()).NodeType=WriterApp.Data.Documents.ProjectNodeType.Scene;await db.SaveChangesAsync();}
        string endpoint=$"api/projects/{f.Project}/scenes/{f.Scene}/content";using var initial=await f.Http.PutAsJsonAsync(endpoint,new{ContentJson="<p>Authored scene</p>"});initial.EnsureSuccessStatusCode();
        var source=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&sceneId={f.Scene}"))!;
        var proposal=Guid.NewGuid();await using(var db=new AppDbContext(f.Options)) {db.AiActionHistoryEntries.Add(new(){Id=proposal,OwnerUserId="ordinary-author",DocumentId=f.Document,SectionId=f.Sections[0],ActionKey="rewrite.selection",CreatedAt=DateTimeOffset.UtcNow,ResultJson=JsonSerializer.Serialize(new AiActionExecuteResponseDto(proposal,"Authored scene","Reviewed scene",null,DateTimeOffset.UtcNow,"rewrite.selection",WebSource:source),WebAiHistoryContracts.Json)});await db.SaveChangesAsync();}
        var id=Guid.NewGuid();var intent=new WebAiHistoryIntent(1,id,id,proposal,1,null,"Applied",source,"SceneContent","<p>Authored scene</p>","<p><em>Reviewed scene</em></p>");
        var storage=new HistoryMemoryModule();var lease=new WebCheckedAi.Lease(source,f.Http.BaseAddress!.AbsoluteUri,null);await using var outbox=new WebAiHistoryOutbox(f.Http,storage,new WebCheckedAi(f.Http));await outbox.Prepare(intent,lease);using var saved=await SaveHistory(f,intent);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        if(laterEdit)await f.Http.PutAsJsonAsync(endpoint,new{ContentJson="<p>Later scene</p>"});await outbox.Flush(lease);Assert.Equal("Confirmed",Assert.Single(await outbox.List(lease)).Status);
        var current=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&sceneId={f.Scene}"))!;
        using var move=await f.Http.PostAsJsonAsync("api/ai/actions/history/web/move",new WebAiHistoryMoveRequest(current,"Undone"));Assert.Equal(laterEdit?HttpStatusCode.Conflict:HttpStatusCode.OK,move.StatusCode);
        if(!laterEdit){var undo=(await move.Content.ReadFromJsonAsync<WebAiHistoryMove>())!.Intent;Assert.Equal("SceneContent",undo.TargetKind);using var prepare=await f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",undo);prepare.EnsureSuccessStatusCode();using var undone=await SaveHistory(f,undo);Assert.Equal(HttpStatusCode.OK,undone.StatusCode);}
        await using var verify=new AppDbContext(f.Options);Assert.Equal(f.Html[3],(await verify.Pages.SingleAsync(p=>p.Id==f.Pages[3])).Content);
    }
    [Theory][InlineData("account")][InlineData("signout")][InlineData("backend")][InlineData("cancel")]
    public async Task WebHistoryClientNeverDeliversOutsideItsResolvedAccountBackendOrCancelledLifetime(string boundary) {
        await using var f=new TranslationFixture();await f.Start();var intent=await HistoryIntent(f);var storage=new HistoryMemoryModule();var auth=new AuthStateService(f.Http,NullLogger<AuthStateService>.Instance,new DeletedAccountStateService(),new DuplicateAccountStateService());
        void Account(string? owner) {typeof(AuthStateService).GetField("_cached",TranslationPrivate)!.SetValue(auth,owner is null?AuthState.Anonymous:new AuthState(true,"synthetic",owner,new Dictionary<string,string>()));typeof(AuthStateService).GetField("_cachedAtUtc",TranslationPrivate)!.SetValue(auth,DateTimeOffset.UtcNow);}
        Account("ordinary-author");var ai=new WebCheckedAi(f.Http,auth);var lease=new WebCheckedAi.Lease(intent.Source,f.Http.BaseAddress!.AbsoluteUri,"ordinary-author");await using var outbox=new WebAiHistoryOutbox(f.Http,storage,ai);await outbox.Prepare(intent,lease);using(await SaveHistory(f,intent)){}
        if(boundary=="account")Account("other-owner");if(boundary=="signout")Account(null);
        if(boundary=="cancel") {using var cancel=new CancellationTokenSource();cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>outbox.Flush(lease,cancel.Token));}
        else await Assert.ThrowsAsync<InvalidOperationException>(()=>outbox.Flush(boundary=="backend"?lease with {Backend="http://other-backend/"}:lease));
        await using(var db=new AppDbContext(f.Options))Assert.Null((await db.WebAiHistoryOperations.SingleAsync()).ReportedAt);
        Account("ordinary-author");await outbox.Flush(lease);Assert.Equal("Confirmed",Assert.Single(await outbox.List(lease)).Status);
    }
    [Theory][InlineData("uncommitted")][InlineData("lost-commit-ack")][InlineData("later-edit")]
    public async Task WebHistoryAggregateReconcilesItsOwnDurableReceiptWithoutTreatingSnapshotsAsPageHtml(string boundary) {
        await using var f=new TranslationFixture();await f.Start();var approval=await f.Approval("section","replace");
        var source=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&pageId={f.Pages[0]}"))!;
        await using(var db=new AppDbContext(f.Options)) {var row=await db.AiActionHistoryEntries.SingleAsync(p=>p.Id==approval.ProposalId);var result=JsonSerializer.Deserialize<AiActionExecuteResponseDto>(row.ResultJson,WebAiHistoryContracts.Json)!;row.ResultJson=JsonSerializer.Serialize(result with {WebSource=source},WebAiHistoryContracts.Json);await db.SaveChangesAsync();}
        var intent=new WebAiHistoryIntent(1,approval.OperationId,approval.OperationId,approval.ProposalId,1,null,"Applied",source,"Aggregate",null,null,approval.OperationId);
        var storage=new HistoryMemoryModule();var lease=new WebCheckedAi.Lease(source,f.Http.BaseAddress!.AbsoluteUri,null);await using(var outbox=new WebAiHistoryOutbox(f.Http,storage,new WebCheckedAi(f.Http))) {await outbox.Prepare(intent,lease);var receipt=await f.Approve(approval);if(boundary!="uncommitted")await f.Commit(receipt.OperationId);}
        if(boundary=="later-edit")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Later aggregate edit"});
        f.Restart();await using var reopened=new WebAiHistoryOutbox(f.Http,storage,new WebCheckedAi(f.Http));await reopened.Flush(lease);
        Assert.Equal(boundary=="uncommitted"?"Prepared":"Confirmed",Assert.Single(await reopened.List(lease)).Status);await using var verify=new AppDbContext(f.Options);var operation=await verify.WebAiHistoryOperations.SingleAsync();Assert.Equal(boundary!="uncommitted",operation.ReportedAt is not null);
        if(boundary!="uncommitted") {Assert.Contains("originalCopyId",operation.SavedResponseJson);Assert.Null(WebAiHistoryOperations.Intent(operation).AfterContent);var pageSource=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&pageId={f.Pages[0]}"))!;using var move=await f.Http.PostAsJsonAsync("api/ai/actions/history/web/move",new WebAiHistoryMoveRequest(pageSource,"Undone"));Assert.Equal(HttpStatusCode.NoContent,move.StatusCode);}
    }
    [Fact]
    public async Task WebHistoryConcurrentTabsCommitAtMostOneDifferentApprovalAndReplayCannotOverwriteLaterWriting() {
        await using var f=new TranslationFixture();await f.Start();var first=await HistoryIntent(f);var second=first with {OperationId=Guid.NewGuid(),ApplicationId=Guid.NewGuid(),AfterContent="<p>Other approved proposal</p>"};second=second with {ApplicationId=second.OperationId};
        var preparations=await Task.WhenAll(f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",first),f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",second));foreach(var response in preparations){Assert.Equal(HttpStatusCode.OK,response.StatusCode);response.Dispose();}
        var saves=await Task.WhenAll(SaveHistory(f,first),SaveHistory(f,second));Assert.Single(saves,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(saves,r=>r.StatusCode==HttpStatusCode.Conflict);foreach(var response in saves)response.Dispose();
        await using(var db=new AppDbContext(f.Options))Assert.Single(await db.WebAiHistoryOperations.Where(o=>o.CommittedAt!=null).ToListAsync());
        await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Later writing"});
        await using var read=new AppDbContext(f.Options);var committed=WebAiHistoryOperations.Intent(await read.WebAiHistoryOperations.SingleAsync(o=>o.CommittedAt!=null));using var replay=await SaveHistory(f,committed);Assert.Equal(HttpStatusCode.Conflict,replay.StatusCode);Assert.Equal("Later writing",(await read.Pages.SingleAsync(p=>p.Id==f.Pages[0])).Content);
        using var mismatch=await SaveHistory(f,committed with {AfterContent="Tampered"});Assert.Equal(HttpStatusCode.Conflict,mismatch.StatusCode);
    }
}
