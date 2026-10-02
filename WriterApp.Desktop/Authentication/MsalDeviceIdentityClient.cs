using System.Security.Cryptography;
using System.Text;
using Microsoft.Identity.Client;
using WriterApp.Device.Shared.Services;

namespace WriterApp.Desktop.Authentication;

/// <summary>MSAL owns PKCE/state/nonce and token renewal. Only SecureStorage persists its cache.</summary>
public sealed class MsalDeviceIdentityClient(DeviceAuthOptions options) : IDeviceIdentityClient
{
    private IPublicClientApplication? _app;
    private string CacheKey => "prosa.msal.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        options.Authority + "|" + options.ClientId + "|" + string.Join(' ', options.Scopes))));
    public bool IsConfigured => options.IsConfigured;

    private IPublicClientApplication GetApp()
    {
        if (_app is not null) return _app;
        var app = PublicClientApplicationBuilder.Create(options.ClientId)
            .WithAuthority(options.Authority).WithRedirectUri(options.RedirectUri).Build();
        app.UserTokenCache.SetBeforeAccessAsync(async args =>
        {
            string? cache = await SecureStorage.Default.GetAsync(CacheKey);
            args.TokenCache.DeserializeMsalV3(cache is null ? null : Convert.FromBase64String(cache), shouldClearExistingCache: true);
        });
        app.UserTokenCache.SetAfterAccessAsync(async args =>
        {
            if (args.HasStateChanged)
                await SecureStorage.Default.SetAsync(CacheKey, Convert.ToBase64String(args.TokenCache.SerializeMsalV3()));
        });
        return _app = app;
    }

    public async Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;
        try
        {
            var app = GetApp();
            AuthenticationResult result;
            if (interactive)
            {
                result = await app.AcquireTokenInteractive(options.Scopes).WithUseEmbeddedWebView(false)
                    .WithPrompt(Prompt.SelectAccount).ExecuteAsync(cancellationToken);
            }
            else
            {
                var accounts = (await app.GetAccountsAsync()).ToArray();
                if (accounts.Length == 0) return null;
                if (accounts.Length != 1) throw new DeviceSignInRequiredException();
                result = await app.AcquireTokenSilent(options.Scopes, accounts[0]).ExecuteAsync(cancellationToken);
            }
            if (result.Account is null || !string.Equals(result.TenantId, options.TenantId, StringComparison.OrdinalIgnoreCase))
                throw new DeviceSignInRequiredException();
            if (interactive)
                foreach (var previous in await app.GetAccountsAsync())
                    if (previous.HomeAccountId.Identifier != result.Account.HomeAccountId.Identifier)
                        await app.RemoveAsync(previous);
            return new(result.AccessToken, result.ExpiresOn, result.Account?.Username);
        }
        catch (MsalUiRequiredException) { throw new DeviceSignInRequiredException(); }
        catch (MsalClientException error) when (error.ErrorCode == "authentication_canceled") { throw new OperationCanceledException(cancellationToken); }
        catch (MsalServiceException error) when (interactive && error.ErrorCode is ("invalid_client" or "unauthorized_client" or "invalid_scope" or "invalid_resource"))
        { throw new DeviceIdentityConfigurationException(); }
        catch (OperationCanceledException) { throw; }
        catch (DeviceSignInRequiredException) { throw; }
        catch (Exception) { _app = null; throw new DeviceIdentityUnavailableException(); }
    }

    public Task SignOutAsync()
    {
        _app = null; // Drop the in-memory cache as well as all persisted tokens/account material.
        try { SecureStorage.Default.Remove(CacheKey); return Task.CompletedTask; }
        catch (Exception) { throw new DeviceIdentityUnavailableException(); }
    }
}
