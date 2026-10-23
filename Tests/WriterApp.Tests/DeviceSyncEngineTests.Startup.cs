using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceSyncEngineTests
{
    [Fact]
    public async Task StartupRestoresAccountAndPullsCloudWithoutUploadingLocalOnlyWriting()
    {
        var remote = await Store.CreateAsync("Cloud manuscript");
        await Engine().EnableAsync(remote.DocumentId);
        var local = await Store.CreateAsync("Local manuscript");
        _api.EditRemote("<p>Updated in cloud</p>");
        var identity = new StartupIdentity();
        var engine = Engine(account: new DeviceAccountService(identity));
        var first = engine.StartAsync();
        Assert.Same(first, engine.StartAsync());
        await first;
        Assert.Equal(1, identity.Restores);
        Assert.Equal("<p>Updated in cloud</p>", Text((await Store.GetAsync(remote.DocumentId))!));
        Assert.Null((await Store.GetAsync(local.DocumentId))!.ServerDocumentId);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task EnablingCloudLinksActiveLocalManuscriptsAndSkipsTrashAndExistingLinks()
    {
        var linked = await Store.CreateAsync("Already linked");
        var engine = Engine();
        await engine.EnableAsync(linked.DocumentId);
        var local = await Store.CreateAsync("Local only");
        var trashed = await Store.CreateAsync("Trash");
        await Store.MoveToTrashAsync(trashed.DocumentId, trashed.LocalRevision);
        await engine.EnableLocalDocumentsAsync();
        await engine.EnableLocalDocumentsAsync();
        Assert.NotNull((await Store.GetAsync(local.DocumentId))!.ServerDocumentId);
        Assert.Null((await Store.GetAsync(trashed.DocumentId))!.ServerDocumentId);
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task StartupOfflineRetainsWritingAndSynchronizesAfterReconnect()
    {
        var doc = await Store.CreateAsync("Linked");
        await Engine().EnableAsync(doc.DocumentId);
        await EditAsync(doc.DocumentId, "<p>Offline edit</p>");
        _network.SetOnline(false);
        var clock = new StepTime();
        var engine = Engine(clock, account: new DeviceAccountService(new StartupIdentity()));
        await engine.StartAsync();
        Assert.Contains("Offline", engine.Message);
        Assert.Single(_api.Requests);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.Changed += () => { if (!engine.IsRunning && engine.Message == "Synchronization complete.") finished.TrySetResult(); };
        _network.SetOnline(true);
        await clock.TickAsync();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal("<p>Offline edit</p>", Text((await Store.GetAsync(doc.DocumentId))!));
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task StartupRecommendationIsVisibleOutsideSettingsForLocalOnlyWriting()
    {
        await Store.CreateAsync("Local manuscript");
        using var services = RenderServices();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DeviceCloudStartup>());
        string html = await renderer.Dispatcher.InvokeAsync(output.ToHtmlString);
        Assert.Contains("saved only on this device", html);
        Assert.Contains("Enable cloud sync</button>", html);
        Assert.False(ButtonDisabled(html, "Enable cloud sync"));
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task SignedOutStartupDoesNotOpenInteractiveSignIn()
    {
        var identity = new StartupIdentity { HasSession = false };
        var account = new DeviceAccountService(identity);
        await Engine(account: account).StartAsync();
        Assert.False(account.IsSignedIn);
        Assert.Equal(1, identity.Restores);
        Assert.Empty(_api.Requests);
    }

    [Fact]
    public async Task EnableButtonUploadsLocalWritingAndRemovesRecommendation()
    {
        var doc = await Store.CreateAsync("Local manuscript");
        var components = new StartupComponents();
        using var services = RenderServices(components);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<DeviceCloudStartup>();
            Assert.Contains("Enable cloud sync", output.ToHtmlString());
            await (Task)typeof(DeviceCloudStartup).GetMethod("EnableAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(components.Startup, [])!;
            typeof(ComponentBase).GetMethod("StateHasChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(components.Startup, []);
            Assert.DoesNotContain("Enable cloud sync", output.ToHtmlString());
        });
        Assert.Single(_api.Requests);
        Assert.NotNull((await Store.GetAsync(doc.DocumentId))!.ServerDocumentId);
    }

    [Fact]
    public async Task OfflineRecommendationExplainsWhyEnableButtonIsDisabled()
    {
        await Store.CreateAsync("Local manuscript");
        using var services = RenderServices();
        services.GetRequiredService<DeviceConnectivity>().SetOnline(false);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var output = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DeviceCloudStartup>());
        string html = await renderer.Dispatcher.InvokeAsync(output.ToHtmlString);
        Assert.Contains("Connect to the internet", html);
        Assert.True(ButtonDisabled(html, "Enable cloud sync"));
        Assert.Empty(_api.Requests);
    }

    private sealed class StartupComponents : IComponentActivator
    {
        public DeviceCloudStartup Startup = default!;
        public IComponent CreateInstance(Type type)
        {
            var component = (IComponent)Activator.CreateInstance(type)!;
            if (component is DeviceCloudStartup startup) Startup = startup;
            return component;
        }
    }

    private sealed class StartupIdentity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public bool HasSession = true;
        public int Restores;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken cancellationToken)
        {
            Assert.False(interactive);
            Restores++;
            return Task.FromResult<DeviceAccessToken?>(HasSession ? new("test", DateTimeOffset.UtcNow.AddHours(1), "Test", "paid") : null);
        }
        public Task SignOutAsync() => Task.CompletedTask;
    }
}
