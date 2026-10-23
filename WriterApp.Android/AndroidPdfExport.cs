using global::Android.Webkit;
using global::Android.Print;
using WriterApp.Device.Shared.Services;
using NativeWebView = global::Android.Webkit.WebView;

namespace WriterApp.Android;

/// <summary>Android's print spooler owns destination selection and vector/text PDF rendering of an immutable offline snapshot.</summary>
public sealed class AndroidPdfExport : IDevicePdfPrint
{
    public bool IsAvailable => true;
    public Task<bool> PrintAsync(string html, double widthMm, double heightMm, CancellationToken ct = default) => MainThread.InvokeOnMainThreadAsync(async () =>
    {
        if (!double.IsFinite(widthMm) || !double.IsFinite(heightMm) || widthMm is < 20 or > 1000 || heightMm is < 20 or > 1000)
            throw new ArgumentException("PDF dimensions must be between 20 and 1000 mm.");
        if (html.Length > 8 * 1024 * 1024) throw new InvalidDataException("PDF preview exceeds 8 MB.");
        ct.ThrowIfCancellationRequested();
        var activity = Platform.CurrentActivity ?? throw new IOException("Open Prosa before exporting PDF.");
        using var view = new NativeWebView(activity);
        view.Settings.JavaScriptEnabled = false;
        view.Settings.AllowFileAccess = false;
        view.Settings.AllowContentAccess = false;
        view.Settings.BlockNetworkLoads = true;
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new OfflineClient(loaded);
        view.SetWebViewClient(client);
        PrintDocumentAdapter? adapter = null;
        PrintJob? job = null;
        try
        {
            view.LoadDataWithBaseURL("https://offline.invalid/", html, "text/html", "UTF-8", null);
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(30), ct);
            adapter = view.CreatePrintDocumentAdapter("Prosa manuscript");
            using var attributes = new PrintAttributes.Builder()
                .SetMediaSize(new PrintAttributes.MediaSize("prosa", "Prosa", (int)Math.Round(widthMm / 25.4 * 1000), (int)Math.Round(heightMm / 25.4 * 1000)))
                .SetResolution(new PrintAttributes.Resolution("pdf", "PDF", 300, 300))
                .SetMinMargins(PrintAttributes.Margins.NoMargins!).SetColorMode(PrintColorMode.Color).Build()!;
            var manager = activity.GetSystemService(global::Android.Content.Context.PrintService) as PrintManager
                ?? throw new IOException("Android print service is unavailable.");
            job = manager.Print("Prosa manuscript", adapter!, attributes) ?? throw new IOException("Android could not start printing.");
            var timeout = System.Diagnostics.Stopwatch.StartNew();
            while (!job.IsCompleted && !job.IsCancelled)
            {
                ct.ThrowIfCancellationRequested();
                if (job.IsFailed || job.IsBlocked) throw new IOException("The Android print job failed or is blocked. Retry in Android printing settings.");
                if (timeout.Elapsed > TimeSpan.FromMinutes(10)) throw new TimeoutException("Android print/export did not complete in ten minutes.");
                await Task.Delay(300, ct);
            }
            return job.IsCompleted;
        }
        finally
        {
            if (job is not null && !job.IsCompleted && !job.IsCancelled) job.Cancel();
            adapter?.Dispose(); view.StopLoading(); view.Destroy(); job?.Dispose();
        }
    });
    private sealed class OfflineClient(TaskCompletionSource loaded) : WebViewClient
    {
        public override void OnPageFinished(NativeWebView? view, string? url) => loaded.TrySetResult();
        public override WebResourceResponse? ShouldInterceptRequest(NativeWebView? view, IWebResourceRequest? request)
        {
            string uri = request?.Url?.ToString() ?? "";
            return uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || uri == "about:blank" ? null
                : new WebResourceResponse("text/plain", "UTF-8", 403, "Offline export", new Dictionary<string, string>(), new MemoryStream());
        }
    }
}
