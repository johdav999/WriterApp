using System.Reflection;
using System.Text.Json;
using WriterApp.Device.Shared.Services;
using WriterApp.iOS.Authentication;

namespace WriterApp.iOS;

internal static class IosBuildConfiguration
{
#if IOS
    public static IosIdentityOptions Read()
    {
        var assembly = typeof(IosBuildConfiguration).Assembly;
        using var settings = assembly.GetManifestResourceStream("Prosa.IosNativeAuthentication")
            ?? throw new InvalidOperationException("iOS public authentication settings are missing.");
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(a => a.Key, a => a.Value);
        return ReadSettings(settings, metadata,
#if DEBUG
            true,
#else
            false,
#endif
            System.Environment.GetEnvironmentVariable, AppInfo.Current.PackageName);
    }
#endif
    internal static IosIdentityOptions ReadSettings(Stream settings, IReadOnlyDictionary<string,string?> metadata,
        bool allowDevelopmentOverrides, Func<string,string?> readSetting, string bundleId)
    {
        if (bundleId != IosIdentityOptions.BundleId) throw new InvalidOperationException("iOS bundle, callback and Keychain group must be updated together before changing the app ID.");
        var environment = DeviceEnvironmentConfiguration.Create(metadata.GetValueOrDefault("ProsaEnvironment") ?? "",
            metadata.GetValueOrDefault("ProsaApiBaseUrl") ?? "", null);
        DeviceAuthOptions defaults;
        try { defaults = JsonSerializer.Deserialize<DeviceAuthOptions>(settings) ?? new(); }
        catch (JsonException) { defaults = new(); } // Malformed public auth settings disable sign-in, not offline writing.
        var auth = allowDevelopmentOverrides && environment.Environment == DeviceEnvironment.Development
            ? DeviceAuthOptions.FromSettings(defaults, readSetting) : defaults;
        return new() { Authentication = auth, Environment = environment };
    }
}
