using System.Reflection;
using System.Text.Json;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Desktop;

internal static class DesktopBuildConfiguration
{
    public static DeviceAuthOptions ReadAuth()
    {
        using Stream settings = typeof(DesktopBuildConfiguration).Assembly
            .GetManifestResourceStream("Prosa.NativeAuthentication")
            ?? throw new InvalidOperationException("Desktop authentication settings are missing from this build.");
        var defaults = JsonSerializer.Deserialize<DeviceAuthOptions>(settings)
            ?? throw new InvalidOperationException("Desktop authentication settings are invalid.");
        return DeviceAuthOptions.FromEnvironment(defaults);
    }

    public static string DataDirectory(string defaultDirectory, DeviceEnvironment environment)
    {
        string? validationDirectory = typeof(DesktopBuildConfiguration).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .SingleOrDefault(item => item.Key == "ProsaValidationDataDirectory")?.Value;
        if (string.IsNullOrWhiteSpace(validationDirectory)) return defaultDirectory;
        if (environment != DeviceEnvironment.Development || !Path.IsPathFullyQualified(validationDirectory))
            throw new InvalidOperationException("Validation storage requires a Development build and an absolute directory.");
        return Path.GetFullPath(validationDirectory);
    }

    public static DeviceEnvironmentConfiguration Read()
    {
        var metadata = typeof(DesktopBuildConfiguration).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        string environment = Required(metadata, "ProsaEnvironment");
        string api = Required(metadata, "ProsaApiBaseUrl");
        string? manifest = metadata.GetValueOrDefault("ProsaUpdateManifestUrl");
#if DEBUG
        string? localOverride = Environment.GetEnvironmentVariable("WRITERAPP_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(localOverride)) api = localOverride;
#endif
        return DeviceEnvironmentConfiguration.Create(environment, api, manifest);
    }

    private static string Required(Dictionary<string, string?> metadata, string key) =>
        metadata.GetValueOrDefault(key) is { Length: > 0 } value ? value
            : throw new InvalidOperationException($"Desktop build metadata {key} is missing.");
}
