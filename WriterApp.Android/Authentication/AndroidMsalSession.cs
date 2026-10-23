using Microsoft.Identity.Client;
using WriterApp.Device.Shared.Services;
using global::Android.Content.PM;
using System.Security.Cryptography;

namespace WriterApp.Android.Authentication;

public sealed class AndroidMsalSession(AndroidIdentityOptions options) : IAndroidMsalSession
{
    private IPublicClientApplication? app;
    private IPublicClientApplication App => app ??= PublicClientApplicationBuilder.Create(options.Authentication.ClientId)
        .WithAuthority(options.Authentication.Authority).WithRedirectUri(options.Authentication.RedirectUri).Build();

    public async Task<IReadOnlyList<string>> AccountsAsync(CancellationToken ct)
    {
        var accounts = await App.GetAccountsAsync(); ct.ThrowIfCancellationRequested();
        return accounts.Select(a => a.HomeAccountId.Identifier).ToArray();
    }

    public async Task<AndroidIdentityResult> AcquireAsync(bool interactive, string? accountId, CancellationToken ct)
    {
        try
        {
            AuthenticationResult result;
            if (interactive)
            {
                result = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    ct.ThrowIfCancellationRequested();
                    var activity = Platform.CurrentActivity ?? throw new DeviceIdentityUnavailableException();
                    RequireSigningCertificate(activity);
                    return await App.AcquireTokenInteractive(options.Authentication.Scopes).WithParentActivityOrWindow(activity)
                        .WithUseEmbeddedWebView(false).WithPrompt(Prompt.SelectAccount).ExecuteAsync(ct);
                });
            }
            else
            {
                var account = (await App.GetAccountsAsync()).SingleOrDefault(a => a.HomeAccountId.Identifier == accountId)
                    ?? throw new DeviceSignInRequiredException();
                result = await App.AcquireTokenSilent(options.Authentication.Scopes, account).ExecuteAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            return new(new(result.AccessToken, result.ExpiresOn, result.Account?.Username, result.Account?.HomeAccountId.Identifier), result.TenantId);
        }
        catch (MsalUiRequiredException) { throw new DeviceSignInRequiredException(); }
        catch (MsalClientException e) when (e.ErrorCode == "authentication_canceled") { throw new OperationCanceledException(ct); }
        catch (MsalServiceException e) when (e.ErrorCode is "invalid_client" or "unauthorized_client" or "invalid_scope" or "invalid_resource") { throw new DeviceIdentityConfigurationException(); }
        catch (OperationCanceledException) { throw; }
        catch (DeviceSignInRequiredException) { throw; }
        catch (DeviceIdentityConfigurationException) { throw; }
        catch (Exception) { throw new DeviceIdentityUnavailableException(); }
    }

    private void RequireSigningCertificate(global::Android.App.Activity activity)
    {
        // Android's MSAL callback is bound to the installed signing certificate, including Play app signing.
        IList<Signature>? signatures;
        if (OperatingSystem.IsAndroidVersionAtLeast(28))
        {
            var info = activity.PackageManager!.GetPackageInfo(AndroidIdentityOptions.PackageId, PackageInfoFlags.SigningCertificates);
            signatures = info?.SigningInfo?.GetApkContentsSigners();
        }
        else
        {
#pragma warning disable CS0618, CA1422
            signatures = activity.PackageManager!.GetPackageInfo(AndroidIdentityOptions.PackageId, PackageInfoFlags.Signatures)?.Signatures;
#pragma warning restore CS0618, CA1422
        }
        if (signatures is null || signatures.Count != 1
            || Convert.ToBase64String(SHA1.HashData(signatures[0].ToByteArray()!)) != options.SignatureHash)
            throw new DeviceIdentityConfigurationException();
    }

    public async Task RemoveAccountsAsync()
    {
        try { foreach (var account in await App.GetAccountsAsync()) await App.RemoveAsync(account); }
        finally { app = null; }
    }
}

public sealed class AndroidIdentitySelectionStore : IAndroidIdentitySelectionStore
{
    public Task<string?> ReadAsync(string key) => SecureStorage.Default.GetAsync(key);
    public Task WriteAsync(string key, string accountId) => SecureStorage.Default.SetAsync(key, accountId);
    public async Task RemoveAsync(string key)
    {
        if (!SecureStorage.Default.Remove(key) && await SecureStorage.Default.GetAsync(key) is not null)
            throw new DeviceIdentityUnavailableException();
    }
}
