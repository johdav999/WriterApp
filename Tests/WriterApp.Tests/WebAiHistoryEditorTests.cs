using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Client.Components.Editor;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class HistoryTransport(TranslationFixture f):DelegatingHandler(f.Server.GetTestServer().CreateHandler()) {
        public string? Failure;public int ReportCalls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message,CancellationToken ct) {
            bool save=message.Method==HttpMethod.Put && message.RequestUri!.AbsolutePath.Contains("/pages/");
            bool report=message.RequestUri!.AbsolutePath.EndsWith("/report");
            if(report)ReportCalls++;
            if(report && Failure=="report-cancel")throw new OperationCanceledException("Synthetic reporting cancellation after content save");
            if(save && Failure=="save-offline" || report && Failure=="report-offline")throw new HttpRequestException("Synthetic offline boundary");
            var response=await base.SendAsync(message,ct);
            if(Failure=="malformed-receipt" && message.Method==HttpMethod.Get && message.RequestUri.AbsolutePath.Contains("/history/web/operations/")) {
                response.Content.Dispose(); response.Content=new StringContent("{",System.Text.Encoding.UTF8,"application/json");
            }
            if(Failure=="bad-receipt" && message.Method==HttpMethod.Get && message.RequestUri.AbsolutePath.Contains("/history/web/operations/")) {
                var receipt=(await response.Content.ReadFromJsonAsync<WebAiHistoryReceipt>(ct))!;response.Content.Dispose();response.Content=JsonContent.Create(receipt with {RequestHash=new string('0',64)});
            }
            if(save && Failure=="commit-ack" || report && Failure=="report-ack") {response.Dispose();throw new HttpRequestException("Synthetic lost acknowledgement after commit");}
            return response;
        }
    }
    [Theory][InlineData(null)][InlineData("commit-ack")][InlineData("report-ack")][InlineData("report-offline")][InlineData("save-offline")][InlineData("storage")][InlineData("bad-receipt")][InlineData("report-cancel")][InlineData("malformed-receipt")]
    public async Task ActualPageEditorRetainsDurableIntentBeforeSaveAndSeparatesHistoryFailureFromPersistence(string? failure) {
        await using var f=new TranslationFixture();await f.Start();var intent=await HistoryIntent(f);var storage=new HistoryMemoryModule {RefuseStorage=failure=="storage"};
        using var transport=new HistoryTransport(f){Failure=failure};using var http=new HttpClient(transport){BaseAddress=f.Http.BaseAddress};http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");var ai=new WebCheckedAi(http);
        await using var outbox=new WebAiHistoryOutbox(http,storage,ai);var editor=new PageEditor {AiHistoryOutbox=outbox,CheckedAi=ai};
        void Property(string name,object value)=>typeof(PageEditor).GetProperty(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.SetValue(editor,value);
        Property("Http",http);Property("JSRuntime",storage);Property("Logger",NullLogger<PageEditor>.Instance);
        var page=(await f.Http.GetFromJsonAsync<WriterApp.Application.Documents.PageDto[]>($"api/sections/{f.Sections[0]}/pages"))!.Single(p=>p.Id==f.Pages[0]);
        typeof(PageEditor).GetField("_currentPage",TranslationPrivate)!.SetValue(editor,page);typeof(PageEditor).GetField("_content",TranslationPrivate)!.SetValue(editor,page.Content);
        var lease=new WebCheckedAi.Lease(intent.Source,http.BaseAddress!.AbsoluteUri,null);editor.SetAiSaveSource(intent.Source);editor.SetAiHistoryProposal(intent.ProposalId,lease);
        var save=(Task<EditorSaveResult>)typeof(PageEditor).GetMethod("SaveContentAsync",TranslationPrivate)!.Invoke(editor,[intent.AfterContent!,CancellationToken.None])!;var result=await save;
        Assert.Equal(failure is not ("commit-ack" or "save-offline" or "storage"),result.Succeeded);
        await using(var verify=new AppDbContext(f.Options)) {var current=(await verify.Pages.SingleAsync(p=>p.Id==f.Pages[0])).Content;Assert.Equal(failure is "save-offline" or "storage"?intent.BeforeContent:intent.AfterContent,current);if(failure=="storage")Assert.Empty(await verify.WebAiHistoryOperations.ToListAsync());}
        if(failure=="storage")return;
        transport.Failure=null;await outbox.Flush(lease);var entries=await outbox.List(lease);
        Assert.Equal(failure=="save-offline"?"Prepared":"Confirmed",Assert.Single(entries).Status);
        await using var db=new AppDbContext(f.Options);Assert.Single(await db.WebAiHistoryOperations.ToListAsync());Assert.Equal(0,await db.UsageEvents.CountAsync());
    }
    private sealed class HistoryPlan : WriterApp.Application.Subscriptions.IEntitlementService {
        public WriterApp.Application.Subscriptions.PlanTier Tier=WriterApp.Application.Subscriptions.PlanTier.Free;
        public Task<WriterApp.Application.Subscriptions.UserEntitlements> GetEntitlementsAsync(string owner)=>Task.FromResult(new WriterApp.Application.Subscriptions.UserEntitlements(owner,"professional","Professional",new Dictionary<string,string>()));
        public WriterApp.Application.Subscriptions.PlanTier GetUserTier(WriterApp.Application.Subscriptions.UserEntitlements e)=>Tier;
        public Task<bool> HasAsync(string owner,string key)=>Task.FromResult(true);public Task<int?> GetIntAsync(string owner,string key)=>Task.FromResult<int?>(0);public void InvalidateForUser(string owner){}
    }
    [Fact]
    public async Task WebHistoryPlanRejectionRetainsCommittedWritingAndRetryDoesNotConsumeAiQuota() {
        var plan=new HistoryPlan();await using var f=new TranslationFixture {Configure=s=>s.AddSingleton<WriterApp.Application.Subscriptions.IEntitlementService>(plan)};await f.Start();var intent=await HistoryIntent(f);var storage=new HistoryMemoryModule();var lease=new WebCheckedAi.Lease(intent.Source,f.Http.BaseAddress!.AbsoluteUri,null);
        await using var outbox=new WebAiHistoryOutbox(f.Http,storage,new WebCheckedAi(f.Http));await outbox.Prepare(intent,lease);using var saved=await SaveHistory(f,intent);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);await outbox.Flush(lease);
        Assert.Equal("Rejected",Assert.Single(await outbox.List(lease)).Status);await using(var db=new AppDbContext(f.Options)){Assert.Equal(intent.AfterContent,(await db.Pages.SingleAsync(p=>p.Id==f.Pages[0])).Content);Assert.Null((await db.WebAiHistoryOperations.SingleAsync()).ReportedAt);Assert.Empty(await db.UsageEvents.ToListAsync());}
        plan.Tier=WriterApp.Application.Subscriptions.PlanTier.Professional;await outbox.Flush(lease);Assert.Equal("Confirmed",Assert.Single(await outbox.List(lease)).Status);
    }
    [Theory][InlineData("deleted-document")][InlineData("deleted-owner")][InlineData("wrong-owner")]
    public async Task WebHistoryDeletionOrSignOutNeverReportsAnotherOwnersSavedIntent(string mode) {
        await using var f=new TranslationFixture();await f.Start();var intent=await HistoryIntent(f);var storage=new HistoryMemoryModule();var lease=new WebCheckedAi.Lease(intent.Source,f.Http.BaseAddress!.AbsoluteUri,null);await using var outbox=new WebAiHistoryOutbox(f.Http,storage,new WebCheckedAi(f.Http));await outbox.Prepare(intent,lease);using(await SaveHistory(f,intent)){}
        if(mode=="wrong-owner") {f.Http.DefaultRequestHeaders.Remove("X-Test-Owner");f.Http.DefaultRequestHeaders.Add("X-Test-Owner","other-owner");}
        else {await using var db=new AppDbContext(f.Options);if(mode=="deleted-document")(await db.Documents.SingleAsync(d=>d.Id==f.Document)).DeletedAtUtc=DateTime.UtcNow;else db.DeletedUserIdentities.Add(new(){UserId="ordinary-author",DeletedAtUtc=DateTime.UtcNow});await db.SaveChangesAsync();}
        await outbox.Flush(lease);Assert.Equal("Rejected",Assert.Single(await outbox.List(lease)).Status);await using var verify=new AppDbContext(f.Options);Assert.Null((await verify.WebAiHistoryOperations.SingleAsync()).ReportedAt);
    }
}
