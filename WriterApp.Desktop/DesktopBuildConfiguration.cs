using System.Reflection;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Desktop;

internal static class DesktopBuildConfiguration
{
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
