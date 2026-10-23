using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace WriterApp.Tests;

public sealed class CollaborationBrowserFactAttribute : FactAttribute
{
    public CollaborationBrowserFactAttribute() { if (Environment.GetEnvironmentVariable("WRITERAPP_COLLAB_BROWSER_TESTS") != "1") Skip = "Set WRITERAPP_COLLAB_BROWSER_TESTS=1 with the documented Playwright/Edge runtime."; }
}
public sealed partial class CollaborationSessionTests
{
    [CollaborationBrowserFact]
    public async Task ActualWebAndDeviceBundlesRecoverJournalAndReplayThroughAuthenticatedTransport()
    {
        _clock.RealTime = true; _settings.CurrentValue.CommitsEnabled = true;
        var start = new ProcessStartInfo("node") { WorkingDirectory = FindRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("WriterApp.Collaboration/tests/schema-fixture.mjs");
        using var fixture = Process.Start(start)!; string json = await fixture.StandardOutput.ReadToEndAsync(); string error = await fixture.StandardError.ReadToEndAsync();
        await fixture.WaitForExitAsync(); Assert.True(fixture.ExitCode == 0, error);
        using var metadata = JsonDocument.Parse(json); string fingerprint = metadata.RootElement.GetProperty("support").GetProperty("schemaFingerprint").GetString()!;
        byte[] seed = Convert.FromBase64String(metadata.RootElement.GetProperty("state").GetString()!);
        await using (var db = Open())
        {
            await db.CollaborationPages.ExecuteUpdateAsync(p => p.SetProperty(x => x.SchemaFingerprint, fingerprint));
            await db.CollaborationSnapshots.ExecuteUpdateAsync(p => p.SetProperty(x => x.State, seed).SetProperty(x => x.Sha256, Hash(seed)));
        }
        await RunProbe("journal-browser-probe.mjs", Request with { SchemaFingerprint = fingerprint, RequestCommit = true });
        await using var inspect = Open(); Assert.Equal("<p>Legacy retained</p>", (await inspect.Pages.SingleAsync()).Content);
    }
}
