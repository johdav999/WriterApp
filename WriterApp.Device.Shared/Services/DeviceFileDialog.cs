namespace WriterApp.Device.Shared.Services;

public sealed record DeviceImportFile(string FileName, byte[] Content);

/// <summary>Platform-owned open/save dialogs. A null result means the user cancelled.</summary>
public interface IDeviceFileDialog
{
    bool IsAvailable { get; }
    Task<DeviceImportFile?> PickImportAsync(CancellationToken cancellationToken = default);
    Task<bool> SaveAsync(string suggestedFileName, string extension, byte[] content,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableDeviceFileDialog : IDeviceFileDialog
{
    public bool IsAvailable => false;
    public Task<DeviceImportFile?> PickImportAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Native file dialogs are not configured for this device.");
    public Task<bool> SaveAsync(string suggestedFileName, string extension, byte[] content,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Native file dialogs are not configured for this device.");
}
