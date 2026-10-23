using WriterApp.Device.Shared.Services;

namespace WriterApp.Android;

public sealed class AndroidExternalLauncher : IDeviceExternalLauncher
{
    public async Task OpenAsync(Uri address)
    {
        if (!address.IsAbsoluteUri || address.Scheme != "https" || address.UserInfo.Length != 0)
            throw new ArgumentException("Choose an HTTPS browser destination.", nameof(address));
        if (!await Browser.Default.OpenAsync(address, BrowserLaunchMode.External)) throw new IOException("Android could not open the browser.");
    }
}
