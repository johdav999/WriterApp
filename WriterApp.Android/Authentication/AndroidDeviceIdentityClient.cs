using WriterApp.Device.Shared.Services;

namespace WriterApp.Android.Authentication;

public sealed class AndroidIdentityResult(DeviceAccessToken token, string tenantId)
{
    public DeviceAccessToken Token { get; } = token;
    public string TenantId { get; } = tenantId;
    public override string ToString() => "[redacted native identity result]";
}
public interface IAndroidMsalSession
{
    Task<IReadOnlyList<string>> AccountsAsync(CancellationToken ct);
    Task<AndroidIdentityResult> AcquireAsync(bool interactive, string? accountId, CancellationToken ct);
    Task RemoveAccountsAsync();
}
public interface IAndroidIdentitySelectionStore
{
    Task<string?> ReadAsync(string key);
    Task WriteAsync(string key, string accountId);
    Task RemoveAsync(string key);
}

/// <summary>MSAL owns OAuth/PKCE and the encrypted token cache. Only an explicitly selected, backend-scoped account can restore.</summary>
public sealed class AndroidDeviceIdentityClient(AndroidIdentityOptions options, IAndroidMsalSession session,
    IAndroidIdentitySelectionStore selection, TimeProvider? time = null) : IDeviceIdentityClient
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool signedOut;
    private volatile bool foreground = true, interactive;
    public bool IsConfigured => options.IsConfigured;
    public bool IsInteractive => interactive;
    public void SetForeground(bool value) => foreground = value;

    public async Task<DeviceAccessToken?> AcquireAsync(bool useInteraction, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        await gate.WaitAsync(ct);
        try
        {
            if (useInteraction && !foreground) throw new DeviceIdentityUnavailableException();
            interactive = useInteraction;
            string? selected = null;
            if (!useInteraction)
            {
                if (signedOut) return null;
                selected = await selection.ReadAsync(options.SelectionKey);
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(selected)) return null;
                if ((await session.AccountsAsync(ct)).Count(id => id == selected) != 1) throw new DeviceSignInRequiredException();
            }
            var result = await session.AcquireAsync(useInteraction, selected, ct);
            ct.ThrowIfCancellationRequested();
            var token = result.Token;
            if (!string.Equals(result.TenantId, options.Authentication.TenantId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(token.AccountId) || string.IsNullOrWhiteSpace(token.Value)
                || token.ExpiresOn <= (time ?? TimeProvider.System).GetUtcNow()
                || !useInteraction && token.AccountId != selected) throw new DeviceSignInRequiredException();
            if (useInteraction)
            {
                await selection.WriteAsync(options.SelectionKey, token.AccountId);
                if (ct.IsCancellationRequested) { await selection.RemoveAsync(options.SelectionKey); ct.ThrowIfCancellationRequested(); }
                signedOut = false;
            }
            return token;
        }
        catch (OperationCanceledException) { throw; }
        catch (DeviceSignInRequiredException) { throw; }
        catch (DeviceIdentityConfigurationException) { throw; }
        catch (DeviceIdentityUnavailableException) { throw; }
        catch (Exception) { throw new DeviceIdentityUnavailableException(); }
        finally { interactive = false; gate.Release(); }
    }

    public async Task SignOutAsync()
    {
        signedOut = true;
        if (!IsConfigured) return;
        await gate.WaitAsync();
        try
        {
            // An in-flight interactive result may have cleared this while sign-out waited for the gate.
            signedOut = true;
            bool failed = false;
            try { await selection.RemoveAsync(options.SelectionKey); } catch { failed = true; }
            try { await session.RemoveAccountsAsync(); } catch { failed = true; }
            if (failed) throw new DeviceIdentityUnavailableException();
        }
        finally { gate.Release(); }
    }
}
