using System.Security.Cryptography;
using System.Text;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Android.Authentication;

public sealed class AndroidIdentityOptions
{
    public const string PackageId = "com.prosa.writer.android";
    public required DeviceAuthOptions Authentication { get; init; }
    public required DeviceEnvironmentConfiguration Environment { get; init; }
    public required string SignatureHash { get; init; }
    public bool IsConfigured => Authentication.IsProviderConfigured && ValidHash(SignatureHash)
        && Authentication.RedirectUri == Callback(SignatureHash);
    public static string Callback(string hash) => "msauth://" + PackageId + "/" + Uri.EscapeDataString(hash);
    public static bool ValidHash(string hash)
    {
        try { return Convert.FromBase64String(hash).Length == 20 && Convert.ToBase64String(Convert.FromBase64String(hash)) == hash; }
        catch (FormatException) { return false; }
    }
    public string SelectionKey => "prosa.android.identity.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        Environment.Environment + "|" + Environment.ApiBaseAddress.AbsoluteUri + "|" + Authentication.TenantId + "|"
        + Authentication.Authority + "|" + Authentication.ClientId + "|" + Authentication.RedirectUri + "|"
        + string.Join(' ', Authentication.Scopes.Order(StringComparer.Ordinal)))));
}
