using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Desktop;

public sealed class WindowsBrainstormingMicrophone:IBrainstormingMicrophone {
    private Recording? _current;
    public async Task<BrainstormingCapability> AvailabilityAsync(CancellationToken ct=default) {
        var devices=await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture).AsTask(ct);
        return new(devices.Count>0?BrainstormingCapabilityState.Available:BrainstormingCapabilityState.Unavailable,devices.Count>0?"Windows microphone capture is available; permission is requested only on Start.":"No recording device found. Connect a microphone or continue typing.");
    }
    public async Task<IBrainstormingRecording> StartAsync(CancellationToken ct=default) {
        if(_current is { } active && !active.Audio.IsCompleted)throw new InvalidOperationException("Another recording is active.");
        var recording=new Recording(ct);_current=recording;try{await recording.StartAsync();return recording;}catch{await recording.DisposeAsync();_ = recording.Audio.Exception;throw;}
    }
    public Task CancelAsync()=>_current?.CancelAsync()??Task.CompletedTask;
    private sealed class Recording(CancellationToken token):IBrainstormingRecording {
        private readonly TaskCompletionSource<byte[]> completed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private MediaCapture? capture;private InMemoryRandomAccessStream? stream;private bool cancelled,started;private readonly SemaphoreSlim gate=new(1,1);private CancellationTokenRegistration registration;private CancellationTokenSource monitor=new();
        public Task<byte[]> Audio=>completed.Task;
        public async Task StartAsync() {
            registration=token.Register(()=>{cancelled=true;_ = CancelAsync();});
            try {
                await MainThread.InvokeOnMainThreadAsync(async()=>{
                    await gate.WaitAsync();try {
                        token.ThrowIfCancellationRequested();capture=new();capture.Failed+=Failed;stream=new();
                        await capture.InitializeAsync(new MediaCaptureInitializationSettings {StreamingCaptureMode=StreamingCaptureMode.Audio,MediaCategory=MediaCategory.Speech}).AsTask(token);
                        token.ThrowIfCancellationRequested();var profile=MediaEncodingProfile.CreateWav(AudioEncodingQuality.Low);profile.Audio=AudioEncodingProperties.CreatePcm(16000,1,16);
                        await capture.StartRecordToStreamAsync(profile,stream).AsTask(token);started=true;
                    }finally{gate.Release();}
                });
                if(cancelled || token.IsCancellationRequested){await CancelAsync();throw new OperationCanceledException(token);}_=BoundCapture();
            }catch(Exception e) {await CancelAsync();if(e is OperationCanceledException)throw;if(e is UnauthorizedAccessException)throw new BrainstormingSpeechException("permission","Microphone permission denied. Enable desktop microphone access in Windows Privacy settings.");throw new BrainstormingSpeechException("device","Microphone could not start. Check connection, privacy settings and whether another app owns it.");}
        }
        private async Task BoundCapture() {
            var clock=System.Diagnostics.Stopwatch.StartNew();try {while(!monitor.IsCancellationRequested){await Task.Delay(250,monitor.Token);if(clock.Elapsed.TotalSeconds>=59 || stream?.Size>=1_900_000){await StopAsync();break;}}}catch(OperationCanceledException){}catch(ObjectDisposedException){}
        }
        private void Failed(MediaCapture sender,MediaCaptureFailedEventArgs args){_ = Finish(new BrainstormingSpeechException("device","Recording device was interrupted or disconnected. Composer retained."));}
        public Task StopAsync()=>Finish(null);
        public Task CancelAsync(){cancelled=true;return Finish(new OperationCanceledException("Recording cancelled."));}
        private Task Finish(Exception? failure)=>MainThread.InvokeOnMainThreadAsync(async()=>{
            await gate.WaitAsync();try {
                if(completed.Task.IsCompleted)return;monitor.Cancel();byte[]? audio=null;
                try {if(started && capture is not null)await capture.StopRecordAsync();started=false;
                    if(failure is null && !cancelled && stream is not null){if(stream.Size>BrainstormingSpeech.MaxAudioBytes)throw new InvalidDataException("Recording exceeded its size limit.");stream.Seek(0);audio=new byte[(int)stream.Size];using var input=stream.AsStreamForRead();await input.ReadExactlyAsync(audio);BrainstormingSpeech.ValidateWav(audio);}
                    if(failure is not null || cancelled)completed.TrySetException(failure??new OperationCanceledException());else completed.TrySetResult(audio??throw new InvalidDataException("No audio recorded."));
                }catch(Exception e){if(audio is not null)Array.Clear(audio);completed.TrySetException(e);}
                finally{if(capture is not null){capture.Failed-=Failed;capture.Dispose();capture=null;}stream?.Dispose();stream=null;}
            }finally{gate.Release();}
        });
        public async ValueTask DisposeAsync(){if(!Audio.IsCompleted)await CancelAsync();registration.Dispose();monitor.Dispose();}
    }
}
