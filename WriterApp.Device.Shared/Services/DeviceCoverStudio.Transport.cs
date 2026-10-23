namespace WriterApp.Device.Shared.Services;

public sealed partial class DeviceCoverStudio
{
    private const string ConnectionFailure = "The connection to the cover service was interrupted. Check your connection and that the backend is running, then try again. Your saved cover and cached concepts are preserved.";

    // Only wrap transport work. Validation, HTTP status and local storage errors retain
    // their own messages. Never retry a generation POST: it may already be chargeable.
    private static async Task<T> TransportAsync<T>(Func<Task<T>> action)
    {
        try { return await action(); }
        catch (Exception error) when (error is HttpRequestException or IOException or System.Net.WebException)
        {
            throw new CoverConnectionException(error);
        }
    }

    private sealed class CoverConnectionException(Exception inner) : IOException(ConnectionFailure, inner);
}
