namespace WriterApp.Device.Shared.Services;

public sealed class DeviceAuthOptions
{
    public string TenantId { get; init; } = "";
    public string Authority { get; init; } = "";
    public string ClientId { get; init; } = "";
    public string RedirectUri { get; init; } = "http://localhost";
    public string[] Scopes { get; init; } = [];
    public bool IsProviderConfigured => Guid.TryParse(TenantId, out var tenant) && tenant != Guid.Empty
        && Guid.TryParse(ClientId, out var client) && client != Guid.Empty
        && Uri.TryCreate(Authority, UriKind.Absolute, out var uri) && uri.Scheme == "https"
        && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Host.EndsWith(".ciamlogin.com", StringComparison.OrdinalIgnoreCase)
            || (uri.Host == "login.microsoftonline.com" && uri.AbsolutePath.Trim('/') == TenantId))
        && Scopes is { Length: > 0 }
        && Scopes.All(s => Uri.TryCreate(s, UriKind.Absolute, out _) && !s.EndsWith("/.default", StringComparison.Ordinal)
            && !s.Any(char.IsWhiteSpace));
    public bool IsConfigured => IsProviderConfigured && RedirectUri == "http://localhost";

    public static DeviceAuthOptions FromEnvironment(DeviceAuthOptions? defaults = null) =>
        FromSettings(defaults ?? new(), Environment.GetEnvironmentVariable);

    /// <summary>Public build settings work for normal launches; environment settings can override them for development.</summary>
    public static DeviceAuthOptions FromSettings(DeviceAuthOptions defaults, Func<string, string?> readSetting) => new()
    {
        TenantId = readSetting("WRITERAPP_AUTH_TENANT_ID") ?? defaults.TenantId,
        Authority = readSetting("WRITERAPP_AUTH_AUTHORITY") ?? defaults.Authority,
        ClientId = readSetting("WRITERAPP_AUTH_CLIENT_ID") ?? defaults.ClientId,
        RedirectUri = readSetting("WRITERAPP_AUTH_REDIRECT_URI") ?? defaults.RedirectUri,
        Scopes = readSetting("WRITERAPP_AUTH_SCOPES") is { } scopes
            ? scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : defaults.Scopes?.ToArray() ?? []
    };
}

// Avoid generated record ToString() methods that would expose credentials in diagnostics.
public sealed class DeviceAccessToken(string value, DateTimeOffset expiresOn, string? displayName, string? accountId = null)
{
    public string Value { get; } = value;
    public DateTimeOffset ExpiresOn { get; } = expiresOn;
    public string? DisplayName { get; } = displayName;
    public string? AccountId { get; } = accountId;
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
public sealed class DeviceIdentityConfigurationException() : Exception("The sign-in provider rejected this app's configuration. Check its native registration, callback and API permission.");

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
    private readonly object _sessionLock = new();
    private CancellationTokenSource _session = new();
    public bool IsConfigured => identity.IsConfigured;
    public bool IsSignedIn { get; private set; }
    public string? DisplayName { get; private set; }
    public string? AccountId { get; private set; }
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
        long captured;
        CancellationToken session;
        lock (_sessionLock) { captured = Generation; session = _session.Token; }
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session);
        await _gate.WaitAsync(lifetime.Token);
        try
        {
            if (captured != Generation) throw new OperationCanceledException(lifetime.Token);
            if (!identity.IsConfigured || (!interactive && _requiresInteraction)) throw new DeviceSignInRequiredException();
            DeviceAccessToken? token = await identity.AcquireAsync(interactive, lifetime.Token);
            lifetime.Token.ThrowIfCancellationRequested();
            if (token is null || string.IsNullOrWhiteSpace(token.Value) || token.ExpiresOn <= _time.GetUtcNow())
                throw new DeviceSignInRequiredException();
            lock (_sessionLock)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                if (captured != Generation) throw new OperationCanceledException(lifetime.Token);
                IsSignedIn = true;
                DisplayName = token.DisplayName;
                if (AccountId is not null && AccountId != token.AccountId) Interlocked.Increment(ref _generation);
                AccountId = token.AccountId;
                Message = null;
                _requiresInteraction = false;
                if (interactive) Interlocked.Increment(ref _generation);
                token.Generation = Generation;
            }
            return token;
        }
        catch (DeviceSignInRequiredException)
        {
            if (captured != Generation) throw new OperationCanceledException(lifetime.Token);
            if (IsSignedIn || AccountId is not null) AdvanceSession();
            IsSignedIn = false;
            DisplayName = null;
            AccountId = null;
            Message = identity.IsConfigured ? "Sign in to use online features. Local editing remains available." : "Native sign-in is not configured for this host.";
            throw;
        }
        catch (DeviceIdentityUnavailableException)
        {
            if (captured != Generation) throw new OperationCanceledException(lifetime.Token);
            Message = "Cannot reach sign-in or secure storage. Retry when available; local editing still works.";
            throw;
        }
        finally { _gate.Release(); Changed?.Invoke(); }
    }

    public void RejectSession(long generation)
    {
        lock (_sessionLock) {
            if (Generation != generation) return;
            AdvanceSession();
            _requiresInteraction = true;
            IsSignedIn = false;
            DisplayName = null;
            AccountId = null;
            Message = "The server rejected this session. Sign in again. Local documents are unchanged.";
        }
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        // Invalidate immediately, even while a provider ignores cancellation or a browser is open.
        lock (_sessionLock) {
            AdvanceSession();
            _requiresInteraction = true;
            IsSignedIn = false; DisplayName = null; AccountId = null;
        }
        Changed?.Invoke();
        await _gate.WaitAsync();
        try
        {
            _requiresInteraction = true;
            IsSignedIn = false; DisplayName = null; AccountId = null;
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

    public void CancelPendingAuthentication()
    {
        AdvanceSession();
        Changed?.Invoke();
    }

    private void AdvanceSession()
    {
        CancellationTokenSource previous;
        lock (_sessionLock) { Interlocked.Increment(ref _generation); previous = _session; _session = new(); }
        previous.Cancel();
        // Acquisitions may still register against the old token; let GC dispose its registrations.
    }
}
