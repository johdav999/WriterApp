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

        Uri apiBaseAddress = DeviceHostOptions.ResolveApiBaseAddress(
            Environment.GetEnvironmentVariable("WRITERAPP_API_BASE_URL"));
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
