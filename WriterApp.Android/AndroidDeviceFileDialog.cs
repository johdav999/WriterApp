using global::Android.Content;
using global::Android.Provider;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Android;

public sealed class AndroidDeviceFileDialog : IDeviceFileDialog, IDeviceFileShare
{
    public bool IsAvailable => true;
    public Task<DeviceImportFile?> PickImportAsync(CancellationToken cancellationToken = default) => PickAsync(false, cancellationToken);
    public Task<DeviceImportFile?> PickCoverAsync(CancellationToken cancellationToken = default) => PickAsync(true, cancellationToken);

    private static async Task<DeviceImportFile?> PickAsync(bool cover, CancellationToken ct)
    {
        using var intent = new Intent(Intent.ActionOpenDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(cover ? "image/png" : "*/*");
        if (!cover) intent.PutExtra(Intent.ExtraMimeTypes, new[] { "text/plain", "text/html", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" });
        var uri = await AndroidActivityResults.LaunchAsync(intent, ct);
        if (uri is null) return null;
        var resolver = global::Android.App.Application.Context.ContentResolver!;
        string? name = null;
        using (var cursor = resolver.Query(uri, [IOpenableColumns.DisplayName], null, null, null))
            if (cursor?.MoveToFirst() == true) name = cursor.GetString(0);
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("The file provider did not supply a file name.");
        string extension = Path.GetExtension(name).ToLowerInvariant();
        if (!(cover ? extension == ".png" : extension is ".txt" or ".html" or ".htm" or ".docx"))
            throw new InvalidDataException(cover ? "Choose a PNG cover." : "Choose TXT, HTML or DOCX.");
        using var source = resolver.OpenInputStream(uri) ?? throw new IOException("The selected file could not be opened.");
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[64 * 1024];
        int count;
        while ((count = await source.ReadAsync(chunk, ct)) != 0)
        {
            if (buffer.Length + count > DeviceDocumentFormats.MaxImportBytes) throw new InvalidDataException("The import file must be at most 5 MB.");
            buffer.Write(chunk, 0, count);
        }
        return new(Path.GetFileName(name), buffer.ToArray());
    }

    public async Task<bool> SaveAsync(string suggestedFileName, string extension, byte[] content, CancellationToken cancellationToken = default)
    {
        using var intent = new Intent(Intent.ActionCreateDocument);
        intent.AddCategory(Intent.CategoryOpenable);
        intent.SetType(Mime(extension));
        intent.PutExtra(Intent.ExtraTitle, Path.GetFileName(suggestedFileName));
        var uri = await AndroidActivityResults.LaunchAsync(intent, cancellationToken);
        if (uri is null) return false;
        await using var output = global::Android.App.Application.Context.ContentResolver!.OpenOutputStream(uri, "wt")
            ?? throw new IOException("The selected destination could not be opened.");
        await output.WriteAsync(content, cancellationToken);
        await output.FlushAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    public async Task ShareAsync(DevicePreparedExport file, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        string root = Path.Combine(FileSystem.CacheDirectory, "shared-exports");
        Directory.CreateDirectory(root);
        // Each request owns its directory; a later export cannot overwrite a recipient's pending file.
        foreach (var old in Directory.EnumerateDirectories(root))
            if (Directory.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddDays(-7))
                try { Directory.Delete(old, true); } catch (IOException) { }
        string folder = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, Path.GetFileName(file.SuggestedFileName));
        await File.WriteAllBytesAsync(path, file.Content, ct);
        await MainThread.InvokeOnMainThreadAsync(() => Share.Default.RequestAsync(new ShareFileRequest("Share Prosa export", new ShareFile(path, Mime(file.Extension)))));
    }

    private static string Mime(string extension) => extension.ToLowerInvariant() switch
    {
        ".txt" => "text/plain", ".html" or ".htm" => "text/html", ".md" => "text/markdown",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".epub" => "application/epub+zip", ".pdf" => "application/pdf", ".json" => "application/json", ".png" => "image/png",
        _ => throw new ArgumentException("Unsupported export extension.", nameof(extension))
    };
}
