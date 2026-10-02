using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Feedback;
using WriterApp.Application.Security;
using WriterApp.Device.Shared.Services;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceAccountOverviewTests
{
    private static readonly AuthMeDto Profile = new() { IsAuthenticated = true, UserId = "test-owner", Name = "Test writer", PlanKey = "Standard", EffectivePlanKey = "Standard", IsPaidAccessActive = true, AiMonthlyTokenBudget = 100, AiTokensUsedThisPeriod = 100 };
    [Fact]
    public async Task RefreshUsesProtectedProbeAndProfileCacheIsDisplayOnlyAndIsolated()
    {
        var account = await SignedIn(); var network = new DeviceConnectivity(); var calls = new List<string>();
        using var http = Http(async request => { calls.Add(request.RequestUri!.PathAndQuery); await Task.CompletedTask; return new(HttpStatusCode.OK) { Content = JsonContent.Create(Profile) }; });
        using var overview = new DeviceAccountOverview(http, account, network);
        await overview.RefreshAsync(); Assert.Equal(new[] { "/api/native/session", "/api/auth/me?force=1" }, calls);
        Assert.Equal(DeviceAccountStatus.Ready, overview.Status); Assert.NotNull(overview.CheckedAt); Assert.False(overview.IsStale);
        Assert.Equal("Standard", overview.Profile!.PlanKey);
        network.SetOnline(false); await overview.RefreshAsync(); Assert.True(overview.IsStale); Assert.NotNull(overview.Profile); Assert.Equal(2, calls.Count);
        await account.SignOutAsync(); Assert.Null(overview.Profile);
        await account.SignInAsync(); Assert.Null(overview.Profile);
    }
    [Theory]
    [InlineData(401, "", DeviceAccountStatus.Expired)]
    [InlineData(403, "", DeviceAccountStatus.Forbidden)]
    [InlineData(402, "", DeviceAccountStatus.Forbidden)]
    [InlineData(409, "duplicate_account", DeviceAccountStatus.Duplicate)]
    [InlineData(403, "account_deleted", DeviceAccountStatus.Deleted)]
    [InlineData(503, "", DeviceAccountStatus.Unavailable)]
    public async Task BackendFailuresHaveAccurateGuidanceAndNoAssumedPlan(int status, string code, DeviceAccountStatus expected)
    {
        var account = await SignedIn();
        using var http = Http(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = JsonContent.Create(new { code }) }));
        using var overview = new DeviceAccountOverview(http, account, new()); await overview.RefreshAsync();
        Assert.Equal(expected, overview.Status); Assert.Null(overview.Profile); Assert.True(overview.IsStale); Assert.NotEmpty(overview.Message);
    }
    [Fact]
    public async Task AccountSwitchDiscardsLateResponseAndSignedOutRefreshDoesNotCallBackend()
    {
        var account = await SignedIn(); int count = 0;
        using var http = Http(async _ => { count++; await account.SignOutAsync(); await account.SignInAsync(); return new(HttpStatusCode.OK) { Content = JsonContent.Create(Profile) }; });
        using var overview = new DeviceAccountOverview(http, account, new()); await overview.RefreshAsync(); Assert.Null(overview.Profile);
        await account.SignOutAsync(); int previous = count; await overview.RefreshAsync(); Assert.Equal(previous, count); Assert.Equal(DeviceAccountStatus.SignedOut, overview.Status);
    }
    [Fact]
    public async Task TransientFailureRetainsOnlyDatedCachedInformation()
    {
        bool fail = false; var account = await SignedIn();
        using var http = Http(_ => fail ? throw new HttpRequestException() : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(Profile) }));
        using var overview = new DeviceAccountOverview(http, account, new()); await overview.RefreshAsync(); var stamp = overview.CheckedAt;
        fail = true; await overview.RefreshAsync(); Assert.True(overview.IsStale); Assert.Equal(stamp, overview.CheckedAt); Assert.NotNull(overview.Profile);
    }
    [Theory]
    [InlineData("http://app.example/")]
    [InlineData("https://user:secret@app.example/")]
    [InlineData("https://app.example/?token=private")]
    [InlineData("https://app.example/#redirect")]
    public void UnsafeBillingConfigurationCannotLaunch(string uri)
    {
        var launch = new Launcher(); var links = new DeviceAccountLinks(new("Test", new(uri)), launch);
        Assert.Throws<InvalidOperationException>(() => links.Address(AccountDestination.Billing)); Assert.Empty(launch.Opened);
    }
    [Fact]
    public async Task BillingDestinationsAreFixedSameOriginAndNeverIncludeTokensOrReturnUrls()
    {
        var launch = new Launcher(); var links = new DeviceAccountLinks(new("Test", new("https://staging.example/")), launch);
        Assert.Equal("https://staging.example/app/account/billing", links.Address(AccountDestination.Billing).AbsoluteUri);
        Assert.Equal("https://staging.example/start?plan=standard", links.Address(AccountDestination.UpgradeStandard).AbsoluteUri);
        Assert.Equal("https://staging.example/start?plan=pro", links.Address(AccountDestination.UpgradePro).AbsoluteUri);
        Assert.Equal("https://docs.prosa-app.com/", links.Address(AccountDestination.Documentation).AbsoluteUri);
        Assert.Empty(launch.Opened); await links.OpenAsync(AccountDestination.Billing); Assert.Single(launch.Opened);
    }
    [Fact]
    public async Task FeedbackSendsOnlyReviewedFieldsWithoutAutomaticRetry()
    {
        var account = await SignedIn(); int count = 0;
        using var http = Http(async request =>
        {
            count++; Assert.Equal("/api/feedback", request.RequestUri!.AbsolutePath);
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(5, body.RootElement.EnumerateObject().Count()); Assert.False(body.RootElement.GetProperty("includeDiagnostics").GetBoolean());
            Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("diagnostics").ValueKind);
            Assert.Equal("Reviewed text", body.RootElement.GetProperty("description").GetString());
            throw new HttpRequestException("Unknown delivery");
        });
        using var overview = new DeviceAccountOverview(http, account, new());
        await Assert.ThrowsAsync<HttpRequestException>(() => overview.SendFeedbackAsync(new("bug", "Title", "Reviewed text"), default)); Assert.Equal(1, count);
        await account.SignOutAsync(); await Assert.ThrowsAsync<InvalidOperationException>(() => overview.SendFeedbackAsync(new("bug", "Title", "Reviewed text"), default)); Assert.Equal(1, count);
    }
    [Fact]
    public async Task SharedPlanShowsQuotaAndFeedbackStartsInertWithEscapedContent()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var plan = await renderer.RenderComponentAsync<AccountSummary>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Profile"] = Profile, ["Freshness"] = "Cached <response>" }));
            Assert.Contains("AI allowance reached", plan.ToHtmlString()); Assert.Contains("Cached &lt;response&gt;", plan.ToHtmlString());
            var feedback = await renderer.RenderComponentAsync<FeedbackComposer>();
            Assert.Contains("Preview feedback", feedback.ToHtmlString()); Assert.DoesNotContain("Send reviewed feedback", feedback.ToHtmlString());
        });
    }
    private static HttpClient Http(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) => new(new Handler(handler)) { BaseAddress = new("https://test.invalid/") };
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => handler(request);
    }
    private sealed class Launcher : IDeviceExternalLauncher { public List<Uri> Opened { get; } = []; public Task OpenAsync(Uri address) { Opened.Add(address); return Task.CompletedTask; } }
    private static async Task<DeviceAccountService> SignedIn() { var service = new DeviceAccountService(new Identity()); await service.SignInAsync(); return service; }
    private sealed class Identity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken) => Task.FromResult<DeviceAccessToken?>(new("test", DateTimeOffset.UtcNow.AddHours(1), "Test writer"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
}
