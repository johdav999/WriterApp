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
    public static IEnumerable<object[]> AdvertisedToneRequests => WritingActions.ToneDescriptors.SelectMany(t =>
        new[] { "rewrite.selection", "change_tone.section" }.Select(action => new object[] { action, t.Value }));

    [Theory][MemberData(nameof(AdvertisedToneRequests))]
    public async Task AdvertisedToneReachesWritingEndpointAndProviderRequest(string action, string tone)
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section, out var page);
        db.DocumentSyncRecords.Add(new DocumentSyncRecord { DocumentId = id, OwnerUserId = "user-1", Version = "v1", Sequence = 1 });
        await db.SaveChangesAsync();
        var parameters = new Dictionary<string, object?> { ["tone"] = tone, ["length"] = "Same", ["preserve_terms"] = true };
        var descriptor = WritingActions.ToneDescriptors.Single(t => t.Value == tone);
        if (action == "rewrite.selection") parameters["instruction"] = descriptor.ClientPresetLabel;
        else parameters[WritingActions.Parameter] = WritingActions.Serialize(new WritingStructure(1, id, section, [new(page, [new("0.0", "Maya stepped forward.")])]));
        var ai = new WritingOrchestrator();
        var request = new AiActionExecuteRequestDto(id, section, page, action == "rewrite.selection" ? 0 : null,
            action == "rewrite.selection" ? 4 : null, action == "rewrite.selection" ? "Maya" : null, null, null, parameters, "v1");
        var result = await BuildController(db, ai).ExecuteAction(action, request, default);
        Assert.IsType<OkObjectResult>(result.Result); Assert.Equal(1, ai.Calls);
        Assert.Equal(tone, ai.Request!.Inputs["tone"]);
        Assert.Contains(action == "rewrite.selection" ? descriptor.ClientPresetLabel : $"Change the tone to {tone}.", (string)ai.Request.Inputs["instruction"]);
        Assert.Contains("Maya checked", db.Pages.Single().Content);
    }

    [Theory][InlineData("current",1,true)][InlineData("stale",0,false)][InlineData("foreign-owner",0,false)][InlineData("wrong-page",0,false)]
    [InlineData("partial",1,false)][InlineData("late-source",1,false)][InlineData("wrong-section",0,false)][InlineData("invalid-provider",1,false)]
    public async Task WritingStructureRequiresOwnedCompletePageTargetsAndCurrentRevision(string scenario,int calls,bool success) {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        db.DocumentSyncRecords.Add(new DocumentSyncRecord{DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        if(scenario=="foreign-owner"){db.Documents.Single().OwnerUserId="other";await db.SaveChangesAsync();}
        var source=new WritingStructure(1,id,scenario=="wrong-section"?Guid.NewGuid():section,[new(scenario=="wrong-page"?Guid.NewGuid():page,[new("0.0","Maya stepped forward.")])]);
        var ai=new WritingOrchestrator{Partial=scenario=="partial",InvalidProvider=scenario=="invalid-provider"};if(scenario=="late-source")ai.Before=()=>{db.DocumentSyncRecords.Single().Version="v2";db.SaveChanges();};
        var request=new AiActionExecuteRequestDto(id,section,page,null,null,null,null,null,new(){[WritingActions.Parameter]=WritingActions.Serialize(source)},scenario=="stale"?"old":"v1");
        var result=await BuildController(db,ai).ExecuteAction("expand.section",request,default);Assert.Equal(calls,ai.Calls);
        if(success){var response=Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);Assert.Equal("v1",response.SourceDocumentVersion);_=WritingActions.Result(response.ProposedText!,source);Assert.Equal(WritingActions.Serialize(source),ai.Request!.Context.OriginalText);Assert.Equal(true,ai.Request.Inputs["structured_writing"]);}
        else Assert.True(result.Result is ConflictObjectResult or NotFoundResult or BadRequestObjectResult || result.Result is ObjectResult{StatusCode:502});
        if (scenario == "partial") {
            var failure = Assert.IsType<ObjectResult>(result.Result);
            var json = System.Text.Json.JsonSerializer.SerializeToElement(failure.Value);
            Assert.Equal("ai.invalid_section_revision", json.GetProperty("code").GetString());
        }
        if (scenario == "invalid-provider") {
            var failure = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(502, failure.StatusCode);
            Assert.Equal("ai.invalid_section_revision", Assert.IsType<ProblemDetails>(failure.Value).Extensions["code"]);
        }
        Assert.Contains("Maya checked",db.Pages.Single().Content);
    }
    [Theory][InlineData(PlanTier.Free)][InlineData(PlanTier.Standard)][InlineData(PlanTier.Professional)]
    public async Task WritingAvailabilityUsesSameRegisteredActionsAndFeatureEntitlementsAsExecution(PlanTier tier) {
        await using var db=BuildDbContext();var ai=new WritingOrchestrator();var response=await BuildController(db,ai,new StubEntitlementService(tier)).WritingAvailability();
        var result=Assert.IsType<WritingAvailability>(Assert.IsType<OkObjectResult>(response.Result).Value);Assert.True(result.StructuredSections);
        foreach(var action in ai.Actions){var feature=action.RequiresSelection?FeatureKey.RewriteSelection:action.ActionId=="propose.next-paragraph"?FeatureKey.NextParagraph:FeatureKey.AdvancedReviseTools;Assert.Equal(FeatureRegistry.IsFeatureAllowed(feature,tier),result.AllowedActions.Contains(action.ActionId));}
        Assert.DoesNotContain("missing.action",result.AllowedActions);
    }
    [Fact]
    public async Task NextParagraphActionUsesExactSavedMultiPageOverrideAndSceneMetadataWithoutSelection() {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        db.DocumentSyncRecords.Add(new DocumentSyncRecord{DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        var ai=new WritingOrchestrator();var request=new AiActionExecuteRequestDto(id,section,page,null,null,null,"First saved page.\n\nLast saved page.",null,new(){["emotional_beat"]="A sudden discovery",["instruction"]="Continue the scene."},"v1");
        var result=await BuildController(db,ai).ExecuteAction("propose.next-paragraph",request,default);Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(request.SurroundingText,ai.Request!.Inputs["recent_context"]);Assert.Equal("",ai.Request.Context.OriginalText);Assert.True(ai.Request.Inputs.ContainsKey("narrative_intent"));
    }
    private sealed class WritingOrchestrator : IAiOrchestrator {
        public int Calls;public bool Partial;public bool InvalidProvider;public Action? Before;public AiRequest? Request;
        public IReadOnlyList<IAiAction> Actions=>[new ExpandSectionAction(),new TightenSectionAction(),new ChangeToneSectionAction(),new ShowDontTellSectionAction(),new RewriteSelectionAction(),new ProposeNextParagraphAction()];
        public IAiAction? GetAction(string id)=>Actions.SingleOrDefault(a=>a.ActionId==id);public bool CanRunAction(string id)=>true;
        public AiStreamingCapabilities GetStreamingCapabilities(string id)=>new(false,false);
        public Task<AiExecutionResult> ExecuteActionAsync(string id,AiActionInput input,CancellationToken ct){Calls++;Request=GetAction(id)!.BuildRequest(input);Before?.Invoke();
            if(InvalidProvider)throw new AiProviderException("openai","OpenAI returned an invalid structured writing revision.");
            return Task.FromResult(AiExecutionResult.Success(new(Guid.NewGuid(),input.ActiveSectionId,"Writing",id,"mock",Guid.NewGuid(),DateTime.UtcNow,null,[],[],"Writing","section",null,Request.Context.OriginalText,Partial?"{}":id=="propose.next-paragraph"?"A new visitor arrived.":Request.Context.OriginalText)));}
        public AiStreamingSession StreamActionAsync(string id,AiActionInput input,CancellationToken ct)=>throw new NotSupportedException();
    }
}
