using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Client.Components.Projects;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

#pragma warning disable BL0005, BL0006 // Framework renderer exercises production handlers with synthetic parameters.

public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class ApprovalInspector : StoryboardSceneInspector {
        protected override void BuildRenderTree(RenderTreeBuilder builder) { }
        protected override Task OnParametersSetAsync() => Task.CompletedTask;
    }
    private sealed class SceneApprovalHarness : IAsyncDisposable {
        public readonly TranslationFixture Fixture=new(){Configure=ConfigureCheckedScenes};
        public CheckedProvider Provider=null!;public IComponent Component=null!;public Type Type=null!;
        private ServiceProvider Services=null!;private TranslationRenderer Renderer=null!;private TranslationEditor Editor=null!;
        public async Task Start(bool inspector,string? focus=null) {
            await Fixture.Start();
            await using(var db=new AppDbContext(Fixture.Options)) {
                var scene=await db.ProjectNodes.SingleAsync();scene.NodeType=ProjectNodeType.Scene;
                var chapter=Guid.NewGuid();scene.ParentId=chapter;
                db.ProjectNodes.Add(new(){Id=chapter,ProjectId=Fixture.Project,DocumentId=Fixture.Document,NodeType=ProjectNodeType.Chapter,Title="Chapter"});
                db.SceneCards.Add(new(){SceneNodeId=Fixture.Scene,Summary="Authored summary",Status="Final",EmotionalBeat="Authored beat",KeyEvents="Authored events",OpenQuestions="Authored questions",TagsJson="[\"Keep\"]",SubplotTagsJson="[\"Plot\"]",ReferencesJson="[]"});
                db.SectionSceneCards.Add(new(){SectionId=Fixture.Sections[0],Summary="Authored summary",Status="Final",EmotionalBeat="Authored beat",KeyEvents="Authored events",OpenQuestions="Authored questions",TagsJson="[\"Keep\"]",SubplotTagsJson="[\"Plot\"]",ReferencesJson="[]"});
                await db.SaveChangesAsync();
            }
            Provider=new(Fixture){ResolveScene=true,SceneProposal=new(null,"Proposed beat","Proposed events","Proposed questions",Summary:"Proposed summary",Status:"Idea",Tags:["Proposed tag"],SubplotTags:["Proposed plot"],NarrativeRole:"Revelation",NarrativeIntent:"Proposed intent")};
            Editor=CheckedEditor(Fixture,Provider);
            var auth=new WriterApp.Client.State.AuthMeStateService(Editor.Http,new WriterApp.Client.State.DeletedAccountStateService(),new WriterApp.Client.State.DuplicateAccountStateService());await auth.RefreshAsync(force:true);Editor.AuthMeStateService=auth;
            var featureType=typeof(DocumentEditor).Assembly.GetType("WriterApp.Client.Services.FeatureAccessService")!;
            var logs=typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(featureType).GetField("Instance")!.GetValue(null);
            var feature=Activator.CreateInstance(featureType,auth,Editor.Navigation,logs)!;
            typeof(DocumentEditor).GetProperty("FeatureAccessService",TranslationPrivate)!.SetValue(Editor,feature);
            Services=new ServiceCollection().AddLogging().BuildServiceProvider();Renderer=new(Services,Services.GetRequiredService<ILoggerFactory>());
            if(inspector) {
                Type=typeof(StoryboardSceneInspector);Component=new ApprovalInspector {ProjectId=Fixture.Project,SceneNode=(await Fixture.Http.GetFromJsonAsync<ProjectTreeDto>($"api/projects/{Fixture.Project}/tree?documentId={Fixture.Document}"))!.Nodes.Single(n=>n.Id==Fixture.Scene)};
                foreach(var pair in new Dictionary<string,object>{{"Http",Editor.Http},{"Logger",Microsoft.Extensions.Logging.Abstractions.NullLogger<StoryboardSceneInspector>.Instance},{"FeatureAccessService",feature},{"CheckedAi",new WebCheckedAi(Editor.Http)},{"HistoryOutbox",new WebAiHistoryOutbox(Editor.Http,Editor.JSRuntime,new WebCheckedAi(Editor.Http))}})
                    Type.GetProperty(pair.Key,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.SetValue(Component,pair.Value);
                Set("_loadedSceneId",Fixture.Scene);
                await Renderer.Attach(Component);await Event("LoadSceneAsync",Fixture.Scene,0L);
                // Inspector's own scene text transport is outside this planning-field test.
                Set("_scenePlainText","Saved scene text 日本語 🧭");
                await Event("RunSceneAiAsync","scene.suggest","Suggest fields for approval");
            } else {
                Type=typeof(DocumentEditor);Component=Editor;
                TranslationSet(Editor,"_activePage",(await Fixture.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{Fixture.Sections[0]}/pages"))!.Single(p=>p.Id==Fixture.Pages[0]));
                await Renderer.Attach(Component);await Event("LoadSceneCardAsync",Fixture.Sections[0]);
                await Event("RunSceneAiAsync","scene.suggest","Suggest fields for approval",focus);
            }
            Assert.True(string.IsNullOrEmpty(Error),Error+" "+Provider.LastHttpError+" "+Get(inspector?"_saveStatus":"_sceneStatus"));
            Assert.NotEmpty((IReadOnlyList<SceneFieldChange>)Get("_sceneAiChanges")!);
        }
        public object? Get(string field)=>Type.GetField(field,TranslationPrivate)!.GetValue(Component);
        public void Set(string field,object? value)=>Type.GetField(field,TranslationPrivate)!.SetValue(Component,value);
        public string? Error=>Get(Type==typeof(DocumentEditor)?"_sceneAiError":"_aiError") as string;
        public Task Event(string name,params object?[] args)=>Renderer.Dispatcher.InvokeAsync(async()=> {
            var method=Type.GetMethods(TranslationPrivate).Single(m=>m.Name==name && m.GetParameters().Length==args.Length);
            if(method.Invoke(Component,args) is Task task)await task;
        });
        public async ValueTask DisposeAsync(){if(Renderer is not null)await Renderer.DisposeAsync();Editor?.Dispose();Provider?.Dispose();if(Services is not null)await Services.DisposeAsync();await Fixture.DisposeAsync();}
    }

    [Theory]
    [InlineData(false,0)][InlineData(true,0)][InlineData(false,1)][InlineData(true,1)][InlineData(false,2)][InlineData(true,2)]
    public async Task WebSceneApprovalPersistsArbitrarySubsetsAndPreciseHistoryAfterReopen(bool inspector,int choice) {
        await using var h=new SceneApprovalHarness();await h.Start(inspector);
        SceneCoachingField[] approved=choice switch {
            0=>[SceneCoachingField.Summary,SceneCoachingField.KeyEvents,SceneCoachingField.NarrativeRole],
            1=>[SceneCoachingField.EmotionalBeat,SceneCoachingField.OpenQuestions,SceneCoachingField.SubplotTags,SceneCoachingField.NarrativeIntent],
            _=>((IReadOnlyList<SceneFieldChange>)h.Get("_sceneAiChanges")!).Select(c=>c.Field).ToArray()
        };
        await h.Event("ApprovedSceneFieldsChanged",approved);await h.Event("ApplySceneAiProposalAsync");
        Assert.True(string.IsNullOrEmpty(h.Error),h.Error);
        await using var db=new AppDbContext(h.Fixture.Options);
        var history=WebAiHistoryOperations.Intent(await db.WebAiHistoryOperations.SingleAsync(o=>o.CommittedAt!=null));
        var payload=JsonSerializer.Deserialize<SceneCardUpdateRequest>(history.AfterContent!,WebAiHistoryContracts.Json)!;
        Assert.Equal(approved,payload.ApprovedFields);Assert.Equal("Applied",history.Outcome);
        Assert.Null(payload.PovCharacterId);Assert.Null(payload.PlaceId);Assert.Null(payload.References);
        var saved=inspector ? (await db.SceneCards.SingleAsync()).Summary : (await db.SectionSceneCards.SingleAsync()).Summary;
        Assert.Equal(approved.Contains(SceneCoachingField.Summary)?"Proposed summary":"Authored summary",saved);
        h.Fixture.Restart();
        string url=inspector?$"api/scenes/{h.Fixture.Scene}/scene-card":$"api/sections/{h.Fixture.Sections[0]}/scene-card";
        var reopened=(await h.Fixture.Http.GetFromJsonAsync<SectionSceneCardProposalDto>(url))!;
        Assert.Equal(approved.Contains(SceneCoachingField.Status)?"Idea":"Final",reopened.Status);
        Assert.Equal(approved.Contains(SceneCoachingField.EmotionalBeat)?"Proposed beat":"Authored beat",reopened.EmotionalBeat);
        Assert.Equal(approved.Contains(SceneCoachingField.KeyEvents)?"Proposed events":"Authored events",reopened.KeyEvents);
        Assert.Equal(approved.Contains(SceneCoachingField.OpenQuestions)?"Proposed questions":"Authored questions",reopened.OpenQuestions);
        Assert.Equal(approved.Contains(SceneCoachingField.NarrativeRole)?"Revelation":null,reopened.NarrativeRole);
        Assert.Equal(approved.Contains(SceneCoachingField.NarrativeIntent)?"Proposed intent":null,reopened.NarrativeIntent);
        Assert.Equal(approved.Contains(SceneCoachingField.Tags)?["Proposed tag"]:["Keep"],reopened.Tags);
        Assert.Equal(approved.Contains(SceneCoachingField.SubplotTags)?["Proposed plot"]:["Plot"],reopened.SubplotTags);
        Assert.Equal(h.Fixture.Html[0],(await db.Pages.SingleAsync(p=>p.Id==h.Fixture.Pages[0])).Content);
    }

    [Theory]
    [InlineData(false,"empty")][InlineData(true,"empty")][InlineData(false,"discard")][InlineData(true,"discard")]
    [InlineData(false,"local")][InlineData(true,"local")][InlineData(false,"concurrent")][InlineData(true,"concurrent")]
    [InlineData(false,"save-failure")][InlineData(true,"save-failure")][InlineData(false,"changed-during-save")][InlineData(true,"changed-during-save")]
    [InlineData(false,"invalid-field")][InlineData(true,"invalid-field")]
    [InlineData(false,"account")][InlineData(true,"account")][InlineData(false,"target")][InlineData(true,"target")]
    public async Task WebSceneApprovalRejectsUnapprovedStaleCancelledAndFailedSaves(bool inspector,string scenario) {
        await using var h=new SceneApprovalHarness();await h.Start(inspector);
        int saves=h.Provider.SceneSaves;
        await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.Summary});
        if(scenario=="empty")await h.Event("ApprovedSceneFieldsChanged",Array.Empty<SceneCoachingField>());
        if(scenario=="discard")await h.Event("DiscardSceneAiProposal");
        if(scenario=="invalid-field")await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.PlaceId});
        if(scenario=="local")h.Set(inspector?"_summary":"_sceneSummary","Local draft");
        if(scenario=="save-failure")h.Provider.SceneSaveFailure=true;
        if(scenario=="account"){var http=inspector?(HttpClient)h.Type.GetProperty("Http",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(h.Component)!:((DocumentEditor)h.Component).Http;http.DefaultRequestHeaders.Remove("X-Test-Owner");http.DefaultRequestHeaders.Add("X-Test-Owner","foreign-author");}
        if(scenario=="target") {
            if(inspector){var node=((StoryboardSceneInspector)h.Component).SceneNode!;h.Type.GetProperty("SceneNode")!.SetValue(h.Component,node with {Id=Guid.NewGuid()});}
            else h.Type.GetProperty("DocumentId")!.SetValue(h.Component,Guid.NewGuid());
        }
        async Task Change() {await using var db=new AppDbContext(h.Fixture.Options);if(inspector)(await db.SceneCards.SingleAsync()).Summary="Concurrent summary";else (await db.SectionSceneCards.SingleAsync()).Summary="Concurrent summary";await db.SaveChangesAsync();}
        if(scenario=="concurrent")await Change();
        if(scenario=="changed-during-save")h.Provider.BeforeSceneSave=Change;
        await h.Event("ApplySceneAiProposalAsync");
        await using var verify=new AppDbContext(h.Fixture.Options);
        Assert.Empty(await verify.WebAiHistoryOperations.Where(o=>o.CommittedAt!=null || o.ReportedAt!=null).ToArrayAsync());
        var summary=inspector?(await verify.SceneCards.SingleAsync()).Summary:(await verify.SectionSceneCards.SingleAsync()).Summary;
        Assert.Equal(scenario is "concurrent" or "changed-during-save"?"Concurrent summary":"Authored summary",summary);
        if(scenario is "empty" or "discard" or "local" or "invalid-field")Assert.Equal(saves,h.Provider.SceneSaves);
        if(scenario is not ("empty" or "discard"))Assert.False(string.IsNullOrEmpty(h.Error));
    }

    [Fact]
    public async Task WebSceneApprovalSingleFieldActionRetainsItsScope() {
        await using var h=new SceneApprovalHarness();await h.Start(false,"summary");
        Assert.Equal(new[]{SceneCoachingField.Summary},h.Get("_approvedSceneFields"));
        Assert.Single((IReadOnlyList<SceneFieldChange>)h.Get("_sceneAiChanges")!);
        await h.Event("ApplySceneAiProposalAsync");Assert.True(string.IsNullOrEmpty(h.Error),h.Error);
        await using var db=new AppDbContext(h.Fixture.Options);var card=await db.SectionSceneCards.SingleAsync();
        Assert.Equal("Proposed summary",card.Summary);Assert.Equal("Final",card.Status);Assert.Equal("Authored events",card.KeyEvents);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WebSceneApprovalPendingSaveFreezesSelectionAndPlanningInput(bool inspector) {
        await using var h=new SceneApprovalHarness();await h.Start(inspector);
        await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.Summary});
        h.Provider.BeforeSceneSave=async()=> {
            Assert.True((bool)h.Get("_sceneApplying")!);
            await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.KeyEvents});
            if(inspector)await h.Event("PlanningChanged",new WriterApp.UI.Shared.Projects.ScenePlanningChange("Summary","Concurrent local draft"));
            else await h.Event("OnSceneSummaryInput",new ChangeEventArgs{Value="Concurrent local draft"});
            Assert.Equal("Authored summary",h.Get(inspector?"_summary":"_sceneSummary"));
            Assert.Equal(new[]{SceneCoachingField.Summary},h.Get("_approvedSceneFields"));
        };
        await h.Event("ApplySceneAiProposalAsync");Assert.True(string.IsNullOrEmpty(h.Error),h.Error);
        await using var db=new AppDbContext(h.Fixture.Options);var saved=inspector?(await db.SceneCards.SingleAsync()).Summary:(await db.SectionSceneCards.SingleAsync()).Summary;
        Assert.Equal("Proposed summary",saved);
    }
}
