using Microsoft.Extensions.Logging;
using WriterApp.Android.Authentication;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Brainstorming;

namespace WriterApp.Android;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder().UseMauiApp<App>();
        var options = AndroidBuildConfiguration.Read();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(options.Environment);
        builder.Services.AddSingleton<IAndroidMsalSession, AndroidMsalSession>();
        builder.Services.AddSingleton<IAndroidIdentitySelectionStore, AndroidIdentitySelectionStore>();
        builder.Services.AddSingleton<AndroidDeviceIdentityClient>();
        builder.Services.AddSingleton<IDeviceIdentityClient>(sp => sp.GetRequiredService<AndroidDeviceIdentityClient>());
        builder.Services.AddSingleton<IDeviceFileDialog, AndroidDeviceFileDialog>();
        builder.Services.AddSingleton<IDeviceFileShare>(sp => (AndroidDeviceFileDialog)sp.GetRequiredService<IDeviceFileDialog>());
        builder.Services.AddSingleton<IDevicePdfPrint, AndroidPdfExport>();
        builder.Services.AddSingleton<IDeviceExternalLauncher, AndroidExternalLauncher>();
        string root = Path.Combine(FileSystem.AppDataDirectory, options.Environment.Environment.ToString().ToLowerInvariant());
        builder.Services.AddSingleton(_ => new DeviceDiagnostics(Path.Combine(root, "diagnostics"), AppInfo.Current.VersionString, options.Environment.Environment));
        builder.Services.AddWriterAppDeviceCore(new DeviceHostOptions("Android phone and tablet", options.Environment.ApiBaseAddress), Path.Combine(root, "documents"));
        builder.Services.AddSingleton<IBrainstormingMicrophone, AndroidBrainstormingMicrophone>();
        builder.Services.AddSingleton<IBrainstormingSpeechOutput, AndroidBrainstormingSpeechOutput>();
        builder.Services.AddSingleton<BrainstormingVoiceCoordinator>();
        builder.Services.AddSingleton<AndroidHostLifecycle>();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif
        var app = builder.Build();
        var connectivity = app.Services.GetRequiredService<DeviceConnectivity>();
        connectivity.SetOnline(Connectivity.Current.NetworkAccess == NetworkAccess.Internet);
        Connectivity.Current.ConnectivityChanged += (_, args) => connectivity.SetOnline(args.NetworkAccess == NetworkAccess.Internet);
        return app;
    }
}
