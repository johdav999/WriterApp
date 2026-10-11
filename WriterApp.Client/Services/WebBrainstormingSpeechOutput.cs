using Microsoft.JSInterop;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Client.Services;

public sealed class WebBrainstormingSpeechOutput(IJSRuntime js):IBrainstormingSpeechOutput,IAsyncDisposable {
    private IJSObjectReference? module;private Playback? current;private readonly SemaphoreSlim gate=new(1,1);
    public event Action<BrainstormingPlaybackProgress>? Progress;
    private async Task<IJSObjectReference> Module(CancellationToken ct)=>module??=await js.InvokeAsync<IJSObjectReference>("import",ct,"/_content/WriterApp.UI.Shared/brainstorming-speech-output.js");
    public async Task<BrainstormingCapability> AvailabilityAsync(string language,CancellationToken ct=default){BrainstormingSpeech.RequireLanguage(language);try{return await (await Module(ct)).InvokeAsync<BrainstormingCapability>("availability",ct,language);}catch(JSException){return new(BrainstormingCapabilityState.UnsupportedPlatform,"Local browser speech output is unavailable. Use text.");}}
    public async Task PlayAsync(BrainstormingPlayback value,CancellationToken ct=default){value.Validate();Playback playback;await gate.WaitAsync(ct);try{if(current is not null)await current.CancelAsync();playback=new(await Module(ct),value,p=>Progress?.Invoke(p));current=playback;}finally{gate.Release();}
        using var registration=ct.Register(()=>{_ = playback.CancelAsync();});try{await playback.StartAsync(ct);await playback.Done.WaitAsync(ct);}catch(JSException){throw new BrainstormingSpeechException("output","Speech output failed. Your saved text remains available.");}finally{await gate.WaitAsync();try{if(current==playback)current=null;await playback.DisposeAsync();}finally{gate.Release();}}
    }
    public async Task StopAsync(){await gate.WaitAsync();try{if(current is not null)await current.CancelAsync();}finally{gate.Release();}}
    public async ValueTask DisposeAsync(){await StopAsync();if(module is not null)try{await module.DisposeAsync();}catch(JSException){}}
    private sealed class Playback(IJSObjectReference module,BrainstormingPlayback value,Action<BrainstormingPlaybackProgress> progress):IAsyncDisposable {
        private readonly TaskCompletionSource completed=new(TaskCreationOptions.RunContinuationsAsynchronously);private DotNetObjectReference<Playback>? receiver;private bool cancelled;
        public Task Done=>completed.Task;
        public async Task StartAsync(CancellationToken ct){receiver=DotNetObjectReference.Create(this);ct.ThrowIfCancellationRequested();await module.InvokeVoidAsync("play",ct,value.Id.ToString(),value.Language,value.Text,receiver);if(cancelled)await CancelAsync();}
        [JSInvokable] public Task PlaybackStarted(string id){if(!cancelled && id==value.Id.ToString())progress(new(value.Id,BrainstormingPlaybackState.Playing,"Playing local synthetic voice; microphone is closed."));return Task.CompletedTask;}
        [JSInvokable] public Task PlaybackFinished(string id,string code,string message){if(id!=value.Id.ToString())return Task.CompletedTask;if(code=="cancelled")completed.TrySetCanceled();else if(code.Length>0)completed.TrySetException(new BrainstormingSpeechException(code,message));else completed.TrySetResult();return Task.CompletedTask;}
        public async Task CancelAsync(){cancelled=true;try{await module.InvokeVoidAsync("stop",value.Id.ToString());}catch(Exception e) when(e is JSException or ObjectDisposedException or InvalidOperationException){}finally{completed.TrySetCanceled();}}
        public async ValueTask DisposeAsync(){if(!Done.IsCompleted)await CancelAsync();receiver?.Dispose();}
    }
}
