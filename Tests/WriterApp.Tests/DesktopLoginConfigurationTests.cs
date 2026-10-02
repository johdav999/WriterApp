using System.Reflection;
using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Security;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using Xunit;

namespace WriterApp.Tests;

public sealed class DesktopLoginConfigurationTests
{
    private static Stream Resource(string name) => typeof(DesktopLoginConfigurationTests).Assembly
        .GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Missing resource {name}");

    [Fact]
    public void ShippedDesktopAndServerUseTheSamePermittedRegistration()
    {
        using var desktopJson = Resource("Prosa.DesktopAuthTestSettings");
        var desktop = DeviceAuthOptions.FromSettings(JsonSerializer.Deserialize<DeviceAuthOptions>(desktopJson)!, _ => null);
        using var serverJson = Resource("Prosa.ServerAuthTestSettings");
        using var settings = JsonDocument.Parse(serverJson);
        var server = settings.RootElement.GetProperty("NativeAuth").Deserialize<NativeBearerOptions>()!;
        Assert.True(desktop.IsConfigured);
        Assert.True(server.Enabled);
        server.Validate();
        Assert.Equal(server.TenantId, desktop.TenantId);
        Assert.Contains(desktop.ClientId, server.AllowedClientIds);
        Assert.Equal($"api://{server.Audience}/{server.RequiredScope}", Assert.Single(desktop.Scopes));
    }

    [Fact]
    public async Task ConfiguredSignedOutMenuEnablesBrowserLoginWithoutOpeningBrowserDuringRestore()
    {
        using var desktopJson = Resource("Prosa.DesktopAuthTestSettings");
        var options = DeviceAuthOptions.FromSettings(JsonSerializer.Deserialize<DeviceAuthOptions>(desktopJson)!, _ => null);
        var identity = new SignedOutIdentity(options);
        var account = new DeviceAccountService(identity);
        using var http = new HttpClient();
        using var overview = new DeviceAccountOverview(http, account, new());
        using var services = new ServiceCollection().AddLogging().AddSingleton(account).AddSingleton(overview).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<DeviceAccountMenu>();
            var document = new HtmlParser().ParseDocument(component.ToHtmlString());
            var button = Assert.Single(document.QuerySelectorAll("button"), b => b.TextContent == "Sign in with browser");
            Assert.False(button.HasAttribute("disabled"));
            Assert.DoesNotContain("not configured", document.Body!.TextContent);
        });
        Assert.Equal(1, identity.SilentCalls);
        Assert.Equal(0, identity.InteractiveCalls);
    }

    private sealed class SignedOutIdentity(DeviceAuthOptions options) : IDeviceIdentityClient
    {
        public bool IsConfigured => options.IsConfigured;
        public int SilentCalls { get; private set; }
        public int InteractiveCalls { get; private set; }
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct)
        {
            if (interactive) InteractiveCalls++; else SilentCalls++;
            return Task.FromResult<DeviceAccessToken?>(null);
        }
        public Task SignOutAsync() => Task.CompletedTask;
    }
}
