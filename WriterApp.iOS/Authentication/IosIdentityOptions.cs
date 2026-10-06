using System.Security.Cryptography;
using System.Text;
using WriterApp.Device.Shared.Services;

namespace WriterApp.iOS.Authentication;

public sealed class IosIdentityOptions
{
    public const string BundleId = "com.prosa.writer.ios";
    public const string Callback = "msauth." + BundleId + "://auth";
    public const string KeychainGroup = BundleId + ".msal";
    public required DeviceAuthOptions Authentication { get; init; }
    public required DeviceEnvironmentConfiguration Environment { get; init; }
    public bool IsConfigured => Authentication.IsProviderConfigured && Authentication.RedirectUri == Callback;
    // Only a backend-scoped explicitly selected account may restore, even if MSAL has other cached accounts.
    public string SelectionKey => "prosa.ios.identity.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        Environment.Environment + "|" + Environment.ApiBaseAddress.AbsoluteUri + "|" + Authentication.TenantId + "|"
        + Authentication.Authority + "|" + Authentication.ClientId + "|" + Authentication.RedirectUri + "|"
        + string.Join(' ', Authentication.Scopes.Order(StringComparer.Ordinal)))));
    public bool MatchesCallback(Uri uri) => uri.IsAbsoluteUri && uri.Scheme == "msauth." + BundleId
        && uri.Host == "auth" && uri.AbsolutePath is "" or "/" && uri.Port == -1 && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0;
}
