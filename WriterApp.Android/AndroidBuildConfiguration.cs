using System.Reflection;
using System.Text.Json;
using WriterApp.Device.Shared.Services;
using WriterApp.Android.Authentication;

namespace WriterApp.Android;

internal static class AndroidBuildConfiguration
{
#if ANDROID
    public static AndroidIdentityOptions Read()
    {
        var assembly = typeof(AndroidBuildConfiguration).Assembly;
        using var settings = assembly.GetManifestResourceStream("Prosa.AndroidNativeAuthentication")
            ?? throw new InvalidOperationException("Android public authentication settings are missing.");
        return ReadSettings(settings, assembly.GetCustomAttributes<AssemblyMetadataAttribute>().ToDictionary(a => a.Key, a => a.Value), AppInfo.Current.PackageName);
    }
#endif
    internal static AndroidIdentityOptions ReadSettings(Stream settings, IReadOnlyDictionary<string,string?> metadata, string package)
    {
        if (package != AndroidIdentityOptions.PackageId) throw new InvalidOperationException("Android package and callback configuration do not match.");
        var environment = DeviceEnvironmentConfiguration.Create(metadata.GetValueOrDefault("ProsaEnvironment") ?? "",
            metadata.GetValueOrDefault("ProsaApiBaseUrl") ?? "", null);
        DeviceAuthOptions auth;
        try { auth = JsonSerializer.Deserialize<DeviceAuthOptions>(settings) ?? new(); }
        catch (JsonException) { auth = new(); }
        return new() { Authentication = auth, Environment = environment, SignatureHash = metadata.GetValueOrDefault("ProsaAndroidSignatureHash") ?? "" };
    }
}
