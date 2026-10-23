using global::Android.App;
using global::Android.Runtime;

namespace WriterApp.Android;

[Application]
public sealed class MainApplication(nint handle, JniHandleOwnership ownership) : MauiApplication(handle, ownership)
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
