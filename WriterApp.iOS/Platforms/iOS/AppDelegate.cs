using Foundation;
using UIKit;
using WriterApp.iOS.Authentication;

namespace WriterApp.iOS;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        if (!Uri.TryCreate(url.AbsoluteString, UriKind.Absolute, out var callback)) return false;
        return IPlatformApplication.Current?.Services.GetService<IosDeviceIdentityClient>()?.HandleCallback(callback) == true;
    }
}
