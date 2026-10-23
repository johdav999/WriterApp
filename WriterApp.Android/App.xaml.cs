namespace WriterApp.Android;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly AndroidHostLifecycle lifecycle;
    public App(AndroidHostLifecycle lifecycle) { this.lifecycle = lifecycle; InitializeComponent(); }
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "Prosa" };
        window.Created += async (_, _) => await lifecycle.ResumeAsync();
        window.Resumed += async (_, _) => await lifecycle.ResumeAsync();
        window.Deactivated += async (_, _) => await lifecycle.SuspendAsync();
        window.Stopped += async (_, _) => await lifecycle.SuspendAsync();
        window.Destroying += (_, _) => lifecycle.Destroy();
        return window;
    }
}
