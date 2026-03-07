using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WriterApp.Client.Services
{
    public sealed class EditorSaveCoordinator : IAsyncDisposable
    {
        // Canonical server saves are intentionally slower to reduce SQL churn.
        private static readonly TimeSpan AutosaveDebounce = TimeSpan.FromSeconds(10);
        // Continuous typing still forces a real save on a longer cadence.
        private static readonly TimeSpan AutosaveMaxInterval = TimeSpan.FromSeconds(60);
        // Recovery drafts stay faster than server autosave so crash recovery remains responsive.
        private static readonly TimeSpan RecoveryDraftDebounce = TimeSpan.FromSeconds(3);
        private readonly RecoveryDraftService _recoveryDrafts;
        private readonly ILogger<EditorSaveCoordinator> _logger;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private CancellationTokenSource? _autosaveDebounceCts;
        private CancellationTokenSource? _autosaveMaxIntervalCts;
        private CancellationTokenSource? _recoveryDraftCts;
        private RecoveryDraftKey? _draftKey;
        private Func<string, CancellationToken, Task<EditorSaveResult>>? _saveAsync;
        private string _latestContent = string.Empty;
        private string _lastPersistedHash = "0";
        private int _sessionVersion;
        private bool _disposed;

        public EditorSaveCoordinator(
            RecoveryDraftService recoveryDrafts,
            ILogger<EditorSaveCoordinator> logger)
        {
            _recoveryDrafts = recoveryDrafts;
            _logger = logger;
        }

        public event Func<Task>? StateChanged;

        public EditorSaveState State { get; private set; } = EditorSaveState.Idle;

        public bool IsDirty { get; private set; }

        public void StartSession(
            RecoveryDraftKey draftKey,
            string persistedContent,
            Func<string, CancellationToken, Task<EditorSaveResult>> saveAsync)
        {
            ThrowIfDisposed();

            CancelPendingAutosave();
            CancelPendingRecoveryDraft();
            _draftKey = draftKey;
            _saveAsync = saveAsync;
            _latestContent = persistedContent ?? string.Empty;
            _lastPersistedHash = ComputeHash(_latestContent);
            _sessionVersion++;
            IsDirty = false;
            State = EditorSaveState.Idle;
            _ = NotifyStateChangedAsync();
        }

        public async Task<RecoveryDraft?> GetRestoreCandidateAsync(
            string persistedContent,
            DateTimeOffset persistedUpdatedAt)
        {
            if (_draftKey is null)
            {
                return null;
            }

            return await _recoveryDrafts.GetRestoreCandidateAsync(_draftKey, persistedContent, persistedUpdatedAt);
        }

        public async Task NotifyContentChangedAsync(string content)
        {
            ThrowIfDisposed();

            if (_draftKey is null)
            {
                return;
            }

            _latestContent = content ?? string.Empty;
            IsDirty = true;
            State = EditorSaveState.Idle;
            ScheduleRecoveryDraft(_latestContent, _sessionVersion);
            ScheduleAutosave(_sessionVersion);
            await NotifyStateChangedAsync();
        }

        public async Task MarkRecoveryDraftRestoredAsync(string content)
        {
            ThrowIfDisposed();

            if (_draftKey is null)
            {
                return;
            }

            _latestContent = content ?? string.Empty;
            IsDirty = true;
            State = EditorSaveState.Idle;
            await _recoveryDrafts.SaveAsync(_draftKey, _latestContent);
            await NotifyStateChangedAsync();
        }

        public async Task DiscardRecoveryDraftAsync()
        {
            if (_draftKey is null)
            {
                return;
            }

            await _recoveryDrafts.ClearAsync(_draftKey);
        }

        public async Task<EditorSaveResult?> SaveNowAsync(string content, bool force = false)
        {
            ThrowIfDisposed();

            _latestContent = content ?? string.Empty;
            CancelPendingAutosave();
            return await SaveCoreAsync(_sessionVersion, force, CancellationToken.None);
        }

        private void ScheduleAutosave(int sessionVersion)
        {
            _autosaveDebounceCts?.Cancel();
            _autosaveDebounceCts?.Dispose();
            CancellationTokenSource debounceCts = new();
            _autosaveDebounceCts = debounceCts;
            _ = DebouncedSaveAsync(sessionVersion, debounceCts);

            if (_autosaveMaxIntervalCts is null)
            {
                CancellationTokenSource maxIntervalCts = new();
                _autosaveMaxIntervalCts = maxIntervalCts;
                _ = MaxIntervalSaveAsync(sessionVersion, maxIntervalCts);
            }
        }

        private void ScheduleRecoveryDraft(string content, int sessionVersion)
        {
            _recoveryDraftCts?.Cancel();
            _recoveryDraftCts?.Dispose();
            CancellationTokenSource cts = new();
            _recoveryDraftCts = cts;
            _ = DebouncedRecoveryDraftAsync(content, sessionVersion, cts);
        }

        private async Task DebouncedSaveAsync(int sessionVersion, CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(AutosaveDebounce, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (cts.IsCancellationRequested || sessionVersion != _sessionVersion)
            {
                return;
            }

            await SaveCoreAsync(sessionVersion, force: false, cts.Token);
        }

        private async Task MaxIntervalSaveAsync(int sessionVersion, CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(AutosaveMaxInterval, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (cts.IsCancellationRequested || sessionVersion != _sessionVersion)
            {
                return;
            }

            await SaveCoreAsync(sessionVersion, force: true, cts.Token);
        }

        private async Task DebouncedRecoveryDraftAsync(string content, int sessionVersion, CancellationTokenSource cts)
        {
            try
            {
                await Task.Delay(RecoveryDraftDebounce, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (cts.IsCancellationRequested || sessionVersion != _sessionVersion || _draftKey is null)
            {
                return;
            }

            await _recoveryDrafts.SaveAsync(_draftKey, content);
        }

        private async Task<EditorSaveResult?> SaveCoreAsync(
            int sessionVersion,
            bool force,
            CancellationToken ct)
        {
            if (!force && !IsDirty)
            {
                return null;
            }

            if (_draftKey is null || _saveAsync is null || sessionVersion != _sessionVersion)
            {
                return null;
            }

            string content = _latestContent ?? string.Empty;
            string contentHash = ComputeHash(content);
            if (!force && string.Equals(contentHash, _lastPersistedHash, StringComparison.Ordinal))
            {
                IsDirty = false;
                CancelPendingAutosave();
                CancelPendingRecoveryDraft();
                await _recoveryDrafts.ClearAsync(_draftKey);
                State = EditorSaveState.Idle;
                await NotifyStateChangedAsync();
                return new EditorSaveResult(true, content, DateTimeOffset.UtcNow);
            }

            await _saveLock.WaitAsync(ct);
            try
            {
                if (_draftKey is null || _saveAsync is null || sessionVersion != _sessionVersion)
                {
                    return null;
                }

                State = EditorSaveState.Saving;
                await NotifyStateChangedAsync();

                EditorSaveResult result = await _saveAsync(content, ct);
                if (sessionVersion != _sessionVersion)
                {
                    return result;
                }

                if (result.Succeeded)
                {
                    IsDirty = false;
                    State = EditorSaveState.Idle;
                    _lastPersistedHash = ComputeHash(result.PersistedContent);
                    CancelPendingAutosave();
                    CancelPendingRecoveryDraft();
                    await _recoveryDrafts.ClearAsync(_draftKey);
                }
                else
                {
                    IsDirty = true;
                    State = EditorSaveState.Failed;
                    await _recoveryDrafts.SaveAsync(_draftKey, content);
                }

                await NotifyStateChangedAsync();
                return result;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Editor save coordination failed.");
                if (_draftKey is not null)
                {
                    await _recoveryDrafts.SaveAsync(_draftKey, content);
                }

                IsDirty = true;
                State = EditorSaveState.Failed;
                await NotifyStateChangedAsync();
                return EditorSaveResult.Failed(ex.Message);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        private void CancelPendingAutosave()
        {
            _autosaveDebounceCts?.Cancel();
            _autosaveDebounceCts?.Dispose();
            _autosaveDebounceCts = null;

            _autosaveMaxIntervalCts?.Cancel();
            _autosaveMaxIntervalCts?.Dispose();
            _autosaveMaxIntervalCts = null;
        }

        private void CancelPendingRecoveryDraft()
        {
            _recoveryDraftCts?.Cancel();
            _recoveryDraftCts?.Dispose();
            _recoveryDraftCts = null;
        }

        private async Task NotifyStateChangedAsync()
        {
            if (StateChanged is null)
            {
                return;
            }

            foreach (Func<Task> handler in StateChanged.GetInvocationList().Cast<Func<Task>>())
            {
                await handler();
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }

            _disposed = true;
            CancelPendingAutosave();
            CancelPendingRecoveryDraft();
            _saveLock.Dispose();
            return ValueTask.CompletedTask;
        }

        private static string ComputeHash(string? content)
        {
            if (string.IsNullOrEmpty(content))
            {
                return "0";
            }

            byte[] bytes = Encoding.UTF8.GetBytes(content);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash.AsSpan(0, 8));
        }
    }

    public enum EditorSaveState
    {
        Idle,
        Saving,
        Failed
    }

    public sealed record EditorSaveResult(
        bool Succeeded,
        string PersistedContent,
        DateTimeOffset PersistedAt,
        string? FailureReason = null)
    {
        public static EditorSaveResult Failed(string? reason = null)
        {
            return new(false, string.Empty, DateTimeOffset.MinValue, reason);
        }
    }
}
