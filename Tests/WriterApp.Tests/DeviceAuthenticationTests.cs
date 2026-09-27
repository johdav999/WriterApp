using System.Net;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceAuthenticationTests
{
    [Fact]
    public async Task RestoreIsSilentAndSignOutClearsCredentialsWithoutDeletingDocuments()
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.AuthTests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new LocalDocumentRepository(new FileLocalDocumentStore(root));
            var document = await repository.CreateAsync("Offline writing");
            var identity = new FakeIdentity();
            var account = new DeviceAccountService(identity);
            await account.RestoreAsync();
            Assert.True(account.IsSignedIn);
            Assert.False(identity.LastInteractive);
            await account.SignOutAsync();
            Assert.False(identity.HasCredentials);
            Assert.False(account.IsSignedIn);
            Assert.Null(account.DisplayName);
            Assert.NotNull(await repository.LoadAsync(document.DocumentId));
            Assert.False(new DeviceAccountService(identity).IsSignedIn);
            await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => account.GetTokenAsync(default));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("https://attacker.example/api/documents")]
    [InlineData("http://api.example/api/documents")]
    [InlineData("https://api.example:444/api/documents")]
    [InlineData("https://api.example/.auth/me")]
    [InlineData("https://user@api.example/api/documents")]
    public async Task TokensNeverLeaveConfiguredApiOrigin(string destination)
    {
        var identity = new FakeIdentity();
        var transport = new Transport();
        using var client = Client(new(identity), transport);
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync(destination));
        Assert.Equal(0, identity.Acquisitions);
        Assert.Equal(0, transport.Calls);
    }

    [Fact]
    public async Task UnauthorizedMutationIsNotReplayedAndRequiresExplicitSignIn()
    {
        var identity = new FakeIdentity();
        var account = new DeviceAccountService(identity);
        var transport = new Transport { Status = HttpStatusCode.Unauthorized };
        using var client = Client(account, transport);
        using var response = await client.PostAsync("api/documents", new StringContent("test"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(1, transport.Calls);
        Assert.Equal("Bearer", transport.Scheme);
        Assert.False(identity.LastInteractive);
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => client.GetAsync("api/documents"));
        Assert.Equal(1, transport.Calls);
        await account.SignInAsync();
        Assert.True(identity.LastInteractive);
        transport.Status = HttpStatusCode.OK;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/documents")).StatusCode);
    }

    [Fact]
    public async Task OldUnauthorizedResponseCannotRejectNewSignIn()
    {
        var account = new DeviceAccountService(new FakeIdentity());
        await account.SignInAsync();
        long old = account.Generation;
        await account.SignInAsync();
        account.RejectSession(old);
        Assert.True(account.IsSignedIn);
    }

    [Fact]
    public async Task ExpiredTokenNeverReachesBackendAndNetworkFailureKeepsCredentials()
    {
        var identity = new FakeIdentity { Expired = true };
        var account = new DeviceAccountService(identity);
        var transport = new Transport();
        using var client = Client(account, transport);
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => client.GetAsync("api/documents"));
        Assert.Equal(0, transport.Calls);
        identity.Expired = false;
        await account.SignInAsync();
        identity.Offline = true;
        await account.RestoreAsync();
        Assert.True(identity.HasCredentials);
        Assert.Contains("local editing", account.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignOutFailureIsVisibleAndSuppressesFurtherSilentTokenUse()
    {
        var identity = new FakeIdentity { FailRemove = true };
        var account = new DeviceAccountService(identity);
        await account.SignInAsync();
        await Assert.ThrowsAsync<DeviceIdentityUnavailableException>(() => account.SignOutAsync());
        Assert.False(account.IsSignedIn);
        Assert.Contains("could not be removed", account.Message);
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(() => account.GetTokenAsync(default));
        identity.FailRemove = false;
        await account.SignOutAsync();
        Assert.False(identity.HasCredentials);
    }

    [Fact]
    public async Task ForbiddenResponseDoesNotEraseValidSession()
    {
        var account = new DeviceAccountService(new FakeIdentity());
        using var client = Client(account, new Transport { Status = HttpStatusCode.Forbidden });
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("api/documents")).StatusCode);
        Assert.True(account.IsSignedIn);
    }

    [Fact]
    public void ConfigurationRejectsRemoteRedirectAndTokensAreRedacted()
    {
        var valid = new DeviceAuthOptions
        {
            TenantId = Guid.NewGuid().ToString(), ClientId = Guid.NewGuid().ToString(),
            Authority = "https://tenant.ciamlogin.com/", Scopes = ["api://api-id/access_as_user"]
        };
        Assert.True(valid.IsConfigured);
        Assert.False(new DeviceAuthOptions { Authority = valid.Authority, TenantId = valid.TenantId,
            ClientId = valid.ClientId, Scopes = valid.Scopes, RedirectUri = "https://attacker.example" }.IsConfigured);
        Assert.DoesNotContain("credential", new DeviceAccessToken("credential", DateTimeOffset.MaxValue, "Writer").ToString());
    }

    private static HttpClient Client(DeviceAccountService account, Transport transport) => new(
        new DeviceAuthenticatedHandler(account, new("https://api.example/")) { InnerHandler = transport })
        { BaseAddress = new("https://api.example/") };

    private sealed class FakeIdentity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public bool HasCredentials { get; private set; } = true;
        public bool Expired { get; set; }
        public bool Offline { get; set; }
        public bool FailRemove { get; set; }
        public bool LastInteractive { get; private set; }
        public int Acquisitions { get; private set; }
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken)
        {
            Acquisitions++; LastInteractive = interactive;
            cancellationToken.ThrowIfCancellationRequested();
            if (Offline) throw new DeviceIdentityUnavailableException();
            if (interactive) HasCredentials = true;
            return Task.FromResult(HasCredentials ? new DeviceAccessToken("synthetic-token",
                Expired ? DateTimeOffset.MinValue : DateTimeOffset.MaxValue, "Writer") : null);
        }
        public Task SignOutAsync()
        {
            if (FailRemove) throw new DeviceIdentityUnavailableException();
            HasCredentials = false; return Task.CompletedTask;
        }
    }
    private sealed class Transport : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;
        public int Calls { get; private set; }
        public string? Scheme { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; Scheme = request.Headers.Authorization?.Scheme; return Task.FromResult(new HttpResponseMessage(Status)); }
    }
}
