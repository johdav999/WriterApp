using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private static async Task<WebTranslationApproval> CheckedAggregateApproval(TranslationFixture f,string scope="section",string mode="replace") {
        var lease=await new WebCheckedAi(f.Http).Capture(f.Document,f.Sections[0],f.Pages[0]);
        var source=await f.Source(scope);var original=TranslationFixture.Capture(source);var translated=TranslationFixture.Translate(original);var proposal=Guid.NewGuid();
        await f.SaveProposal(source,original,translated,proposal,lease.Source);
        var approval=new WebTranslationApproval(Guid.NewGuid(),proposal,source,original,translated,mode,"auto");
        var intent=new WebAiHistoryIntent(1,approval.OperationId,approval.OperationId,proposal,1,null,"Applied",lease.Source,"Aggregate",null,null,approval.OperationId);
        using var prepared=await f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",intent);
        Assert.True(prepared.IsSuccessStatusCode,await prepared.Content.ReadAsStringAsync());
        await f.Approve(approval);return approval;
    }
    private static async Task<WebRecoveryItem> AggregateItem(TranslationFixture f)=>Assert.Single((await f.Http.GetFromJsonAsync<WebRecoveryItem[]>($"api/ai/actions/history/recovery/{f.Document}"))!,i=>i.TargetKind=="Aggregate");
    [Theory][InlineData("section")][InlineData("document")]
    public async Task WebAggregateRecoveryRestoresRichOrderedPagesAndActualMirrorsAcrossRestart(string scope) {
        await using var f=new TranslationFixture();await f.Start();var approval=await CheckedAggregateApproval(f,scope);await f.Commit(approval.OperationId);
        var item=await AggregateItem(f);Assert.True(item.CanUndo);Assert.False(item.CanRedo);
        string[] after;await using(var db=new AppDbContext(f.Options))after=(await db.Pages.ToListAsync()).OrderBy(p=>Array.IndexOf(f.Pages,p.Id)).Select(p=>p.Content).ToArray();
        await using(var db=new AppDbContext(f.Options)) {
            if(scope=="section")(await db.Pages.FindAsync(f.Pages[3]))!.Content="<p><em>Unrelated 日本語</em></p>";
            // An unchanged empty page within the approved scope is not owned by this action.
            (await db.Pages.FindAsync(f.Pages[2]))!.Content="<p>Later previously empty page.</p>";
            (await db.Sections.FindAsync(f.Sections[0]))!.NarrativePurpose="Later purpose";
            await db.SaveChangesAsync();
        }
        var undo=await ReplayRecovery(f,item,"Undone");f.Restart();
        using(var retry=await f.Http.PostAsJsonAsync("api/ai/actions/history/recovery/replay",undo.Request))Assert.Equal(undo.Receipt,(await retry.Content.ReadFromJsonAsync<WebRecoveryReceipt>()));
        item=await AggregateItem(f);Assert.False(item.CanUndo);Assert.True(item.CanRedo);
        await using(var db=new AppDbContext(f.Options)) {
            Assert.Equal(f.Html[0],(await db.Pages.FindAsync(f.Pages[0]))!.Content);Assert.Equal(f.Html[1],(await db.Pages.FindAsync(f.Pages[1]))!.Content);
            Assert.Equal("<p>Later previously empty page.</p>",(await db.Pages.FindAsync(f.Pages[2]))!.Content);
            Assert.Equal(scope=="section"?"<p><em>Unrelated 日本語</em></p>":f.Html[3],(await db.Pages.FindAsync(f.Pages[3]))!.Content);
            Assert.Empty(await db.SceneContents.ToListAsync());Assert.Equal("sv",(await db.Sections.FindAsync(f.Sections[0]))!.LanguageCode);
            Assert.Equal("Later purpose",(await db.Sections.FindAsync(f.Sections[0]))!.NarrativePurpose);
            var entry=Assert.Single(await new EfCoreAiActionHistoryStore(db).ListAsync("ordinary-author",f.Document,default));Assert.False(entry.IsApplied);Assert.True(entry.CanCloudRedo);
        }
        var redo=await ReplayRecovery(f,item,"Redone");f.Restart();
        using(var retry=await f.Http.PostAsJsonAsync("api/ai/actions/history/recovery/replay",redo.Request))Assert.Equal(redo.Receipt,(await retry.Content.ReadFromJsonAsync<WebRecoveryReceipt>()));
        await using(var db=new AppDbContext(f.Options)) {
            Assert.Equal(after[0],(await db.Pages.FindAsync(f.Pages[0]))!.Content);Assert.Equal(after[1],(await db.Pages.FindAsync(f.Pages[1]))!.Content);
            Assert.Contains("<strong>EN Opening</strong>",(await db.SceneContents.SingleAsync()).ContentJson);
            Assert.Equal("en",(await db.Sections.FindAsync(f.Sections[0]))!.LanguageCode);Assert.Equal(scope=="document"?"en":"sv",(await db.Documents.FindAsync(f.Document))!.LanguageCode);
            Assert.Equal(4,await db.Pages.CountAsync());Assert.Equal(3,await db.WebAiHistoryOperations.CountAsync());Assert.Empty(await db.UsageEvents.ToListAsync());
        }
        await ReplayRecovery(f,item,"Undone");
    }
    [Theory][InlineData("page")][InlineData("mirror")][InlineData("order")][InlineData("deleted")][InlineData("language")][InlineData("history-storage")][InlineData("mid-page-storage")][InlineData("legacy")][InlineData("foreign")][InlineData("race")]
    public async Task WebAggregateRecoveryRefusesUnsafeChangesAndRollsBackWholeReplay(string failure) {
        await using var f=new TranslationFixture();await f.Start();var approval=await CheckedAggregateApproval(f);await f.Commit(approval.OperationId);var item=await AggregateItem(f);
        var lease=await new WebCheckedAi(f.Http).Capture(f.Document,item.SectionId,page:item.PageId);
        await using(var db=new AppDbContext(f.Options)) {
            if(failure=="page")(await db.Pages.FindAsync(f.Pages[1]))!.Content="<p>Later affected writing</p>";
            if(failure=="mirror")(await db.SceneContents.SingleAsync()).ContentJson="<p>Later mirror writing</p>";
            if(failure=="order")(await db.Pages.FindAsync(f.Pages[1]))!.OrderIndex=99;
            if(failure=="deleted")db.Pages.Remove((await db.Pages.FindAsync(f.Pages[1]))!);
            if(failure=="language")(await db.Sections.FindAsync(f.Sections[0]))!.LanguageCode="ja";
            if(failure=="legacy")(await db.WebAiHistoryOperations.SingleAsync()).RecoveryJson=null;
            if(failure=="race")(await db.Pages.FindAsync(f.Pages[3]))!.Content="<p>Concurrent unrelated writing</p>";
            if(failure=="history-storage")await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailRecovery BEFORE INSERT ON WebAiHistoryOperations WHEN NEW.Sequence>1 BEGIN SELECT RAISE(ABORT,'injected outcome failure'); END;");
            if(failure=="mid-page-storage") {
#pragma warning disable EF1003 // Generated GUID only in test DDL.
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailRecovery BEFORE UPDATE ON Pages WHEN NEW.Id='"+f.Pages[1].ToString("D").ToUpperInvariant()+"' BEGIN SELECT RAISE(ABORT,'injected second-page failure'); END;");
#pragma warning restore EF1003
            }
            await db.SaveChangesAsync();
        }
        if(failure is not ("foreign" or "race"))lease=await new WebCheckedAi(f.Http).Capture(f.Document,item.SectionId,page:item.PageId);
        if(failure=="foreign"){f.Http.DefaultRequestHeaders.Remove("X-Test-Owner");f.Http.DefaultRequestHeaders.Add("X-Test-Owner","other");}
        string[] before;await using(var db=new AppDbContext(f.Options))before=(await db.Pages.OrderBy(p=>p.Id).ToListAsync()).Select(p=>p.Content).ToArray();
        var request=new WebRecoveryRequest(Guid.NewGuid(),item.ApplicationId,"Undone",lease.Source);
        using var refused=await f.Http.PostAsJsonAsync("api/ai/actions/history/recovery/replay",request);Assert.False(refused.IsSuccessStatusCode);
        await using(var db=new AppDbContext(f.Options)) {
            Assert.Equal(before,(await db.Pages.OrderBy(p=>p.Id).ToListAsync()).Select(p=>p.Content));Assert.Single(await db.WebAiHistoryOperations.ToListAsync());
            Assert.Equal("Applied",(await db.WebAiHistoryOperations.SingleAsync()).Outcome);
            if(failure is "history-storage" or "mid-page-storage") {
                Assert.Equal(HttpStatusCode.ServiceUnavailable,refused.StatusCode);await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailRecovery");
            }
        }
        if(failure is "history-storage" or "mid-page-storage") {
            using var retried=await f.Http.PostAsJsonAsync("api/ai/actions/history/recovery/replay",request);Assert.True(retried.IsSuccessStatusCode,await retried.Content.ReadAsStringAsync());
            await using var verify=new AppDbContext(f.Options);Assert.Equal(f.Html[0],(await verify.Pages.FindAsync(f.Pages[0]))!.Content);Assert.Equal(2,await verify.WebAiHistoryOperations.CountAsync());
        }
    }
    [Fact]
    public async Task WebAggregateRecoverySnapshotFailureRollsBackApplyAndRetainsApprovalForSameIdRetry() {
        await using var f=new TranslationFixture();await f.Start();var approval=await CheckedAggregateApproval(f);
        await using(var db=new AppDbContext(f.Options))await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailAggregateSnapshot BEFORE UPDATE ON WebAiHistoryOperations WHEN NEW.RecoveryJson IS NOT NULL BEGIN SELECT RAISE(ABORT,'injected snapshot failure'); END;");
        using(var failed=await f.Http.PostAsync($"{f.Endpoint}/operations/{approval.OperationId}/commit",null))Assert.Equal(HttpStatusCode.ServiceUnavailable,failed.StatusCode);
        await using(var db=new AppDbContext(f.Options)) {
            Assert.Equal(f.Html[0],(await db.Pages.FindAsync(f.Pages[0]))!.Content);Assert.Empty(await db.SceneContents.ToListAsync());Assert.Null((await db.WebAiHistoryOperations.SingleAsync()).CommittedAt);
            Assert.Equal("Approved",JsonSerializer.Deserialize<WebTranslationReceipt>((await db.WebTranslationOperations.SingleAsync()).ReceiptJson,WebAiHistoryContracts.Json)!.State);
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailAggregateSnapshot");
        }
        f.Restart();await f.Commit(approval.OperationId);Assert.True((await AggregateItem(f)).CanUndo);
    }
    [Theory][InlineData("section","duplicate-section")][InlineData("document","duplicate-document")]
    public async Task WebAggregateRecoveryKeepsTranslatedCopiesAndEditsAndOriginalCopyIsIdempotent(string scope,string mode) {
        await using var f=new TranslationFixture();await f.Start();var approval=await CheckedAggregateApproval(f,scope,mode);var committed=await f.Commit(approval.OperationId);
        var item=await AggregateItem(f);Assert.False(item.CanUndo);Assert.False(item.CanRedo);Assert.Contains("does not delete copies",item.UnavailableReason);
        Guid edited;await using(var db=new AppDbContext(f.Options)) {
            var copy=await db.Pages.FirstAsync(p=>mode=="duplicate-section"?p.SectionId==committed.ResultSectionId:p.DocumentId==committed.ResultDocumentId);copy.Content="<p>Edited copy 日本語</p>";edited=copy.Id;await db.SaveChangesAsync();
        }
        f.Restart();Assert.Equal(committed,await f.Commit(approval.OperationId));
        var lease=await new WebCheckedAi(f.Http).Capture(f.Document,item.SectionId,page:item.PageId);
        using(var refused=await f.Http.PostAsJsonAsync("api/ai/actions/history/recovery/replay",new WebRecoveryRequest(Guid.NewGuid(),item.ApplicationId,"Undone",lease.Source)))Assert.Equal(HttpStatusCode.Conflict,refused.StatusCode);
        using(var original=await f.Http.PostAsync($"{f.Endpoint}/operations/{approval.OperationId}/original",null))Assert.Equal(HttpStatusCode.OK,original.StatusCode);
        await using(var db=new AppDbContext(f.Options)) {
            Assert.Equal("<p>Edited copy 日本語</p>",(await db.Pages.FindAsync(edited))!.Content);
            var page=await db.Pages.FirstAsync(p=>p.DocumentId==committed.OriginalCopyId);page.Content="<p>Edited original copy</p>";await db.SaveChangesAsync();
        }
        using(var retry=await f.Http.PostAsync($"{f.Endpoint}/operations/{approval.OperationId}/original",null))Assert.Equal(HttpStatusCode.OK,retry.StatusCode);
        await using var verify=new AppDbContext(f.Options);Assert.Contains(await verify.Pages.ToListAsync(),p=>p.DocumentId==committed.OriginalCopyId && p.Content=="<p>Edited original copy</p>");
    }
    [Fact]
    public async Task WebAggregateRecoveryActualRecommendedSectionHandlersRetainWritingLanguageAndReopenRecovery() {
        await using var h=await RecommendedFixture();await RunRecommended(h,"novel.deepen_character");await h.Event("OnApplyPendingAiProposal");
        Assert.Null(TranslationField(h.Editor,"_webTranslationError"));var item=await AggregateItem(h.Data);Assert.True(item.CanUndo);
        await ReplayRecovery(h.Data,item,"Undone");h.Data.Restart();
        await using(var db=new AppDbContext(h.Data.Options)){Assert.Equal(StyleHtml,(await db.Pages.FindAsync(h.Data.Pages[0]))!.Content);Assert.Equal("sv",(await db.Sections.FindAsync(h.Data.Sections[0]))!.LanguageCode);}
        await ReplayRecovery(h.Data,item,"Redone");Assert.Equal(1,h.Provider.Calls);
        await using var verify=new AppDbContext(h.Data.Options);Assert.Contains("Reviewed",(await verify.Pages.FindAsync(h.Data.Pages[0]))!.Content);Assert.Equal("sv",(await verify.Sections.FindAsync(h.Data.Sections[0]))!.LanguageCode);
    }
}
