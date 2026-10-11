namespace WriterApp.Shared.Brainstorming;

// Audio never enters the durable cache. A lost response is inspected by identity, never reuploaded.
public sealed class BrainstormingDictationWorkflow(IBrainstormingCacheStore store,IBrainstormingSpeechTranscriber remote) {
    public Task PrepareAsync(BrainstormingScope scope,CancellationToken ct=default)=>remote.PrepareAsync(scope,ct);
    public async Task<BrainstormingCache> TranscribeAsync(BrainstormingScope scope,string language,byte[] wav,CancellationToken ct=default) {
        BrainstormingSpeech.RequireLanguage(language);BrainstormingSpeech.ValidateWav(wav);var current=await Required(scope,ct);
        if((current.Dictations??[]).Any(j=>j.Result is null || j.Result.State is BrainstormingSpeechState.Transcribing or BrainstormingSpeechState.Unknown))throw new InvalidDataException("Inspect the previous recording before another transcription.");
        var journal=new BrainstormingDictationJournal(Guid.NewGuid(),language,null);
        await store.WriteAsync(current with {Dictations=[..current.Dictations??[],journal]},current.CacheRevision,Guid.NewGuid(),ct);
        var result=await remote.TranscribeAsync(scope,journal.OperationId,language,wav,ct);ct.ThrowIfCancellationRequested();return await Adopt(scope,journal.OperationId,result,ct);
    }
    public async Task<BrainstormingCache> InspectAsync(BrainstormingScope scope,CancellationToken ct=default) {
        var current=await Required(scope,ct);var pending=(current.Dictations??[]).LastOrDefault(j=>!j.Adopted && (j.Result is null || j.Result.State is BrainstormingSpeechState.Transcribing or BrainstormingSpeechState.Unknown or BrainstormingSpeechState.Completed));if(pending is null)return current;
        var result=await remote.InspectAsync(scope,pending.OperationId,ct);
        if(result is null) {var server=current.Mapping?.Server??throw new InvalidDataException("Original mapping unavailable.");result=new(1,server,pending.OperationId,pending.Language,BrainstormingSpeechState.Failed,"","Recording was not accepted. Start a new microphone action when ready.",new(BrainstormingUsageState.NotDispatched,"","",0,0));}
        return await Adopt(scope,pending.OperationId,result,ct);
    }
    private async Task<BrainstormingCache> Adopt(BrainstormingScope scope,Guid operation,BrainstormingSpeechResult result,CancellationToken ct) {
        for(int attempt=0;attempt<3;attempt++) {var current=await Required(scope,ct);BrainstormingSpeech.Validate(result,current.Mapping?.Server??throw new InvalidDataException("Original mapping unavailable."),operation);
            var journal=(current.Dictations??[]).Single(j=>j.OperationId==operation);if(journal.Adopted)return current;if(result.Language!=journal.Language)throw new InvalidDataException("Dictation language changed.");
            var composer=current.Composer;bool adopted=false;if(result.State==BrainstormingSpeechState.Completed && composer.Length+result.Text.Length+(composer.Length>0?1:0)<=8000){composer+=(composer.Length>0?"\n":"")+result.Text;adopted=true;}
            var next=current with {Composer=composer,Dictations=current.Dictations!.Select(j=>j.OperationId==operation?j with {Result=result,Adopted=adopted}:j).ToArray()};
            try{return await store.WriteAsync(next,current.CacheRevision,Guid.NewGuid(),ct);}catch(BrainstormingConcurrencyException) when(attempt<2){}
        }throw new BrainstormingConcurrencyException();
    }
    private async Task<BrainstormingCache> Required(BrainstormingScope scope,CancellationToken ct)=>await store.ReadAsync(scope,ct)??throw new InvalidDataException("Conversation unavailable; composer retained.");
}
