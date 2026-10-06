using Microsoft.AspNetCore.Mvc;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Data.Documents;
using WriterApp.Shared.Canon;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class AiActionsControllerTests
{
    [Theory][InlineData("scene.suggest")][InlineData("scene.refine")][InlineData("scene.find-open-questions")]
    public async Task SceneCoachingEndpointsCarryExactPagesScopeCatalogueAndVersionedCanon(string key) {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        db.DocumentSyncRecords.Add(new DocumentSyncRecord{DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        var api=new SceneOrchestrator();var request=SceneRequest(id,section,page,key=="scene.find-open-questions"?"openQuestions":"status");
        var response=await BuildController(db,api).ExecuteAction(key,request,default);var result=Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal("v1",result.SourceDocumentVersion);Assert.Empty(result.SourceCanonVersions!);Assert.Equal(request.SurroundingText,api.Request!.Inputs["section_text"]);
        Assert.Equal(request.Parameters!["scene_entities_json"],api.Request.Inputs["scene_entities_json"]);Assert.Equal(request.Parameters["current_scene_card"],api.Request.Inputs["current_scene_card"]);Assert.Equal(request.Parameters["focus_field"],api.Request.Inputs["focus_field"]);
        Assert.Contains("Maya",db.Pages.Single().Content);
    }
    [Theory][InlineData("old")][InlineData("foreign")][InlineData("missing-canon")][InlineData("bad-focus")][InlineData("bad-card")][InlineData("bad-catalogue")][InlineData("new-contract")][InlineData("late-source")]
    public async Task InvalidOrChangedSceneContractsNeverMutateWriting(string scenario) {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        db.DocumentSyncRecords.Add(new DocumentSyncRecord{DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        var api=new SceneOrchestrator();var request=SceneRequest(id,section,page,"summary");
        if(scenario=="old")request=request with{ExpectedDocumentVersion="old"};
        if(scenario=="foreign"){db.Documents.Single().OwnerUserId="another";await db.SaveChangesAsync();}
        if(scenario=="missing-canon")request=request with{ExpectedCanonVersions=null};
        if(scenario=="bad-focus")request.Parameters!["focus_field"]="writing";
        if(scenario=="bad-card")request.Parameters!["current_scene_card"]="[]";
        if(scenario=="bad-catalogue")request.Parameters!["scene_entities_json"]="{}";
        if(scenario=="new-contract")request.Parameters!["scene_coaching_version"]=2;
        if(scenario=="late-source")api.Before=()=>{db.DocumentSyncRecords.Single().Version="v2";db.SaveChanges();};
        var result=await BuildController(db,api).ExecuteAction("scene.suggest",request,default);Assert.True(result.Result is NotFoundResult or ConflictObjectResult or BadRequestObjectResult);
        Assert.Equal(scenario=="late-source"?1:0,api.Calls);Assert.Contains("Maya",db.Pages.Single().Content);
    }
    private static AiActionExecuteRequestDto SceneRequest(Guid id,Guid section,Guid page,string field) => new(id,section,page,null,null,"{}",new string('x',6000),null,
        new(){["instruction"]="Suggest ONLY "+field,["scene_coaching_version"]=1,["focus_field"]=field,["current_scene_card"]="{\"Status\":\"Revised\"}",["scene_entities_json"]="[]",["character_bible_json"]="{}",["place_bible_json"]="{}",["timeline_bible_json"]="{}"},"v1",new Dictionary<CanonKind,string>());
    private sealed class SceneOrchestrator : IAiOrchestrator {
        public int Calls;public Action? Before;public AiRequest? Request;
        public IReadOnlyList<IAiAction> Actions=>[new SceneSuggestAction(),new SceneRefineAction(),new SceneFindOpenQuestionsAction()];
        public IAiAction? GetAction(string key)=>Actions.SingleOrDefault(a=>a.ActionId==key);public bool CanRunAction(string key)=>true;public AiStreamingCapabilities GetStreamingCapabilities(string key)=>new(false,false);
        public Task<AiExecutionResult> ExecuteActionAsync(string key,AiActionInput input,CancellationToken ct){Calls++;Request=GetAction(key)!.BuildRequest(input);Before?.Invoke();return Task.FromResult(AiExecutionResult.Success(new(Guid.NewGuid(),input.ActiveSectionId,"Scene",key,"mock",Guid.NewGuid(),DateTime.UtcNow,null,[],[],"Scene","section",null,input.SelectedText,"{\"status\":\"Final\",\"openQuestions\":\"Who arrived?\"}")));}
        public AiStreamingSession StreamActionAsync(string key,AiActionInput input,CancellationToken ct)=>throw new NotSupportedException();
    }
}
