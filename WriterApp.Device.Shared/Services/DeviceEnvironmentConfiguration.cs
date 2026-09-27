namespace WriterApp.Device.Shared.Services;

public enum DeviceEnvironment { Development, Staging, Production }

public sealed record DeviceEnvironmentConfiguration(
    DeviceEnvironment Environment, Uri ApiBaseAddress, Uri? UpdateManifestAddress)
{
    public static DeviceEnvironmentConfiguration Create(string environment, string apiBaseAddress,
        string? updateManifestAddress)
    {
        if (!Enum.TryParse(environment, ignoreCase: true, out DeviceEnvironment selected)
            || !Enum.IsDefined(selected)
            || !Enum.GetNames<DeviceEnvironment>().Contains(environment, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Choose Development, Staging, or Production.", nameof(environment));
        if (!Uri.TryCreate(apiBaseAddress, UriKind.Absolute, out Uri? api)
            || !AllowedEndpoint(api, selected))
            throw new ArgumentException("The backend URL must be HTTPS (local HTTP is allowed only in Development).", nameof(apiBaseAddress));
        Uri? manifest = null;
        if (!string.IsNullOrWhiteSpace(updateManifestAddress))
        {
            if (!Uri.TryCreate(updateManifestAddress, UriKind.Absolute, out manifest)
                || manifest.Scheme != Uri.UriSchemeHttps || manifest.Host != api.Host
                || manifest.Port != api.Port || manifest.UserInfo.Length != 0
                || manifest.Fragment.Length != 0)
                throw new ArgumentException("The update manifest must use HTTPS on the configured backend host.", nameof(updateManifestAddress));
        }
        return new(selected, api, manifest);
    }

    private static bool AllowedEndpoint(Uri uri, DeviceEnvironment environment) =>
        uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && (uri.Scheme == Uri.UriSchemeHttps || environment == DeviceEnvironment.Development
            && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
}
