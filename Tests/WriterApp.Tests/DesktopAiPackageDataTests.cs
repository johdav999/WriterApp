using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Canon;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    [Fact]
    public async Task SqliteLegacySectionRoutingOrdersOwnedScenesByUtcInstant()
    {
        string root = Path.Combine(Path.GetTempPath(), "WriterApp.SceneRouting", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string connection = $"Data Source={Path.Combine(root, "routing.db")};Pooling=False";
        var project = Guid.NewGuid(); var document = Guid.NewGuid(); var section = Guid.NewGuid();
        var newest = Guid.NewGuid();
        try
        {
            await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options))
            {
                await db.Database.EnsureCreatedAsync();
                db.Projects.Add(new() { Id = project, OwnerUserId = "author" });
                db.Documents.Add(new() { Id = document, ProjectId = project, OwnerUserId = "author" });
                db.Sections.Add(new() { Id = section, DocumentId = document });
                var now = DateTimeOffset.UtcNow;
                db.ProjectNodes.AddRange(
                    new ProjectNodeRecord { Id = Guid.NewGuid(), ProjectId = project, NodeType = ProjectNodeType.Scene, LinkedSectionId = section, UpdatedUtc = now.AddMinutes(-1).ToOffset(TimeSpan.FromHours(5)) },
                    new ProjectNodeRecord { Id = newest, ProjectId = project, NodeType = ProjectNodeType.Scene, LinkedSectionId = section, UpdatedUtc = now.ToOffset(TimeSpan.FromHours(-5)) });
                await db.SaveChangesAsync();
            }
            using var server = Host(connection); using var http = server.GetTestClient();
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync($"api/sections/{section}/scene-target")).StatusCode);
            http.DefaultRequestHeaders.Add("X-Test-Owner", "author");
            var target = (await http.GetFromJsonAsync<ProjectSceneOpenTargetDto>($"api/sections/{section}/scene-target"))!;
            Assert.Equal(newest, target.SceneNodeId); Assert.Equal(document, target.DocumentId);
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"api/sections/{Guid.NewGuid()}/scene-target")).StatusCode);
            http.DefaultRequestHeaders.Remove("X-Test-Owner"); http.DefaultRequestHeaders.Add("X-Test-Owner", "foreign");
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"api/sections/{section}/scene-target")).StatusCode);
        }
        finally { Directory.Delete(root, true); }
    }
}

public sealed class DesktopAiPackageDataTests
{
    // Executes the production stores, not an installed package or platform token cache.
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task PriorVersionedDataUpgradePreservesWritingGuidePendingTransfersCanonAndRecovery(int version)
    {
        using var fixture = new HistoryTestFixture();
        await fixture.Start();
        fixture.Source = await fixture.Book.Store.SetProjectCoverAsync(fixture.Source, CoverTestFixture.Image, Guid.NewGuid());
        var approved = fixture.Entry("Applied");
        await fixture.Book.Repository.SaveAsync(approved.After!);
        await fixture.Store.SaveHistoryAsync(approved);
        var history = Assert.Single(await fixture.Store.HistoryAsync(fixture.Source.DocumentId));
        Assert.Equal("Applied", Assert.Single(history.Deliveries!).State);
        var prompt = await fixture.Store.SavePromptAsync("Åsa preset", "Preserve 日本語 and rich writing");
        var transfer = await fixture.Store.QueuePresetCopyAsync(fixture.Scope, prompt.Id, prompt.Revision, false);
        var guideRoot = Path.Combine(fixture.Book.Root, "guide");
        var guides = new LocalOnboardingStore(guideRoot);
        var guide = await guides.ReadAsync(fixture.Scope);
        guide = await guides.UpdateAsync(guide, state => state with { PracticeDocumentId = fixture.Source.DocumentId, PracticeCreated = true, Step = 2 });
        var guidePath = Path.Combine(guideRoot, fixture.Scope + ".json");
        // An actual version-1 guide migrates separately, retaining its original identity.
        await File.WriteAllBytesAsync(guidePath, JsonSerializer.SerializeToUtf8Bytes(guide with { Version = 1 }));
        byte[] oldGuide = await File.ReadAllBytesAsync(guidePath);
        var canonRoot = Path.Combine(fixture.Book.Root, "canon");
        var canon = new LocalBibleStore(canonRoot);
        var snapshot = new DeviceBibleSnapshot(1, fixture.CloudDocument, CanonKind.Character, "v1", "v1", "v1", new string('A', 64), DateTimeOffset.UtcNow,
            "{\"schemaVersion\":\"1.0\",\"characters\":[{\"id\":\"asa\",\"name\":\"Åsa\"}]}", 0, true);
        await canon.SaveAsync(new(1, fixture.Scope, fixture.Source.DocumentId, fixture.CloudDocument, CanonKind.Character, snapshot, DateTimeOffset.UtcNow, "Ready"));

        string path = Path.Combine(fixture.Book.Root, fixture.Source.DocumentId.ToString("N") + ".json");
        var current = (await fixture.Book.Store.GetAsync(fixture.Source.DocumentId))!;
        var envelope = JsonNode.Parse(await File.ReadAllBytesAsync(path))!;
        envelope["schemaVersion"] = version;
        envelope["document"]!["futureAuthoredMetadata"] = "retain 日本語";
        byte[] original = System.Text.Encoding.UTF8.GetBytes(envelope.ToJsonString());
        await File.WriteAllBytesAsync(path, original);
        var sidecarPaths = Directory.EnumerateFiles(fixture.Book.Root, "*.json", SearchOption.AllDirectories).Where(p => p != path && p != guidePath).ToArray();
        var hashes = sidecarPaths.ToDictionary(p => p, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));

