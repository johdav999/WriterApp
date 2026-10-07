using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Synopsis;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using WriterApp.Shared.Sync;
using Xunit;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using Microsoft.EntityFrameworkCore;

namespace WriterApp.Tests;
public sealed partial class AiActionsControllerTests
{
    private sealed class CheckedPlanningProvider : IAiOrchestrator {
        public Action? Before=null;public int Calls;public string Output="{\"summary\":\"Reviewed summary\",\"explanation\":\"Reasoning\"}";public AiRequest? Request;
        public IReadOnlyList<IAiAction> Actions=>[new SceneSuggestAction(),new SceneRefineAction(),new SceneFindOpenQuestionsAction(),new ContinuityCheckAction(),new StoryboardSuggestNextSceneAction(),new StoryboardDetectMissingScenesAction(),new StoryboardCheckSubplotContinuityAction(),new StoryboardAnalyzePovBalanceAction()];
        public IAiAction? GetAction(string key)=>Actions.SingleOrDefault(a=>a.ActionId==key);public bool CanRunAction(string key)=>true;
        public AiStreamingCapabilities GetStreamingCapabilities(string key)=>new(false,false);public AiStreamingSession StreamActionAsync(string key,AiActionInput input,CancellationToken ct)=>throw new NotSupportedException();
        public Task<AiExecutionResult> ExecuteActionAsync(string key,AiActionInput input,CancellationToken ct) {Calls++;Request=GetAction(key)!.BuildRequest(input);Before?.Invoke();return Task.FromResult(AiExecutionResult.Success(new(Guid.NewGuid(),input.ActiveSectionId,"Planning",key,"synthetic",Guid.NewGuid(),DateTime.UtcNow,null,[],[],"Planning","scene",null,input.SelectedText,Output)));}
    }
    [Theory][InlineData("storyboard.suggest-next-scene")][InlineData("storyboard.detect-missing-scenes")][InlineData("storyboard.check-subplot-continuity")][InlineData("storyboard.analyze-pov-balance")]
    public async Task WebCheckedStoryboardProviderReceivesCurrentOwnedPlanningInsteadOfClientContext(string key)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out _);var project=db.Documents.Single().ProjectId;var chapter=Guid.NewGuid();var scene=Guid.NewGuid();
        db.ProjectNodes.AddRange(new(){Id=chapter,DocumentId=id,ProjectId=project,NodeType=ProjectNodeType.Chapter,Title="Current chapter"},new(){Id=scene,DocumentId=id,ProjectId=project,ParentId=chapter,NodeType=ProjectNodeType.Scene,LinkedSectionId=section,Title="Current scene"});
        db.SceneCards.Add(new(){SceneNodeId=scene,Summary="Authored current summary",SubplotTagsJson="[\"Mystery\"]",KeyEvents="A clue is revealed"});db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        var source=await new WebAiSourceService(db,"user-1").Capture(id,section,null,null,default);var provider=new CheckedPlanningProvider();
        var result=await BuildController(db,provider).ExecuteAction(key,new(id,section,null,null,null,null,"Old prose",null,new(){["storyboard_context"]="Old client planning",["selected_scene_title"]="Old scene title"},WebSource:source),default);
        Assert.IsType<OkObjectResult>(result.Result);Assert.Equal(1,provider.Calls);
        string context=provider.Request!.Inputs["storyboard_context"]!.ToString()!;Assert.Contains("Current chapter",context);Assert.Contains("Authored current summary",context);Assert.DoesNotContain("Old client planning",context);
        using var json=System.Text.Json.JsonDocument.Parse(context);
        var savedScene=json.RootElement.GetProperty("chapters")[0].GetProperty("scenes")[0];
        Assert.Equal(System.Text.Json.JsonValueKind.Array,savedScene.GetProperty("subplotTags").ValueKind);
        Assert.Equal("Mystery",savedScene.GetProperty("subplotTags")[0].GetString());
        Assert.Equal("A clue is revealed",savedScene.GetProperty("keyEvents").GetString());
        if (key == "storyboard.suggest-next-scene") {
            Assert.Equal("Maya checked her phone at 08:05 and sighed.", provider.Request.Inputs["section_text"]);
            Assert.Equal("Current scene", provider.Request.Inputs["selected_scene_title"]);
            Assert.Equal("Current chapter", provider.Request.Inputs["preferred_chapter_title"]);
        }
    }
    [Theory][InlineData("scene.suggest")][InlineData("scene.refine")][InlineData("scene.find-open-questions")]
    public async Task WebCheckedSceneCoachingUsesTypedOutputAndRejectsForeignEntityAllowList(string key)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        var source=await new WebAiSourceService(db,"user-1").Capture(id,section,page,null,default);var provider=new CheckedPlanningProvider { Output="{\"povCharacterId\":\"foreign-character\"}" };
        var request=new AiActionExecuteRequestDto(id,section,page,null,null,"{}","Current writing",null,new(){["scene_coaching_version"]="1",["current_scene_card"]="{}",["character_bible_json"]="{}",["place_bible_json"]="{}",["timeline_bible_json"]="{}",["scene_entities_json"]="[{\"kind\":0,\"id\":\"foreign-character\",\"name\":\"Foreign\"}]"},"v1",new Dictionary<WriterApp.Shared.Canon.CanonKind,string>(),WebSource:source);
        var result=await BuildController(db,provider).ExecuteAction(key,request,default);Assert.Equal(502,Assert.IsType<ObjectResult>(result.Result).StatusCode);Assert.Equal(1,provider.Calls);Assert.Empty(db.AiActionHistoryEntries);
        provider.Output="{\"summary\":\"Reviewed summary\",\"explanation\":\"Readable reasoning\"}";
        result=await BuildController(db,provider).ExecuteAction(key,request,default);var response=Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);Assert.Equal("Reviewed summary",response.ProposedSceneCard!.Summary);Assert.Equal(source,response.WebSource);Assert.Empty(db.SceneCards);
    }
    [Theory]
    [InlineData("writing",false)][InlineData("writing",true)][InlineData("planning",false)][InlineData("planning",true)]
    [InlineData("canon",false)][InlineData("canon",true)][InlineData("synopsis",false)][InlineData("synopsis",true)]
    [InlineData("project",false)][InlineData("project",true)][InlineData("owner",false)][InlineData("owner",true)]
    public async Task WebCheckedSourceRejectsContentChangesEvenWithoutSyncTokenChange(string change,bool during)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});
        db.SectionSceneCards.Add(new(){SectionId=section,Summary="Authored"});
        db.DocumentSynopses.Add(new(){DocumentId=id,Logline="Authored"});
        db.BibleSnapshots.Add(new(){Id=Guid.NewGuid(),DocumentId=id,BibleType="character",ContentJson="{\"characters\":[]}",LastRefreshSourceHash="source"});await db.SaveChangesAsync();
        var source=await new WebAiSourceService(db,"user-1").Capture(id,section,page,null,default);
        void Change() {
            if(change=="writing")db.Pages.Single().Content="<p>Concurrent prose</p>";
            if(change=="planning")db.SectionSceneCards.Single().Summary="Concurrent planning";
            if(change=="canon")db.BibleSnapshots.Single().ContentJson="{\"characters\":[{\"name\":\"Concurrent\"}]}";
            if(change=="synopsis")db.DocumentSynopses.Single().Logline="Concurrent synopsis";
            if(change=="project")db.Projects.Single().Title="Concurrent project";
            if(change=="owner")db.Documents.Single().OwnerUserId="other-owner";
            db.SaveChanges();
        }
        var provider=new WritingOrchestrator();if(during)provider.Before=Change;else Change();
        var result=await BuildController(db,provider).ExecuteAction("rewrite.selection",new(id,section,page,0,4,"Maya","Maya",null,new(),WebSource:source),default);
        if(change=="owner"&&!during)Assert.IsType<NotFoundResult>(result.Result);else Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(during?1:0,provider.Calls);
    }
    [Theory][InlineData("rewrite.selection")][InlineData("propose.next-paragraph")][InlineData("scene.suggest")]
    public async Task WebReceiptEchoesExactIdentityAndAllowsUnchangedMetadataAcknowledgements(string key)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        var sync=new DocumentSyncRecord{DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1};db.DocumentSyncRecords.Add(sync);await db.SaveChangesAsync();
        var source=await new WebAiSourceService(db,"user-1").Capture(id,section,page,null,default);
        var ai=new CheckedPlanningProvider{Before=()=>{sync.Version="metadata-ack";db.SaveChanges();}};
        // The writing orchestrator has the actual writing action builders.
        var writing=new WritingOrchestrator{Before=()=>{sync.Version="metadata-ack";db.SaveChanges();}};
        var parameters=key=="scene.suggest"?new Dictionary<string,object?>{["scene_coaching_version"]="1",["current_scene_card"]="{}",["scene_entities_json"]="[]",["character_bible_json"]="{}",["place_bible_json"]="{}",["timeline_bible_json"]="{}"}:new();
        var result=await BuildController(db,key=="scene.suggest"?ai:writing).ExecuteAction(key,new(id,section,page,0,4,"Maya","Maya",null,parameters,"v1",key=="scene.suggest"?new Dictionary<WriterApp.Shared.Canon.CanonKind,string>():null,WebSource:source),default);
        var response=Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(source,response.WebSource);
        WebAiSources.RequireCurrent(await new WebAiSourceService(db,"user-1").Capture(id,section,page,null,default),source);
    }
    [Theory][InlineData("page")][InlineData("section")][InlineData("scene")][InlineData("owner")][InlineData("trash")]
    public async Task WebSourceDiscoveryRefusesForeignOrDeletedTargets(string target)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);var sync=new DocumentSyncRecord{DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1};db.DocumentSyncRecords.Add(sync);await db.SaveChangesAsync();
        if(target=="trash"){sync.IsTrashed=true;await db.SaveChangesAsync();}
        var service=new WebAiSourceService(db,target=="owner"?"other-owner":"user-1");
        await Assert.ThrowsAsync<DocumentSyncException>(()=>service.Capture(id,target=="section"?Guid.NewGuid():section,target=="page"?Guid.NewGuid():page,target=="scene"?Guid.NewGuid():null,default));
    }
    [Theory][InlineData("evaluate")][InlineData("questions")][InlineData("suggest")]
    public async Task WebSynopsisAlsoChecksWritingAndCanonAfterItsActualDedicatedProvider(string mode)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out _,out _);db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        var source=await new WebAiSourceService(db,"user-1").Capture(id,null,null,null,default);
        var provider=new SynopsisOrchestrator{Before=()=>{db.Pages.Single().Content="Concurrent writing without a sync update";db.SaveChanges();}};
        var request=new SynopsisAiRequestDto(mode=="suggest"?"premise":null,"Author intent",1,"v1",new SyncSynopsis(),db.Documents.Single().ProjectId,source);
        var response=await SynopsisRun(SynopsisController(db,provider),id,mode,request);
        Assert.IsType<ConflictObjectResult>(response.Result);Assert.Equal(1,provider.Calls);Assert.Empty(db.DocumentSynopses);
    }
}
