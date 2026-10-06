using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    // A shared browser-storage seam for handler/crash tests; actual IndexedDB is verified separately in Edge.
    private sealed class HistoryMemoryModule : IJSObjectReference,IJSRuntime {
        private sealed record Row(string Scope,Guid DocumentId,string OperationId,string Json,string Status,string? Message,string? Receipt);
        private readonly Dictionary<string,Row> _rows=new();
        public bool RefuseStorage;
        public void CorruptConfirmation(string scope,Guid operation) {lock(_rows) {var key=scope+"/"+operation;_rows[key]=_rows[key] with {Status="Confirmed",Receipt="{}"};} }
        public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        public ValueTask<T> InvokeAsync<T>(string name,object?[]? args)=>InvokeAsync<T>(name,default,args);
        public ValueTask<T> InvokeAsync<T>(string name,CancellationToken ct,object?[]? args) {
            if(name.StartsWith("tiptapEditor.",StringComparison.Ordinal))return ValueTask.FromResult(default(T)!);
            ct.ThrowIfCancellationRequested();if(RefuseStorage)throw new JSException("Synthetic storage refusal");
            lock(_rows) {
                if(name=="import")return ValueTask.FromResult((T)(object)this);
                var scope=(string)args![0]!;
                if(name=="prepare") {
                    var json=(string)args[1]!;var intent=JsonSerializer.Deserialize<WebAiHistoryIntent>(json,WebAiHistoryContracts.Json)!;var key=scope+"/"+intent.OperationId;
                    if(_rows.TryGetValue(key,out var old) && old.Json!=json)throw new JSException("Immutable intent mismatch");
                    _rows.TryAdd(key,new(scope,intent.Source.DocumentId,intent.OperationId.ToString(),json,"Prepared",null,null));
                } else if(name=="mark") {
                    var key=scope+"/"+args[1];var row=_rows[key];if(row.Status!="Confirmed")_rows[key]=row with {Status=(string)args[2]!,Message=(string?)args[3],Receipt=(string?)args[4]};
                } else if(name=="list") {
                    var doc=Guid.Parse((string)args[1]!);return ValueTask.FromResult((T)(object)JsonSerializer.Serialize(_rows.Values.Where(r=>r.Scope==scope && r.DocumentId==doc),WebAiHistoryContracts.Json));
                } else throw new JSException(name);
                return ValueTask.FromResult(default(T)!);
            }
        }
    }
    private async Task<WebAiHistoryIntent> HistoryIntent(TranslationFixture f,string target="Page") {
        var source=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&pageId={f.Pages[0]}"))!;
        var proposal=Guid.NewGuid();var request=new AiActionExecuteRequestDto(f.Document,f.Sections[0],f.Pages[0],0,4,"Text",null,null,new(),WebSource:source);
        var result=new AiActionExecuteResponseDto(proposal,"Text","Reviewed 日本語",null,DateTimeOffset.UtcNow,"rewrite.selection",WebSource:source);
        await using var db=new AppDbContext(f.Options);db.AiActionHistoryEntries.Add(new(){Id=proposal,OwnerUserId="ordinary-author",DocumentId=f.Document,SectionId=f.Sections[0],PageId=f.Pages[0],ActionKey="rewrite.selection",
            RequestJson=JsonSerializer.Serialize(request,WebAiHistoryContracts.Json),ResultJson=JsonSerializer.Serialize(result,WebAiHistoryContracts.Json),CreatedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync();
        var id=Guid.NewGuid();return new(1,id,id,proposal,1,null,"Applied",source,target,f.Html[0],"<p><strong>Reviewed 日本語 🧭</strong></p>");
    }
    private static async Task<HttpResponseMessage> SaveHistory(TranslationFixture f,WebAiHistoryIntent intent) {
        string url=intent.TargetKind=="SceneContent"?$"api/projects/{intent.Source.ProjectId}/scenes/{intent.Source.SceneId}/content":$"api/pages/{intent.Source.PageId}";
        using var request=new HttpRequestMessage(HttpMethod.Put,url){Content=intent.TargetKind=="SceneContent"?JsonContent.Create(new {ContentJson=intent.AfterContent}):JsonContent.Create(new {Content=intent.AfterContent})};
        request.Headers.Add("X-WriterApp-AI-Source",JsonSerializer.Serialize(intent.Source));request.Headers.Add("X-WriterApp-AI-Operation",intent.OperationId.ToString());return await f.Http.SendAsync(request);
    }
    [Theory][InlineData("commit-crash")][InlineData("report-crash")][InlineData("later-edit")][InlineData("duplicate-tab")]
    public async Task WebHistoryReconcilesDurableSavedProofAfterRestartAndDuplicateDelivery(string boundary) {
        await using var f=new TranslationFixture();await f.Start();var intent=await HistoryIntent(f);var storage=new HistoryMemoryModule();var ai=new WebCheckedAi(f.Http);var lease=new WebCheckedAi.Lease(intent.Source,f.Http.BaseAddress!.AbsoluteUri,null);
        await using(var outbox=new WebAiHistoryOutbox(f.Http,storage,ai)) {
            await outbox.Prepare(intent,lease);using var saved=await SaveHistory(f,intent);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
            if(boundary=="report-crash")using(await f.Http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{intent.OperationId}/report",intent)){}
            if(boundary=="duplicate-tab")using(await SaveHistory(f,intent)){}
        }
        if(boundary=="later-edit")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{Content="<p>Later authored prose</p>"});
        f.Restart();await using var reopened=new WebAiHistoryOutbox(f.Http,storage,new WebCheckedAi(f.Http));await reopened.Flush(lease);await reopened.Flush(lease);
        Assert.Equal("Confirmed",Assert.Single(await reopened.List(lease)).Status);
        await using var verify=new AppDbContext(f.Options);var row=Assert.Single(await verify.WebAiHistoryOperations.ToListAsync());Assert.NotNull(row.CommittedAt);Assert.NotNull(row.ReportedAt);
        var cloud=await new EfCoreAiActionHistoryStore(verify).ListAsync("ordinary-author",f.Document,default);Assert.True(Assert.Single(cloud).IsApplied);Assert.Equal(1,cloud[0].AppliedCount);
        if(boundary=="later-edit") {var current=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&pageId={f.Pages[0]}"))!;using var move=await f.Http.PostAsJsonAsync("api/ai/actions/history/web/move",new WebAiHistoryMoveRequest(current,"Undone"));Assert.Equal(HttpStatusCode.Conflict,move.StatusCode);Assert.Equal("<p>Later authored prose</p>",(await verify.Pages.AsNoTracking().SingleAsync(p=>p.Id==f.Pages[0])).Content);}
    }
    [Theory][InlineData("uncommitted")][InlineData("payload")][InlineData("foreign-owner")][InlineData("foreign-proposal")][InlineData("foreign-target")][InlineData("concurrent")][InlineData("storage")]
    public async Task WebHistoryInvalidIntentOrUnconfirmedPersistenceNeverReportsApplied(string scenario) {
        await using var f=new TranslationFixture();await f.Start();var intent=await HistoryIntent(f);var storage=new HistoryMemoryModule {RefuseStorage=scenario=="storage"};
        var lease=new WebCheckedAi.Lease(intent.Source,f.Http.BaseAddress!.AbsoluteUri,null);await using var outbox=new WebAiHistoryOutbox(f.Http,storage,new WebCheckedAi(f.Http));
        if(scenario=="storage") {await Assert.ThrowsAsync<JSException>(()=>outbox.Prepare(intent,lease));await using var verify=new AppDbContext(f.Options);Assert.Empty(await verify.WebAiHistoryOperations.ToListAsync());return;}
        if(scenario=="foreign-proposal")intent=intent with {ProposalId=Guid.NewGuid()};
        if(scenario=="foreign-target")intent=intent with {Source=intent.Source with {PageId=f.Pages[1]}};
        if(scenario is "foreign-proposal" or "foreign-target") {using var refused=await f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",intent);Assert.Equal(HttpStatusCode.Conflict,refused.StatusCode);return;}
        await outbox.Prepare(intent,lease);
        if(scenario=="concurrent") {await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{Content="Concurrent"});using var save=await SaveHistory(f,intent);Assert.Equal(HttpStatusCode.Conflict,save.StatusCode);}
        if(scenario=="foreign-owner") {f.Http.DefaultRequestHeaders.Remove("X-Test-Owner");f.Http.DefaultRequestHeaders.Add("X-Test-Owner","other-owner");}
        using var response=await f.Http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{intent.OperationId}/report",scenario=="payload"?intent with {AfterContent="Other"}:intent);
        Assert.False(response.IsSuccessStatusCode);await using var db=new AppDbContext(f.Options);Assert.All(await db.WebAiHistoryOperations.ToListAsync(),row=>Assert.Null(row.ReportedAt));
    }
    [Fact]
    public async Task WebHistoryUndoRedoPersistsOrderedOutcomesAndRequiresPredecessorDelivery() {
        await using var f=new TranslationFixture();await f.Start();var apply=await HistoryIntent(f);using(await f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",apply)){}using(await SaveHistory(f,apply)){}
        async Task<WebAiHistoryIntent> Move(string outcome) {var source=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&pageId={f.Pages[0]}"))!;using var response=await f.Http.PostAsJsonAsync("api/ai/actions/history/web/move",new WebAiHistoryMoveRequest(source,outcome));response.EnsureSuccessStatusCode();return (await response.Content.ReadFromJsonAsync<WebAiHistoryMove>())!.Intent;}
        var undo=await Move("Undone");using(await f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",undo)){}using var undone=await SaveHistory(f,undo);Assert.Equal(HttpStatusCode.OK,undone.StatusCode);
        using var premature=await f.Http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{undo.OperationId}/report",undo);Assert.Equal(HttpStatusCode.Conflict,premature.StatusCode);
        using(await f.Http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{apply.OperationId}/report",apply)){}using(await f.Http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{undo.OperationId}/report",undo)){}
        var redo=await Move("Redone");using(await f.Http.PostAsJsonAsync("api/ai/actions/history/web/operations",redo)){}using var redone=await SaveHistory(f,redo);Assert.Equal(HttpStatusCode.OK,redone.StatusCode);
        using(await f.Http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{redo.OperationId}/report",redo)){}
        await using var db=new AppDbContext(f.Options);Assert.Equal(3,await db.WebAiHistoryOperations.CountAsync());Assert.True(Assert.Single(await new EfCoreAiActionHistoryStore(db).ListAsync("ordinary-author",f.Document,default)).IsApplied);Assert.Equal(apply.AfterContent,(await db.Pages.SingleAsync(p=>p.Id==f.Pages[0])).Content);
    }
}
