using System.Text;
using System.Text.Json;
using WriterApp.Android;
using WriterApp.Android.Authentication;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class AndroidHostTests
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111", Client = "22222222-2222-2222-2222-222222222222";
    private static string Hash => Convert.ToBase64String(Enumerable.Range(0, 20).Select(x => (byte)x).ToArray());
    private static AndroidIdentityOptions Options(string backend = "https://api.example/", string? callback = null) => new()
    {
        SignatureHash = Hash,
        Authentication = new() { TenantId = Tenant, ClientId = Client, Authority = "https://login.microsoftonline.com/" + Tenant,
            RedirectUri = callback ?? AndroidIdentityOptions.Callback(Hash), Scopes = ["api://api/access_as_user"] },
        Environment = DeviceEnvironmentConfiguration.Create("Production", backend, null)
    };
    private sealed class Store : IAndroidIdentitySelectionStore
    {
        public Dictionary<string, string> Values = [];
        public Action? Written;
        public bool FailRemove;
        public Task<string?> ReadAsync(string key) => Task.FromResult(Values.GetValueOrDefault(key));
        public Task WriteAsync(string key, string accountId) { Values[key] = accountId; Written?.Invoke(); return Task.CompletedTask; }
        public Task RemoveAsync(string key) { if (FailRemove) throw new IOException(); Values.Remove(key); return Task.CompletedTask; }
    }
    private sealed class Session : IAndroidMsalSession
    {
        public List<string> Accounts = [];
        public string NextAccount = "account-a", NextTenant = Tenant;
        public int Calls, Removals;
        public bool Expired, FailRemove;
        public TaskCompletionSource<AndroidIdentityResult>? Pending;
        public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<string>> AccountsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>(Accounts);
        public async Task<AndroidIdentityResult> AcquireAsync(bool interactive, string? accountId, CancellationToken ct)
        {
            Calls++; Started.TrySetResult();
            if (Pending is not null) return await Pending.Task;
            if (!Accounts.Contains(NextAccount)) Accounts.Add(NextAccount);
            return Result();
        }
        public AndroidIdentityResult Result() => new(new("synthetic-not-a-real-token", DateTimeOffset.UtcNow.AddHours(Expired ? -1 : 1), "Synthetic author", NextAccount), NextTenant);
        public Task RemoveAccountsAsync() { Removals++; if(FailRemove) throw new IOException(); Accounts.Clear(); return Task.CompletedTask; }
    }

    [Fact]
    public void CallbackIsCertificateBoundAndBackendSelectionIsIsolated()
    {
        Assert.True(Options().IsConfigured);
        Assert.False(Options(callback: "msauth://foreign/" + Hash).IsConfigured);
        Assert.False(Options(callback: "http://localhost").IsConfigured);
        Assert.False(AndroidIdentityOptions.ValidHash("unconfigured"));
        Assert.NotEqual(Options().SelectionKey, Options("https://other.example/").SelectionKey);
        Assert.Contains("%3D", AndroidIdentityOptions.Callback(Hash));
    }
    [Fact]
    public async Task UnselectedCachedAccountNeverRestores()
    {
        var session = new Session { Accounts = ["account-a"] };
        var identity = new AndroidDeviceIdentityClient(Options(), session, new Store());
        Assert.Null(await identity.AcquireAsync(false, default)); Assert.Equal(0, session.Calls);
    }
    [Fact]
    public async Task SelectedAccountRestoresAcrossClientRecreationAndSignOutRemovesBothCaches()
    {
        var options = Options(); var store = new Store(); var session = new Session();
        var first = new AndroidDeviceIdentityClient(options, session, store);
        Assert.NotNull(await first.AcquireAsync(true, default));
        var reopened = new AndroidDeviceIdentityClient(options, session, store);
        Assert.Equal("account-a", (await reopened.AcquireAsync(false, default))!.AccountId);
        await reopened.SignOutAsync(); Assert.Empty(store.Values); Assert.Empty(session.Accounts);
        Assert.Null(await reopened.AcquireAsync(false, default)); Assert.Equal(1, session.Removals);
    }
    [Theory]
    [InlineData(true, false)] [InlineData(false, true)]
    public async Task ExpiredOrWrongTenantCannotBecomeSelected(bool expired, bool wrongTenant)
    {
        var session = new Session { Expired = expired, NextTenant = wrongTenant ? Client : Tenant }; var store = new Store();
        var identity = new AndroidDeviceIdentityClient(Options(), session, store);
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => identity.AcquireAsync(true, default)); Assert.Empty(store.Values);
    }
    [Fact]
    public async Task SilentResultCannotSwitchAccount()
    {
        var store = new Store(); store.Values[Options().SelectionKey] = "account-a";
        var session = new Session { Accounts = ["account-a"], NextAccount = "foreign" };
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => new AndroidDeviceIdentityClient(Options(), session, store).AcquireAsync(false, default));
    }
    [Fact]
    public async Task CancelledLateProviderResultNeverPersistsSelection()
    {
        var session = new Session { Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) }; var store = new Store();
        using var cancel = new CancellationTokenSource();
        var operation = new AndroidDeviceIdentityClient(Options(), session, store).AcquireAsync(true, cancel.Token);
        await session.Started.Task; cancel.Cancel(); session.Pending.SetResult(session.Result());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation); Assert.Empty(store.Values);
    }
    [Fact]
    public async Task CancellationDuringSecureWriteRemovesSelection()
    {
        using var cancel = new CancellationTokenSource(); var store = new Store { Written = cancel.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AndroidDeviceIdentityClient(Options(), new Session(), store).AcquireAsync(true, cancel.Token));
        Assert.Empty(store.Values);
    }
    [Fact]
    public async Task SignOutFailsClosedEvenWhenSecureStorageFails()
    {
        var store = new Store(); var session = new Session(); var identity = new AndroidDeviceIdentityClient(Options(), session, store);
        await identity.AcquireAsync(true, default); store.FailRemove = true;
        await Assert.ThrowsAsync<DeviceIdentityUnavailableException>(identity.SignOutAsync);
        Assert.Null(await identity.AcquireAsync(false, default)); Assert.Equal(1, session.Removals);
    }
    [Fact]
    public async Task SignOutWaitingForInteractiveResultStillFailsClosedIfBothCachesFailToClear()
    {
        var store = new Store { FailRemove = true };
        var session = new Session { Accounts = ["account-a"], FailRemove = true, Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var identity = new AndroidDeviceIdentityClient(Options(), session, store);
        var signIn = identity.AcquireAsync(true, default);
        await session.Started.Task;
        var signOut = identity.SignOutAsync();
        session.Pending.SetResult(session.Result());
        await signIn;
        await Assert.ThrowsAsync<DeviceIdentityUnavailableException>(() => signOut);
        Assert.Null(await identity.AcquireAsync(false, default));
        Assert.Equal(1, session.Calls);
    }
    [Fact]
    public async Task BackgroundRefusesInteractiveAuthentication()
    {
        var session = new Session(); var identity = new AndroidDeviceIdentityClient(Options(), session, new Store()); identity.SetForeground(false);
        await Assert.ThrowsAsync<DeviceIdentityUnavailableException>(() => identity.AcquireAsync(true, default)); Assert.Equal(0, session.Calls);
    }
    [Fact]
    public void InvalidAuthRetainsOfflineWritingButInvalidBackendFails()
    {
        var metadata = new Dictionary<string, string?> { ["ProsaEnvironment"] = "Production", ["ProsaApiBaseUrl"] = "https://api.example/", ["ProsaAndroidSignatureHash"] = Hash };
        using var badAuth = new MemoryStream(Encoding.UTF8.GetBytes("{invalid"));
        Assert.False(AndroidBuildConfiguration.ReadSettings(badAuth, metadata, AndroidIdentityOptions.PackageId).IsConfigured);
        using var auth = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(Options().Authentication));
        Assert.True(AndroidBuildConfiguration.ReadSettings(auth, metadata, AndroidIdentityOptions.PackageId).IsConfigured);
        metadata["ProsaApiBaseUrl"] = "http://api.example/"; auth.Position = 0;
        Assert.Throws<ArgumentException>(() => AndroidBuildConfiguration.ReadSettings(auth, metadata, AndroidIdentityOptions.PackageId));
    }
    [Theory]
    [InlineData(3200)] [InlineData(32000)] [InlineData(1920000)]
    public void MicrophonePcmProducesBackendCompatibleWav(int length)
    {
        byte[] pcm = new byte[length]; pcm[0] = 100;
        var wav = AndroidPcmWav.Encode(pcm);
        Assert.Equal(length + 44, wav.Length); Assert.Equal(pcm, wav[44..]);
        Assert.Equal(length / 32000d, BrainstormingSpeech.ValidateWav(wav).Seconds);
    }
    [Theory]
    [InlineData(0)] [InlineData(3198)] [InlineData(3201)] [InlineData(1920002)]
    public void MicrophoneRejectsUnboundedOrIncompleteFrames(int length) => Assert.Throws<InvalidDataException>(() => AndroidPcmWav.Encode(new byte[length]));
}
