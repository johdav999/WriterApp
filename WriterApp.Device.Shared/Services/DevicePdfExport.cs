namespace WriterApp.Device.Shared.Services;
public interface IDevicePdfExport
{
    bool IsAvailable { get; }
    Task<byte[]?> RenderAsync(string html, double widthMm, double heightMm, CancellationToken ct=default);
}
public sealed class UnavailableDevicePdfExport : IDevicePdfExport
{
    public bool IsAvailable => false;
    public Task<byte[]?> RenderAsync(string html, double widthMm, double heightMm, CancellationToken ct=default) => throw new NotSupportedException("Native PDF export is unavailable on this host.");
}
