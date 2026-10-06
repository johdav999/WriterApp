using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.State;
using WriterApp.Data;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Quality;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    [Theory]
    [InlineData("🧭 elin visited Rostok. Elin stayed.", false)]
    [InlineData("Åsa met åsa near ROSTOCK. Rostok was quiet.", false)]
    [InlineData("🧭 Prefix. elin visited Rostok.", true)]
    [InlineData("Elin stayed in Rostock.", false)]
    public async Task OwnedGlossaryEndpointAndServerQualityMatchDesktopCasingNearMatchesAndExactOffsets(string prose, bool selection)
    {
        await using var f = new TranslationFixture(); f.Html[0] = "<p>" + prose + "</p>"; await f.Start();
        foreach (var term in new[] { "Rostock", "Åsa", "Elin" })
            Assert.True((await f.Http.PostAsJsonAsync($"api/documents/{f.Document}/glossary", new GlossaryEntryCreateRequest(term, "Authored note"))).IsSuccessStatusCode);
        var snapshot = await new DeviceGlossaryApi(f.Http).ReadAsync(f.Document, default);
        Assert.Equal(new[] { "Elin", "Rostock", "Åsa" }, snapshot.Terms);
        var localStore = new FileLocalDocumentStore(Path.Combine(f.Root, "local"));
        var doc = await new LocalDocumentRepository(localStore).CreateImportedAsync("Synthetic local copy", f.Html[0]);
        doc = await localStore.ApplySyncAsync(doc with { ServerDocumentId = f.Document, ServerVersion = "v1", SyncState = LocalSyncState.Synced }, doc.LocalRevision, default);
        int from = selection ? prose.IndexOf("elin", StringComparison.Ordinal) : 0;
        var context = new LocalGlossaryContext(doc.DocumentId, f.Document, snapshot.Terms, GlossaryAvailability.Verified, DateTimeOffset.UtcNow, "", 1);
        var local = LocalQualityChecks.Analyze(doc, doc.Sections[0].Pages[0].PageId,
            new(f.Html[0], prose, prose[from..], from, prose.Length, from + 1, prose.Length + 1, 0), selection, context);
        await using var db = new AppDbContext(f.Options); var page = await db.Pages.SingleAsync(p => p.Id == f.Pages[0]);
        var quality = new QualityCheckService(db, NullLogger<QualityCheckService>.Instance);
        var computed = await quality.RunChecksAsync("ordinary-author", page, new(selection ? "selection" : "page", selection ? prose[from..] : null, false), default);
        Assert.Equal(GlossaryFindings(computed.Issues, from), GlossaryFindings(local.Issues));
        Assert.All(local.Issues.Where(i => i.RuleId == "terminology.glossary"), i => Assert.Equal(i.AnchorText, prose[i.StartOffset..i.EndOffset]));
        if (!selection && computed.Issues.Count > 0)
        {
            var cached = await quality.RunChecksAsync("ordinary-author", page, new("page", null, false), default);
            Assert.True(cached.FromCache); Assert.Equal(GlossaryFindings(computed.Issues), GlossaryFindings(cached.Issues));
        }
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await localStore.GetAsync(doc.DocumentId))!));
        Assert.Equal(f.Html[0], (await db.Pages.AsNoTracking().SingleAsync(p => p.Id == f.Pages[0])).Content);
    }
    private static string[] GlossaryFindings(IReadOnlyList<PageQualityIssueDto> issues, int shift = 0) => issues
        .Where(i => i.RuleId == "terminology.glossary").OrderBy(i => i.StartOffset).Select(i =>
            System.Text.Json.JsonSerializer.Serialize(new { i.IssueKey, i.RuleId, i.Kind, i.Severity, i.Message, i.Suggestion, i.AnchorText,
                From = i.StartOffset + shift, To = i.EndOffset + shift,
                Fix = i.Fix is null ? null : new { i.Fix.Kind, From = i.Fix.From + shift, To = i.Fix.To + shift, i.Fix.Text } })).ToArray();

    [Fact]
    public async Task GlossaryUpdateDeletionAndEmptySnapshotInvalidateServerFindingsWithoutChangingProse()
    {
        await using var f = new TranslationFixture(); f.Html[0] = "<p>elin visited Rostok.</p>"; await f.Start();
        var endpoint = $"api/documents/{f.Document}/glossary";
        var initial = await f.Http.GetFromJsonAsync<DeviceGlossarySnapshot>(endpoint + "/device");
        Assert.Empty(initial!.Terms); initial.Validate(f.Document);
        var response = await f.Http.PostAsJsonAsync(endpoint, new GlossaryEntryCreateRequest("Elin", "Retained authored note"));
        var created = (await response.Content.ReadFromJsonAsync<GlossaryEntryDto>())!;
        await using var db = new AppDbContext(f.Options); var page = await db.Pages.SingleAsync(p => p.Id == f.Pages[0]);
        var service = new QualityCheckService(db, NullLogger<QualityCheckService>.Instance);
        var first = await service.RunChecksAsync("ordinary-author", page, new("page", null, false), default);
        Assert.Single(first.Issues, i => i.RuleId == "terminology.glossary");
        await f.Http.PostAsJsonAsync(endpoint, new GlossaryEntryCreateRequest("Rostock", null));
        var added = await service.RunChecksAsync("ordinary-author", page, new("page", null, false), default);
        Assert.False(added.FromCache); Assert.NotEqual(first.ContentHash, added.ContentHash);
        Assert.Equal(2, added.Issues.Count(i => i.RuleId == "terminology.glossary"));
        Assert.Equal(HttpStatusCode.NoContent, (await f.Http.DeleteAsync(endpoint + "/" + created.Id)).StatusCode);
        var deleted = await service.RunChecksAsync("ordinary-author", page, new("page", null, false), default);
        Assert.False(deleted.FromCache); Assert.Single(deleted.Issues, i => i.RuleId == "terminology.glossary");
        var entries = (await f.Http.GetFromJsonAsync<GlossaryEntryDto[]>(endpoint))!;
        foreach (var entry in entries) await f.Http.DeleteAsync(endpoint + "/" + entry.Id);
        var empty = await f.Http.GetFromJsonAsync<DeviceGlossarySnapshot>(endpoint + "/device");
        Assert.Empty(empty!.Terms); Assert.Equal(initial.Revision, empty.Revision);
        var cleared = await service.RunChecksAsync("ordinary-author", page, new("page", null, false), default);
        Assert.False(cleared.FromCache); Assert.DoesNotContain(cleared.Issues, i => i.RuleId == "terminology.glossary");
        Assert.Equal(f.Html[0], page.Content);
    }
    [Fact]
    public async Task DeviceGlossarySnapshotRequiresTheOrdinaryDocumentOwnerAndNeverDisclosesForeignTerms()
    {
        await using var f = new TranslationFixture(); await f.Start();
        var endpoint = $"api/documents/{f.Document}/glossary/device";
        await f.Http.PostAsJsonAsync($"api/documents/{f.Document}/glossary", new GlossaryEntryCreateRequest("PrivateSavedTerm", null));
        using var anonymous = f.Server.GetTestClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(endpoint)).StatusCode);
        using var foreign = f.Server.GetTestClient(); foreign.DefaultRequestHeaders.Add("X-Test-Owner", "foreign-author");
        var denied = await foreign.GetAsync(endpoint); Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.DoesNotContain("PrivateSavedTerm", await denied.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Http.GetAsync($"api/documents/{Guid.NewGuid()}/glossary/device")).StatusCode);
        f.Restart(); Assert.Equal(new[] { "PrivateSavedTerm" }, (await f.Http.GetFromJsonAsync<DeviceGlossarySnapshot>(endpoint))!.Terms);
    }
    [Theory][InlineData("count")][InlineData("length")]
    public async Task OversizedOwnedGlossaryIsRejectedWholeWithoutTruncatingOrChangingAuthoredEntries(string bound)
    {
        await using var f = new TranslationFixture(); await f.Start();
        await using var db = new AppDbContext(f.Options);
        int count = bound == "count" ? DeviceGlossarySnapshot.MaximumTerms + 1 : 1;
        for (int i = 0; i < count; i++) db.DocumentGlossaryEntries.Add(new() {
            Id = Guid.NewGuid(), DocumentId = f.Document, Term = bound == "length" ? new string('a', 257) : "Saved" + i, NormalizedTerm = "saved" + i });
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await f.Http.GetAsync($"api/documents/{f.Document}/glossary/device")).StatusCode);
        Assert.Equal(count, await db.DocumentGlossaryEntries.CountAsync());
    }
}
