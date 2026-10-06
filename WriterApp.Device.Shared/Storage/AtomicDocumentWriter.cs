using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("WriterApp.Tests")]

namespace WriterApp.Device.Shared.Storage;

internal sealed class AtomicDocumentWriter(Action<string>? beforeCommit = null)
{
    public async Task WriteAsync(string destination, byte[] bytes, CancellationToken cancellationToken)
    {
        // Same-directory replacement stays on one filesystem. Orphan staging files are never loaded.
        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            beforeCommit?.Invoke(temporary); // Internal fault-injection seam after durable staging.
            cancellationToken.ThrowIfCancellationRequested();
            // Windows readers/indexers can briefly deny replacing a just-opened snapshot.
            // Retry the same durable staging file; never fall back to truncating the destination.
            for (int attempt = 0; ; attempt++) {
                cancellationToken.ThrowIfCancellationRequested();
                try { File.Move(temporary, destination, overwrite: true); break; }
                catch (IOException error) when (OperatingSystem.IsWindows() && attempt < 5
                    && (error.HResult & 0xffff) is 5 or 32 or 33) {
                    await Task.Delay(25 << attempt, cancellationToken);
                }
                catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows() && attempt < 5
                    && File.Exists(destination) && !File.GetAttributes(destination).HasFlag(FileAttributes.ReadOnly)) {
                    await Task.Delay(25 << attempt, cancellationToken);
                }
            }
        }
        finally
        {
            // Cleanup must never mask the save result or the original failure.
            try { File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
