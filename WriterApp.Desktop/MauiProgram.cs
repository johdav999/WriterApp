using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddSingleton<IDeviceIdentityClient>(_ =>
            new Authentication.MsalDeviceIdentityClient(DeviceAuthOptions.FromEnvironment()));
        builder.Services.AddSingleton<IDeviceFileDialog, WindowsDeviceFileDialog>();
        DeviceEnvironmentConfiguration configuration = DesktopBuildConfiguration.Read();
        builder.Services.AddSingleton(configuration);
        builder.Services.AddSingleton(_ => new DeviceDiagnostics(
            Path.Combine(FileSystem.AppDataDirectory, "diagnostics"), AppInfo.Current.VersionString, configuration.Environment));
        builder.Services.AddSingleton<IDeviceExternalLauncher, WindowsExternalLauncher>();
        builder.Services.AddSingleton<IDeviceUpdateService>(sp => new DeviceUpdateService(
            new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }),
            configuration, AppInfo.Current.VersionString, sp.GetRequiredService<IDeviceExternalLauncher>()));

        Uri apiBaseAddress = configuration.ApiBaseAddress;
        string localDocumentPath = Path.Combine(FileSystem.AppDataDirectory, "documents");

        builder.Services.AddWriterAppDeviceCore(
            new DeviceHostOptions("Windows desktop", apiBaseAddress),
            localDocumentPath);
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
