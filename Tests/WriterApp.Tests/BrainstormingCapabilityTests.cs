using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using WriterApp.Application.Security;
using WriterApp.Client.Services;
using WriterApp.Controllers;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingCapabilityTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct); }
    private static HttpClient Http(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
        new(new Handler(send)) { BaseAddress = new("https://test.invalid/") };
    private static HttpResponseMessage Current() => new(HttpStatusCode.OK) { Content = JsonContent.Create(BrainstormingCapabilities.Current) };

    [Theory]
    [InlineData(404, BrainstormingCapabilityState.UnsupportedServer)][InlineData(405, BrainstormingCapabilityState.UnsupportedServer)]
    [InlineData(501, BrainstormingCapabilityState.UnsupportedServer)][InlineData(401, BrainstormingCapabilityState.AuthenticationRequired)]
    [InlineData(403, BrainstormingCapabilityState.AuthenticationRequired)][InlineData(500, BrainstormingCapabilityState.Unavailable)]
    public async Task DiscoveryIsOneReadWithoutLegacyGenerationFallback(int status, BrainstormingCapabilityState state)
    {
        int calls = 0;
        using var http = Http((r, _) => { calls++; Assert.Equal(HttpMethod.Get, r.Method); Assert.Equal("/" + BrainstormingCapabilityTransport.Route, r.RequestUri!.AbsolutePath);
            Assert.Null(r.Content); return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)); });
        using var request = new HttpRequestMessage(HttpMethod.Get, BrainstormingCapabilityTransport.Route);
        var result = await BrainstormingCapabilityTransport.ReadAsync(http, request);
        Assert.Equal(state, result.Conversation.State); Assert.False(result.ManuscriptApplication.IsAvailable); Assert.NotEmpty(result.Conversation.Message); Assert.Equal(1, calls);
    }
    [Theory]
    [InlineData("schema")][InlineData("oversize")][InlineData("markup")][InlineData("inconsistent")][InlineData("unknown")]
    public async Task MalformedCapabilityResponsesFailClosed(string problem)
    {
        using var http = Http((_, _) => Task.FromResult<HttpResponseMessage>(problem switch
        {
            "schema" => new(HttpStatusCode.OK) { Content = JsonContent.Create(BrainstormingCapabilities.Current with { Version = 2 }) },
            "inconsistent" => new(HttpStatusCode.OK) { Content = JsonContent.Create(BrainstormingCapabilities.Current with { Conversation = new(BrainstormingCapabilityState.NotImplemented, "Unavailable") }) },
            "unknown" => new(HttpStatusCode.OK) { Content = new StringContent("{\"unexpected\":1}") },
            "oversize" => new(HttpStatusCode.OK) { Content = new StringContent(new('x', BrainstormingCapabilityTransport.MaxResponseBytes + 1)) },
            _ => new(HttpStatusCode.OK) { Content = new StringContent("<html>Old login page</html>") }
        }));
        using var request = new HttpRequestMessage(HttpMethod.Get, BrainstormingCapabilityTransport.Route);
        Assert.Equal(BrainstormingCapabilityState.Unavailable, (await BrainstormingCapabilityTransport.ReadAsync(http, request)).Conversation.State);
    }
    [Fact]
    public async Task CancellationPropagatesAndNetworkFailureIsActionable()
    {
        using var http = Http((_, _) => throw new HttpRequestException());
        using var request = new HttpRequestMessage(HttpMethod.Get, BrainstormingCapabilityTransport.Route);
        Assert.Equal(BrainstormingCapabilityState.Unavailable, (await BrainstormingCapabilityTransport.ReadAsync(http, request)).Conversation.State);
        using var cancelled = new HttpRequestMessage(HttpMethod.Get, BrainstormingCapabilityTransport.Route);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BrainstormingCapabilityTransport.ReadAsync(http, cancelled, new(true)));
    }
    [Fact]
    public async Task ProductionEndpointRequiresAuthenticationAndReportsActualCurrentSupport()
    {
        using var host = new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.Configure<WriterApp.AI.Abstractions.WriterAiOptions>(o => o.Enabled = true);
            services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("test", _ => { });
            services.AddAuthorization(); services.AddControllers().AddApplicationPart(typeof(BrainstormingCapabilitiesController).Assembly);
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); })).Build();
        await host.StartAsync(); using var http = host.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(BrainstormingCapabilityTransport.Route)).StatusCode);
        http.DefaultRequestHeaders.Add("X-Test-Identity", "synthetic-owner");
        var result = await http.GetFromJsonAsync<BrainstormingCapabilities>(BrainstormingCapabilityTransport.Route);
        Assert.NotNull(result); result.Validate(); Assert.Equal(BrainstormingCapabilityState.Available, result.Conversation.State);
        Assert.True(result.Sessions.IsAvailable); Assert.False(result.Dictation.IsAvailable); Assert.True(result.Streaming.IsAvailable);
    }
    public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(!Request.Headers.ContainsKey("X-Test-Identity")
            ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(
                new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "synthetic-owner"), new Claim("oid", "synthetic-owner")], "test")), "test")));
    }
    [Fact]
    public async Task WebAdapterUsesProductionDiscoveryAndRefusesLateSignedOutResults()
    {
        AuthStateService? auth = null; bool change = false; int discovery = 0;
        using var http = Http((r, _) =>
        {
            if (r.RequestUri!.AbsolutePath == "/api/auth/me") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = JsonContent.Create(new AuthMeDto { IsAuthenticated = true, UserId = "synthetic-owner" }) });
            discovery++; if (change) auth!.Reset(); return Task.FromResult(Current());
        });
        auth = new(http, NullLogger<AuthStateService>.Instance, new(), new());
        var service = new WebBrainstormingCapabilities(http, auth);
        var result = await service.ReadAsync(); Assert.Equal(BrainstormingCapabilityState.Available, result.Conversation.State);
        Assert.Equal(BrainstormingCapabilityState.UnsupportedPlatform, result.Dictation.State);
        change = true; await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReadAsync());
        Assert.Equal(BrainstormingCapabilityState.AuthenticationRequired, (await service.ReadAsync()).Conversation.State); Assert.Equal(2, discovery);
    }
    [Fact]
    public async Task DeviceAdapterChecksAccountGenerationOfflineAndSpeechBoundary()
    {
        var account = new DeviceAccountService(new Identity()); await account.SignInAsync(); var network = new DeviceConnectivity(); int calls = 0; bool change = false;
        using var http = Http(async (r, _) =>
        {
            calls++; Assert.True(r.Options.TryGetValue(DeviceAuthenticatedHandler.ExpectedAccountGeneration, out var generation));
            Assert.Equal(account.Generation, generation); if (change) await account.SignOutAsync(); return Current();
        });
        var service = new DeviceBrainstormingCapabilities(http, account, network);
        var result = await service.ReadAsync(); Assert.Equal(BrainstormingCapabilityState.Available, result.Conversation.State);
        Assert.Equal(BrainstormingCapabilityState.UnsupportedPlatform, result.SpokenDialogue.State);
        network.SetOnline(false); Assert.Equal(BrainstormingCapabilityState.Offline, (await service.ReadAsync()).Conversation.State); Assert.Equal(1, calls);
        network.SetOnline(true); change = true; await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ReadAsync());
        Assert.Equal(BrainstormingCapabilityState.AuthenticationRequired, (await service.ReadAsync()).Conversation.State); Assert.Equal(2, calls);
    }
    private sealed class Identity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken) =>
            Task.FromResult<DeviceAccessToken?>(new("synthetic-token", DateTimeOffset.UtcNow.AddHours(1), "Synthetic writer", "synthetic-owner"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
}
