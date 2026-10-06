using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Services;
using WriterApp.iOS.Authentication;

namespace WriterApp.iOS;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        MauiAppBuilder builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        var identityOptions = IosBuildConfiguration.Read();
        builder.Services.AddSingleton(identityOptions);
        builder.Services.AddSingleton(identityOptions.Environment);
        builder.Services.AddSingleton<IIosMsalSession, IosMsalSession>();
        builder.Services.AddSingleton<IIosIdentitySelectionStore, IosIdentitySelectionStore>();
        builder.Services.AddSingleton<IosDeviceIdentityClient>();
        builder.Services.AddSingleton<IDeviceIdentityClient>(sp => sp.GetRequiredService<IosDeviceIdentityClient>());
        builder.Services.AddSingleton<IosIdentityLifecycle>();
        Uri apiBaseAddress = identityOptions.Environment.ApiBaseAddress;
        string localDocumentPath = Path.Combine(FileSystem.AppDataDirectory, "documents");

        builder.Services.AddWriterAppDeviceCore(
            new DeviceHostOptions("iPhone and iPad", apiBaseAddress),
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
