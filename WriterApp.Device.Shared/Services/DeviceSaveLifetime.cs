namespace WriterApp.Device.Shared.Services;

/// <summary>The native host awaits the mounted editor before closing its WebView.</summary>
public sealed class DeviceSaveLifetime
{
    public Func<bool, Task<bool>>? Flush { get; set; }
    public Task<bool> FlushAsync(bool closing = false) => Flush?.Invoke(closing) ?? Task.FromResult(true);
}
