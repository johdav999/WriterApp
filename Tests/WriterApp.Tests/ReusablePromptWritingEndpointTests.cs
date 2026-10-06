using Microsoft.AspNetCore.Mvc;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.Subscriptions;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class AiActionsControllerTests
{
    [Theory][InlineData("selection",true)][InlineData("section",true)][InlineData("stale",false)][InlineData("late-source",false)][InlineData("parameters",false)]
    [InlineData("target",false)][InlineData("context",false)][InlineData("project",false)][InlineData("partial",false)][InlineData("foreign",false)][InlineData("plan",false)][InlineData("range",false)]
    public async Task ReusablePresetExecutionRequiresDeclaredCompleteOwnedTargetAndCurrentRevision(string scenario,bool success) {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        if(scenario=="foreign"){db.Documents.Single().OwnerUserId="other";await db.SaveChangesAsync();}
        var scope=scenario is "selection" or "range"?WritingScope.Selection:WritingScope.Section;
        var preset=ReusablePromptTransferTests.Custom() with{Scope=scope,ProjectId=scenario=="project"?Guid.NewGuid():null};var parameters=ReusablePrompts.ExecutionParameters(preset);parameters[ReusablePrompts.Parameter]=ReusablePrompts.Serialize(preset);
        var mapped=new WritingStructure(1,id,section,[new(page,[new("0.0","Maya stepped forward.")])]);parameters["context"]="Maya stepped forward.";
        if(scope==WritingScope.Section&&scenario!="target")parameters[WritingActions.Parameter]=WritingActions.Serialize(mapped);
        if(scenario=="parameters")parameters["tone"]="Changed without review";if(scenario=="context")parameters["context"]="Other page";
        var ai=new PresetWritingOrchestrator{Partial=scenario=="partial"};if(scenario=="late-source")ai.Before=()=>{db.DocumentSyncRecords.Single().Version="v2";db.SaveChanges();};
        var request=new AiActionExecuteRequestDto(id,section,page,scope==WritingScope.Selection?0:null,scope==WritingScope.Selection?(scenario=="range"?20:21):null,scope==WritingScope.Selection?"Maya stepped forward.":null,"Maya stepped forward.",null,parameters,scenario=="stale"?"old":"v1");
        var controller=BuildController(db,ai,scenario=="plan"?new StubEntitlementService(PlanTier.Free):null);
        if(scenario is "selection" or "section") {
            var outline=Assert.IsType<WritingOutlineSnapshot>(Assert.IsType<OkObjectResult>((await controller.GetWritingOutline(id,default)).Result).Value);
            request=request with{WritingOutline=outline};
        }
        var result=await controller.ExecuteAction("custom_transform",request,default);
        if(success){var response=Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);Assert.Equal("v1",response.SourceDocumentVersion);Assert.Contains("more tension",ai.Request!.Inputs["instruction"].ToString());
            if(request.WritingOutline is { } outline){Assert.Equal(outline.Fingerprint,response.SourceOutlineFingerprint);Assert.Equal(WritingOutline.ProviderText(outline),ai.Request.Context.OutlineText);}
            if(scope==WritingScope.Section){_=WritingActions.Result(response.ProposedText!,mapped);Assert.Equal(true,ai.Request.Inputs["structured_writing"]);Assert.Contains("run IDs",ai.Request.Inputs["instruction"].ToString());}else Assert.Equal("Maya stepped forward.",ai.Request.Context.OriginalText);
        }else {Assert.True(result.Result is BadRequestObjectResult or NotFoundResult or ConflictObjectResult||result.Result is ObjectResult{StatusCode:402 or 403 or 502});if(scenario is not ("partial" or "late-source"))Assert.Equal(0,ai.Calls);}
        Assert.Contains("Maya checked",db.Pages.Single().Content);
    }
    private sealed class PresetWritingOrchestrator:IAiOrchestrator {
        public int Calls;public bool Partial;public Action? Before;public AiRequest? Request;
        public IReadOnlyList<IAiAction> Actions=>[new CustomTransformAction()];public IAiAction? GetAction(string id)=>Actions.SingleOrDefault(a=>a.ActionId==id);public bool CanRunAction(string id)=>true;public AiStreamingCapabilities GetStreamingCapabilities(string id)=>new(false,false);
        public Task<AiExecutionResult> ExecuteActionAsync(string id,AiActionInput input,CancellationToken ct){Calls++;Request=GetAction(id)!.BuildRequest(input);Before?.Invoke();return Task.FromResult(AiExecutionResult.Success(new(Guid.NewGuid(),input.ActiveSectionId,"Preset",id,"mock",Guid.NewGuid(),DateTime.UtcNow,null,[],[],"Preset","section",null,Request.Context.OriginalText,Partial?"{}":Request.Inputs.ContainsKey("structured_writing")?Request.Context.OriginalText:"Maya waited.")));}
        public AiStreamingSession StreamActionAsync(string id,AiActionInput input,CancellationToken ct)=>throw new NotSupportedException();
    }
}
