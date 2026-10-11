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
public sealed class BrainstormingCapabilitiesController(IOptions<WriterAiOptions>? options=null,OpenAiKeyProvider? keys=null,IAiProviderRegistry? registry=null,AppDbContext? db=null) : ControllerBase
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
        return Ok(BrainstormingCapabilities.Current with {Dictation=speech,SpokenDialogue=speech});
    }
}
