using global::Android.App;
using global::Android.Content;
using global::Android.Content.PM;
using global::Android.OS;
using global::Android.Views;
using Microsoft.Identity.Client;

namespace WriterApp.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, Exported = true,
    LaunchMode = LaunchMode.SingleTop, WindowSoftInputMode = SoftInput.AdjustResize,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
    private BackCallback? _back;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        _back = new BackCallback(this);
        OnBackPressedDispatcher.AddCallback(this, _back);
    }

    // MAUI's legacy Back route can consume WebView history before AndroidX
    // dispatches our callback. Use the same keyboard/panel/save ordering there.
    public override void OnBackPressed() => _back?.HandleOnBackPressed();

    public override bool DispatchKeyEvent(KeyEvent? keyEvent)
    {
        // A focused WebView consumes hardware/three-button Back as browser
        // history before OnBackPressed. Route it through our same callback.
        if (keyEvent?.KeyCode == Keycode.Back && _back is not null)
        {
            if (keyEvent.Action == KeyEventActions.Up && !keyEvent.IsCanceled)
                _back.HandleOnBackPressed();
            return true;
        }
        return base.DispatchKeyEvent(keyEvent);
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        if (!AndroidActivityResults.Complete(requestCode, resultCode, data))
            AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(requestCode, resultCode, data);
    }

    private sealed class BackCallback(MainActivity activity) : AndroidX.Activity.OnBackPressedCallback(true)
    {
        private bool busy;
        public override async void HandleOnBackPressed()
        {
            if (busy) return;
            busy = true;
            try
            {
                var view = activity.Window!.DecorView;
                var insets = AndroidX.Core.View.ViewCompat.GetRootWindowInsets(view);
                if (insets?.IsVisible(AndroidX.Core.View.WindowInsetsCompat.Type.Ime()) == true)
                {
                    AndroidX.Core.View.WindowCompat.GetInsetsController(activity.Window!, view)?.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.Ime());
                    return;
                }
                var services = IPlatformApplication.Current!.Services;
                if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is MainPage currentPage
                    && await currentPage.ClosePanelAsync()) return;
                if (!await services.GetRequiredService<AndroidHostLifecycle>().FlushAsync()) return;
                if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is MainPage page
                    && await page.BackAsync()) return;
                activity.MoveTaskToBack(true);
            }
            catch (Exception error) { await IPlatformApplication.Current!.Services.GetRequiredService<AndroidHostLifecycle>().RecordFailureAsync(error); }
            finally { busy = false; }
        }
    }
}
