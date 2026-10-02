using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Application.Security;
using WriterApp.Application.Feedback;

namespace WriterApp.Device.Shared.Services;

public enum DeviceAccountStatus { SignedOut, Ready, Offline, Expired, Forbidden, Duplicate, Deleted, Unavailable }

/// <summary>Memory-only display cache, bound to one native account generation. Never authorizes operations.</summary>
public sealed class DeviceAccountOverview(HttpClient http, DeviceAccountService account, DeviceConnectivity connectivity) : IDisposable
{
    private AuthMeDto? _profile;
    private long _generation = -1;
    public AuthMeDto? Profile => account.IsSignedIn && account.Generation == _generation ? _profile : null;
    public DateTimeOffset? CheckedAt { get; private set; }
    public DeviceAccountStatus Status { get; private set; } = DeviceAccountStatus.SignedOut;
    public string Message { get; private set; } = "Sign in for cloud features. Local writing remains available.";
    public bool IsStale => Status != DeviceAccountStatus.Ready || !connectivity.IsOnline || CheckedAt < DateTimeOffset.UtcNow.AddMinutes(-5);
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        long generation = account.Generation;
        if (!account.IsSignedIn) { Clear(); return; }
        if (generation != _generation) Clear();
        if (!connectivity.IsOnline) { Status = DeviceAccountStatus.Offline; Message = "Offline. Any displayed account information is cached; local writing remains available."; return; }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(30));
            using var probe = await http.GetAsync("api/native/session", timeout.Token);
            await RequireSuccess(probe, timeout.Token);
            if (!account.IsSignedIn || account.Generation != generation) { Clear(); return; }
            var profile = await http.GetAsync("api/auth/me?force=1", timeout.Token);
            using (profile)
            {
                await RequireSuccess(profile, timeout.Token);
                var value = await profile.Content.ReadFromJsonAsync<AuthMeDto>(cancellationToken: timeout.Token);
                timeout.Token.ThrowIfCancellationRequested();
                if (!account.IsSignedIn || account.Generation != generation) { Clear(); return; }
                if (value?.IsAuthenticated != true || string.IsNullOrWhiteSpace(value.UserId)) throw new AccountFailure(DeviceAccountStatus.Expired);
                _profile = value; _generation = generation; CheckedAt = DateTimeOffset.UtcNow; Status = DeviceAccountStatus.Ready;
                Message = "Backend connection verified. Account information refreshed.";
            }
        }
        catch (AccountFailure e) { if (e.Status != DeviceAccountStatus.Unavailable) Clear(); Status = e.Status; Message = Guidance(e.Status); }
        catch (DeviceSignInRequiredException) { Clear(); Status = DeviceAccountStatus.Expired; Message = Guidance(Status); }
        catch (OperationCanceledException) { Status = DeviceAccountStatus.Unavailable; Message = "Account refresh canceled or timed out. Any displayed information is cached."; }
        catch (Exception e) when (e is HttpRequestException or JsonException or DeviceIdentityUnavailableException)
        {
            Status = DeviceAccountStatus.Unavailable; Message = "Cannot refresh the backend account. Any displayed information is cached. Local writing remains available.";
        }
        if (account.Generation != generation) Clear();
        else if (!account.IsSignedIn) { _profile = null; CheckedAt = null; }
    }
    public void Clear() { _profile = null; _generation = -1; CheckedAt = null; Status = DeviceAccountStatus.SignedOut; Message = Guidance(Status); }
    public static string Guidance(DeviceAccountStatus status) => status switch
    {
        DeviceAccountStatus.Expired => "The session expired or was rejected. Sign in again; local documents are preserved.",
        DeviceAccountStatus.Duplicate => "This identity conflicts with an existing account. Contact support to review linking. No accounts were merged.",
        DeviceAccountStatus.Deleted => "This backend account was deleted. Sign out before registering again. Local documents remain on this device.",
        DeviceAccountStatus.Forbidden => "The account cannot access this backend feature. Check your plan or contact support.",
        DeviceAccountStatus.Unavailable => "Backend account information is unavailable. Retry later; local writing remains available.",
        _ => "Sign in for cloud features. Local writing remains available."
    };
    private sealed class AccountFailure(DeviceAccountStatus status) : Exception { public DeviceAccountStatus Status { get; } = status; }
    private static async Task RequireSuccess(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        string? code = null;
        try { using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)); if (json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String) code = c.GetString(); }
        catch (JsonException) { }
        throw new AccountFailure(code == "account_deleted" ? DeviceAccountStatus.Deleted : (int)response.StatusCode switch
        {
            401 => DeviceAccountStatus.Expired,
            402 or 403 => DeviceAccountStatus.Forbidden,
            409 => DeviceAccountStatus.Duplicate,
            _ => DeviceAccountStatus.Unavailable
        });
    }
    public async Task SendFeedbackAsync(FeedbackDraft draft, CancellationToken ct)
    {
        draft.Validate(); if (!account.IsSignedIn || !connectivity.IsOnline) throw new InvalidOperationException("Sign in and connect before sending feedback.");
        long generation = account.Generation;
        using var response = await http.PostAsJsonAsync("api/feedback", draft, ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException((int)response.StatusCode == 429 ? "Feedback rate limit reached. Wait before trying again." : "Feedback delivery was not confirmed. Check the connection and account before retrying.");
        if (account.Generation != generation || !account.IsSignedIn) throw new InvalidOperationException("The account changed during delivery. Feedback may already have been sent; do not retry automatically.");
    }
    public void Dispose() => Clear();
}
