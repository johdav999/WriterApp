using System.Text.Json;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Canon;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalConsistencyContextTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ConsistencyContext", Guid.NewGuid().ToString("N"));
    private async Task<(AdvancedAiPrepared Prepared, ConsistencyPageText[] Pages, DeviceCanonContext Canon)> Fixture()
    {
        var store = new FileLocalDocumentStore(_root); var doc = await store.CreateProjectAsync("Book");
        doc = await store.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>Elin carried a <strong>heavy</strong> suitcase.</p>" }).ToArray() }).ToArray() });
        doc = await store.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced }, doc.LocalRevision, default, projects: true);
        var pages = new[] { new ConsistencyPageText(doc.Sections[0].Pages[0].PageId, "Elin carried a heavy suitcase.") };
        var prepared = AdvancedAiRequests.Build(doc, doc.Sections[0].SectionId, AdvancedAiAction.Consistency, consistencyPages: pages);
        var snapshots = Enum.GetValues<CanonKind>().ToDictionary(k => k, k => new DeviceBibleSnapshot(1, doc.ServerDocumentId!.Value, k,
            k + "-token", "v1", "v1", "hash", DateTimeOffset.UtcNow,
            JsonSerializer.Serialize(new Dictionary<string, object> { ["schemaVersion"] = "1.0", [CanonContent.Collection(k)] = Array.Empty<object>() }), 0, true));
        return (prepared, pages, new(doc.ServerDocumentId!.Value, "v1", snapshots));
    }
    [Fact]
    public async Task ContextIncludesExactPagesAllCanonAndPlanningWithoutChangingSource()
    {
        var (p, pages, canon) = await Fixture(); var before = LocalDocumentCodec.Encode(p.Source);
        var request = LocalConsistencyContext.Prepare(p, canon, pages, false).Request.Request;
        Assert.Equal(pages[0].PlainText, request.SurroundingText); Assert.Contains("synopsis", request.OutlineText);
        Assert.Equal(3, request.ExpectedCanonVersions!.Count);
        foreach (var pair in canon.Snapshots) { Assert.Equal(pair.Value.ContentJson, request.Parameters![pair.Key.ToString().ToLowerInvariant() + "_bible_json"]); Assert.Equal(pair.Value.SnapshotVersion, request.ExpectedCanonVersions[pair.Key]); }
        Assert.Equal(before, LocalDocumentCodec.Encode(p.Source));
        var wire = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var decoded = JsonSerializer.Deserialize<WriterApp.Application.AI.AiActionExecuteRequestDto>(wire, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(request.ExpectedCanonVersions, decoded.ExpectedCanonVersions);
    }
    [Theory]
    [InlineData("document")][InlineData("version")][InlineData("source")][InlineData("missing")][InlineData("pages")][InlineData("oversize")][InlineData("planning")]
    public async Task InvalidContextFailsClosed(string change)
    {
        var (p, pages, canon) = await Fixture();
        if (change == "document") canon = canon with { DocumentId = Guid.NewGuid() };
        if (change == "version") canon = canon with { DocumentVersion = "old" };
        if (change == "source") canon = canon with { Snapshots = canon.Snapshots.ToDictionary(k => k.Key, v => v.Value with { SourceDocumentVersion = "old" }) };
        if (change == "missing") canon = canon with { Snapshots = new Dictionary<CanonKind, DeviceBibleSnapshot>() };
        if (change == "pages") pages = [];
        if (change == "oversize") pages = [pages[0] with { PlainText = new string('a', 100001) }];
        if (change == "planning") p = p with { Source = p.Source with { Project = p.Source.Project! with { Synopsis = new() { Notes = new string('a', 100001) } } } };
        Assert.ThrowsAny<Exception>(() => LocalConsistencyContext.Prepare(p, canon, pages, false));
    }
    [Fact]
    public async Task ReducedContextIsExplicitAndChangedCanonRejectsApproval()
    {
        var (p, pages, canon) = await Fixture(); var reduced = canon with { Snapshots = new Dictionary<CanonKind, DeviceBibleSnapshot> { [CanonKind.Character] = canon.Snapshots[CanonKind.Character] } };
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyContext.Prepare(p, reduced, pages, false));
        var request = LocalConsistencyContext.Prepare(p, reduced, pages, true).Request.Request;
        Assert.Equal("{}", request.Parameters!["place_bible_json"]); Assert.Single(request.ExpectedCanonVersions!);
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyContext.RequireSameCanon(canon, reduced));
        var newer = canon with { Snapshots = canon.Snapshots.ToDictionary(k => k.Key, v => v.Value with { SnapshotVersion = "new" }) };
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyContext.RequireSameCanon(canon, newer));
    }
    [Fact]
    public async Task UnknownCanonReferencesRemainDisclosedWithoutBlockingChecks()
    {
        var (p, pages, canon) = await Fixture();
        canon = canon with { Snapshots = canon.Snapshots.ToDictionary(k => k.Key, v => v.Key == CanonKind.Timeline
            ? v.Value with { ContentJson = "{\"schemaVersion\":\"1.0\",\"events\":[{\"title\":\"Arrival\",\"locationId\":\"unknown\",\"participants\":[\"unbound\"]}]}" } : v.Value) };
        Assert.Equal(2, canon.UnresolvedReferences().Count);
        Assert.Equal(3, LocalConsistencyContext.Prepare(p, canon, pages, false).Request.Request.ExpectedCanonVersions!.Count);
        Assert.Equal(3, LocalConsistencyContext.Prepare(p, canon, pages, true).Request.Request.ExpectedCanonVersions!.Count);
        Assert.Equal(2, canon.UnresolvedReferences().Count);
    }
    [Fact]
    public void InvalidSentenceAndSurroundingDuplicationCannotBecomeApprovedWriting()
    {
        var revision = new ConsistencyRevision(Guid.NewGuid(), "The train arrived.", "", 0);
        Assert.Throws<InvalidDataException>(() => LocalConsistencyRevisions.ValidateReplacement(revision, revision.Original, "lowercase without punctuation"));
        LocalConsistencyRevisions.ValidateReplacement(revision, revision.Original, "The train left.");
        revision = new(revision.PageId, "heavy", "", 0);
        Assert.Throws<InvalidDataException>(() => LocalConsistencyRevisions.ValidateReplacement(revision, "heavy The train reached the station.", "light The train reached the station."));
    }
    [Theory]
    [InlineData("Adjust the arrival time.")][InlineData("Rewrite this passage.")][InlineData("<b>New prose</b>")][InlineData("```new prose```")][InlineData("{\"wrong\":\"value\"}")][InlineData("")]
    public void InstructionsAndInvalidProseAreNeverDirectReplacements(string value) => Assert.Null(LocalConsistencyRevisions.Prose(value));
    [Theory]
    [InlineData("light", "light")][InlineData("<<REVISED>>Elin arrived.<<END>>", "Elin arrived.")][InlineData("{\"revisedText\":\"Elin arrived.\"}", "Elin arrived.")]
    public void ClientStructuredProseContractsAreReadAsInertText(string value, string expected) => Assert.Equal(expected, LocalConsistencyRevisions.Prose(value));
    [Fact]
    public async Task ShiftedUniquePassageRelocatesButLaterEditsAndRepeatedMatchesReject()
    {
        var (p, pages, canon) = await Fixture(); var revision = new ConsistencyRevision(pages[0].PageId, "heavy", "light", 15, "Suitcase weight");
        Assert.Equal(22, LocalConsistencyRevisions.Locate(revision, [pages[0] with { PlainText = "Before " + pages[0].PlainText }]).Start);
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyRevisions.Locate(revision, [pages[0] with { PlainText = "heavy heavy" }]));
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyRevisions.Locate(revision, [pages[0] with { PlainText = "Gone" }]));
        Assert.Throws<InvalidOperationException>(() => LocalConsistencyRevisions.RequireUnchanged(p.Source with { Title = "Later authored edit" }, p.Source));
        var request = LocalConsistencyRevisions.RewriteRequest(p.Source, revision, pages[0].PlainText);
        Assert.Equal("rewrite.selection", request.Key); Assert.Equal("heavy", request.SelectedText); Assert.Equal("v1", request.Request.ExpectedDocumentVersion);
        Assert.Contains("Suitcase weight", request.Request.Parameters!["instruction"]!.ToString());
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
