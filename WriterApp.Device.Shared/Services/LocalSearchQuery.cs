namespace WriterApp.Device.Shared.Services;

// Generation checks protect against out-of-order completion even if a slow read ignores cancellation.
public sealed class LocalSearchQuery(LocalDocumentSearch search) : IDisposable
{
    private CancellationTokenSource? _pending;
    private long _generation;
    public void Cancel() { Interlocked.Increment(ref _generation); try { _pending?.Cancel(); } catch (ObjectDisposedException) { } }
    public async Task<LocalSearchResults?> RunAsync(string query, TimeSpan delay, Func<Task<bool>>? beforeQuery = null, LocalSearchScope scope = LocalSearchScope.Writing)
    {
        Cancel();
        long generation = _generation;
        using var pending = new CancellationTokenSource(); _pending = pending;
        try
        {
            await Task.Delay(delay, pending.Token);
            if (beforeQuery is not null && !await beforeQuery()) return null;
            pending.Token.ThrowIfCancellationRequested();
            var results = await search.SearchAsync(query, pending.Token, scope);
            return generation == _generation && !pending.IsCancellationRequested ? results : null;
        }
        catch (OperationCanceledException) when (pending.IsCancellationRequested) { return null; }
        finally { if (_pending == pending) _pending = null; }
    }
    public void Dispose() => Cancel();
}
