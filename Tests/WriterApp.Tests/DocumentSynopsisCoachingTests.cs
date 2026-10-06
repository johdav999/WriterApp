using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.AI.StoryCoach;
using WriterApp.Application.Synopsis;
using WriterApp.Application.Subscriptions;
using WriterApp.Controllers;
using WriterApp.Data.Documents;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class AiActionsControllerTests
{
    private sealed class SynopsisOrchestrator : IAiOrchestrator {
        public IReadOnlyList<IAiAction> Actions {get;}=[new SynopsisEvaluateAction(),new SynopsisQuestionsAction(),new StoryCoachAction()];
        public int Calls;public Action? Before;public string? Output;public string? Code;public AiActionInput? Input;public string? Key;
        public IAiAction? GetAction(string id)=>Actions.SingleOrDefault(a=>a.ActionId==id);public bool CanRunAction(string id)=>true;
        public AiStreamingCapabilities GetStreamingCapabilities(string id)=>new(false,false);
        public AiStreamingSession StreamActionAsync(string id,AiActionInput input,CancellationToken ct)=>throw new NotSupportedException();
        public Task<AiExecutionResult> ExecuteActionAsync(string id,AiActionInput input,CancellationToken ct) {
            Calls++;Key=id;Input=input;Before?.Invoke();if(Code is not null)return Task.FromResult(AiExecutionResult.Blocked(Code,"Blocked"));
            string output=Output??(id==StoryCoachAction.ActionIdValue?"{\"proposedText\":\"Refined synopsis\",\"commentary\":\"Reasoning stays separate\"}":"Strengths:\nClear intent\nMissing elements:\nMotives");
            return Task.FromResult(AiExecutionResult.Success(new AiProposal(Guid.NewGuid(),Guid.Empty,"Coaching",id,"mock",Guid.NewGuid(),DateTime.UtcNow,null,[],[],"Coaching","synopsis",null,null,output)));
        }
    }
    [Theory][InlineData("evaluate")][InlineData("questions")][InlineData("suggest")]
    public async Task SynopsisCoachingDedicatedEndpointsConfirmAllSavedFieldsAndMode(string mode) {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out _,out _);
        db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});
        db.DocumentSynopses.Add(new(){DocumentId=id,Logline="Intended local logline",Premise="Premise",Theme="Theme",ProtagonistArc="Arc",CentralConflict="Conflict",Stakes="Stakes",Setting="Setting",EndingIntent="Ending",OpenQuestions="Questions",Notes="Authored notes",UpdatedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync();
        var source=new SyncSynopsis("Intended local logline","Premise","Theme","Arc","Conflict","Stakes","Setting","Ending","Questions","Authored notes");
        var orchestrator=new SynopsisOrchestrator();var history=new InMemoryAiActionHistoryStore();var controller=SynopsisController(db,orchestrator,history:history);
        var r=new SynopsisAiRequestDto(mode=="suggest"?"premise":null,"Author request",1,"v1",source,db.Documents.Single().ProjectId);
        var response=await SynopsisRun(controller,id,mode,r);var result=Assert.IsType<SynopsisAiResponseDto>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(source,result.SourceSynopsis);Assert.Equal("v1",result.SourceDocumentVersion);Assert.Equal(id,result.DocumentId);Assert.Equal(mode,result.Mode);Assert.NotEqual(Guid.Empty,result.ProposalId);
        Assert.Equal(WriterApp.Shared.SynopsisCoaching.Action(mode),orchestrator.Key);Assert.Equal(Guid.Empty,orchestrator.Input!.ActiveSectionId);
        foreach(var field in WriterApp.Shared.SynopsisCoaching.Fields)Assert.Contains(WriterApp.Shared.SynopsisCoaching.Value(source,field.Key),orchestrator.Input.Options!["synopsis_context"]!.ToString());
        Assert.Equal("Author request",orchestrator.Input.Options!["user_notes"]);
        if(mode=="suggest") {Assert.Equal("Refined synopsis",result.ProposedText);Assert.Equal("Reasoning stays separate",result.OutputText);Assert.Equal("1",new StoryCoachAction().BuildRequest(orchestrator.Input).Inputs["synopsis_coaching_version"]);}
        else Assert.Null(result.ProposedText);
        Assert.Equal("Intended local logline",db.DocumentSynopses.Single().Logline);
        var entry=Assert.Single(await history.ListAsync("user-1",id,default));Assert.Equal(result.ProposedText??result.OutputText,entry.ProposedText);
        var stored=System.Text.Json.JsonSerializer.Deserialize<AiActionExecuteResponseDto>(entry.ResultJson!,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;
        Assert.Equal("v1",stored.SourceDocumentVersion);if(mode=="suggest") {Assert.Equal("Premise",entry.OriginalText);Assert.Equal(result.OutputText,stored.ProposalExplanation);}
    }
    [Theory][InlineData("old-version")][InlineData("old-synopsis")][InlineData("changed-synopsis")][InlineData("changed-version")][InlineData("changed-owner")][InlineData("foreign-project")][InlineData("trashed")][InlineData("foreign-document")]
    public async Task SynopsisCoachingOwnershipAndPrePostSourceChecksRejectStaleCopies(string reason) {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out _,out _);var sync=new DocumentSyncRecord{DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1};db.DocumentSyncRecords.Add(sync);
        var s=new DocumentSynopsisRecord{DocumentId=id,Logline="Current",UpdatedAt=DateTimeOffset.UtcNow};db.DocumentSynopses.Add(s);await db.SaveChangesAsync();
        var o=new SynopsisOrchestrator();if(reason.StartsWith("changed"))o.Before=()=>{if(reason=="changed-synopsis")s.Logline="Web save without sync token change";if(reason=="changed-version")sync.Version="v2";if(reason=="changed-owner")db.Documents.Single().OwnerUserId="foreign";db.SaveChanges();};
        if(reason=="trashed"){sync.IsTrashed=true;await db.SaveChangesAsync();}
        if(reason=="foreign-document"){db.Documents.Single().OwnerUserId="foreign";await db.SaveChangesAsync();}
        var r=new SynopsisAiRequestDto(null,"",1,reason=="old-version"?"old":"v1",new(Logline:reason=="old-synopsis"?"Old":"Current"),reason=="foreign-project"?Guid.NewGuid():db.Documents.Single().ProjectId);
        var result=await SynopsisRun(SynopsisController(db,o),id,"evaluate",r);
        if(reason=="foreign-document")Assert.IsType<NotFoundResult>(result.Result);else Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(reason.StartsWith("changed")?1:0,o.Calls);
    }
    [Theory][InlineData("malformed")][InlineData("empty")][InlineData("field")][InlineData("version")][InlineData("downgrade")][InlineData("quota")][InlineData("free")]
    public async Task SynopsisCoachingMalformedAndEntitlementFailuresPreserveSynopsis(string reason) {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out _,out _);db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        var o=new SynopsisOrchestrator();if(reason=="malformed")o.Output="{\"proposedText\":\"Missing commentary\"}";if(reason=="empty")o.Output="";if(reason=="quota")o.Code="AI_QUOTA_EXCEEDED";
        var r=new SynopsisAiRequestDto(reason=="field"?"outline_draft":"logline","Author story intent",reason=="version"?9:reason=="downgrade"?0:1,"v1",new(),db.Documents.Single().ProjectId);
        var result=await SynopsisRun(SynopsisController(db,o,reason=="free"?PlanTier.Free:PlanTier.Professional),id,"suggest",r);
        if(reason=="free"||reason=="quota")Assert.Equal(402,Assert.IsAssignableFrom<ObjectResult>(result.Result).StatusCode);else Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(db.DocumentSynopses);
    }
    private static DocumentSynopsisController SynopsisController(WriterApp.Data.AppDbContext db,IAiOrchestrator o,PlanTier tier=PlanTier.Professional,IAiActionHistoryStore? history=null)=>new(db,new DocumentRepository(db,NullLogger<DocumentRepository>.Instance),new StubUserIdResolver(),o,new SynopsisAiContextBuilder(),new StoryCoachContextBuilder(),new StubEntitlementService(tier),history??new InMemoryAiActionHistoryStore(),NullLogger<DocumentSynopsisController>.Instance){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
    private static Task<ActionResult<SynopsisAiResponseDto>> SynopsisRun(DocumentSynopsisController c,Guid id,string mode,SynopsisAiRequestDto r)=>mode switch{"evaluate"=>c.EvaluateSynopsis(id,r,default),"questions"=>c.AskQuestions(id,r,default),_=>c.SuggestField(id,r,default)};
}
