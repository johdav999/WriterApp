using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Continuity;
using WriterApp.Data.Documents;
using WriterApp.Shared.Canon;
using WriterApp.Controllers;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DocumentBiblesControllerTests
{
    private static void SeedVersion(WriterApp.Data.AppDbContext db, Guid id)
    {
        db.DocumentSyncRecords.Add(new DocumentSyncRecord { DocumentId = id, OwnerUserId = "user-1", Version = "v1", Sequence = 1 });
        db.SaveChanges();
    }
    private static string Payload(CanonKind kind, string name = "Anna") =>
        System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object> { ["schemaVersion"] = "1.0",
            [CanonContent.Collection(kind)] = new[] { new Dictionary<string,string> { [kind == CanonKind.Timeline ? "title" : "name"] = name, ["id"] = "entry-1" } } });
    private static DeviceBibleSnapshot Value(ActionResult<DeviceBibleSnapshot> result) =>
        Assert.IsType<DeviceBibleSnapshot>(Assert.IsType<OkObjectResult>(result.Result).Value);

    [Theory]
    [InlineData(CanonKind.Character)]
    [InlineData(CanonKind.Place)]
    [InlineData(CanonKind.Timeline)]
    public async Task DeviceCanonExtractsPersistsReloadsAndUpdatesEachKind(CanonKind kind)
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        var controller = BuildController(db, Payload(kind));
        var missing = Value(await controller.GetDeviceSnapshot(id, kind.ToString(), "v1", default));
        Assert.False(missing.Exists);
        var created = Value(await controller.RefreshDeviceSnapshot(id, kind.ToString(), new("v1", missing.SnapshotVersion, true, section), default));
        Assert.Equal("v1", created.SourceDocumentVersion);
        Assert.Single(CanonContent.Parse(kind, created.ContentJson).Entries);
        var reloaded = Value(await BuildController(db, Payload(kind)).GetDeviceSnapshot(id, kind.ToString(), "v1", default));
        Assert.Equal(created, reloaded);
        db.Pages.Single().Content = "<p>Anna arrives at noon.</p>";
        db.DocumentSyncRecords.Single().Version = "v2"; await db.SaveChangesAsync();
        var stale = Value(await controller.GetDeviceSnapshot(id, kind.ToString(), "v2", default));
        Assert.Null(stale.SourceDocumentVersion); Assert.Equal(1, stale.ChangedSections);
        var updated = Value(await BuildController(db, Payload(kind, "Updated")).RefreshDeviceSnapshot(id, kind.ToString(), new("v2", stale.SnapshotVersion, false, section), default));
        Assert.Equal("v2", updated.SourceDocumentVersion);
        Assert.Equal("Updated", Assert.Single(CanonContent.Parse(kind, updated.ContentJson).Entries).Name);
    }

    [Fact]
    public async Task DeviceCanonRejectsForeignOwnershipRevisionSectionAndSnapshotBeforeAi()
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        int calls = 0; var controller = BuildController(db, Payload(CanonKind.Character), () => { calls++; return Task.CompletedTask; });
        Assert.IsType<NotFoundResult>((await controller.GetDeviceSnapshot(Guid.NewGuid(), "character", "v1", default)).Result);
        Assert.IsType<ConflictObjectResult>((await controller.GetDeviceSnapshot(id, "character", "old", default)).Result);
        Assert.IsType<ConflictObjectResult>((await controller.RefreshDeviceSnapshot(id, "character", new("v1", "other", true, section), default)).Result);
        Assert.IsType<BadRequestObjectResult>((await controller.RefreshDeviceSnapshot(id, "character", new("v1", "missing", true, Guid.NewGuid()), default)).Result);
        db.Documents.Single().OwnerUserId = "other-account"; await db.SaveChangesAsync();
        Assert.IsType<NotFoundResult>((await controller.GetDeviceSnapshot(id, "character", "v1", default)).Result);
        Assert.Equal(0, calls); Assert.Empty(db.BibleSnapshots);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("canon")]
    [InlineData("cancel")]
    public async Task DeviceCanonLateChangesCannotOverwriteExistingSnapshot(string change)
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        var created = Value(await BuildController(db, Payload(CanonKind.Character)).RefreshDeviceSnapshot(id, "character", new("v1", "missing", true, section), default));
        using var cancellation = new CancellationTokenSource();
        var controller = BuildController(db, Payload(CanonKind.Character, "Overwrite"), async () =>
        {
            if (change == "source") db.DocumentSyncRecords.Single().Version = "v2";
            if (change == "canon") db.BibleSnapshots.Single().ContentJson = Payload(CanonKind.Character, "Concurrent author");
            if (change == "cancel") cancellation.Cancel();
            else await db.SaveChangesAsync();
        });
        if (change == "cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.RefreshDeviceSnapshot(id, "character", new("v1", created.SnapshotVersion, true, section), cancellation.Token));
        else Assert.IsType<ConflictObjectResult>((await controller.RefreshDeviceSnapshot(id, "character", new("v1", created.SnapshotVersion, true, section), default)).Result);
        Assert.DoesNotContain("Overwrite", db.BibleSnapshots.AsNoTracking().Single().ContentJson);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"characters\":[{\"id\":\"entry-1\"}]}")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Bad\",\"evidence\":[{\"sectionId\":\"not-a-guid\"}]}]}")]
    public async Task DeviceCanonInvalidOrPartialOutputPreservesSavedCanon(string invalid)
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        var original = Value(await BuildController(db, Payload(CanonKind.Character)).RefreshDeviceSnapshot(id, "character", new("v1", "missing", true, section), default));
        var result = await BuildController(db, invalid).RefreshDeviceSnapshot(id, "character", new("v1", original.SnapshotVersion, true, section), default);
        Assert.IsType<UnprocessableEntityObjectResult>(result.Result);
        Assert.Equal(original.ContentJson, db.BibleSnapshots.AsNoTracking().Single().ContentJson);
    }

    [Fact]
    public async Task DeviceTimelineAcceptsUnknownPlaceFromExistingProviderContract()
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        const string payload = """
            {"schemaVersion":"1.0","events":[{"id":"evt_arrival","title":"Arrival","locationId":"","participants":[]}]}
            """;
        var snapshot = Value(await BuildController(db, payload).RefreshDeviceSnapshot(id, "timeline", new("v1", "missing", true, section), default));
        var entry = Assert.Single(CanonContent.Parse(CanonKind.Timeline, snapshot.ContentJson).Entries);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, entry.Details["locationId"].ValueKind);
        Assert.Empty(new DeviceCanonContext(id, "v1", new Dictionary<CanonKind, DeviceBibleSnapshot> { [CanonKind.Timeline] = snapshot }).UnresolvedReferences());
        Assert.Equal(snapshot, Value(await BuildController(db, payload).GetDeviceSnapshot(id, "timeline", "v1", default)));
    }

    [Fact]
    public async Task DeviceTimelineFallbackIsReadableAndPersists()
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        var snapshot = Value(await BuildController(db, "{").RefreshDeviceSnapshot(id, "timeline", new("v1", "missing", false, section), default));
        var entry = Assert.Single(CanonContent.Parse(CanonKind.Timeline, snapshot.ContentJson).Entries);
        Assert.Equal(section.ToString(), entry.Id);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, entry.Details["locationId"].ValueKind);
        Assert.Single(db.BibleSnapshots);
    }

    [Fact]
    public async Task DeviceTimelineLoadsLegacyUnknownPlaceAndSkipsUnchangedRefreshWithoutAi()
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        const string payload = """
            {"schemaVersion":"1.0","events":[{"id":"evt_arrival","title":"Arrival","locationId":null,"participants":[]}]}
            """;
        _ = Value(await BuildController(db, payload).RefreshDeviceSnapshot(id, "timeline", new("v1", "missing", true, section), default));
        var row = db.BibleSnapshots.Single();
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(row.ContentJson)!;
        legacy["events"]![0]!["locationId"] = "";
        row.ContentJson = legacy.ToJsonString();
        await db.SaveChangesAsync();
        int calls = 0;
        var controller = BuildController(db, "{", () => { calls++; return Task.CompletedTask; });
        var loaded = Value(await controller.GetDeviceSnapshot(id, "timeline", "v1", default));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, Assert.Single(CanonContent.Parse(CanonKind.Timeline, loaded.ContentJson).Entries).Details["locationId"].ValueKind);
        var unchanged = Value(await controller.RefreshDeviceSnapshot(id, "timeline", new("v1", loaded.SnapshotVersion, false, section), default));
        Assert.Equal(loaded, unchanged);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task LegacyRefreshCannotOverwriteCanonAfterManuscriptChangesDuringProviderCall()
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        var controller = BuildController(db, Payload(CanonKind.Character), async () => {
            db.Pages.Single().Content = "<p>Concurrent writing</p>"; await db.SaveChangesAsync();
        });
        Assert.IsType<ConflictObjectResult>((await controller.Refresh(id, "character", new(false, section), default)).Result);
        Assert.Empty(db.BibleSnapshots);
    }

    [Fact]
    public async Task RemovedSceneEvidenceForcesRebuildRatherThanSkippingUnchangedRemainingProse()
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section); SeedVersion(db, id);
        _ = Value(await BuildController(db, Payload(CanonKind.Character)).RefreshDeviceSnapshot(id, "character", new("v1", "missing", true, section), default));
        var store = new EfCoreBibleStore(db);
        var state = (await store.GetSnapshotAsync(id, BibleType.Character, default))!;
        state.Cursor.SectionHashes.Add(Guid.NewGuid(), "deleted-scene-hash");
        db.BibleSnapshots.Single().LastRefreshCursorJson = System.Text.Json.JsonSerializer.Serialize(state.Cursor, BibleJson.JsonOptions);
        await db.SaveChangesAsync();
        var current = Value(await BuildController(db, Payload(CanonKind.Character)).GetDeviceSnapshot(id, "character", "v1", default));
        Assert.Equal(1, current.ChangedSections);
        bool full = false;
        var controller = BuildController(db, Payload(CanonKind.Character), inspect: input => full = input.Options!["full_rebuild"] is true);
        _ = Value(await controller.RefreshDeviceSnapshot(id, "character", new("v1", current.SnapshotVersion, false, section), default));
        Assert.True(full);
        Assert.Single((await store.GetSnapshotAsync(id, BibleType.Character, default))!.Cursor.SectionHashes);
    }
}

