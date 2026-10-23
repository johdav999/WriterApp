using WriterApp.Android.Authentication;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Android;

public sealed class AndroidHostLifecycle(AndroidDeviceIdentityClient identity, DeviceAccountService account,
    DeviceSaveLifetime saves, DeviceDiagnostics diagnostics, IBrainstormingMicrophone microphone,
    IBrainstormingSpeechOutput output, BrainstormingVoiceCoordinator voice)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private int restoring;
    public async Task<bool> FlushAsync()
    {
        try { return await saves.FlushAsync(); }
        catch (Exception error) { await RecordFailureAsync(error); return false; }
    }
    public async Task SuspendAsync()
    {
        identity.SetForeground(false);
        await gate.WaitAsync();
        try
        {
            try { await voice.SuspendAsync(); } catch (Exception error) { await RecordFailureAsync(error); }
            try { await microphone.CancelAsync(); } catch (Exception error) { await RecordFailureAsync(error); }
            try { await output.StopAsync(); } catch (Exception error) { await RecordFailureAsync(error); }
            await FlushAsync();
        }
        finally { gate.Release(); }
    }
    public async Task ResumeAsync()
    {
        identity.SetForeground(true);
        if (identity.IsInteractive || Interlocked.CompareExchange(ref restoring, 1, 0) != 0) return;
        try { await account.RestoreAsync(); }
        catch (Exception error) when (error is OperationCanceledException or DeviceIdentityConfigurationException) { }
        finally { Volatile.Write(ref restoring, 0); }
    }
    public void Destroy() { identity.SetForeground(false); account.CancelPendingAuthentication(); }
    public async Task RecordFailureAsync(Exception error)
    {
        try { await diagnostics.RecordAsync(DeviceDiagnosticEvent.SaveFailed, error: error); }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException) { }
    }
}
