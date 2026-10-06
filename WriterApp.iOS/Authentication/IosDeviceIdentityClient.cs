using WriterApp.Device.Shared.Services;

namespace WriterApp.iOS.Authentication;

public sealed class IosIdentityResult(DeviceAccessToken token, string tenantId)
{
    public DeviceAccessToken Token { get; } = token;
    public string TenantId { get; } = tenantId;
    public override string ToString() => "[redacted native identity result]";
}
public interface IIosMsalSession
{
    Task<IReadOnlyList<string>> AccountsAsync(CancellationToken ct);
    Task<IosIdentityResult> AcquireAsync(bool interactive, string? accountId, CancellationToken ct);
    Task RemoveAccountsAsync();
    bool Continue(Uri callback);
}
public interface IIosIdentitySelectionStore
{
    Task<string?> ReadAsync(string key);
    Task WriteAsync(string key, string accountId);
    Task RemoveAsync(string key);
}

/// <summary>Native MSAL performs OAuth/PKCE, state, nonce and renewal; this adapter owns host/session isolation.</summary>
public sealed class IosDeviceIdentityClient(IosIdentityOptions options, IIosMsalSession session,
    IIosIdentitySelectionStore selection, TimeProvider? time = null) : IDeviceIdentityClient
{
    private readonly SemaphoreSlim _gate = new(1,1);
    private readonly object _callbackLock = new();
    private CancellationToken _interactiveToken;
    private bool _interactive, _forwarded, _foreground = true, _signedOut;
    public bool IsConfigured => options.IsConfigured;
    public bool IsInteractive { get { lock (_callbackLock) return _interactive; } }
    public void SetForeground(bool value) { lock (_callbackLock) _foreground = value; }
    public bool HandleCallback(Uri callback)
    {
        lock (_callbackLock) {
            if (!_interactive || _forwarded || _interactiveToken.IsCancellationRequested || !options.MatchesCallback(callback)) return false;
            try { return _forwarded = session.Continue(callback); }
            catch { return false; } // Malformed callbacks cannot crash the native host or consume approval.
        }
    }
    public async Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;
        await _gate.WaitAsync(cancellationToken);
        try {
            if (interactive) {
                lock (_callbackLock) {
                    if (!_foreground) throw new DeviceIdentityUnavailableException();
                    _interactive = true; _forwarded = false; _interactiveToken = cancellationToken;
                }
            }
            string? accountId = null;
            if (!interactive) {
                if (_signedOut) return null;
                accountId = await selection.ReadAsync(options.SelectionKey);
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(accountId)) return null;
                var accounts = await session.AccountsAsync(cancellationToken);
                if (accounts.Count(a => a == accountId) != 1) throw new DeviceSignInRequiredException();
            }
            var result = await session.AcquireAsync(interactive, accountId, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var token = result.Token;
            if (!string.Equals(result.TenantId, options.Authentication.TenantId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(token.AccountId) || string.IsNullOrWhiteSpace(token.Value)
                || token.ExpiresOn <= (time ?? TimeProvider.System).GetUtcNow()
                || !interactive && token.AccountId != accountId) throw new DeviceSignInRequiredException();
            if (interactive) {
                await selection.WriteAsync(options.SelectionKey, token.AccountId);
                // A cancelled provider result must never become a restorable selected account.
                if (cancellationToken.IsCancellationRequested) { await selection.RemoveAsync(options.SelectionKey); cancellationToken.ThrowIfCancellationRequested(); }
                _signedOut = false;
            }
            return token;
        }
        catch (OperationCanceledException) { throw; }
        catch (DeviceSignInRequiredException) { throw; }
        catch (DeviceIdentityConfigurationException) { throw; }
        catch (DeviceIdentityUnavailableException) { throw; }
        catch (Exception) { throw new DeviceIdentityUnavailableException(); }
        finally { lock (_callbackLock) { _interactive = false; _forwarded = false; } _gate.Release(); }
    }
    public async Task SignOutAsync()
    {
        _signedOut = true;
        if (!IsConfigured) return;
        await _gate.WaitAsync();
        try {
            bool failed = false;
            try { await selection.RemoveAsync(options.SelectionKey); } catch { failed = true; }
            try { await session.RemoveAccountsAsync(); } catch { failed = true; }
            if (failed) throw new DeviceIdentityUnavailableException();
        }
        finally { _gate.Release(); }
    }
}
