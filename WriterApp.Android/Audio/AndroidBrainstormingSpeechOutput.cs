using global::Android.Speech.Tts;
using global::Android.OS;
using WriterApp.Shared.Brainstorming;
using TextToSpeech = global::Android.Speech.Tts.TextToSpeech;

namespace WriterApp.Android;

public sealed class AndroidBrainstormingSpeechOutput : Java.Lang.Object, IBrainstormingSpeechOutput, TextToSpeech.IOnInitListener, IAsyncDisposable
{
    private readonly TaskCompletionSource<bool> initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SemaphoreSlim gate = new(1, 1);
    private TextToSpeech? engine;
    private Playback? current;
    private readonly object sync = new();
    private sealed record Playback(BrainstormingPlayback Value, TaskCompletionSource Done);
    public event Action<BrainstormingPlaybackProgress>? Progress;
    public void OnInit(OperationResult status) => initialized.TrySetResult(status == OperationResult.Success);

    private async Task<TextToSpeech> EngineAsync(CancellationToken ct)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (engine is not null) return;
            engine = new TextToSpeech(global::Android.App.Application.Context, this);
            engine.SetOnUtteranceProgressListener(new Listener(this));
        });
        if (!await initialized.Task.WaitAsync(TimeSpan.FromSeconds(10), ct)) throw new BrainstormingSpeechException("voice", "Android speech engine is unavailable.");
        return engine!;
    }
    private static Voice? VoiceFor(TextToSpeech engine, string language) => engine.Voices?.FirstOrDefault(v =>
        v.Locale?.Language == language && !v.IsNetworkConnectionRequired);
    public async Task<BrainstormingCapability> AvailabilityAsync(string language, CancellationToken ct = default)
    {
        BrainstormingSpeech.RequireLanguage(language);
        try
        {
            var tts = await EngineAsync(ct);
            bool available = VoiceFor(tts, language) is not null;
            return new(available ? BrainstormingCapabilityState.Available : BrainstormingCapabilityState.Unavailable,
                available ? "Installed Android voice is available offline." : "Install an offline English/Swedish voice in Android text-to-speech settings or continue typing.");
        }
        catch (Exception error) when (error is BrainstormingSpeechException or TimeoutException or Java.Lang.IllegalStateException)
        { return new(BrainstormingCapabilityState.Unavailable, "Android speech output is unavailable. Continue typing."); }
    }
    public async Task PlayAsync(BrainstormingPlayback value, CancellationToken ct = default)
    {
        value.Validate();
        var tts = await EngineAsync(ct);
        var voice = VoiceFor(tts, value.Language) ?? throw new BrainstormingSpeechException("voice", "Install an offline voice for the selected language.");
        var playback = new Playback(value, new(TaskCreationOptions.RunContinuationsAsynchronously));
        await gate.WaitAsync(ct);
        try
        {
            await StopAsync();
            ct.ThrowIfCancellationRequested();
            lock (sync) current = playback;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                if (tts.SetVoice(voice) != OperationResult.Success || tts.Speak(value.Text, QueueMode.Flush, new Bundle(), value.Id.ToString("N")) != OperationResult.Success)
                    throw new BrainstormingSpeechException("output", "Android could not start speech playback. Saved text retained.");
            });
        }
        catch { lock (sync) { if (current == playback) current = null; } throw; }
        finally { gate.Release(); }
        using var registration = ct.Register(() => { lock (sync) if (current == playback) { engine?.Stop(); playback.Done.TrySetCanceled(ct); } });
        try { await playback.Done.Task.WaitAsync(TimeSpan.FromMinutes(5), ct); }
        finally { lock (sync) { if (current == playback) { engine?.Stop(); current = null; } } }
    }
    public Task StopAsync() => MainThread.InvokeOnMainThreadAsync(() =>
    {
        lock (sync) { engine?.Stop(); current?.Done.TrySetCanceled(); current = null; }
    });
    private void Event(string? id, bool started, bool failed)
    {
        Playback? playback;
        lock (sync) { playback = current; if (playback?.Value.Id.ToString("N") != id) return; }
        if (started) Progress?.Invoke(new(playback!.Value.Id, BrainstormingPlaybackState.Playing, "Playing installed Android voice; microphone is closed."));
        else if (failed) playback!.Done.TrySetException(new BrainstormingSpeechException("output", "Android speech playback failed. Saved text retained."));
        else playback!.Done.TrySetResult();
    }
    public async ValueTask DisposeAsync() { await StopAsync(); await MainThread.InvokeOnMainThreadAsync(() => { engine?.Shutdown(); engine?.Dispose(); engine = null; }); }
    private sealed class Listener(AndroidBrainstormingSpeechOutput owner) : UtteranceProgressListener
    {
        public override void OnStart(string? id) => owner.Event(id, true, false);
        public override void OnDone(string? id) => owner.Event(id, false, false);
        [Obsolete("Required Android compatibility callback.")]
        public override void OnError(string? id) => owner.Event(id, false, true);
        public override void OnError(string? id, TextToSpeechError error) => owner.Event(id, false, true);
    }
}
