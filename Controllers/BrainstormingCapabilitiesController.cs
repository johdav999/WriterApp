using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Shared.Brainstorming;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Providers.OpenAI;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;

namespace WriterApp.Controllers;

[ApiController, Authorize, Route("api/brainstorming/capabilities")]
public sealed class BrainstormingCapabilitiesController(IOptions<WriterAiOptions>? options=null,OpenAiKeyProvider? keys=null,IAiProviderRegistry? registry=null,AppDbContext? db=null,
    WriterApp.Application.Subscriptions.IEntitlementService? entitlements=null,WriterApp.Application.Security.IUserIdResolver? identity=null) : ControllerBase
{
    // Do not infer conversation or speech support from provider text-streaming flags.
    [HttpGet]
    public async Task<ActionResult<BrainstormingCapabilities>> Get(CancellationToken ct=default)
    {
        Response.Headers.CacheControl = "no-store";
        if(db is not null && (await db.Database.GetPendingMigrationsAsync(ct)).Any(m=>m.Contains("Brainstorming",StringComparison.Ordinal)))
            return Ok(BrainstormingCapabilities.All(BrainstormingCapabilityState.Unavailable,
                "Brainstorm needs a database update on the backend. Your writing and unsent message remain on this device. Update the backend, then reopen Brainstorm."));
        var configured=options is not null && OpenAiSpeechProvider.Configured(options.Value,keys?.HasKey==true) && registry?.GetById(options.Value.Providers.DefaultAudioProviderId)?.Capabilities.SupportsAudio==true;
        var speech=new BrainstormingCapability(configured?BrainstormingCapabilityState.Available:BrainstormingCapabilityState.Unavailable,configured?"Bounded English/Swedish transcription is configured. Voice requires supported installed/local output and explicit Start.":"Configure an authorized speech model and server API key to enable voice. Text brainstorming remains available.");
        var capabilities=BrainstormingCapabilities.Current with {Dictation=speech,SpokenDialogue=speech};
        string? blocked=options?.Value.Enabled==false ? "AI is disabled on this backend. Your message is retained." : null;
        if(blocked is null && entitlements is not null && identity is not null) {
            var account=await entitlements.GetEntitlementsAsync(identity.ResolveUserId(User));
            var enabled=account.Entitlements.GetValueOrDefault("ai.enabled");
            int Budget(string key)=>int.TryParse(account.Entitlements.GetValueOrDefault(key),out var value)?Math.Max(0,value):0;
            blocked=!string.Equals(enabled,"true",StringComparison.OrdinalIgnoreCase) ? "AI is not enabled for your plan. Your message is retained."
                : Budget("ai.monthly_tokens")<=Budget("ai.tokens_used_this_period") ? "AI quota is exhausted. Your message is retained; check your account before retrying." : null;
        }
        if(blocked is not null) {
            var unavailable=new BrainstormingCapability(BrainstormingCapabilityState.Unavailable,blocked);
            capabilities=capabilities with {Conversation=unavailable,Streaming=unavailable,Dictation=unavailable,SpokenDialogue=unavailable};
        }
        return Ok(capabilities);
    }
}
