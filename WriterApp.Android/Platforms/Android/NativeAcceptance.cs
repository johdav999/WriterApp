#if DEBUG
using global::Android.App;
using global::Android.OS;
using WriterApp.Shared.Brainstorming;
using System.Text.Json;

namespace WriterApp.Android;

/// <summary>adb-only Debug instrumentation exercises native audio adapters without transcription, AI or external sends.</summary>
[Instrumentation(Name = "com.prosa.writer.android.NativeAcceptance", TargetPackage = "com.prosa.writer.android", FunctionalTest = true)]
public sealed class NativeAcceptance : Instrumentation
{
    public NativeAcceptance() { }
    public NativeAcceptance(nint handle, global::Android.Runtime.JniHandleOwnership ownership) : base(handle, ownership) { }
    public override void OnCreate(Bundle? arguments) { base.OnCreate(arguments); Start(); }
    public override async void OnStart()
    {
        var result = new Bundle();
        try
        {
            var launch = TargetContext!.PackageManager!.GetLaunchIntentForPackage("com.prosa.writer.android")!;
            launch.AddFlags(global::Android.Content.ActivityFlags.NewTask);
            StartActivitySync(launch);
            var services = IPlatformApplication.Current!.Services;
            var microphone = services.GetRequiredService<IBrainstormingMicrophone>();
            var output = services.GetRequiredService<IBrainstormingSpeechOutput>();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var availability = await microphone.AvailabilityAsync(timeout.Token);
            await using var recording = await microphone.StartAsync(timeout.Token);
            await Task.Delay(400, timeout.Token);
            await recording.StopAsync();
            byte[] wav = await recording.Audio;
            var audio = BrainstormingSpeech.ValidateWav(wav);
            var en = await output.AvailabilityAsync("en", timeout.Token);
            var sv = await output.AvailabilityAsync("sv", timeout.Token);
            result.PutString("report", JsonSerializer.Serialize(new { microphone = availability.State.ToString(),
                wavBytes = wav.Length, seconds = audio.Seconds, silence = audio.Silence,
                englishVoice = en.State.ToString(), swedishVoice = sv.State.ToString(), providerRequests = 0 }));
            Array.Clear(wav);
            Finish(Result.Ok, result);
        }
        catch (Exception error) { result.PutString("failure", error.GetType().Name); Finish(Result.Canceled, result); }
    }
}
#endif
