using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WriterApp.Device.Shared.Services;

public enum DeviceUpdateState { Unavailable, Current, Available }
public sealed record DeviceUpdateResult(DeviceUpdateState State, string Message, string? Version = null, Uri? DownloadAddress = null);

public interface IDeviceExternalLauncher
{
    Task OpenAsync(Uri address);
}

public interface IDeviceUpdateService
{
    bool IsAvailable { get; }
    Task<DeviceUpdateResult> CheckAsync(CancellationToken ct = default);
    Task OpenInstallerPageAsync(DeviceUpdateResult result);
}

public sealed class UnavailableDeviceUpdateService : IDeviceUpdateService
{
    public bool IsAvailable => false;
    public Task<DeviceUpdateResult> CheckAsync(CancellationToken ct = default) =>
        Task.FromResult(new DeviceUpdateResult(DeviceUpdateState.Unavailable, "No update feed is configured for this build."));
    public Task OpenInstallerPageAsync(DeviceUpdateResult result) => Task.CompletedTask;
}

public sealed class UnavailableDeviceExternalLauncher : IDeviceExternalLauncher
{
    public Task OpenAsync(Uri address) => throw new NotSupportedException("External links are unavailable on this device.");
}

/// <summary>Checks a small same-host HTTPS manifest; it never downloads or installs packages.</summary>
public sealed class DeviceUpdateService(HttpClient http, DeviceEnvironmentConfiguration configuration,
    string installedVersion, IDeviceExternalLauncher launcher) : IDeviceUpdateService
{
    private static readonly Regex SemanticVersion = new(@"^\d+\.\d+\.\d+$", RegexOptions.Compiled);
    private static readonly Regex InstalledVersion = new(@"^\d+\.\d+\.\d+(?:\.\d+)?$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public bool IsAvailable => configuration.UpdateManifestAddress is not null;

    public async Task<DeviceUpdateResult> CheckAsync(CancellationToken ct = default)
    {
        Uri? manifestAddress = configuration.UpdateManifestAddress;
        if (manifestAddress is null)
            return new(DeviceUpdateState.Unavailable, "No update feed is configured for this build.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response = await http.GetAsync(manifestAddress, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode != HttpStatusCode.OK)
                return new(DeviceUpdateState.Unavailable, "The update feed is unavailable. Try again later.");
            if (response.Content.Headers.ContentLength is > 16_384)
                return new(DeviceUpdateState.Unavailable, "The update feed returned an invalid response.");
            await response.Content.LoadIntoBufferAsync(16_384, timeout.Token);
            var manifest = await response.Content.ReadFromJsonAsync<UpdateManifest>(Json, timeout.Token);
            if (manifest is null || !TryVersion(manifest.Version, out Version? latest)
                || !TryInstalledVersion(installedVersion, out Version? installed)
                || !ValidDownload(manifest.DownloadUrl, manifestAddress, out Uri? download))
                return new(DeviceUpdateState.Unavailable, "The update feed returned an invalid release.");
            Version installedSemantic = new(installed!.Major, installed.Minor, installed.Build);
            return latest > installedSemantic
                ? new(DeviceUpdateState.Available, $"Prosa {manifest.Version} is available.", manifest.Version, download)
                : new(DeviceUpdateState.Current, "This version is current.", installedVersion);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return new(DeviceUpdateState.Unavailable, "The update check timed out. Try again later."); }
        catch (Exception error) when (error is HttpRequestException or JsonException or IOException)
        { return new(DeviceUpdateState.Unavailable, "The update feed could not be read. Try again later."); }
    }

    public Task OpenInstallerPageAsync(DeviceUpdateResult result)
    {
        if (result.State != DeviceUpdateState.Available || result.DownloadAddress is null
            || configuration.UpdateManifestAddress is null
            || !ValidDownload(result.DownloadAddress.AbsoluteUri, configuration.UpdateManifestAddress, out Uri? address)
            || address is null)
            throw new InvalidOperationException("There is no verified update link to open.");
        return launcher.OpenAsync(address);
    }

    private static bool TryVersion(string? text, out Version? version)
    {
        version = null;
        return text is not null && SemanticVersion.IsMatch(text) && Version.TryParse(text, out version);
    }
    private static bool TryInstalledVersion(string? text, out Version? version)
    {
        version = null;
        return text is not null && InstalledVersion.IsMatch(text) && Version.TryParse(text, out version);
    }
    private static bool ValidDownload(string? text, Uri manifestAddress, out Uri? address)
    {
        address = null;
        return Uri.TryCreate(text, UriKind.Absolute, out address)
            && address.Scheme == Uri.UriSchemeHttps && address.Host == manifestAddress.Host
            && address.Port == manifestAddress.Port && string.IsNullOrEmpty(address.UserInfo);
    }
    private sealed record UpdateManifest(string? Version, string? DownloadUrl);
}
