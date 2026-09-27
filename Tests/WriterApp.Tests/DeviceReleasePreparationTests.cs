using System.Net;
using System.Net.Http.Json;
using System.Text;
using WriterApp.Device.Shared.Services;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceReleasePreparationTests
{
    [Fact]
    public void EnvironmentConfigurationRequiresSafeBackendAndUpdateHosts()
    {
        var production = DeviceEnvironmentConfiguration.Create("Production", "https://app.prosa-app.com/",
            "https://app.prosa-app.com/desktop/updates/stable.json");
        Assert.Equal(DeviceEnvironment.Production, production.Environment);
        Assert.Throws<ArgumentException>(() => DeviceEnvironmentConfiguration.Create("Production", "http://app.prosa-app.com/", null));
        Assert.Throws<ArgumentException>(() => DeviceEnvironmentConfiguration.Create("Production", "https://user:password@app.prosa-app.com/", null));
        Assert.Throws<ArgumentException>(() => DeviceEnvironmentConfiguration.Create("0", "https://app.prosa-app.com/", null));
        Assert.Throws<ArgumentException>(() => DeviceEnvironmentConfiguration.Create("Staging", "", null));
        Assert.Throws<ArgumentException>(() => DeviceEnvironmentConfiguration.Create("Production", "https://app.prosa-app.com/",
            "https://other.example/updates.json"));
        Assert.Throws<ArgumentException>(() => DeviceEnvironmentConfiguration.Create("Production", "https://app.prosa-app.com/",
            "https://app.prosa-app.com:444/updates.json"));
        Assert.Equal(DeviceEnvironment.Development,
            DeviceEnvironmentConfiguration.Create("Development", "http://localhost:5387/", null).Environment);
    }

    [Fact]
    public async Task UpdateCheckUsesSameHostHttpsLinkAndDoesNotAutoOpenIt()
    {
        var config = DeviceEnvironmentConfiguration.Create("Production", "https://app.prosa-app.com/",
            "https://app.prosa-app.com/desktop/updates/stable.json");
        var launcher = new FakeLauncher();
        int calls = 0;
        var service = new DeviceUpdateService(Client(request =>
        {
            calls++;
            Assert.Equal(config.UpdateManifestAddress, request.RequestUri);
            Assert.Null(request.Headers.Authorization);
            return new(HttpStatusCode.OK)
            { Content = JsonContent.Create(new { version = "0.2.0", downloadUrl = "https://app.prosa-app.com/desktop/Prosa.msix" }) };
        }), config, "0.1.0", launcher);
        DeviceUpdateResult result = await service.CheckAsync();
        Assert.Equal(DeviceUpdateState.Available, result.State);
        Assert.Equal(1, calls);
        Assert.Null(launcher.Opened);
        await service.OpenInstallerPageAsync(result);
        Assert.Equal(result.DownloadAddress, launcher.Opened);
    }

    [Theory]
    [InlineData("http://app.prosa-app.com/Prosa.msix")]
    [InlineData("https://other.example/Prosa.msix")]
    [InlineData("javascript:alert(1)")]
    public async Task UnsafeUpdateLinksAreNeverOpened(string link)
    {
        var config = DeviceEnvironmentConfiguration.Create("Production", "https://app.prosa-app.com/",
            "https://app.prosa-app.com/desktop/updates/stable.json");
        var launcher = new FakeLauncher();
        var service = new DeviceUpdateService(Client(_ => new(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { version = "0.2.0", downloadUrl = link }) }), config, "0.1.0", launcher);
        DeviceUpdateResult result = await service.CheckAsync();
        Assert.Equal(DeviceUpdateState.Unavailable, result.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.OpenInstallerPageAsync(result));
        Assert.Null(launcher.Opened);
    }

    [Fact]
    public async Task CurrentVersionAndMissingFeedAreReportedWithoutInstalling()
    {
        var config = DeviceEnvironmentConfiguration.Create("Development", "https://localhost:7384/", null);
        var launcher = new FakeLauncher();
        var absent = new DeviceUpdateService(Client(_ => throw new Exception("No request expected.")), config,
            "0.1.0", launcher);
        Assert.Equal(DeviceUpdateState.Unavailable, (await absent.CheckAsync()).State);
        config = DeviceEnvironmentConfiguration.Create("Production", "https://app.prosa-app.com/",
            "https://app.prosa-app.com/desktop/updates/stable.json");
        var current = new DeviceUpdateService(Client(_ => new(HttpStatusCode.OK)
        { Content = JsonContent.Create(new { version = "0.1.0", downloadUrl = "https://app.prosa-app.com/desktop/Prosa.msix" }) }),
            config, "0.1.0.1", launcher);
        Assert.Equal(DeviceUpdateState.Current, (await current.CheckAsync()).State);
        Assert.Null(launcher.Opened);
    }

    [Fact]
    public async Task DiagnosticsKeepOnlyAllowlistedFieldsAndRotate()
    {
        using var fixture = new Fixture();
        var logs = new DeviceDiagnostics(fixture.Root, "0.1.0", DeviceEnvironment.Production, maxFileBytes: 512);
        const string secret = "Bearer secret-token and private document body";
        await logs.RecordAsync(DeviceDiagnosticEvent.SaveFailed, error: new IOException(secret));
        byte[] first = await logs.ExportAsync();
        string content = Encoding.UTF8.GetString(first);
        Assert.Contains("SaveFailed", content);
        Assert.Contains("Storage", content);
        Assert.Contains("0.1.0", content);
        Assert.DoesNotContain("secret-token", content);
        Assert.DoesNotContain("private document body", content);
        await logs.RecordAsync(DeviceDiagnosticEvent.SyncChanged,
            new DeviceSyncDiagnosticSnapshot(false, 2, 1, 0, 1, DateTimeOffset.UtcNow));
        Assert.Contains("\"conflicts\":1", Encoding.UTF8.GetString(await logs.ExportAsync()));
        for (int index = 0; index < 20; index++)
            await logs.RecordAsync(DeviceDiagnosticEvent.UpdateChecked);
        Assert.InRange(Directory.GetFiles(fixture.Root, "*.jsonl").Length, 1, 4);
    }

    [Fact]
    public async Task DuplicateSyncSnapshotsDoNotFloodDiagnostics()
    {
        using var fixture = new Fixture();
        var logs = new DeviceDiagnostics(fixture.Root, "0.1.0", DeviceEnvironment.Development);
        var status = new DeviceSyncDiagnosticSnapshot(true, 1, 1, 0, 0, null);
        await logs.RecordAsync(DeviceDiagnosticEvent.SyncChanged, status);
        await logs.RecordAsync(DeviceDiagnosticEvent.SyncChanged, status);
        Assert.Single(Encoding.UTF8.GetString(await logs.ExportAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new Handler(respond)) { BaseAddress = new Uri("https://app.prosa-app.com/") };
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request)); }
    private sealed class FakeLauncher : IDeviceExternalLauncher
    {
        public Uri? Opened { get; private set; }
        public Task OpenAsync(Uri address) { Opened = address; return Task.CompletedTask; }
    }
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "WriterApp_DiagnosticTests_" + Guid.NewGuid().ToString("N"));
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
