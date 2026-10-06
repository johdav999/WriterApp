using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.AI;
using WriterApp.Application.Subscriptions;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class AiActionsControllerTests
{
    [Theory][InlineData("available")][InlineData("used")][InlineData("expired")][InlineData("metadata-only")][InlineData("ordinary")]
    public async Task RealDemoEligibilityAllowsOnlyTheServerGrantAndOrdinaryFreeRequestsRemainDenied(string scenario) {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var doc,out var section,out var page);SeedOnboardingDemoScene(db,section,false);
        var grant=db.OnboardingDemoWorkspaces.Single();
        if(scenario=="used")grant.RequestUsed=true;
        if(scenario=="expired")grant.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1);
        if(scenario=="metadata-only")db.OnboardingDemoWorkspaces.Remove(grant);
        db.DocumentSyncRecords.Add(new(){DocumentId=doc,OwnerUserId="user-1",Version="v1",Sequence=1});await db.SaveChangesAsync();
        var ai=new WritingOrchestrator();var controller=BuildController(db,ai,new StubEntitlementService(PlanTier.Free));
        var structure=new WritingStructure(1,doc,section,[new(page,[new("0.0","Maya stepped forward.")])]);
        var request=new AiActionExecuteRequestDto(doc,section,page,null,null,null,null,null,new(){[WritingActions.Parameter]=WritingActions.Serialize(structure),
            [OnboardingDemoAiUsage.RequestParameterKey]=scenario!="ordinary"},"v1");
        var result=await controller.ExecuteAction(OnboardingAiDemoRequest.ActionKey,request,default);
        if(scenario=="available"){
            Assert.IsType<OkObjectResult>(result.Result);Assert.Equal(1,ai.Calls);
            await db.Entry(grant).ReloadAsync();Assert.True(grant.RequestUsed);Assert.NotNull(grant.ProposalId);
            var second=await BuildController(db,ai,new StubEntitlementService(PlanTier.Free)).ExecuteAction(OnboardingAiDemoRequest.ActionKey,request,default);
            Assert.IsNotType<OkObjectResult>(second.Result);Assert.Equal(1,ai.Calls);
        }else {Assert.IsNotType<OkObjectResult>(result.Result);Assert.Equal(0,ai.Calls);}
        Assert.Contains("Maya checked",db.Pages.Single().Content);
    }
}
