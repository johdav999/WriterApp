using WriterApp.Device.Shared.Services;

namespace WriterApp.iOS.Authentication;

public sealed class IosIdentityLifecycle(IosDeviceIdentityClient identity, DeviceAccountService account)
{
    private int _restoring;
    public void Background() => identity.SetForeground(false);
    public void Destroy() { identity.SetForeground(false); account.CancelPendingAuthentication(); }
    public async Task ResumeAsync()
    {
        identity.SetForeground(true);
        // Opening the system auth session backgrounds the app. Resume must not start a second acquisition.
        if (identity.IsInteractive || Interlocked.CompareExchange(ref _restoring,1,0) != 0) return;
        try { await account.RestoreAsync(); }
        catch (OperationCanceledException) { }
        catch (DeviceIdentityConfigurationException) { }
        finally { Volatile.Write(ref _restoring,0); }
    }
}
