using Microsoft.Identity.Client;
using WriterApp.Device.Shared.Services;

namespace WriterApp.iOS.Authentication;

/// <summary>Uses MSAL's native iOS Keychain cache and system authentication session, never a WebView or file cache.</summary>
public sealed class IosMsalSession(IosIdentityOptions options) : IIosMsalSession
{
    private IPublicClientApplication? _app;
    private IPublicClientApplication App => _app ??= PublicClientApplicationBuilder.Create(options.Authentication.ClientId)
        .WithAuthority(options.Authentication.Authority).WithRedirectUri(options.Authentication.RedirectUri)
        .WithIosKeychainSecurityGroup(IosIdentityOptions.KeychainGroup).Build();
    public async Task<IReadOnlyList<string>> AccountsAsync(CancellationToken ct)
    {
        var accounts = await App.GetAccountsAsync(); ct.ThrowIfCancellationRequested();
        return accounts.Select(a => a.HomeAccountId.Identifier).ToArray();
    }
    public async Task<IosIdentityResult> AcquireAsync(bool interactive, string? accountId, CancellationToken ct)
    {
        try {
            AuthenticationResult result;
            if (interactive) {
                result = await MainThread.InvokeOnMainThreadAsync(async () => {
                    ct.ThrowIfCancellationRequested();
                    var parent = Platform.GetCurrentUIViewController() ?? throw new DeviceIdentityUnavailableException();
                    return await App.AcquireTokenInteractive(options.Authentication.Scopes).WithParentActivityOrWindow(parent)
                        .WithUseEmbeddedWebView(false).WithPrompt(Prompt.SelectAccount).ExecuteAsync(ct);
                });
            } else {
                var account = (await App.GetAccountsAsync()).SingleOrDefault(a => a.HomeAccountId.Identifier == accountId)
                    ?? throw new DeviceSignInRequiredException();
                result = await App.AcquireTokenSilent(options.Authentication.Scopes, account).ExecuteAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            return new(new(result.AccessToken, result.ExpiresOn, result.Account?.Username,
                result.Account?.HomeAccountId.Identifier), result.TenantId);
        }
        catch (MsalUiRequiredException) { throw new DeviceSignInRequiredException(); }
        catch (MsalClientException e) when(e.ErrorCode == "authentication_canceled") { throw new OperationCanceledException(ct); }
        catch (MsalServiceException e) when(e.ErrorCode is "invalid_client" or "unauthorized_client" or "invalid_scope" or "invalid_resource")
        { throw new DeviceIdentityConfigurationException(); }
        catch (OperationCanceledException) { throw; }
        catch (DeviceSignInRequiredException) { throw; }
        catch (DeviceIdentityConfigurationException) { throw; }
        catch (Exception) { throw new DeviceIdentityUnavailableException(); }
    }
    public bool Continue(Uri callback) => AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(new Foundation.NSUrl(callback.AbsoluteUri));
    public async Task RemoveAccountsAsync()
    {
        try { foreach(var account in await App.GetAccountsAsync()) await App.RemoveAsync(account); }
        finally { _app = null; }
    }
}

public sealed class IosIdentitySelectionStore : IIosIdentitySelectionStore
{
    public Task<string?> ReadAsync(string key) => SecureStorage.Default.GetAsync(key);
    public Task WriteAsync(string key, string accountId) => SecureStorage.Default.SetAsync(key, accountId);
    public async Task RemoveAsync(string key)
    {
        if (!SecureStorage.Default.Remove(key) && await SecureStorage.Default.GetAsync(key) is not null)
            throw new DeviceIdentityUnavailableException();
    }
}
