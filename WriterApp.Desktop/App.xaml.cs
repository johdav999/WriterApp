namespace WriterApp.Desktop;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly WriterApp.Device.Shared.Services.DeviceSaveLifetime _saves;
    private readonly WriterApp.Device.Shared.Services.DeviceDiagnostics _diagnostics;

    public App(WriterApp.Device.Shared.Services.DeviceSaveLifetime saves,
        WriterApp.Device.Shared.Services.DeviceDiagnostics diagnostics)
    {
        _saves = saves;
        _diagnostics = diagnostics;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "Prosa" };
        window.Deactivated += async (_, _) =>
        {
            try { await _saves.FlushAsync(); }
            catch (Exception error) { await RecordSaveFailureAsync(error); }
        };
        window.Created += (_, _) =>
        {
            if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native) return;
            bool allowClose = false;
            bool closing = false;
            native.AppWindow.Closing += async (_, args) =>
            {
                if (allowClose) return;
                args.Cancel = true; // Keep the WebView alive while its last JS snapshot is flushed.
                if (closing) return;
                closing = true;
                try
                {
                    if (await _saves.FlushAsync(closing: true))
                    { allowClose = true; native.Close(); }
                }
                catch (Exception error)
                { await RecordSaveFailureAsync(error); }
                finally { closing = false; }
            };
        };
        return window;
    }

    private async Task RecordSaveFailureAsync(Exception error)
    {
        try { await _diagnostics.RecordAsync(WriterApp.Device.Shared.Services.DeviceDiagnosticEvent.SaveFailed, error: error); }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException) { }
    }
}
