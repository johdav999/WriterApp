using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped(_ => new HttpClient { BaseAddress = options.ApiBaseAddress });
        services.AddSingleton<ILocalDocumentStore>(_ => new FileLocalDocumentStore(localDocumentPath));
        services.AddSingleton<LocalDocumentRepository>();
        services.AddScoped<LocalDocumentLibrary>();
        return services;
    }
}
