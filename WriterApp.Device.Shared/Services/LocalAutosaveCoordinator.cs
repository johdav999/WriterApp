using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

/// <summary>Serializes journal/main writes, debounces idle typing and bounds continuous typing.</summary>
public sealed class LocalAutosaveCoordinator : IDisposable
{
    private readonly LocalEditorSession _session;
    private readonly LocalRecoveryStore _recovery;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ITimer _idle;
    private readonly ITimer _maximum;
    private readonly Func<Task> _flush;
    private bool _scheduled;
    private bool _disposed;
    private readonly Guid _recoveryId = Guid.NewGuid();
    public string? Error { get; private set; }
    public event Action? Changed;

    public LocalAutosaveCoordinator(LocalEditorSession session, LocalRecoveryStore recovery,
        Func<Task> flush, TimeProvider? time = null)
    {
        _session = session; _recovery = recovery; _flush = flush;
        time ??= TimeProvider.System;
        _idle = time.CreateTimer(_ => Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _maximum = time.CreateTimer(_ => Tick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    private async void Tick()
    {
        try { if (!_disposed) await _flush(); }
        catch (Exception error) { Error = $"Autosave failed: {error.GetType().Name}. Try Save again."; Changed?.Invoke(); }
    }

    public async Task EditAsync(Guid pageId, string html)
    {
        _session.Edit(pageId, html);
        _idle.Change(TimeSpan.FromSeconds(2), Timeout.InfiniteTimeSpan);
        if (!_scheduled)
        {
            _scheduled = true;
            _maximum.Change(TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan);
        }
        await _gate.WaitAsync();
        try
        {
            await _recovery.WriteAsync(_session.Snapshot(), _recoveryId);
            // Do not clear an earlier save failure until the primary save succeeds.
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { Error = "Recovery could not be written. Keep this window open and retry Save."; }
        finally { _gate.Release(); Changed?.Invoke(); }
    }

    public async Task SaveAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _idle.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _maximum.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _scheduled = false;
            // Do not prevent the primary save if only the journal destination is unavailable.
            if (_session.IsDirty)
            {
                try { await _recovery.WriteAsync(_session.Snapshot(), _recoveryId); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
            await _session.SaveAsync();
            if (_session.IsDirty) await _recovery.WriteAsync(_session.Snapshot(), _recoveryId);
            else
            {
                try { await _recovery.DiscardAsync(_recoveryId); }
                // The primary commit is durable. Discovery ignores a redundant journal.
                catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
            }
            Error = null;
        }
        catch { Error = "Saving failed. Your edits remain here. Check local storage and try Save again."; throw; }
        finally { _gate.Release(); Changed?.Invoke(); }
    }

    public void Dispose() { _disposed = true; _idle.Dispose(); _maximum.Dispose(); }
}
