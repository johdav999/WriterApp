using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WriterApp.Application.Documents;
using WriterApp.Application.State;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Quality;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private const string QualityDecisionText = "🧭 Elin carried carried her heavy leather suitcase through the crowded square. Anna carried carried the box.";
    private static async Task<(string Hash, string[] Keys)> QualityDecisionSource(TranslationFixture f)
    {
        await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}", new { content = "<p>" + QualityDecisionText + "</p>" });
        var run = (await f.Http.PostAsJsonAsync($"api/pages/{f.Pages[0]}/quality-checks/run", new QualityCheckRunRequest("page", null, true)))!;
        run.EnsureSuccessStatusCode();
        var result = (await run.Content.ReadFromJsonAsync<QualityCheckRunResultDto>())!;
        return (result.ContentHash, result.Issues.Where(i => i.RuleId == "style.repeated_words").Select(i => i.IssueKey).ToArray());
    }
    [Fact]
    public async Task DeviceQualityExchangeSurvivesServerRestartAndFiltersOnlyItsVerifiedOccurrenceInClientReruns()
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; await f.Start();
        var (hash, keys) = await QualityDecisionSource(f); Assert.Equal(2, keys.Length);
        var request = new QualityDismissalRequest(1, f.Document, hash, [new(keys[0], true)]);
        string endpoint = $"api/pages/{f.Pages[0]}/quality-checks/device-decisions";
        (await f.Http.PostAsJsonAsync(endpoint, request)).EnsureSuccessStatusCode();
        (await f.Http.PostAsJsonAsync(endpoint, request)).EnsureSuccessStatusCode();
        await using (var db = new AppDbContext(f.Options)) Assert.Single(await db.PageQualityIssueDismissals.ToListAsync());
        f.Restart();
        var receipt = (await (await f.Http.PostAsJsonAsync(endpoint, request with { Decisions = [] })).Content.ReadFromJsonAsync<QualityDismissalReceipt>())!;
        Assert.Contains(new QualityDismissalDecision(keys[0], true), receipt.Decisions);
        foreach (bool force in new[] { false, true })
        {
            var result = (await (await f.Http.PostAsJsonAsync($"api/pages/{f.Pages[0]}/quality-checks/run", new QualityCheckRunRequest("page", null, force))).Content.ReadFromJsonAsync<QualityCheckRunResultDto>())!;
            Assert.DoesNotContain(result.Issues, i => i.IssueKey == keys[0]); Assert.Contains(result.Issues, i => i.IssueKey == keys[1]);
        }
        var visible = (await f.Http.GetFromJsonAsync<PageQualityIssueDto[]>($"api/pages/{f.Pages[0]}/quality-checks/issues"))!;
        var all = (await f.Http.GetFromJsonAsync<PageQualityIssueDto[]>($"api/pages/{f.Pages[0]}/quality-checks/issues?includeDismissed=true"))!;
        Assert.DoesNotContain(visible, i => i.IssueKey == keys[0]); Assert.Contains(all, i => i.IssueKey == keys[0]);
        // Existing client Restore also removes the additive scoped decision for the cached source.
        (await f.Http.DeleteAsync($"api/pages/{f.Pages[0]}/quality-checks/issues/{keys[0]}/dismiss")).EnsureSuccessStatusCode();
        receipt = (await (await f.Http.PostAsJsonAsync(endpoint, request with { Decisions = [] })).Content.ReadFromJsonAsync<QualityDismissalReceipt>())!;
        Assert.Contains(new QualityDismissalDecision(keys[0], false), receipt.Decisions);
    }
    [Fact]
    public async Task DeviceQualityExchangePreservesLegacyClientDismissalsAndDoesNotClaimTheirSourceEquivalence()
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; await f.Start();
        var (hash, keys) = await QualityDecisionSource(f);
        (await f.Http.PostAsync($"api/pages/{f.Pages[0]}/quality-checks/issues/{keys[0]}/dismiss", null)).EnsureSuccessStatusCode();
        var request = new QualityDismissalRequest(1, f.Document, hash, []); string endpoint = $"api/pages/{f.Pages[0]}/quality-checks/device-decisions";
        var receipt = (await (await f.Http.PostAsJsonAsync(endpoint, request)).Content.ReadFromJsonAsync<QualityDismissalReceipt>())!;
        Assert.Contains(keys[0], receipt.LegacyIssueKeys!); Assert.Contains(new QualityDismissalDecision(keys[0], false), receipt.Decisions);
        await using (var db = new AppDbContext(f.Options)) Assert.Equal(keys[0], Assert.Single(await db.PageQualityIssueDismissals.ToListAsync()).IssueKey);
        var result = (await (await f.Http.PostAsJsonAsync($"api/pages/{f.Pages[0]}/quality-checks/run", new QualityCheckRunRequest("page", null, true))).Content.ReadFromJsonAsync<QualityCheckRunResultDto>())!;
        Assert.DoesNotContain(result.Issues, i => i.IssueKey == keys[0]);
        (await f.Http.PostAsJsonAsync(endpoint, request with { Decisions = [new(keys[0], false)] })).EnsureSuccessStatusCode();
        await using (var db = new AppDbContext(f.Options)) Assert.Empty(await db.PageQualityIssueDismissals.ToListAsync());
    }
    [Theory][InlineData("writing")][InlineData("glossary")][InlineData("unknown-key")][InlineData("document")][InlineData("owner")][InlineData("page")][InlineData("version")][InlineData("duplicate")][InlineData("oversized")]
    public async Task DeviceQualityExchangeRefusesUnsafeOrForeignMappingsWithoutChangingAnyDecision(string failure)
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; await f.Start();
        var (hash, keys) = await QualityDecisionSource(f);
        var request = new QualityDismissalRequest(1, f.Document, hash, [new(keys[0], true)]);
        Guid page = f.Pages[0];
        if (failure == "writing") await f.Http.PutAsJsonAsync($"api/pages/{page}", new { content = "<p>" + QualityDecisionText.Replace("box", "cat") + "</p>" });
        if (failure == "glossary") { await using var db = new AppDbContext(f.Options); db.DocumentGlossaryEntries.Add(new DocumentGlossaryEntryRecord { Id = Guid.NewGuid(), DocumentId = f.Document, Term = "Elin", CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }
        if (failure == "unknown-key") request = request with { Decisions = [new(new string('a', 64), true)] };
        if (failure == "document") request = request with { DocumentId = Guid.NewGuid() };
        if (failure == "page") page = f.Pages[1];
        if (failure == "owner") { f.Http.DefaultRequestHeaders.Remove("X-Test-Owner"); f.Http.DefaultRequestHeaders.Add("X-Test-Owner", "another-writer"); }
        if (failure == "version") request = request with { Version = 999 };
        if (failure == "duplicate") request = request with { Decisions = [new(keys[0], true), new(keys[0], false)] };
        if (failure == "oversized") request = request with { Decisions = Enumerable.Repeat(new QualityDismissalDecision(keys[0], true), 201).ToArray() };
        var response = await f.Http.PostAsJsonAsync($"api/pages/{page}/quality-checks/device-decisions", request);
        Assert.False(response.IsSuccessStatusCode); await using var verify = new AppDbContext(f.Options); Assert.Empty(await verify.PageQualityIssueDismissals.ToListAsync());
    }
    [Fact]
    public async Task DeviceQualityChangedPassageIsEligibleEvenWhenItsLegacyFindingKeyRemainsIdentical()
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; await f.Start();
        var (hash, keys) = await QualityDecisionSource(f);
        (await f.Http.PostAsJsonAsync($"api/pages/{f.Pages[0]}/quality-checks/device-decisions", new QualityDismissalRequest(1, f.Document, hash, [new(keys[0], true)]))).EnsureSuccessStatusCode();
        await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}", new { content = "<p>" + QualityDecisionText.Replace("box", "cat") + "</p>" });
        var result = (await (await f.Http.PostAsJsonAsync($"api/pages/{f.Pages[0]}/quality-checks/run", new QualityCheckRunRequest("page", null, true))).Content.ReadFromJsonAsync<QualityCheckRunResultDto>())!;
        Assert.NotEqual(hash, result.ContentHash); Assert.Contains(result.Issues, i => i.IssueKey == keys[0]);
    }
    [Fact]
    public async Task DeviceQualityExchangeRollsBackFailedStorageAndSameDesiredDecisionRetriesIdempotently()
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; await f.Start();
        var (hash, keys) = await QualityDecisionSource(f); string endpoint = $"api/pages/{f.Pages[0]}/quality-checks/device-decisions";
        await using (var db = new AppDbContext(f.Options)) await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailQualityDismissal BEFORE INSERT ON PageQualityIssueDismissals BEGIN SELECT RAISE(ABORT,'injected failure'); END;");
        var request = new QualityDismissalRequest(1, f.Document, hash, [new(keys[0], true), new(keys[1], true)]);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Http.PostAsJsonAsync(endpoint, request)).StatusCode);
        await using (var db = new AppDbContext(f.Options)) { Assert.Empty(await db.PageQualityIssueDismissals.ToListAsync()); await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailQualityDismissal"); }
        (await f.Http.PostAsJsonAsync(endpoint, request)).EnsureSuccessStatusCode(); (await f.Http.PostAsJsonAsync(endpoint, request)).EnsureSuccessStatusCode();
        await using var verify = new AppDbContext(f.Options); Assert.Equal(2, await verify.PageQualityIssueDismissals.CountAsync());
    }

    [Fact]
    public async Task DeviceQualityActualLocalJournalAndServerConvergeAfterOfflineDismissAndClientRestore()
    {
        await using var f = new TranslationFixture { Configure = s => s.AddScoped<IQualityCheckService, QualityCheckService>() }; await f.Start();
        var (hash, keys) = await QualityDecisionSource(f);
        var store = new FileLocalDocumentStore(Path.Combine(f.Root, "device")); var repository = new LocalDocumentRepository(store);
        var local = await repository.CreateImportedAsync("Synthetic device copy", "<p>" + QualityDecisionText + "</p>");
        local = await store.ApplySyncAsync(local with { ServerDocumentId = f.Document, ServerVersion = "v1", SyncState = LocalSyncState.Synced,
            Sections = local.Sections.Select(s => s with { ServerSectionId = f.Sections[0], Pages = s.Pages.Select(p => p with { ServerPageId = f.Pages[0] }).ToArray() }).ToArray() }, local.LocalRevision, default);
        var account = new DeviceAccountService(new QualityDecisionIdentity()); await account.SignInAsync(); var network = new DeviceConnectivity();
        var journal = new LocalQualityDismissalStore(Path.Combine(f.Root, "decisions"));
        DeviceQualityDismissals Service() => new(journal, new(f.Http), repository, account, network, new("Synthetic", new("http://localhost/")));
        var page = local.Sections[0].Pages[0];
        var analysis = LocalQualityChecks.Analyze(local, page.PageId, new(page.Content, QualityDecisionText, "", 0, 0, 1, 1, 0), false,
            new(local.DocumentId, f.Document, [], GlossaryAvailability.Verified, DateTimeOffset.UtcNow, "Verified empty", account.Generation));
        Assert.Equal(hash, DeviceQualityDismissals.SourceHash(analysis));
        network.SetOnline(false); Assert.Contains(keys[0], (await Service().DismissAsync(analysis, keys[0])).Hidden);
        await using (var db = new AppDbContext(f.Options)) Assert.Empty(await db.PageQualityIssueDismissals.ToListAsync());
        network.SetOnline(true); var synced = await Service().LoadAsync(analysis); Assert.Contains(keys[0], synced.Hidden); Assert.False(synced.Items.Single().Pending);
        // A client explicitly reopens this source-bound decision; the next verified desktop check converges.
        (await f.Http.DeleteAsync($"api/pages/{f.Pages[0]}/quality-checks/issues/{keys[0]}/dismiss")).EnsureSuccessStatusCode();
        Assert.Empty((await Service().LoadAsync(analysis)).Hidden);
        Assert.Equal(LocalDocumentCodec.Encode(local), LocalDocumentCodec.Encode((await store.GetAsync(local.DocumentId))!));
    }
    private sealed class QualityDecisionIdentity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic", DateTimeOffset.UtcNow.AddHours(1), "Writer", "ordinary-author"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
}
