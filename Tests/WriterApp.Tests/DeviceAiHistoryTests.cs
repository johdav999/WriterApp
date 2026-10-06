using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class AiActionsControllerTests
{
    internal static (Guid Document,Guid Section,Guid Page) SeedHistoryGraph(AppDbContext db) { SeedDocumentGraph(db,out var doc,out var section,out var page); return(doc,section,page); }
    internal static AiActionsController HistoryController(AppDbContext db,string owner="user-1", bool free=false) => new(
        new StubAiOrchestrator(true,false),new DocumentRepository(db,NullLogger<DocumentRepository>.Instance),
        new SectionRepository(db,NullLogger<SectionRepository>.Instance,new ConfigurationBuilder().Build()),new PageRepository(db),db,
        new HistoryOwner(owner),free ? new StubEntitlementService(PlanTier.Free) : new StubEntitlementService(),
        new OnboardingDemoEligibilityService(db,NullLogger<OnboardingDemoEligibilityService>.Instance),new EfCoreAiActionHistoryStore(db),
        new StubVersionHistoryService(),NullLogger<AiActionsController>.Instance) { ControllerContext=new(){HttpContext=new DefaultHttpContext()} };
    private sealed class HistoryOwner(string owner):IUserIdResolver { public string ResolveUserId(ClaimsPrincipal principal)=>owner; }
}

internal sealed class HistoryTestFixture : IDisposable
{
    public readonly TranslationTestFixture Book=new();
    public readonly PresetTransferFixture.FixtureIdentity Identity=new(){Id="account-1"};
    public DeviceAccountService Account {get;}
    public LocalAiStore Store {get;}
    public DeviceAiHistoryService Service {get;}
    public HistoryHandler Handler {get;}
    public HttpClient Http {get;}
    public LocalDocument Source=null!;
    public Guid CloudDocument, CloudSection, CloudPage, Proposal=Guid.NewGuid();
    public string Scope=>Service.Scope!;
    public HistoryTestFixture(bool automatic=false) {
        Account=new(Identity); Store=new(Book.Root+"/ai"); Handler=new(this);
        Http=new(new DeviceAuthenticatedHandler(Account,new("https://test.invalid/")){InnerHandler=Handler}){BaseAddress=new("https://test.invalid/")};
        Service=new(Http,Account,Book.Network,new("Test",Http.BaseAddress),Store,automatic);
    }
    public AppDbContext Db()=>new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source="+Book.Root+"/history-cloud.db;Pooling=False").Options);
    public async Task Start() {
        Source=await Book.Create(); await Account.SignInAsync();
        await using var db=Db();await db.Database.EnsureCreatedAsync();var ids=AiActionsControllerTests.SeedHistoryGraph(db);
        (CloudDocument,CloudSection,CloudPage)=ids; await db.SaveChangesAsync();
        var project=await db.Documents.Where(d=>d.Id==CloudDocument).Select(d=>d.ProjectId).SingleAsync();
        Source=await Book.Store.ApplySyncAsync(Source with{ServerDocumentId=CloudDocument,ServerProjectId=project,ServerVersion="v1",
            Project=Source.Project! with{ServerProjectId=project,ServerPrimaryDocumentId=CloudDocument},
            Sections=Source.Sections.Select((s,i)=>i==0?s with{ServerSectionId=CloudSection,Pages=s.Pages.Select((p,j)=>j==0?p with{ServerPageId=CloudPage}:p).ToArray()}:s).ToArray()},Source.LocalRevision,default,projects:true);
        await AddProposal(Proposal);
    }
    public async Task AddProposal(Guid id,string action="rewrite.selection",string owner="user-1",Guid? doc=null,string original="🧭 Åsa",string proposed="Elin") {
        await using var db=Db();await new EfCoreAiActionHistoryStore(db).AddAsync(new(id,action,owner,doc??CloudDocument,CloudSection,DateTimeOffset.UtcNow,"Revision",original,proposed,
            PageId:CloudPage,RequestJson:"{}",ResultJson:JsonSerializer.Serialize(new AiActionExecuteResponseDto(id,original,proposed,"",DateTimeOffset.UtcNow,action,SourceDocumentVersion:"v1"),new JsonSerializerOptions(JsonSerializerDefaults.Web))),default);
    }
    public LocalAiHistory Entry(string state="Reviewed") {
        var after=Source with{Sections=Source.Sections.Select((s,i)=>i==0?s with{Pages=s.Pages.Select((p,j)=>j==0?p with{Content="<p>Elin</p>"}:p).ToArray()}:s).ToArray()};
        return new(1,Guid.NewGuid(),Source.DocumentId,"rewrite.selection","Manuscript revision",Source.LocalRevision,"v1",DateTimeOffset.UtcNow,state,Source,"Elin",
            Source.Sections[0].Pages[0].PageId,OriginalText:"🧭 Åsa",After:after,CloudOrigin:new(Scope,Proposal,CloudDocument,CloudSection,CloudPage,"rewrite.selection","v1"));
    }
    public void Dispose() { Service.Dispose();Http.Dispose();Book.Dispose(); }
    internal sealed class HistoryHandler(HistoryTestFixture f):HttpMessageHandler {
        public bool LoseNext,Unavailable,BadReceipt,Malformed,Free; public int Calls;public Func<Task>? After; public List<DeviceAiHistoryReport> Sent=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            Calls++;if(Unavailable)return new(HttpStatusCode.NotFound);
            await using var db=f.Db();var controller=AiActionsControllerTests.HistoryController(db,f.Account.AccountId=="account-1"?"user-1":"user-2",Free);
            IActionResult? result;
            if(request.Method==HttpMethod.Post){var report=(await request.Content!.ReadFromJsonAsync<DeviceAiHistoryReport>(cancellationToken:ct))!;Sent.Add(report);result=(await controller.ReportDeviceHistory(report,ct)).Result;}
            else result=(await controller.DeviceHistory(f.CloudDocument,ct)).Result;
            if(After is not null)await After(); if(LoseNext){LoseNext=false;throw new HttpRequestException("Lost committed response");}
            if(Malformed)return new(HttpStatusCode.OK){Content=new StringContent("{\"version\":99}")};
            if(result is ObjectResult obj){object? value=obj.Value;if(BadReceipt&&value is DeviceAiHistoryReceipt r)value=r with{RequestHash=new string('0',64)};return new((HttpStatusCode)(obj.StatusCode??200)){Content=JsonContent.Create(value)};}
            return new((HttpStatusCode)(result is StatusCodeResult status?status.StatusCode:500));
        }
    }
}

