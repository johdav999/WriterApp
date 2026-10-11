using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.UI.Shared;

public partial class BrainstormingConversationPanel {
    [Parameter] public BrainstormingDictationWorkflow? Dictation {get;set;}
    [Parameter] public BrainstormingSessionService? Sessions {get;set;}
    [Parameter] public BrainstormingCapability? DictationAvailability {get;set;}
    private IBrainstormingMicrophone Microphone=default!;
    private BrainstormingCapability? _microphoneAvailability;
    private IBrainstormingRecording? _recording;
    private CancellationTokenSource? _speechCancel;
    private bool _dictating,_speechChecked;
    private string _speechLanguage="en";
    private string? _speechMessage;
    private bool HasPendingDictation=>(_cache?.Dictations??[]).Any(d=>d.Result is null || d.Result.State is BrainstormingSpeechState.Transcribing or BrainstormingSpeechState.Unknown);
    private async Task CheckMicrophone() {
        if(_speechChecked)return;_speechChecked=true;Microphone=Services.GetService(typeof(IBrainstormingMicrophone)) as IBrainstormingMicrophone??new UnsupportedBrainstormingMicrophone();
        try{_microphoneAvailability=await Microphone.AvailabilityAsync();}catch(Exception e) when(e is JSException or IOException or InvalidOperationException){_microphoneAvailability=new(BrainstormingCapabilityState.UnsupportedPlatform,"Microphone capture is unavailable. Continue typing.");}
        if(!_disposed)StateHasChanged();
    }
    private async Task StartDictation() {
        if(_disposed || _dictating || _busy || _opening || Dictation is null || DictationAvailability?.IsAvailable!=true || _microphoneAvailability?.IsAvailable!=true || HasPendingDictation)return;
        var epoch=_epoch;var scope=Scope;var cancellation=new CancellationTokenSource();_speechCancel=cancellation;_dictating=true;_speechMessage="Preparing authorized dictation…";await BusyChanged.InvokeAsync(true);IBrainstormingRecording? recording=null;byte[]? audio=null;
        try {
            await SaveComposer();await Dictation.PrepareAsync(scope,cancellation.Token);if(epoch!=_epoch || _disposed)return;
            await using(var floor=await Voice.AcquireMicrophoneAsync(scope,cancellation.Token)) {
            _speechMessage="Requesting microphone permission…";StateHasChanged();recording=await Microphone.StartAsync(floor.Token);if(epoch!=_epoch || _disposed){await recording.CancelAsync();return;}_recording=recording;
            _speechMessage="Listening · stop to transcribe, or cancel to discard audio.";StateHasChanged();audio=await recording.Audio.WaitAsync(floor.Token);_recording=null; }
            if(epoch!=_epoch || _disposed)return;_speechMessage="Transcribing · your typed message is retained.";StateHasChanged();var cache=await Dictation.TranscribeAsync(scope,_speechLanguage,audio,cancellation.Token);
            if(epoch!=_epoch || _disposed)return;await AdoptDictation(cache);
        }catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException or OperationCanceledException or HttpRequestException or JSException or BrainstormingConcurrencyException){if(epoch==_epoch && !_disposed)_speechMessage=e is OperationCanceledException?"Dictation cancelled. Typed text is retained; inspect any dispatched operation before another recording.":e.Message;}
        finally {if(audio is not null)Array.Clear(audio);if(recording is not null)await recording.DisposeAsync();if(ReferenceEquals(_speechCancel,cancellation))_speechCancel=null;cancellation.Dispose();if(epoch==_epoch && !_disposed){_recording=null;_dictating=false;await BusyChanged.InvokeAsync(false);try{var retained=await Store.ReadAsync(scope);if(epoch==_epoch && retained is not null){_cache=retained;_composer=retained.Composer;await CacheChanged.InvokeAsync(retained);}}catch(Exception e) when(e is OperationCanceledException or IOException or HttpRequestException){ }} }
    }
    private Task StopDictation()=>_recording?.StopAsync()??Task.CompletedTask;
    private async Task CancelDictation(){_speechCancel?.Cancel();if(_recording is { } recording)await recording.CancelAsync();}
    private void InvalidateDictation(){InvalidateVoice();_speechCancel?.Cancel();var recording=_recording;_recording=null;_dictating=false;_speechMessage=null;if(recording is not null)_=recording.CancelAsync();}
    private async Task AdoptDictation(BrainstormingCache cache){_cache=cache;_composer=cache.Composer;var last=cache.Dictations?.LastOrDefault();_speechMessage=last?.Result?.Message??"Recording retained for inspection.";if(last?.Result?.State==BrainstormingSpeechState.Completed && !last.Adopted)_speechMessage="Composer is full. Final transcript is retained below; shorten your message then inspect to append it.";await CacheChanged.InvokeAsync(cache);}
    private async Task InspectDictation(){if(_dictating || _busy || Dictation is null)return;var epoch=_epoch;try{await SaveComposer();var cache=await Dictation.InspectAsync(Scope);if(epoch==_epoch && !_disposed)await AdoptDictation(cache);}catch(Exception e) when(e is IOException or InvalidDataException or OperationCanceledException or HttpRequestException or BrainstormingConcurrencyException){if(epoch==_epoch)_speechMessage=e.Message;}}
    private async Task ReviewDictatedProse(){if(_dictating || _busy || !Ready || Sessions is null)return;var epoch=_epoch;var scope=Scope;
        try{await SaveComposer();if(BeforeDraftSend is not null)await BeforeDraftSend();if(BeforeSend is not null)await BeforeSend();if(epoch!=_epoch || _disposed)return;var op=_cache?.Dictations?.LastOrDefault(d=>d.Result?.State==BrainstormingSpeechState.Completed)?.OperationId??throw new InvalidDataException("Inspect completed dictation first.");
            var cache=await Sessions.CreateDictatedDraftAsync(scope,_composer,op);if(epoch!=_epoch || _disposed)return;_cache=cache;await CacheChanged.InvokeAsync(cache);_speechMessage="Author dictated proposal saved for the reviewed destination. Manuscript unchanged; Keep requires separate exact-version approval.";
        }catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException or OperationCanceledException or HttpRequestException or BrainstormingConcurrencyException){if(epoch==_epoch)_speechMessage=e.Message;}
    }
}
