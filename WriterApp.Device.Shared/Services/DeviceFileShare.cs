namespace WriterApp.Device.Shared.Services;

public interface IDeviceFileShare
{
    bool IsAvailable { get; }
    Task ShareAsync(DevicePreparedExport file, CancellationToken ct = default);
}
public sealed class UnavailableDeviceFileShare : IDeviceFileShare
{
    public bool IsAvailable => false;
    public Task ShareAsync(DevicePreparedExport file, CancellationToken ct = default) => throw new NotSupportedException("File sharing is unavailable on this host.");
}
