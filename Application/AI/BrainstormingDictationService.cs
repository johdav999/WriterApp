using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Commands;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Data.Usage;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Application.AI;

public sealed class BrainstormingDictationService(AppDbContext db,IAiRouter router,IAiUsagePolicy policy,IAiQuotaService quota,IUsageMeter meter,
    IOptions<WriterAiOptions> options,BrainstormingGenerationStops stops,string owner,string backend) {
    private async Task Owned(BrainstormingScope scope,CancellationToken ct) {var session=await new BrainstormingSessions(db,owner,backend).ReadAsync(scope.DocumentId,scope.SessionId,ct)??throw new BrainstormingAccessException();BrainstormingValidation.RequireScope(session.Scope,scope);if(session.State==BrainstormingConversationState.Deleted)throw new BrainstormingAccessException();}
    private static BrainstormingSpeechResult Decode(BrainstormingTranscriptionRecord row)=>JsonSerializer.Deserialize<BrainstormingSpeechResult>(row.ResultJson,BrainstormingValidation.Json)!;
    public async Task<BrainstormingSpeechResult?> InspectAsync(BrainstormingScope scope,Guid operation,CancellationToken ct) {
        await Owned(scope,ct);var row=await db.BrainstormingTranscriptions.AsNoTracking().SingleOrDefaultAsync(r=>r.SessionId==scope.SessionId && r.OperationId==operation,ct);if(row is null)return null;
        var result=Decode(row);return result.State==BrainstormingSpeechState.Transcribing && !stops.Running(scope.SessionId,operation)?result with {State=BrainstormingSpeechState.Unknown,Message="Transcription worker ended without a confirmed outcome. Audio is not retained or replayed.",Usage=result.Usage with {State=BrainstormingUsageState.Unknown}}:result;
    }
    public async Task<BrainstormingSpeechResult> TranscribeAsync(BrainstormingSpeechRequest recording,CancellationToken ct) {
        BrainstormingSpeech.Validate(recording);await Owned(recording.Scope,ct);
        var hash=BrainstormingPersistence.Hash(new {recording.Version,recording.Scope,recording.OperationId,recording.Language,AudioHash=Convert.ToHexString(SHA256.HashData(recording.Wav))});
        var existing=await db.BrainstormingTranscriptions.AsNoTracking().SingleOrDefaultAsync(r=>r.SessionId==recording.Scope.SessionId && r.OperationId==recording.OperationId,ct);
        if(existing is not null){if(existing.PayloadHash!=hash)throw new InvalidDataException("Recording identity changed; do not replay this operation.");Array.Clear(recording.Wav);return (await InspectAsync(recording.Scope,recording.OperationId,ct))!;}
        var (seconds,silence)=BrainstormingSpeech.ValidateWav(recording.Wav);var estimate=(int)Math.Ceiling(seconds*100)+2000;
        var request=new AiRequest(recording.OperationId,OpenAiSpeechProvider.Action,[AiModality.Audio],new(recording.Scope.DocumentId,Guid.Empty,new TextRange(0,0),"",null,null,null,recording.Language,null,null,null,null,null,null),new(){{"audio",recording.Wav},{"language",recording.Language}},[],[]);
        var provider=router.Route(request).Provider;if(!provider.Capabilities.SupportsAudio || provider is not IAiBillingProvider {IsBillable:true,RequiresEntitlement:true})throw new InvalidOperationException("A real configured authorized speech provider is required.");
        var permission=await policy.EvaluateAsync(provider,request.ActionId);if(!permission.Allowed || permission.UserId!=owner)throw new BrainstormingSpeechException("quota","Dictation is not authorized for this account or its current quota.");
        var allowed=await quota.EnsureAiAllowedAsync(owner,estimate,ct);if(!allowed.Allowed)throw new BrainstormingSpeechException("quota",allowed.ErrorMessage??"Dictation quota unavailable.");
        using var cancellation=CancellationTokenSource.CreateLinkedTokenSource(ct);cancellation.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.Value.Speech.TimeoutSeconds,1,90)));
        if(!stops.Add(recording.Scope.SessionId,recording.OperationId,cancellation))throw new BrainstormingConcurrencyException();
        var result=new BrainstormingSpeechResult(1,recording.Scope,recording.OperationId,recording.Language,BrainstormingSpeechState.Transcribing,"","Transcribing; no conversation turn has been sent.",new(BrainstormingUsageState.Pending,provider.ProviderId,options.Value.Speech.Model,0,0));
        var row=new BrainstormingTranscriptionRecord {SessionId=recording.Scope.SessionId,OperationId=recording.OperationId,PayloadHash=hash,ResultJson=BrainstormingPersistence.Json(result)};bool accepted=false,dispatched=false;AiResult? answer=null;
        try {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{
                await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);await db.Database.ExecuteSqlRawAsync("UPDATE DocumentSyncClocks SET Sequence=Sequence WHERE Id=1",ct);
                if(await db.BrainstormingTranscriptions.AnyAsync(r=>r.SessionId==row.SessionId && r.OperationId==row.OperationId,ct))throw new BrainstormingConcurrencyException();
                var prior=await db.BrainstormingTranscriptions.AsNoTracking().Where(r=>r.SessionId==row.SessionId).ToArrayAsync(ct);
                if(prior.Any(r=>Decode(r).State is BrainstormingSpeechState.Transcribing or BrainstormingSpeechState.Unknown))throw new BrainstormingSpeechException("pending","Inspect the unresolved dictation before recording again; start a separate conversation to retain an uncertain operation.");
                if(await db.BrainstormingTranscriptions.CountAsync(r=>r.SessionId==row.SessionId,ct)>=100)throw new InvalidDataException("Dictation history is full. Preserve it and start a new conversation.");
                db.BrainstormingTranscriptions.Add(row);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);accepted=true;
            });
            if(silence)return await Save(row,result with {State=BrainstormingSpeechState.Silence,Message="No audible speech detected. Check the microphone and record again.",Usage=result.Usage with {State=BrainstormingUsageState.NotDispatched}});
            cancellation.Token.ThrowIfCancellationRequested();dispatched=true;answer=await provider.ExecuteAsync(request,cancellation.Token);
            // Persist uncertain settlement before quota/meter side effects; a restart never recharges or replays it.
            result=result with {State=BrainstormingSpeechState.Unknown,Message="Transcription received; usage settlement pending.",Usage=result.Usage with {State=BrainstormingUsageState.Unknown,InputTokens=answer.Usage.InputTokens,OutputTokens=answer.Usage.OutputTokens}};await Save(row,result);
            var charged=await quota.ChargeActualUsageAsync(owner,request with {Inputs=[]},answer,default);if(!charged.Applied)throw new InvalidDataException("Dictation usage settlement is unconfirmed.");
            await meter.RecordAsync(new UsageEvent {UserId=owner,Kind=request.ActionId,Provider=provider.ProviderId,Model=options.Value.Speech.Model,InputTokens=answer.Usage.InputTokens,OutputTokens=answer.Usage.OutputTokens,DocumentId=recording.Scope.DocumentId,CorrelationId=recording.OperationId});
            var text=(string)answer.ProviderMeta["text"];if(text.Length>8000 || text.Contains('\0'))throw new InvalidDataException("Recognition exceeded composer capacity.");
            cancellation.Token.ThrowIfCancellationRequested();await Owned(recording.Scope,default);
            return await Save(row,result with {State=string.IsNullOrWhiteSpace(text)?BrainstormingSpeechState.Silence:BrainstormingSpeechState.Completed,Text=string.IsNullOrWhiteSpace(text)?"":text,
                Message=string.IsNullOrWhiteSpace(text)?"Speech could not be recognized. Check device, language and noise; your typed text is unchanged.":"Final transcript is editable. Check names, language and punctuation before explicitly sending or reviewing a prose destination.",Usage=result.Usage with {State=BrainstormingUsageState.Settled},Quality=answer.ProviderMeta.TryGetValue("recognitionQuality",out var quality) && quality is BrainstormingRecognitionQuality q?q:BrainstormingRecognitionQuality.Unassessed});
        }catch(Exception e) when(e is OperationCanceledException or HttpRequestException or IOException or InvalidDataException or JsonException or InvalidOperationException or BrainstormingAccessException) {
            if(!accepted)throw;
            if(dispatched && answer is null){
                var conservative=new AiResult(recording.OperationId,[],new(estimate,0,TimeSpan.Zero),[]);
                try {var charged=await quota.ChargeActualUsageAsync(owner,request,conservative,default);if(charged.Applied)await meter.RecordAsync(new UsageEvent {UserId=owner,Kind=request.ActionId,Provider=provider.ProviderId,Model=options.Value.Speech.Model,InputTokens=estimate,DocumentId=recording.Scope.DocumentId,CorrelationId=recording.OperationId});}catch(Exception settlement) when(settlement is InvalidOperationException or DbUpdateException or IOException){ }
            }
            var retained=await Save(row,result with {State=dispatched?BrainstormingSpeechState.Unknown:BrainstormingSpeechState.Cancelled,Text="",Message=dispatched?"Transcription outcome/usage is uncertain. This operation will not be replayed; inspect its original result. Your typed composer is retained.":"Recording cancelled before provider dispatch; composer retained.",Usage=result.Usage with {State=dispatched?BrainstormingUsageState.Unknown:BrainstormingUsageState.NotDispatched,InputTokens=answer?.Usage.InputTokens??(dispatched?estimate:0),OutputTokens=answer?.Usage.OutputTokens??0}});
            if(e is BrainstormingAccessException)throw;
            return retained;
        }finally {stops.Remove(recording.Scope.SessionId,recording.OperationId);Array.Clear(recording.Wav);}
    }
    private async Task<BrainstormingSpeechResult> Save(BrainstormingTranscriptionRecord row,BrainstormingSpeechResult result) {BrainstormingSpeech.Validate(result,result.Scope,result.OperationId);row.ResultJson=BrainstormingPersistence.Json(result);db.Entry(row).State=EntityState.Modified;await db.SaveChangesAsync();return result;}
}
