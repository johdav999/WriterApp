using System;
using System.Threading;
using System.Threading.Tasks;

namespace WriterApp.Client.Services
{
    public sealed class AiCommandStatusService : IDisposable
    {
        private CancellationTokenSource? _clearCts;

        public AiCommandStatusSnapshot Current { get; private set; } = AiCommandStatusSnapshot.Empty;

        public event Action<AiCommandStatusSnapshot>? Changed;

        public void Start(string commandName)
        {
            string label = string.IsNullOrWhiteSpace(commandName) ? "AI" : commandName.Trim();
            CancelPendingClear();
            Current = new AiCommandStatusSnapshot(label, $"{label} in progress...", true);
            Changed?.Invoke(Current);
        }

        public void Complete(string commandName, TimeSpan? clearDelay = null)
        {
            string label = string.IsNullOrWhiteSpace(commandName) ? "AI" : commandName.Trim();
            CancelPendingClear();
            Current = new AiCommandStatusSnapshot(label, $"{label} completed", false);
            Changed?.Invoke(Current);

            _clearCts = new CancellationTokenSource();
            _ = ClearLaterAsync(_clearCts, clearDelay ?? TimeSpan.FromSeconds(4));
        }

        public void Clear()
        {
            CancelPendingClear();
            if (Current == AiCommandStatusSnapshot.Empty)
            {
                return;
            }

            Current = AiCommandStatusSnapshot.Empty;
            Changed?.Invoke(Current);
        }

        public void Dispose()
        {
            CancelPendingClear();
        }

        private async Task ClearLaterAsync(CancellationTokenSource cts, TimeSpan delay)
        {
            try
            {
                await Task.Delay(delay, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            if (!cts.IsCancellationRequested)
            {
                Current = AiCommandStatusSnapshot.Empty;
                Changed?.Invoke(Current);
            }
        }

        private void CancelPendingClear()
        {
            _clearCts?.Cancel();
            _clearCts?.Dispose();
            _clearCts = null;
        }
    }

    public sealed record AiCommandStatusSnapshot(string CommandName, string Message, bool IsInProgress)
    {
        public static AiCommandStatusSnapshot Empty { get; } = new(string.Empty, string.Empty, false);
    }
}
