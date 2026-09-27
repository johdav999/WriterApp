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

        return builder.Build();
    }
}
