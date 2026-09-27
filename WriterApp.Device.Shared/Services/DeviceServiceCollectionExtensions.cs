using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public static class DeviceServiceCollectionExtensions
{
    public static IServiceCollection AddWriterAppDeviceCore(
        this IServiceCollection services,
        DeviceHostOptions options,
        string localDocumentPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(localDocumentPath);

        if (!options.ApiBaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("The backend address must be absolute.", nameof(options));
        }

        services.AddSingleton(options);
        services.TryAddSingleton<IDeviceIdentityClient, UnconfiguredDeviceIdentityClient>();
        services.AddSingleton<DeviceAccountService>();
        services.AddScoped(sp => new HttpClient(new DeviceAuthenticatedHandler(sp.GetRequiredService<DeviceAccountService>(), options.ApiBaseAddress)
        { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false } })
        { BaseAddress = options.ApiBaseAddress });
        services.AddSingleton(_ => new FileLocalDocumentStore(localDocumentPath));
        services.AddSingleton<ILocalDocumentStore>(sp => sp.GetRequiredService<FileLocalDocumentStore>());
        services.AddSingleton<LocalDocumentRepository>();
        services.AddSingleton<DeviceConnectivity>();
        services.AddSingleton(_ => new DeviceSyncJournal(Path.Combine(localDocumentPath, "sync")));
        services.AddScoped<IDeviceSyncApi, DeviceSyncApi>();
        services.AddScoped<DeviceSyncEngine>();
        services.AddScoped<IDeviceAiApi, DeviceAiApi>();
        services.AddScoped<DeviceAiService>();
        services.AddSingleton(_ => new DeviceAiUndoStore(Path.Combine(localDocumentPath, "ai-undo")));
        services.AddSingleton(_ => new LocalRecoveryStore(Path.Combine(localDocumentPath, "recovery")));
        services.AddSingleton<LocalRecoveryService>();
        services.AddSingleton<DeviceSaveLifetime>();
        services.AddScoped<LocalDocumentLibrary>();
        return services;
    }
}
