using WriterApp.Device.Shared.Services;
using Windows.Storage.Pickers;

namespace WriterApp.Desktop;

/// <summary>Windows pickers own file selection and the save picker's overwrite confirmation.</summary>
public sealed class WindowsDeviceFileDialog : IDeviceFileDialog
{
    public bool IsAvailable => true;

    public async Task<DeviceImportFile?> PickImportAsync(CancellationToken cancellationToken = default)
        => await PickAsync(false, cancellationToken);

    public Task<DeviceImportFile?> PickCoverAsync(CancellationToken cancellationToken = default) => PickAsync(true, cancellationToken);

    private static async Task<DeviceImportFile?> PickAsync(bool cover, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileOpenPicker picker = new();
        foreach (string extension in cover ? new[] { ".png" } : new[] { ".txt", ".html", ".htm", ".docx" }) picker.FileTypeFilter.Add(extension);
        picker.ViewMode = PickerViewMode.List;
        Initialize(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var properties = await file.GetBasicPropertiesAsync();
        if (properties.Size > DeviceDocumentFormats.MaxImportBytes)
            throw new InvalidDataException("The import file must be at most 5 MB.");
        await using Stream source = await file.OpenStreamForReadAsync();
        using MemoryStream buffer = new();
        byte[] chunk = new byte[64 * 1024];
        int count;
        while ((count = await source.ReadAsync(chunk, cancellationToken)) != 0)
        {
            if (buffer.Length + count > DeviceDocumentFormats.MaxImportBytes)
                throw new InvalidDataException("The import file must be at most 5 MB.");
            buffer.Write(chunk, 0, count);
        }
        return new DeviceImportFile(file.Name, buffer.ToArray());
    }

    public async Task<bool> SaveAsync(string suggestedFileName, string extension, byte[] content,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FileSavePicker picker = new()
        {
            SuggestedFileName = Path.GetFileNameWithoutExtension(suggestedFileName)
        };
        picker.FileTypeChoices.Add("Document (" + extension + ")", [extension]);
        Initialize(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return false;
        cancellationToken.ThrowIfCancellationRequested();
        using var transaction = await file.OpenTransactedWriteAsync();
        using Stream destination = transaction.Stream.AsStreamForWrite();
        destination.SetLength(0);
        await destination.WriteAsync(content, cancellationToken);
        await destination.FlushAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await transaction.CommitAsync();
        return true;
    }

    private static void Initialize(object picker)
    {
        var window = Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView
            as Microsoft.UI.Xaml.Window ?? throw new InvalidOperationException("The Windows file picker needs an open app window.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
    }
}
