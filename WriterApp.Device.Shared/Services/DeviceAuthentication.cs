namespace WriterApp.Device.Shared.Services;

public sealed class DeviceAuthOptions
{
    public string TenantId { get; init; } = "";
    public string Authority { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string RedirectUri { get; init; } = "http://localhost";
    public string[] Scopes { get; init; } = [];
    public bool IsConfigured => Guid.TryParse(TenantId, out _) && Guid.TryParse(ClientId, out _)
        && Uri.TryCreate(Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https"
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Host.EndsWith(".ciamlogin.com", StringComparison.OrdinalIgnoreCase)
            || (uri.Host == "login.microsoftonline.com" && uri.AbsolutePath.Trim('/') == TenantId))
        && RedirectUri == "http://localhost" && Scopes.Length > 0
        && Scopes.All(s => Uri.TryCreate(s, UriKind.Absolute, out _) && !s.EndsWith("/.default", StringComparison.Ordinal)
            && !s.Any(char.IsWhiteSpace));

    public static DeviceAuthOptions FromEnvironment() => new()
    {
        TenantId = Environment.GetEnvironmentVariable("WRITERAPP_AUTH_TENANT_ID") ?? "",
        Authority = Environment.GetEnvironmentVariable("WRITERAPP_AUTH_AUTHORITY") ?? "",
        ClientId = Environment.GetEnvironmentVariable("WRITERAPP_AUTH_CLIENT_ID") ?? "",
        RedirectUri = Environment.GetEnvironmentVariable("WRITERAPP_AUTH_REDIRECT_URI") ?? "http://localhost",
        Scopes = (Environment.GetEnvironmentVariable("WRITERAPP_AUTH_SCOPES") ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    };
}

// Avoid generated record ToString() methods that would expose credentials in diagnostics.
public sealed class DeviceAccessToken(string value, DateTimeOffset expiresOn, string? displayName)
{
    public string Value { get; } = value;
    public DateTimeOffset ExpiresOn { get; } = expiresOn;
    public string? DisplayName { get; } = displayName;
    internal long Generation { get; set; }
    public override string ToString() => "[redacted access token]";
}

public interface IDeviceIdentityClient
{
    bool IsConfigured { get; }
    Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken);
    Task SignOutAsync();
}

public sealed class DeviceSignInRequiredException() : Exception("Sign in to use online features.");
public sealed class DeviceIdentityUnavailableException() : Exception("Sign-in is temporarily unavailable. Check your connection and secure storage, then retry.");

public sealed class UnconfiguredDeviceIdentityClient : IDeviceIdentityClient
{
    public bool IsConfigured => false;
    public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken) => Task.FromResult<DeviceAccessToken?>(null);
    public Task SignOutAsync() => Task.CompletedTask;
}

/// <summary>Account display state is separate from MSAL's secure credentials and local documents.</summary>
public sealed class DeviceAccountService(IDeviceIdentityClient identity, TimeProvider? time = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private bool _requiresInteraction;
    private long _generation;
    public bool IsConfigured => identity.IsConfigured;
    public bool IsSignedIn { get; private set; }
    public string? DisplayName { get; private set; }
    public string? Message { get; private set; }
    public long Generation => Interlocked.Read(ref _generation);
    public event Action? Changed;

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        try { await AcquireAsync(false, cancellationToken); }
        catch (DeviceSignInRequiredException) { }
        catch (DeviceIdentityUnavailableException) { }
    }

    public Task<DeviceAccessToken> SignInAsync(CancellationToken cancellationToken = default) => AcquireAsync(true, cancellationToken);
    public Task<DeviceAccessToken> GetTokenAsync(CancellationToken cancellationToken) => AcquireAsync(false, cancellationToken);

    private async Task<DeviceAccessToken> AcquireAsync(bool interactive, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!identity.IsConfigured || (!interactive && _requiresInteraction)) throw new DeviceSignInRequiredException();
            DeviceAccessToken? token = await identity.AcquireAsync(interactive, cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.Value) || token.ExpiresOn <= _time.GetUtcNow())
                throw new DeviceSignInRequiredException();
            IsSignedIn = true;
            DisplayName = token.DisplayName;
            Message = null;
            _requiresInteraction = false;
            if (interactive) Interlocked.Increment(ref _generation);
            token.Generation = Generation;
            return token;
        }
        catch (DeviceSignInRequiredException)
        {
            IsSignedIn = false;
            Message = identity.IsConfigured ? "Sign in to use online features. Local editing remains available." : "Native sign-in is not configured for this host.";
            throw;
        }
        catch (DeviceIdentityUnavailableException)
        {
            Message = "Cannot reach sign-in or secure storage. Retry when available; local editing still works.";
            throw;
        }
        finally { _gate.Release(); Changed?.Invoke(); }
    }

    public void RejectSession(long generation)
    {
        if (Generation != generation) return;
        _requiresInteraction = true;
        IsSignedIn = false;
        Message = "The server rejected this session. Sign in again. Local documents are unchanged.";
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        await _gate.WaitAsync();
        try
        {
            _requiresInteraction = true;
            Interlocked.Increment(ref _generation);
            IsSignedIn = false; DisplayName = null;
            await identity.SignOutAsync();
            Message = "Signed out on this device. Your browser may still be signed in.";
        }
        catch
        {
            Message = "Secure credentials could not be removed. Retry Sign out when secure storage is available.";
            throw;
        }
        finally { _gate.Release(); Changed?.Invoke(); }
    }
}
