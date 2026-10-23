using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationSessionTests
{
    private sealed class ShareOptions : IOptionsMonitor<CollaborationSharingOptions>
    {
        public CollaborationSharingOptions CurrentValue { get; } = new();
        public CollaborationSharingOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<CollaborationSharingOptions, string?> listener) => null;
    }
    private readonly ShareOptions _sharingOptions = new();
    private readonly CollaborationInteractionOptions _interactions = new();
    [Fact]
    public async Task RealPresenceUsesAuthenticatedIdentityAndRetiresRevokedCursors()
    {
        _clock.RealTime = true; _sharingOptions.CurrentValue.Enabled = true; _interactions.Enabled = true;
        var start = new ProcessStartInfo("node") { WorkingDirectory = FindRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("WriterApp.Collaboration/tests/schema-fixture.mjs");
        using var fixture = Process.Start(start)!; string json = await fixture.StandardOutput.ReadToEndAsync(); string error = await fixture.StandardError.ReadToEndAsync(); await fixture.WaitForExitAsync(); Assert.True(fixture.ExitCode == 0, error);
        using var metadata = JsonDocument.Parse(json); string fingerprint = metadata.RootElement.GetProperty("support").GetProperty("schemaFingerprint").GetString()!;
        byte[] seed = Convert.FromBase64String(metadata.RootElement.GetProperty("state").GetString()!);
        await using (var db = Open())
        {
            await db.CollaborationPages.ExecuteUpdateAsync(p => p.SetProperty(x => x.SchemaFingerprint, fingerprint));
            await db.CollaborationSnapshots.ExecuteUpdateAsync(s => s.SetProperty(x => x.State, seed).SetProperty(x => x.Sha256, Hash(seed)));
            db.CollaborationMembers.AddRange(new() { DocumentId = _document, UserId = "editor", Role = CollaborationRole.Editor }, new() { DocumentId = _document, UserId = "viewer", Role = CollaborationRole.Viewer }); await db.SaveChangesAsync();
            db.UserProfiles.AddRange(new() { UserId = "owner", DisplayName = "Alex Writer" }, new() { UserId = "editor", DisplayName = "Sam Editor" }); await db.SaveChangesAsync();
        }
        await RunProbe("presence-probe.mjs", Request with { SchemaFingerprint = fingerprint });
        await using var verify = Open(); Assert.Empty(await verify.CollaborationUpdates.ToArrayAsync()); Assert.Empty(await verify.CollaborationReceipts.ToArrayAsync());
        Assert.Equal(seed, (await verify.CollaborationSnapshots.AsNoTracking().SingleAsync()).State);
    }
    [Fact]
    public async Task RealSharedSocketsEnforceViewerDowngradeAndOfflineRevocation()
    {
        _clock.RealTime = true; _settings.CurrentValue.CommitsEnabled = true; _sharingOptions.CurrentValue.Enabled = true;
        var start = new ProcessStartInfo("node") { WorkingDirectory = FindRoot(), RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("WriterApp.Collaboration/tests/schema-fixture.mjs");
        using var fixture = Process.Start(start)!; string json = await fixture.StandardOutput.ReadToEndAsync(); string error = await fixture.StandardError.ReadToEndAsync(); await fixture.WaitForExitAsync(); Assert.True(fixture.ExitCode == 0, error);
        using var metadata = JsonDocument.Parse(json); string fingerprint = metadata.RootElement.GetProperty("support").GetProperty("schemaFingerprint").GetString()!;
        byte[] seed = Convert.FromBase64String(metadata.RootElement.GetProperty("state").GetString()!);
        await using (var db = Open())
        {
            await db.CollaborationPages.ExecuteUpdateAsync(p => p.SetProperty(x => x.SchemaFingerprint, fingerprint));
            await db.CollaborationSnapshots.ExecuteUpdateAsync(s => s.SetProperty(x => x.State, seed).SetProperty(x => x.Sha256, Hash(seed)));
            db.CollaborationMembers.AddRange(new() { DocumentId = _document, UserId = "editor", Role = CollaborationRole.Editor }, new() { DocumentId = _document, UserId = "viewer", Role = CollaborationRole.Viewer });
            await db.SaveChangesAsync();
        }
        await RunProbe("sharing-probe.mjs", Request with { SchemaFingerprint = fingerprint, RequestCommit = true });
        await using var verify = Open(); Assert.Single(await verify.CollaborationReceipts.ToArrayAsync());
        Assert.Equal("editor", (await verify.CollaborationUpdates.AsNoTracking().SingleAsync()).AuthorUserId);
        Assert.Equal("<p>Legacy retained</p>", (await verify.Pages.SingleAsync()).Content);
    }
}
