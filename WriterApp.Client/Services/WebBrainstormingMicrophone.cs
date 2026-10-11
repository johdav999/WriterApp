using Microsoft.JSInterop;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Client.Services;

public sealed class WebBrainstormingMicrophone(IJSRuntime js):IBrainstormingMicrophone,IAsyncDisposable {
    private IJSObjectReference? module;private Recording? current;
    private async Task<IJSObjectReference> Module(CancellationToken ct=default)=>module??=await js.InvokeAsync<IJSObjectReference>("import",ct,"/_content/WriterApp.UI.Shared/brainstorming-microphone.js");
    public async Task<BrainstormingCapability> AvailabilityAsync(CancellationToken ct=default)=>await (await Module(ct)).InvokeAsync<BrainstormingCapability>("availability",ct);
    public async Task<IBrainstormingRecording> StartAsync(CancellationToken ct=default) {
        if(current is { } previous && !previous.Audio.IsCompleted)throw new InvalidOperationException("Another recording is active.");ct.ThrowIfCancellationRequested();var recording=new Recording(await Module(ct),ct);current=recording;
        try {await recording.StartAsync();return recording;}catch{await recording.DisposeAsync();throw;}
    }
    public Task CancelAsync()=>current?.CancelAsync()??Task.CompletedTask;
    public async ValueTask DisposeAsync(){if(current is not null)await current.DisposeAsync();if(module is not null)try{await module.DisposeAsync();}catch(JSException){} }
    public sealed class Recording:IAsyncDisposable,IBrainstormingRecording {
        private readonly IJSObjectReference module;private readonly string id=Guid.NewGuid().ToString();private readonly CancellationToken token;private readonly DotNetObjectReference<Recording> receiver;
        private readonly TaskCompletionSource<byte[]> complete=new(TaskCreationOptions.RunContinuationsAsynchronously);private readonly CancellationTokenRegistration registration;private bool disposed,cancelled;
        public Recording(IJSObjectReference module,CancellationToken ct){this.module=module;token=ct;receiver=DotNetObjectReference.Create(this);registration=ct.Register(()=>{cancelled=true;_ = CancelAsync();});}
        public Task<byte[]> Audio=>complete.Task;
        public async Task StartAsync(){token.ThrowIfCancellationRequested();await module.InvokeVoidAsync("start",token,id,receiver);if(cancelled){await CancelAsync();token.ThrowIfCancellationRequested();}}
        [JSInvokable] public Task Recorded(string operation,byte[] wav) {if(operation!=id || cancelled || disposed){Array.Clear(wav);return Task.CompletedTask;}try{BrainstormingSpeech.ValidateWav(wav);if(!complete.TrySetResult(wav))Array.Clear(wav);}catch(InvalidDataException e){Array.Clear(wav);complete.TrySetException(e);}return Task.CompletedTask;}
        [JSInvokable] public Task RecordingFailed(string operation,string code,string message){if(operation==id && !disposed)complete.TrySetException(code=="cancelled"?new OperationCanceledException(message):new BrainstormingSpeechException(code,message));return Task.CompletedTask;}
        public async Task StopAsync(){if(!disposed && !complete.Task.IsCompleted)await module.InvokeVoidAsync("stop",id);}
        public async Task CancelAsync(){cancelled=true;try{if(!disposed)await module.InvokeVoidAsync("cancel",id);}catch(Exception e) when(e is JSException or ObjectDisposedException or InvalidOperationException){}finally{complete.TrySetCanceled();}}
        public async ValueTask DisposeAsync(){if(disposed)return;if(!complete.Task.IsCompleted)await CancelAsync();disposed=true;registration.Dispose();receiver.Dispose();}
    }
}
