namespace WriterApp.Desktop;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly WriterApp.Device.Shared.Services.DeviceSaveLifetime _saves;

    public App(WriterApp.Device.Shared.Services.DeviceSaveLifetime saves)
    {
        _saves = saves;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "Prosa" };
        window.Deactivated += async (_, _) =>
        {
            try { await _saves.FlushAsync(); }
            catch (Exception error) { System.Diagnostics.Debug.WriteLine($"Deactivation save failed: {error.GetType().Name}"); }
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
                { System.Diagnostics.Debug.WriteLine($"Close save failed: {error.GetType().Name}"); }
                finally { closing = false; }
            };
        };
        return window;
    }
}
