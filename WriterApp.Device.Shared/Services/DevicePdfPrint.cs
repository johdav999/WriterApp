namespace WriterApp.Device.Shared.Services;

/// <summary>Hosts whose operating system owns PDF saving and printing use this explicit interactive path.</summary>
public interface IDevicePdfPrint
{
    bool IsAvailable { get; }
    Task<bool> PrintAsync(string html, double widthMm, double heightMm, CancellationToken ct = default);
}
public sealed class UnavailableDevicePdfPrint : IDevicePdfPrint
{
    public bool IsAvailable => false;
    public Task<bool> PrintAsync(string html, double widthMm, double heightMm, CancellationToken ct = default) => throw new NotSupportedException("System PDF printing is unavailable on this host.");
}