        var interrupted = new FileLocalDocumentStore(fixture.Book.Root, TimeProvider.System, new AtomicDocumentWriter(staging =>
        { if (!staging.Contains($".v{version}.bak.", StringComparison.Ordinal)) throw new IOException("Interrupted upgrade"); }));
        await Assert.ThrowsAsync<LocalDocumentReadException>(() => interrupted.GetAsync(current.DocumentId));
        Assert.Equal(original, await File.ReadAllBytesAsync(path));
        Assert.Equal(original, await File.ReadAllBytesAsync(path + $".v{version}.bak"));
        var reopened = (await new FileLocalDocumentStore(fixture.Book.Root).GetAsync(current.DocumentId))!;
        Assert.Equal(current.Sections.SelectMany(s => s.Pages).Select(p => (p.PageId, p.Content)), reopened.Sections.SelectMany(s => s.Pages).Select(p => (p.PageId, p.Content)));
        Assert.Equal(current.Sections.Select(s => s.SectionId), reopened.Sections.Select(s => s.SectionId));
        Assert.Equal(current.Project!.CoverImageUrl, reopened.Project!.CoverImageUrl);
        Assert.Equal("retain 日本語", reopened.ExtensionData!["futureAuthoredMetadata"].GetString());
        Assert.Equal(4, JsonNode.Parse(await File.ReadAllBytesAsync(path))!["schemaVersion"]!.GetValue<int>());
        Assert.Equal(original, await File.ReadAllBytesAsync(path + $".v{version}.bak"));
        var reopenedGuide = await new LocalOnboardingStore(guideRoot).ReadAsync(fixture.Scope);
        Assert.Equal(guide.Id, reopenedGuide.Id); Assert.Equal(current.DocumentId, reopenedGuide.PracticeDocumentId);
        Assert.Equal(guide.Step, reopenedGuide.Step); Assert.Equal(oldGuide, await File.ReadAllBytesAsync(guidePath + ".v1.bak"));
        var ai = new LocalAiStore(Path.Combine(fixture.Book.Root, "ai"));
        Assert.Equal(transfer.Id, Assert.Single(await ai.TransfersAsync(fixture.Scope)).Id);
        Assert.Empty(await ai.TransfersAsync(new string('B', 64)));
        var retained = Assert.Single(await ai.HistoryAsync(current.DocumentId));
        Assert.Equal(history.Id, retained.Id); Assert.Equal(history.Deliveries![0], retained.Deliveries![0]);
        Assert.Equal(history.Before.Sections[0].Pages[0].Content, retained.Before.Sections[0].Pages[0].Content);
        Assert.Equal(snapshot, (await new LocalBibleStore(canonRoot).ReadAsync(fixture.Scope, reopened, CanonKind.Character))!.Snapshot);
        Assert.Null(await new LocalBibleStore(canonRoot).ReadAsync(new string('B', 64), reopened, CanonKind.Character));
        foreach (var pair in hashes) Assert.Equal(pair.Value, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(pair.Key))));
        Assert.Single((await new FileLocalDocumentStore(fixture.Book.Root).ListAsync()).Documents);
    }
}
