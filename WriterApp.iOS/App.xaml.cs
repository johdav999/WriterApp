namespace WriterApp.iOS;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly Authentication.IosIdentityLifecycle _identityLifecycle;
    public App(Authentication.IosIdentityLifecycle identityLifecycle)
    {
        _identityLifecycle = identityLifecycle;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage()) { Title = "Prosa" };
        window.Created += async (_, _) => await _identityLifecycle.ResumeAsync();
        window.Resumed += async (_, _) => await _identityLifecycle.ResumeAsync();
        window.Stopped += (_, _) => _identityLifecycle.Background();
        window.Destroying += (_, _) => _identityLifecycle.Destroy();
        return window;
    }
}
