using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Controllers;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingSendAvailabilityTests
{
    private sealed class Identity : IUserIdResolver
    {
        public string ResolveUserId(ClaimsPrincipal user) => "author";
    }
    private sealed class Entitlements(bool enabled,int budget,int used) : IEntitlementService
    {
        public Task<UserEntitlements> GetEntitlementsAsync(string actor)=>Task.FromResult(new UserEntitlements(actor,"standard","Standard",new Dictionary<string,string> {
            ["ai.enabled"]=enabled?"true":"false",["ai.monthly_tokens"]=budget.ToString(),["ai.tokens_used_this_period"]=used.ToString()
        }));
        public PlanTier GetUserTier(UserEntitlements value)=>PlanTier.Standard;
        public Task<bool> HasAsync(string actor,string key)=>throw new NotSupportedException();
        public Task<int?> GetIntAsync(string actor,string key)=>throw new NotSupportedException();
        public void InvalidateForUser(string actor){}
    }
    [Theory]
    [InlineData(false,true,250000,0,"AI is disabled")]
    [InlineData(true,false,0,0,"not enabled for your plan")]
    [InlineData(true,true,100,100,"quota is exhausted")]
    [InlineData(true,true,100,101,"quota is exhausted")]
    [InlineData(true,true,250000,123,null)]
    public async Task DiscoveryKeepsContextAndSessionsButBlocksUnavailableGeneration(bool configured,bool enabled,int budget,int used,string? blocked)
    {
        var controller=new BrainstormingCapabilitiesController(options:Options.Create(new WriterAiOptions {Enabled=configured}),
            entitlements:new Entitlements(enabled,budget,used),identity:new Identity()) {
                ControllerContext=new() {HttpContext=new DefaultHttpContext()}
            };
        var capabilities=Assert.IsType<BrainstormingCapabilities>(Assert.IsType<OkObjectResult>((await controller.Get()).Result).Value);
        capabilities.Validate();Assert.True(capabilities.Sessions.IsAvailable);Assert.True(capabilities.ContextPreparation!.IsAvailable);
        Assert.Equal(blocked is null,capabilities.Conversation.IsAvailable);Assert.Equal(blocked is null,capabilities.Streaming.IsAvailable);
        if(blocked is not null) {
            Assert.Contains(blocked,capabilities.Conversation.Message);Assert.False(capabilities.Dictation.IsAvailable);Assert.False(capabilities.SpokenDialogue.IsAvailable);
        }
    }
}