public sealed class DeviceAiHistoryTests
{
    [Fact] public async Task LostResponseRestartReplaysExactEventsWithoutDuplicatesAndLegacyListingSeesApplied() {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry();await f.Store.SaveHistoryAsync(entry);
        await f.Store.SaveHistoryAsync(entry with{Status="Applying"});await f.Book.Repository.SaveAsync(entry.After!);await f.Store.SaveHistoryAsync(entry with{Status="Applied"});
        f.Handler.LoseNext=true;await Assert.ThrowsAsync<HttpRequestException>(()=>f.Service.DeliverAsync(entry.DocumentId));
        await using(var db=f.Db())Assert.Single(await db.DeviceAiHistoryEvents.ToListAsync());
        using var restart=new DeviceAiHistoryService(f.Http,f.Account,f.Book.Network,new("Test",f.Http.BaseAddress!),new(f.Book.Root+"/ai"),false);
        Assert.Equal(2,await restart.DeliverAsync(entry.DocumentId));Assert.Equal(DeviceAiHistoryContracts.Hash(f.Handler.Sent[0]),DeviceAiHistoryContracts.Hash(f.Handler.Sent[1]));
        Assert.Equal(0,await restart.DeliverAsync(entry.DocumentId));Assert.Equal(0,await restart.PendingAsync(entry.DocumentId));
        await using(var db=f.Db()){Assert.Equal(2,await db.DeviceAiHistoryEvents.CountAsync());var web=Assert.Single(await new EfCoreAiActionHistoryStore(db).ListAsync("user-1",f.CloudDocument,default));Assert.True(web.IsApplied);Assert.Equal(1,web.AppliedCount);Assert.False(web.CanCloudUndo);Assert.False(web.CanCloudRedo);Assert.Empty(await db.AiActionAppliedEvents.ToListAsync());Assert.Null(await new EfCoreAiActionHistoryStore(db).UndoAsync("user-1",f.CloudDocument,f.CloudSection,f.CloudPage,default));}
        Assert.Equal("<p>Elin</p>",(await f.Book.Repository.LoadAsync(entry.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Fact] public async Task ApplyingIntentNeverReportsAppliedAndLaterUndoRedoRemainDurableAfterRestart() {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry();await f.Store.SaveHistoryAsync(entry);await f.Store.SaveHistoryAsync(entry with{Status="Applying"});
        Assert.Equal("Reviewed",Assert.Single((await f.Store.HistoryAsync(entry.DocumentId)).Single().Deliveries!).State);await f.Service.DeliverAsync(entry.DocumentId);
        Assert.Equal("Reviewed",Assert.Single((await f.Service.RefreshAsync(f.Source)).Snapshot.Entries).State);
        await f.Book.Repository.SaveAsync(entry.After!);await f.Store.SaveHistoryAsync(entry with{Status="Applied"});
        var history=new LocalAiStore(f.Book.Root+"/ai");var actions=new LocalAiHistoryActions(f.Book.Repository,history,f.Account,new("Test",f.Http.BaseAddress!));await actions.ChangeAsync(entry.DocumentId,entry.Id,false);await actions.ChangeAsync(entry.DocumentId,entry.Id,true);
        var saved=Assert.Single(await history.HistoryAsync(entry.DocumentId));Assert.Equal(new[]{"Reviewed","Applied","Undone","Applied"},saved.Deliveries!.Select(e=>e.State));
        Assert.Equal(3,await f.Service.DeliverAsync(entry.DocumentId));var cloud=Assert.Single((await f.Service.RefreshAsync(f.Source)).Snapshot.Entries);Assert.Equal("Applied",cloud.State);Assert.Equal(2,cloud.AppliedCount);
    }
    [Theory][InlineData("foreign-proposal")][InlineData("foreign-document")][InlineData("target")][InlineData("version")][InlineData("sequence")][InlineData("reused")]
    public async Task ReportingRejectsWrongOwnershipIdentitySourceAndSequenceWithoutLocalMutation(string scenario) {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry("Applied");await f.Store.SaveHistoryAsync(entry);entry=Assert.Single(await f.Store.HistoryAsync(entry.DocumentId));var report=LocalAiStore.HistoryReport(entry,entry.Deliveries![0]);
        await using var db=f.Db();var controller=AiActionsControllerTests.HistoryController(db);if(scenario=="foreign-proposal"){(await db.AiActionHistoryEntries.SingleAsync()).OwnerUserId="user-2";await db.SaveChangesAsync();}
        if(scenario=="foreign-document"){(await db.Documents.SingleAsync()).OwnerUserId="user-2";await db.SaveChangesAsync();}
        if(scenario=="target")report=report with{Origin=report.Origin with{PageId=Guid.NewGuid()}};
        if(scenario=="version")report=report with{Origin=report.Origin with{SourceVersion="v2"}};
        if(scenario=="sequence")report=report with{Event=report.Event with{Sequence=2,PreviousOperationId=Guid.NewGuid()}};
        if(scenario=="reused"){Assert.IsType<OkObjectResult>((await controller.ReportDeviceHistory(report,default)).Result);report=report with{Target="Other target"};}
        var result=(await controller.ReportDeviceHistory(report,default)).Result;Assert.True(result is NotFoundResult or ConflictObjectResult);
        Assert.Equal(scenario=="reused"?1:0,await db.DeviceAiHistoryEvents.CountAsync());Assert.Equal(f.Source.Sections[0].Pages[0].Content,(await f.Book.Repository.LoadAsync(entry.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Theory][InlineData("lost")][InlineData("receipt")][InlineData("old-backend")][InlineData("free")]
    public async Task ReportingFailureRetainsSavedWritingAndPendingIntent(string failure) {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry("Applied");await f.Book.Repository.SaveAsync(entry.After!);await f.Store.SaveHistoryAsync(entry);
        f.Handler.LoseNext=failure=="lost";f.Handler.BadReceipt=failure=="receipt";f.Handler.Unavailable=failure=="old-backend";f.Handler.Free=failure=="free";
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Service.DeliverAsync(entry.DocumentId));Assert.Equal(1,await f.Service.PendingAsync(entry.DocumentId));Assert.Equal("Applied",Assert.Single(await f.Store.HistoryAsync(entry.DocumentId)).Status);
        Assert.Equal("<p>Elin</p>",(await f.Book.Repository.LoadAsync(entry.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task AccountSwitchDuringReadOrReportingCannotExposeOrAcknowledgeAnotherAccount(bool reporting) {
        using var f=new HistoryTestFixture();await f.Start();var scope=f.Scope;var entry=f.Entry("Applied");await f.Store.SaveHistoryAsync(entry);
        f.Handler.After=async()=>{f.Identity.Id="other";await f.Account.SignInAsync();};
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(()=>reporting?f.Service.DeliverAsync(entry.DocumentId):(Task)f.Service.RefreshAsync(f.Source));
        Assert.Null(await f.Service.CachedAsync(f.Source));Assert.Equal(0,await f.Service.PendingAsync(entry.DocumentId));
        f.Identity.Id="account-1";await f.Account.SignInAsync();Assert.Equal(1,await f.Service.PendingAsync(entry.DocumentId));Assert.Equal(scope,f.Scope);
    }
    [Fact] public async Task CachedCloudInspectionIsOfflineBoundedAndNeverOffersCloudOnlyUndoOrRecovery() {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry();await f.Store.SaveHistoryAsync(entry);var remote=Guid.NewGuid();await f.AddProposal(remote,original:"<script>private</script>");
        var cache=await f.Service.RefreshAsync(f.Source);f.Book.Network.SetOnline(false);
        var offline=await f.Service.CachedAsync(f.Source);Assert.Equal(cache.Snapshot.CheckedAt,offline!.Snapshot.CheckedAt);
        var local=new AiHistoryItem(entry.Id,entry.Action,entry.Target,entry.Status,entry.CreatedAt,"Original","Elin");var combined=f.Service.Combine(f.Source,[local],[entry],offline);Assert.Equal(2,combined.Count);Assert.Equal("Local + cloud",combined.Single(e=>e.Id==entry.Id).Origin);
        var cloudOnly=combined.Single(e=>e.Id==remote);Assert.False(cloudOnly.CanUndo);Assert.False(cloudOnly.CanRedo);Assert.False(cloudOnly.CanRecover);Assert.Equal("Generated",cloudOnly.Status);
        await Assert.ThrowsAsync<IOException>(()=>f.Service.DeliverAsync(entry.DocumentId));
        var other=new DeviceAiHistoryService(f.Http,f.Account,f.Book.Network,new("Other",new("https://other.invalid/")),f.Store,false);Assert.Null(await other.CachedAsync(f.Source));Assert.Empty(other.Combine(f.Source,[local],[entry],offline));other.Dispose();
    }
    [Fact] public async Task MalformedCloudResponsePreservesPreviousCacheAndOldHistoryReadsPreserveBytes() {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry() with{CloudOrigin=null};await f.Store.SaveHistoryAsync(entry);var path=Path.Combine(f.Book.Root,"ai","history",entry.DocumentId.ToString("N"),entry.Id.ToString("N")+".json");var bytes=await File.ReadAllBytesAsync(path);
        await new LocalAiStore(f.Book.Root+"/ai").HistoryAsync(entry.DocumentId);Assert.Equal(bytes,await File.ReadAllBytesAsync(path));
        var cache=await f.Service.RefreshAsync(f.Source);f.Handler.Malformed=true;await Assert.ThrowsAnyAsync<Exception>(()=>f.Service.RefreshAsync(f.Source));Assert.Equal(cache.Snapshot.CheckedAt,(await f.Service.CachedAsync(f.Source))!.Snapshot.CheckedAt);
    }
    [Fact] public async Task LaterWritingRefusesUndoAndDoesNotProduceUndoneEvent() {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry("Applied");var saved=await f.Book.Repository.SaveAsync(entry.After!);await f.Store.SaveHistoryAsync(entry);
        await f.Book.Repository.SaveAsync(saved with{Sections=saved.Sections.Select((s,i)=>i==0?s with{Pages=s.Pages.Select((p,j)=>j==0?p with{Content="<p>Later authored edit</p>"}:p).ToArray()}:s).ToArray()});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new LocalAiHistoryActions(f.Book.Repository,f.Store,f.Account,new("Test",f.Http.BaseAddress!)).ChangeAsync(entry.DocumentId,entry.Id,false));
        Assert.Equal("Applied",Assert.Single(Assert.Single(await f.Store.HistoryAsync(entry.DocumentId)).Deliveries!).State);Assert.Contains("Later authored",(await f.Book.Repository.LoadAsync(entry.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Fact] public async Task UpgradePreservesPresetReceiptsAndBothProviderSnapshotsMatch() {
        using var f=new HistoryTestFixture();Directory.CreateDirectory(f.Book.Root);var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source="+f.Book.Root+"/upgrade.db;Pooling=False").Options;
        await using(var db=new AppDbContext(options)){await db.GetService<IMigrator>().MigrateAsync("20261003125036_ReusablePresetTransfers");await db.Database.ExecuteSqlRawAsync("INSERT INTO PromptPresetTransfers(OwnerUserId,OperationId,RequestHash,ResultJson,CreatedUtc) VALUES ('old','00000000-0000-0000-0000-000000000001','retained','{{}}','2026-10-03')");await db.Database.MigrateAsync();Assert.Equal("retained",(await db.PromptPresetTransfers.SingleAsync()).RequestHash);Assert.False(db.Database.HasPendingModelChanges());Assert.Empty(await db.DeviceAiHistoryEvents.ToListAsync());}
        await using var sql=new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options);Assert.False(sql.Database.HasPendingModelChanges());Assert.Contains("DeviceAiHistoryEvents",sql.GetService<IMigrator>().GenerateScript("20261003125046_ReusablePresetTransfersSqlServer"));
    }
    [Fact] public async Task ActualWritingPipelineCapturesOriginAndAutomaticDeliveryCannotRollBackSave() {
        using var f=new HistoryTestFixture(automatic:true);await f.Start();var source=f.Source;var section=source.Sections[0];var page=section.Pages[0];string text=page.Content[3..^4];
        var prepared=LocalWriting.Prepare(source,section.SectionId,new("rewrite.selection",WritingScope.Selection,new()),
            selection:new(source,page.PageId,new(page.Content,text,text,0,text.Length,1,text.Length+1,0)));
        var api=new LocalWritingTests.Api();var ai=new DeviceAiService(api,f.Account,f.Book.Network,new("Test",f.Http.BaseAddress!),f.Service);
        var actions=new LocalWritingActions(f.Book.Repository,f.Store,ai,api);var proposal=await actions.ProposeAsync(prepared,default);
        Assert.NotNull(proposal.HistoryOrigin);Assert.Equal(f.Scope,proposal.HistoryOrigin.Scope);Assert.Equal(f.CloudDocument,proposal.HistoryOrigin.DocumentId);
        await f.AddProposal(proposal.ProposalId);f.Handler.Unavailable=true;
        var after=LocalWriting.Preview(prepared,new Dictionary<Guid,string>{{page.PageId,"<p>Elin stepped forward.</p>"}});var entry=LocalWriting.Entry(prepared,proposal,after);
        await f.Store.SaveHistoryAsync(entry);await actions.ApplyAsync(entry,proposal,default);
        for(int i=0;i<40&&f.Service.DeliveryError is null;i++)await Task.Delay(25);
        Assert.NotNull(f.Service.DeliveryError);Assert.Equal("Applied",Assert.Single(await f.Store.HistoryAsync(source.DocumentId)).Status);
        Assert.Equal("<p>Elin stepped forward.</p>",(await f.Book.Repository.LoadAsync(source.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Equal(2,await f.Service.PendingAsync(source.DocumentId));f.Handler.Unavailable=false;await f.Service.DeliverAsync(source.DocumentId);Assert.Equal(0,await f.Service.PendingAsync(source.DocumentId));
    }
    [Fact] public async Task SeparateLocalOutcomesDoNotEraseAnotherAppliedTargetWhenOneIsUndone() {
        using var f=new HistoryTestFixture();await f.Start();var first=f.Entry("Applied");var second=f.Entry("Applied");await f.Store.SaveHistoryAsync(first);await f.Store.SaveHistoryAsync(second);await f.Service.DeliverAsync(first.DocumentId);
        await f.Store.SaveHistoryAsync(first with{Status="Undone"});await f.Service.DeliverAsync(first.DocumentId);
        Assert.Equal("Applied",Assert.Single((await f.Service.RefreshAsync(f.Source)).Snapshot.Entries).State);
        await using var db=f.Db();Assert.True(Assert.Single(await new EfCoreAiActionHistoryStore(db).ListAsync("user-1",f.CloudDocument,default)).IsApplied);
    }
    [Fact] public async Task OriginalAccountIsRequiredForLinkedUndoAndStablePayloadsCannotChange() {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry("Applied");await f.Book.Repository.SaveAsync(entry.After!);await f.Store.SaveHistoryAsync(entry);
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.Store.SaveHistoryAsync(entry with{CloudOrigin=entry.CloudOrigin! with{ProposalId=Guid.NewGuid()}}));
        f.Identity.Id="other";await f.Account.SignInAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>new LocalAiHistoryActions(f.Book.Repository,f.Store,f.Account,new("Test",f.Http.BaseAddress!)).ChangeAsync(entry.DocumentId,entry.Id,false));
        Assert.Equal("Applied",Assert.Single(await f.Store.HistoryAsync(entry.DocumentId)).Status);
    }
    [Theory][InlineData("null-entry")][InlineData("oversize")][InlineData("wrong-document")][InlineData("duplicate")]
    public async Task InvalidCloudCacheCannotReplaceConfirmedInspection(string scenario) {
        using var f=new HistoryTestFixture();await f.Start();var cache=await f.Service.RefreshAsync(f.Source);var snapshot=cache.Snapshot;var first=snapshot.Entries[0];
        snapshot=scenario switch{"null-entry"=>snapshot with{Entries=[null!]},"oversize"=>snapshot with{Entries=[first with{Original=new string('x',100001)}]},"wrong-document"=>snapshot with{Entries=[first with{DocumentId=Guid.NewGuid()}]},_=>snapshot with{Entries=[first,first]}};
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.Store.SaveCloudHistoryAsync(cache with{Snapshot=snapshot}));Assert.Equal(cache.Snapshot.CheckedAt,(await f.Service.CachedAsync(f.Source))!.Snapshot.CheckedAt);
    }
    [Fact] public async Task CloudInspectionLimitIsDisclosedAndStoredOversizeResultIsRetained() {
        using var f=new HistoryTestFixture();await f.Start();await using(var db=f.Db()){
            var row=await db.AiActionHistoryEntries.SingleAsync();for(int i=0;i<200;i++){var id=Guid.NewGuid();db.AiActionHistoryEntries.Add(new(){Id=id,OwnerUserId="user-1",DocumentId=f.CloudDocument,SectionId=f.CloudSection,PageId=f.CloudPage,ActionKey="rewrite.selection",CreatedAt=DateTimeOffset.UtcNow,ResultJson=JsonSerializer.Serialize(new AiActionExecuteResponseDto(id,"Old","New","",DateTimeOffset.UtcNow,"rewrite.selection",SourceDocumentVersion:"v1"),new JsonSerializerOptions(JsonSerializerDefaults.Web))});}await db.SaveChangesAsync();}
        var cache=await f.Service.RefreshAsync(f.Source);Assert.True(cache.Snapshot.Truncated);Assert.Equal(200,cache.Snapshot.Entries.Count);
        await using(var db=f.Db()){var row=await db.AiActionHistoryEntries.OrderBy(e=>e.Id).FirstAsync();row.ResultJson=new string('x',2_000_001);await db.SaveChangesAsync();}
        await Assert.ThrowsAsync<IOException>(()=>f.Service.RefreshAsync(f.Source));Assert.Equal(cache.Snapshot.CheckedAt,(await f.Service.CachedAsync(f.Source))!.Snapshot.CheckedAt);
        await using(var db=f.Db())Assert.Equal(201,await db.AiActionHistoryEntries.CountAsync());
    }
    [Fact] public async Task CloudOnlyOrInterruptedReportingNeverInventsSnapshotsOrOverwritesRecovery() {
        using var f=new HistoryTestFixture();await f.Start();var cache=await f.Service.RefreshAsync(f.Source);var combined=f.Service.Combine(f.Source,[],[],cache);Assert.False(Assert.Single(combined).CanRecover);
        var intent=f.Entry("Applying");await f.Store.SaveHistoryAsync(intent);Assert.Empty(Assert.Single(await f.Store.HistoryAsync(intent.DocumentId)).Deliveries!);Assert.Equal(0,await f.Service.DeliverAsync(intent.DocumentId));
        await using var db=f.Db();Assert.Empty(await db.DeviceAiHistoryEvents.ToListAsync());Assert.Equal(f.Source.Sections[0].Pages[0].Content,(await f.Book.Repository.LoadAsync(intent.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Fact] public async Task LegacyCloudSnapshotCapabilitiesRemainSeparateFromDeviceOutcomes() {
        using var f=new HistoryTestFixture();await f.Start();var entry=f.Entry("Applied");await f.Store.SaveHistoryAsync(entry);await f.Service.DeliverAsync(entry.DocumentId);
        await using var db=f.Db();var history=new EfCoreAiActionHistoryStore(db);await history.AddAppliedEventAsync("user-1",f.Proposal,DateTimeOffset.UtcNow,f.CloudDocument,f.CloudSection,f.CloudPage,"<p>Before</p>","<p>After</p>",default);
        var listed=Assert.Single(await history.ListAsync("user-1",f.CloudDocument,default));Assert.True(listed.CanCloudUndo);Assert.False(listed.CanCloudRedo);
        await history.UndoAsync("user-1",f.CloudDocument,f.CloudSection,f.CloudPage,default);listed=Assert.Single(await history.ListAsync("user-1",f.CloudDocument,default));
        Assert.True(listed.IsApplied);Assert.False(listed.CanCloudUndo);Assert.True(listed.CanCloudRedo);
        var dto=Assert.IsType<IReadOnlyList<AiActionHistoryEntryDto>>(Assert.IsType<OkObjectResult>((await AiActionsControllerTests.HistoryController(db).ListHistory(f.CloudDocument,default)).Result).Value,exactMatch:false);
        Assert.True(Assert.Single(dto).CanCloudRedo);Assert.False(Assert.Single(dto).CanCloudUndo);
    }
    [Fact] public void RealClientAvailabilityUsesExplicitCloudCapabilitiesAndKeepsItsOwnNewApplyUsable() {
        var component=new WriterApp.Client.Pages.DocumentEditor();var type=component.GetType();const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        var entryType=type.GetNestedType("AiHistoryEntry",System.Reflection.BindingFlags.NonPublic)!;var statusType=type.GetNestedType("CommandHistoryStatus",System.Reflection.BindingFlags.NonPublic)!;
        var entries=(System.Collections.IList)type.GetField("_aiHistoryEntries",flags)!.GetValue(component)!;var id=Guid.NewGuid();
        entries.Add(Activator.CreateInstance(entryType,id,"rewrite.selection","Revision",null,"Before","After",DateTimeOffset.UtcNow,true,Enum.Parse(statusType,"Applied"),DateTimeOffset.UtcNow,1,false,false));
        type.GetMethod("UpdateAiUndoRedoAvailability",flags)!.Invoke(component,null);Assert.False((bool)type.GetField("_hasAiUndoHistory",flags)!.GetValue(component)!);Assert.False((bool)type.GetField("_hasAiRedoHistory",flags)!.GetValue(component)!);
        type.GetMethod("UpdateAiHistoryAppliedState",flags)!.Invoke(component,[id,DateTimeOffset.UtcNow]);type.GetMethod("UpdateAiUndoRedoAvailability",flags)!.Invoke(component,null);
        Assert.True((bool)type.GetField("_hasAiUndoHistory",flags)!.GetValue(component)!);
    }
}
