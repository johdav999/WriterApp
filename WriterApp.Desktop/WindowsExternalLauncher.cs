using WriterApp.Device.Shared.Services;

namespace WriterApp.Desktop;

public sealed class WindowsExternalLauncher : IDeviceExternalLauncher
{
    public async Task OpenAsync(Uri address)
    {
        if (address.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Only HTTPS update links can be opened.", nameof(address));
        if (!await Launcher.Default.OpenAsync(address))
            throw new IOException("Windows could not open the update link.");
    }
}
