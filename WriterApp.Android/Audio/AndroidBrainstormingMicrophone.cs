using global::Android.Media;
using global::Android.Content.PM;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Android;

public sealed class AndroidBrainstormingMicrophone : IBrainstormingMicrophone
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private Recording? current;
    public Task<BrainstormingCapability> AvailabilityAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        bool available = global::Android.App.Application.Context.PackageManager!.HasSystemFeature(PackageManager.FeatureMicrophone);
        return Task.FromResult(new BrainstormingCapability(available ? BrainstormingCapabilityState.Available : BrainstormingCapabilityState.Unavailable,
            available ? "Android microphone is available. Permission is requested when recording starts." : "No microphone is available. Continue typing."));
    }
    public async Task<IBrainstormingRecording> StartAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (current is { Audio.IsCompleted: false }) throw new InvalidOperationException("Another recording is active.");
            if (current is not null) await current.DisposeAsync();
            var permission = await MainThread.InvokeOnMainThreadAsync(() => Permissions.RequestAsync<Permissions.Microphone>());
            ct.ThrowIfCancellationRequested();
            if (permission != PermissionStatus.Granted) throw new BrainstormingSpeechException("permission", "Microphone permission denied. Enable it in Android app settings or continue typing.");
            int size = AudioRecord.GetMinBufferSize(16000, ChannelIn.Mono, global::Android.Media.Encoding.Pcm16bit);
            if (size <= 0) throw new BrainstormingSpeechException("device", "This microphone cannot capture the required audio format.");
#pragma warning disable CS0618
            var recorder = new AudioRecord(AudioSource.Mic, 16000, ChannelIn.Mono, global::Android.Media.Encoding.Pcm16bit, Math.Max(size, 8192));
#pragma warning restore CS0618
            if (recorder.State != State.Initialized) { recorder.Dispose(); throw new BrainstormingSpeechException("device", "Microphone initialization failed."); }
            try { recorder.StartRecording(); }
            catch { recorder.Dispose(); throw new BrainstormingSpeechException("device", "Microphone could not start. Check permissions and other recording apps."); }
            return current = new Recording(recorder, ct);
        }
        finally { gate.Release(); }
    }
    public async Task CancelAsync()
    {
        await gate.WaitAsync();
        try { if (current is not null) await current.CancelAsync(); }
        finally { gate.Release(); }
    }

    private sealed class Recording : IBrainstormingRecording
    {
        private readonly object sync = new();
        private AudioRecord? recorder;
        private volatile bool stopping, cancelled;
        private readonly CancellationTokenRegistration registration;
        public Task<byte[]> Audio { get; }
        public Recording(AudioRecord value, CancellationToken ct)
        {
            recorder = value;
            Audio = Task.Run(Capture);
            registration = ct.Register(() => Stop(true));
        }
        private byte[] Capture()
        {
            using var pcm = new MemoryStream();
            byte[] chunk = new byte[4096];
            try
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                while (!stopping && pcm.Length < BrainstormingSpeech.MaxSeconds * 32000 && clock.Elapsed.TotalSeconds < BrainstormingSpeech.MaxSeconds)
                {
                    int count = recorder!.Read(chunk, 0, Math.Min(chunk.Length, BrainstormingSpeech.MaxSeconds * 32000 - (int)pcm.Length));
                    if (count <= 0) { if (stopping) break; throw new BrainstormingSpeechException("device", "Microphone was interrupted. Composer retained."); }
                    if (!cancelled) pcm.Write(chunk, 0, count);
                }
                if (cancelled) throw new OperationCanceledException("Recording cancelled.");
                return AndroidPcmWav.Encode(pcm.GetBuffer().AsSpan(0, (int)pcm.Length));
            }
            finally
            {
                Array.Clear(chunk); Array.Clear(pcm.GetBuffer());
                lock (sync) { try { recorder?.Stop(); } catch (Java.Lang.IllegalStateException) { } recorder?.Release(); recorder?.Dispose(); recorder = null; }
            }
        }
        private void Stop(bool cancel)
        {
            if (cancel) cancelled = true;
            stopping = true;
            lock (sync) { try { recorder?.Stop(); } catch (Java.Lang.IllegalStateException) { } }
        }
        public async Task StopAsync() { Stop(false); await Audio; }
        public async Task CancelAsync() { Stop(true); try { await Audio; } catch (OperationCanceledException) { } catch (BrainstormingSpeechException) { } catch (InvalidDataException) { } }
        public async ValueTask DisposeAsync() { if (!Audio.IsCompleted) await CancelAsync(); registration.Dispose(); }
    }
}
