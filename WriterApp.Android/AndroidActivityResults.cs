using global::Android.App;
using global::Android.Content;

namespace WriterApp.Android;

/// <summary>Unique request codes prevent cancelled/late picker results from completing a newer operation.</summary>
internal static class AndroidActivityResults
{
    private static readonly Dictionary<int, TaskCompletionSource<global::Android.Net.Uri?>> pending = [];
    private static int sequence = 0x5000;
    public static async Task<global::Android.Net.Uri?> LaunchAsync(Intent intent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<global::Android.Net.Uri?>(TaskCreationOptions.RunContinuationsAsynchronously);
        int code;
        lock (pending)
        {
            if (sequence == 0x7fff) throw new InvalidOperationException("Restart Prosa before opening another file dialog.");
            code = ++sequence;
            pending.Add(code, completion);
        }
        using var registration = ct.Register(() => completion.TrySetCanceled(ct));
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ct.ThrowIfCancellationRequested();
                var activity = Platform.CurrentActivity ?? throw new IOException("Open Prosa before using a file dialog.");
#pragma warning disable CS0618
                activity.StartActivityForResult(intent, code);
#pragma warning restore CS0618
            });
            return await completion.Task;
        }
        finally { lock (pending) pending.Remove(code); }
    }
    public static bool Complete(int code, Result result, Intent? intent)
    {
        if (code is < 0x5001 or > 0x7fff) return false;
        TaskCompletionSource<global::Android.Net.Uri?>? completion;
        lock (pending) pending.TryGetValue(code, out completion);
        completion?.TrySetResult(result == Result.Ok ? intent?.Data : null);
        return true;
    }
}
