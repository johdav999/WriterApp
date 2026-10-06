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
        services.AddScoped<DeviceAccountOverview>();
        services.TryAddSingleton<IDeviceExternalLauncher, UnavailableExternalLauncher>();
        services.AddScoped<DeviceAccountLinks>();
        services.AddScoped(sp => new HttpClient(new DeviceAuthenticatedHandler(sp.GetRequiredService<DeviceAccountService>(), options.ApiBaseAddress)
        { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false } })
        { BaseAddress = options.ApiBaseAddress });
        services.AddSingleton(_ => new FileLocalDocumentStore(localDocumentPath));
        services.AddSingleton<ILocalDocumentStore>(sp => sp.GetRequiredService<FileLocalDocumentStore>());
        services.AddSingleton<LocalDocumentRepository>();
        services.AddSingleton(_ => new LocalOnboardingStore(Path.Combine(localDocumentPath, "ai-onboarding")));
        services.AddScoped<DeviceOnboarding>();
        services.AddSingleton<LocalDocumentSearch>();
        services.TryAddSingleton<IDeviceFileDialog, UnavailableDeviceFileDialog>();
        services.TryAddSingleton<IDeviceUpdateService, UnavailableDeviceUpdateService>();
        services.TryAddSingleton(_ => new DeviceDiagnostics(Path.Combine(localDocumentPath, "diagnostics"),
            "unconfigured", DeviceEnvironment.Development));
        services.AddScoped<DeviceDocumentTransfer>();
        services.TryAddSingleton<IDevicePdfExport, UnavailableDevicePdfExport>();
        services.AddSingleton(_ => new LocalPublishingStore(Path.Combine(localDocumentPath,"publishing")));
        services.AddSingleton(_ => new LocalCoverStudioStore(Path.Combine(localDocumentPath,"cover-studio")));
        services.AddKeyedScoped<HttpClient>("cover-studio", (sp, _) => new HttpClient(new DeviceAuthenticatedHandler(sp.GetRequiredService<DeviceAccountService>(), options.ApiBaseAddress)
        { InnerHandler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false } })
        { BaseAddress = options.ApiBaseAddress, Timeout = TimeSpan.FromMinutes(3) });
        services.AddScoped(sp => new DeviceCoverStudio(sp.GetRequiredKeyedService<HttpClient>("cover-studio"),
            sp.GetRequiredService<DeviceAccountService>(), sp.GetRequiredService<DeviceConnectivity>(), options,
            sp.GetRequiredService<LocalDocumentRepository>(), sp.GetRequiredService<LocalCoverStudioStore>()));
        services.AddSingleton<DeviceConnectivity>();
        services.AddSingleton(_ => new DeviceSyncJournal(Path.Combine(localDocumentPath, "sync")));
        services.AddScoped<IDeviceSyncApi, DeviceSyncApi>();
        services.AddScoped<DeviceSyncEngine>();
        services.AddScoped<IDeviceAiApi, DeviceAiApi>();
        services.AddScoped<DeviceAiService>();
        services.AddScoped<LocalStoryboardData>();
        services.AddScoped<WriterApp.UI.Shared.Projects.IStoryboardData>(sp => sp.GetRequiredService<LocalStoryboardData>());
        services.AddSingleton(_ => new DeviceAiUndoStore(Path.Combine(localDocumentPath, "ai-undo")));
        services.AddSingleton(_ => new LocalAiStore(Path.Combine(localDocumentPath, "ai-library")));
        services.AddSingleton<LocalAiHistoryActions>();
        services.AddScoped<DeviceAiHistoryService>();
        services.AddScoped<LocalQualityActions>();
        services.AddSingleton(_ => new LocalGlossaryStore(Path.Combine(localDocumentPath, "glossary-cache")));
        services.AddScoped<DeviceGlossaryApi>();
        services.AddScoped<DeviceGlossaryService>();
        services.AddSingleton(_ => new LocalQualityDismissalStore(Path.Combine(localDocumentPath, "quality-decisions")));
        services.AddScoped<DeviceQualityDismissalApi>();
        services.AddScoped<DeviceQualityDismissals>();
        services.AddScoped<LocalTranslationActions>();
        services.AddScoped<LocalWritingActions>();
        services.AddScoped<DevicePromptLibrary>();
        services.AddSingleton(_ => new LocalBibleStore(Path.Combine(localDocumentPath, "canon-cache")));
        services.AddScoped<DeviceBibleApi>();
        services.AddScoped<DeviceBibleService>();
        services.AddSingleton(_ => new LocalRecoveryStore(Path.Combine(localDocumentPath, "recovery")));
        services.AddSingleton<LocalRecoveryService>();
        services.AddSingleton<DeviceSaveLifetime>();
        services.AddScoped<LocalDocumentLibrary>();
        return services;
    }
}
