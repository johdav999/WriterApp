using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.AI;
using WriterApp.Client.Components;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;
namespace WriterApp.Tests;
#pragma warning disable BL0006 // Dispatch compiled production recovery buttons.
public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class RecoveryRenderer(IServiceProvider services,ILoggerFactory logs) : Renderer(services,logs) {
        private int _root;
        public override Dispatcher Dispatcher {get;}=Dispatcher.CreateDefault();
        protected override void HandleException(Exception e)=>throw new InvalidOperationException("Recovery UI failed",e);
        protected override Task UpdateDisplayAsync(in RenderBatch batch)=>Task.CompletedTask;
        public Task Show(WebAiRecoveryPanel panel,Guid document,bool aggregate,Action changed,List<bool> busy)=>Dispatcher.InvokeAsync(()=> {
            _root=AssignRootComponentId(panel);return RenderRootComponentAsync(_root,ParameterView.FromDictionary(new Dictionary<string,object?> {
                [nameof(WebAiRecoveryPanel.DocumentId)]=document,[nameof(WebAiRecoveryPanel.Aggregate)]=aggregate,
                [nameof(WebAiRecoveryPanel.BeforeWork)]=(Func<Task<bool>>)(()=>Task.FromResult(true)),
                [nameof(WebAiRecoveryPanel.Changed)]=EventCallback.Factory.Create(this,changed),
                [nameof(WebAiRecoveryPanel.BusyChanged)]=EventCallback.Factory.Create<bool>(this,value=>busy.Add(value))
            }));
        });
        public Task Click(int index)=>Dispatcher.InvokeAsync(()=> {
            var frames=GetCurrentRenderTreeFrames(_root);var child=frames.Array.Take(frames.Count).Single(f=>f.FrameType==RenderTreeFrameType.Component && f.Component is AiHistoryPanel);
            var controls=GetCurrentRenderTreeFrames(child.ComponentId);
            return DispatchEventAsync(controls.Array.Take(controls.Count).Where(f=>f.FrameType==RenderTreeFrameType.Attribute && f.AttributeName=="onclick").ElementAt(index).AttributeEventHandlerId,null,new MouseEventArgs());
        });
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WebScopedRecoveryCompiledClientButtonsReplayAndRefreshTheDurableOutcome(bool aggregate) {
        await using var h=new SceneApprovalHarness();await h.Start(false);var f=h.Fixture;
        if(aggregate){var approval=await CheckedAggregateApproval(f);await f.Commit(approval.OperationId);}
        else {await h.Event("ApprovedSceneFieldsChanged",new[]{WriterApp.Application.Documents.SceneCoachingField.Summary});await h.Event("ApplySceneAiProposalAsync");}
        await using(var db=new AppDbContext(f.Options)) {
            foreach(var row in await db.WebAiHistoryOperations.Where(r=>r.CommittedAt!=null).ToListAsync()) {
                using var reported=await f.Http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{row.OperationId}/report",WebAiHistoryOperations.Intent(row));reported.EnsureSuccessStatusCode();
            }
        }
        await using var outbox=new WebAiHistoryOutbox(f.Http,new HistoryMemoryModule(),new WebCheckedAi(f.Http));
        var panel=new WebAiRecoveryPanel();var flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        typeof(WebAiRecoveryPanel).GetProperty("Http",flags)!.SetValue(panel,f.Http);typeof(WebAiRecoveryPanel).GetProperty("CheckedAi",flags)!.SetValue(panel,new WebCheckedAi(f.Http));typeof(WebAiRecoveryPanel).GetProperty("Outbox",flags)!.SetValue(panel,outbox);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new RecoveryRenderer(services,services.GetRequiredService<ILoggerFactory>());
        int changed=0;var busy=new List<bool>();await renderer.Show(panel,f.Document,aggregate,()=>changed++,busy);
        await renderer.Click(1);Assert.Equal(1,changed);Assert.Equal(new[]{true,false},busy);
        var entries=(WebRecoveryItem[])typeof(WebAiRecoveryPanel).GetField("_entries",flags)!.GetValue(panel)!;
        Assert.True(Assert.Single(entries).CanRedo);Assert.Equal("Undone",Assert.Single(entries).Outcome);
        await renderer.Click(1);Assert.Equal(2,changed);
        var lease=await new WebCheckedAi(f.Http).Capture(f.Document);Assert.All(await outbox.List(lease),i=>Assert.Equal("Confirmed",i.Status));
        await using var verify=new AppDbContext(f.Options);Assert.Equal(3,await verify.WebAiHistoryOperations.CountAsync(r=>r.CommittedAt!=null));
    }
    [Theory][InlineData("planning")][InlineData("aggregate")][InlineData("undone")][InlineData("legacy")][InlineData("copy")][InlineData("busy")]
    public async Task WebScopedRecoveryCompiledStatesExposeOnlySupportedRecoveryAndEncodeProviderText(string state) {
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var entry=new AiHistoryItem(Guid.NewGuid(),state=="planning"?"scene.suggest":"translate.section",state=="planning"?"SceneCard":"Aggregate",state=="undone"?"Undone":"Applied",DateTimeOffset.UtcNow,
            "<p><strong>Authored 日本語 🧭</strong></p>","<script>inert</script>",state is "planning" or "aggregate" or "busy",state=="undone",
            state=="legacy"?"Comparison only: no exact scoped snapshots.":state=="copy"?"Translated copy retained; Undo does not delete copies or later edits.":null,"Web scoped snapshots",CanRecover:false);
        string html=await renderer.Dispatcher.InvokeAsync(async()=> (await renderer.RenderComponentAsync<AiHistoryPanel>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            [nameof(AiHistoryPanel.Entries)]=new[]{entry},[nameof(AiHistoryPanel.Busy)]=state=="busy",
            [nameof(AiHistoryPanel.PlainComparisons)]=true,
            [nameof(AiHistoryPanel.Undo)]=EventCallback.Factory.Create<Guid>(services,_=>{}),[nameof(AiHistoryPanel.Redo)]=EventCallback.Factory.Create<Guid>(services,_=>{}),
            [nameof(AiHistoryPanel.Refresh)]=EventCallback.Factory.Create(services,()=>{})
        }))).ToHtmlString());
        var dom=new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);Assert.DoesNotContain("<script>",html);Assert.DoesNotContain("Recover a copy",html);
        var button=dom.QuerySelector(".history-item-actions button")!;Assert.Equal(state is "busy" or "legacy" or "copy",button.HasAttribute("disabled"));
        string? evidence=Environment.GetEnvironmentVariable("WRITERAPP_PARITY_P09_UI_EVIDENCE");if(evidence is not null){Directory.CreateDirectory(evidence);await File.WriteAllTextAsync(Path.Combine(evidence,state+".html"),html);}
    }
    [Fact]
    public async Task WebScopedRecoveryMetadataNeverEnablesTheLegacyPageReplayControl() {
        await using var f=new TranslationFixture{Configure=services=>{ConfigureCheckedSynopsis(services);services.AddScoped<IOnboardingDemoEligibilityService,OnboardingDemoEligibilityService>();}};
        await f.Start();using var provider=new TranslationProvider(f);var editor=CreateTranslationEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"ExecuteStructuredTranslationAsync",TranslationAction("section"));await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        await TranslationEvent(renderer,editor,"LoadAiHistoryAsync",CancellationToken.None);
        Assert.False((bool)TranslationField(editor,"_hasAiUndoHistory")!);Assert.False((bool)TranslationField(editor,"_hasAiRedoHistory")!);
        using var response=await f.Http.GetAsync($"api/ai/actions/history?documentId={f.Document}");Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
        var dto=Assert.Single((await response.Content.ReadFromJsonAsync<AiActionHistoryEntryDto[]>())!);
        Assert.True(dto.CanCloudUndo);Assert.Equal("Scoped",dto.ReplayScope);var item=await AggregateItem(f);Assert.True(item.CanUndo);
        await ReplayRecovery(f,item,"Undone");await TranslationEvent(renderer,editor,"LoadAiHistoryAsync",CancellationToken.None);
        Assert.False((bool)TranslationField(editor,"_hasAiRedoHistory")!);Assert.True((await AggregateItem(f)).CanRedo);
    }
    [Fact]
    public async Task WebScopedRecoveryDoesNotLabelSelectionComparisonsAsAggregateSnapshots() {
        await using var f=new TranslationFixture();await f.Start();
        await using(var db=new AppDbContext(f.Options)) {
            foreach(var key in new[]{"rewrite.selection","expand.section","custom_transform"})db.AiActionHistoryEntries.Add(new(){
                Id=Guid.NewGuid(),OwnerUserId="ordinary-author",DocumentId=f.Document,SectionId=f.Sections[0],ActionKey=key,
                RequestJson=key=="custom_transform"?"{\"parameters\":{\"writing_structure\":\"{}\"}}":"{}",ResultJson="{}",CreatedAt=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        }
        var rows=(await f.Http.GetFromJsonAsync<WebRecoveryItem[]>($"api/ai/actions/history/recovery/{f.Document}"))!;
        Assert.Equal(2,rows.Length);Assert.DoesNotContain(rows,r=>r.Action=="rewrite.selection");Assert.All(rows,r=>{Assert.Equal("Aggregate",r.TargetKind);Assert.False(r.CanUndo);Assert.False(r.CanRedo);Assert.Contains("Comparison only",r.UnavailableReason);});
    }
    [Fact]
    public async Task WebScopedRecoverySqliteUpgradeKeepsExistingHistoryAndDoesNotInventSnapshots() {
        await using var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);await db.Database.OpenConnectionAsync();var migrator=db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261004112925_WebAiHistoryOperations");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO WebAiHistoryOperations (OwnerUserId,OperationId,ApplicationId,ProposalId,DocumentId,Sequence,Outcome,RequestHash,IntentJson,CommittedAt) VALUES ('legacy-owner','legacy-operation','legacy-application','legacy-proposal','legacy-document',1,'Applied','retained-hash','retained-intent','2026-10-04T00:00:00Z')");
        await migrator.MigrateAsync();
        await using var cmd=db.Database.GetDbConnection().CreateCommand();cmd.CommandText="SELECT IntentJson,RecoveryJson FROM WebAiHistoryOperations WHERE OwnerUserId='legacy-owner'";
        await using var reader=await cmd.ExecuteReaderAsync();Assert.True(await reader.ReadAsync());Assert.Equal("retained-intent",reader.GetString(0));Assert.True(reader.IsDBNull(1));Assert.False(db.Database.HasPendingModelChanges());
    }
    [Fact]
    public void WebScopedRecoverySqlServerAddsNullableSnapshotsWithoutBackfillOrDestructiveSql() {
        using var db=new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer("Server=localhost;Database=WriterAppScriptOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        var sql=db.GetService<IMigrator>().GenerateScript("20261004112927_WebAiHistoryOperationsSqlServer",null,MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("[RecoveryJson] nvarchar(max) NULL",sql);Assert.DoesNotContain("UPDATE [WebAiHistoryOperations]",sql);Assert.DoesNotContain("DROP TABLE",sql);Assert.False(db.Database.HasPendingModelChanges());
    }
}
