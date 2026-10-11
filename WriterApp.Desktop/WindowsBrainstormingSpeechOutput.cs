using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Desktop;

public sealed class WindowsBrainstormingSpeechOutput:IBrainstormingSpeechOutput,IAsyncDisposable {
    private Playback? current;private readonly SemaphoreSlim gate=new(1,1);
    public event Action<BrainstormingPlaybackProgress>? Progress;
    private static VoiceInformation? Voice(string language)=>SpeechSynthesizer.AllVoices.FirstOrDefault(v=>v.Language.Split('-')[0].Equals(language,StringComparison.OrdinalIgnoreCase));
    public Task<BrainstormingCapability> AvailabilityAsync(string language,CancellationToken ct=default){BrainstormingSpeech.RequireLanguage(language);ct.ThrowIfCancellationRequested();try{return Task.FromResult(new BrainstormingCapability(Voice(language) is not null?BrainstormingCapabilityState.Available:BrainstormingCapabilityState.Unavailable,Voice(language) is not null?"Installed Windows synthetic voice is available.":"Install an English/Swedish Windows speech voice in Language settings, or continue typing."));}catch(Exception e) when(e is InvalidOperationException or System.Runtime.InteropServices.COMException){return Task.FromResult(new BrainstormingCapability(BrainstormingCapabilityState.Unavailable,"Windows speech output is unavailable. Continue typing."));}}
    public async Task PlayAsync(BrainstormingPlayback value,CancellationToken ct=default){value.Validate();var voice=Voice(value.Language)??throw new BrainstormingSpeechException("voice","Install the selected Windows language voice or continue typing.");Playback playback;await gate.WaitAsync(ct);try{current?.Cancel();playback=new(value,voice,p=>Progress?.Invoke(p));current=playback;}finally{gate.Release();}
        using var registration=ct.Register(playback.Cancel);try{await playback.PlayAsync(ct);}finally{await gate.WaitAsync();try{if(current==playback)current=null;playback.Dispose();}finally{gate.Release();}}
    }
    public async Task StopAsync(){await gate.WaitAsync();try{current?.Cancel();}finally{gate.Release();}}
    public async ValueTask DisposeAsync()=>await StopAsync();
    private sealed class Playback(BrainstormingPlayback value,VoiceInformation voice,Action<BrainstormingPlaybackProgress> progress):IDisposable {
        private readonly object sync=new();private SpeechSynthesizer? synthesizer;private SpeechSynthesisStream? stream;private MediaPlayer? player;private bool cancelled;
        private readonly TaskCompletionSource completed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task PlayAsync(CancellationToken ct){try{lock(sync){ct.ThrowIfCancellationRequested();if(cancelled)throw new OperationCanceledException();synthesizer=new(){Voice=voice};}var audio=await synthesizer.SynthesizeTextToStreamAsync(value.Text).AsTask(ct);
            lock(sync){if(cancelled || ct.IsCancellationRequested){audio.Dispose();throw new OperationCanceledException(ct);}stream=audio;if(stream.Size>4_000_000)throw new InvalidDataException("Synthesized audio exceeded its transient size limit.");player=new(){AutoPlay=false};player.PlaybackSession.PlaybackStateChanged+=Playing;player.MediaEnded+=Ended;player.MediaFailed+=Failed;player.Source=MediaSource.CreateFromStream(stream,stream.ContentType);player.Play();}
            await completed.Task.WaitAsync(ct);
        }catch(OperationCanceledException){throw;}catch(Exception e) when(e is System.Runtime.InteropServices.COMException or InvalidOperationException){throw new BrainstormingSpeechException("output","Windows voice or output device failed. Saved text retained.");}}
        private void Playing(MediaPlaybackSession sender,object args){lock(sync){if(!cancelled && sender.PlaybackState==MediaPlaybackState.Playing)progress(new(value.Id,BrainstormingPlaybackState.Playing,"Playing installed Windows synthetic voice; microphone is closed."));}}
        private void Ended(MediaPlayer sender,object args)=>completed.TrySetResult();
        private void Failed(MediaPlayer sender,MediaPlayerFailedEventArgs args)=>completed.TrySetException(new BrainstormingSpeechException("output","Audio output failed or disconnected. Saved text retained."));
        public void Cancel(){lock(sync){cancelled=true;try{player?.Pause();}catch(System.Runtime.InteropServices.COMException){}finally{Release();completed.TrySetCanceled();}}}
        private void Release(){if(player is not null){player.PlaybackSession.PlaybackStateChanged-=Playing;player.MediaEnded-=Ended;player.MediaFailed-=Failed;var source=player.Source as IDisposable;player.Source=null;player.Dispose();player=null;source?.Dispose();}stream?.Dispose();stream=null;/* Synthesizer stays alive until any pending synthesis has returned. */}
        public void Dispose(){lock(sync){Release();synthesizer?.Dispose();synthesizer=null;}}
    }
}
