using System.Text.Json;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.UI.Shared.Projects;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalStoryboardTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.Storyboard", Guid.NewGuid().ToString("N"));
    private FileLocalDocumentStore Store => new(_root);
    private LocalDocumentRepository Repo => new(Store);
    private readonly Api _api = new();
    private DeviceAccountService Account { get; } = new(new Identity());
    private LocalStoryboardData Data() => new(Repo, new DeviceAiService(_api, Account, new()));
    private async Task<(LocalStoryboardData Data, Guid Project, Guid Chapter, Guid Scene)> Book(bool synced = false)
    {
        var d = await Repo.CreateProjectAsync("Book");
        d = await Repo.SaveAsync(d with { Sections = d.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>Original writing 日本語</p>" }).ToArray() }).ToArray() });
        if (synced)
        {
            d = await Store.ApplySyncAsync(d with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced,
                Sections = d.Sections.Select(s => s with { ServerSectionId = Guid.NewGuid(), Pages = s.Pages.Select(p => p with { ServerPageId = Guid.NewGuid() }).ToArray() }).ToArray() }, d.LocalRevision, default, projects: true);
        }
        var data = Data(); await data.LoadAsync(d.Project!.ProjectId);
        return (data, d.Project.ProjectId, d.Project.Nodes.Single(n => n.NodeType == "chapter").NodeId, d.Project.Nodes.Single(n => n.NodeType == "scene").NodeId);
    }
    [Fact]
    public async Task MultipleManuscriptsHaveIndependentBoardsAndRejectCrossDocumentMutations()
    {
        var first = await Repo.CreateProjectAsync("Novel");
        var projectId = first.Project!.ProjectId;
        var second = await Repo.CreateProjectDocumentAsync(projectId, "Alternative", "manuscript");
        var notes = await Repo.CreateProjectDocumentAsync(projectId, "Research", "notes");
        Assert.Empty(notes.Project!.Nodes);
        var board = Data();
        await board.LoadAsync(projectId, second.DocumentId);
        var scene = second.Project!.Nodes.Single(n => n.NodeType == "scene");
        await board.PatchNodeAsync(projectId, scene.NodeId, new("Alternative opening", scene.ParentId, scene.SectionId, null, "scene"), second.DocumentId);
        Assert.Equal("Section 1", (await Repo.LoadAsync(first.DocumentId))!.Project!.Nodes.Single(n => n.NodeType == "scene").Title);
        Assert.Equal("Alternative opening", (await Repo.LoadAsync(second.DocumentId))!.Sections[0].Title);
        Assert.Equal(second.DocumentId, board.Tree(projectId).Project.DocumentId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => board.DeleteNodeAsync(projectId, scene.NodeId, first.DocumentId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => board.LoadAsync(projectId, notes.DocumentId));
        var primary = await Repo.MakePrimaryAsync((await Repo.LoadAsync(second.DocumentId))!);
        await board.LoadAsync(projectId);
        Assert.Equal(primary.DocumentId, board.Document!.DocumentId);
    }

    [Fact]
    public async Task SharedMetadataSurvivesRestartAndStaleWritingSaves()
    {
        var first = await Repo.CreateProjectAsync("Novel");
        var second = await Repo.CreateProjectDocumentAsync(first.Project!.ProjectId, "Alternative", "manuscript");
        var stale = await Repo.LoadAsync(first.DocumentId);
        await Repo.ChangeProjectAsync(second, new(LocalProjectAction.RenameProject, Title: "Renamed project"));
        var saved = await Repo.SaveAsync(stale! with { Sections = stale.Sections.Select(s => s with
            { Pages = s.Pages.Select(p => p with { Content = "<p>Writing from the older editor</p>" }).ToArray() }).ToArray() });
        Assert.Equal("Renamed project", saved.Project!.Title);
        Assert.Equal("Renamed project", (await Repo.LoadAsync(second.DocumentId))!.Project!.Title);
        Assert.Equal("<p>Writing from the older editor</p>", (await Repo.LoadAsync(first.DocumentId))!.Sections[0].Pages[0].Content);
        var list = (await Repo.ListAsync()).Documents;
        var rows = LocalLibraryView.Rows(list, LocalLibraryFilter.All, "", "title", new HashSet<Guid> { first.Project.ProjectId });
        Assert.Equal(3, rows.Count);
        Assert.Equal(2, Assert.Single(rows.Where(r => r.IsProject)).DocumentCount);
        Assert.Equal(2, rows.Count(r => r.IsChild));
        var library = new LocalDocumentLibrary(Repo);
        await library.RefreshAsync(LocalDocumentScope.Active);
        await library.SetProjectTrashAsync(first.Project.ProjectId, true);
        Assert.Empty((await Repo.ListAsync()).Documents);
        await library.SetProjectTrashAsync(first.Project.ProjectId, false);
        Assert.Equal(2, (await Repo.ListAsync()).Documents.Count);
    }
    [Fact]
    public async Task BoardEditsPersistOfflineAndPreserveUnknownMetadataAndWriting()
    {
        var (data, project, chapter, scene) = await Book(); var before = data.Document!;
        await data.PatchNodeAsync(project, scene, new("Opening", chapter, before.Sections[0].SectionId, "{\"custom\":{\"keep\":true}}", "scene"));
        await data.SaveSceneCardAsync(project, scene, new("Purpose", "Beat", "Events", "Questions", PovCharacterId: "Alice", Tags: ["Keep"], Summary: "Summary", Status: "Final", SubplotTags: ["Mystery"], NarrativeRole: "Setup", NarrativeIntent: "Reveal"));
        await data.CommitAsync(d => LocalPlanning.Scene(d, scene, d.Project!.Nodes.Single(n => n.NodeId == scene).Card!, "Private notes"));
        var reopened = Data(); await reopened.LoadAsync(project);
        var card = await reopened.GetSceneCardAsync(project, scene);
        Assert.Equal("Final", card!.Status); Assert.Equal("Mystery", Assert.Single(card.SubplotTags!));
        var dto = reopened.Tree(project).Nodes.Single(n => n.Id == scene);
        using var metadata = JsonDocument.Parse(dto.MetadataJson!);
        Assert.True(metadata.RootElement.GetProperty("custom").GetProperty("keep").GetBoolean());
        Assert.Equal("Summary", metadata.RootElement.GetProperty("summary").GetString());
        Assert.Equal("Private notes", metadata.RootElement.GetProperty("notesText").GetString());
        Assert.Equal("Opening", reopened.Document!.Sections[0].Title);
        Assert.Equal(before.Sections[0].Pages, reopened.Document.Sections[0].Pages);
        Assert.Equal(before.DocumentId, reopened.Document.DocumentId); Assert.Equal(LocalSyncState.LocalOnly, reopened.Document.SyncState);
        Assert.Equal(0, _api.Calls);
    }
    [Fact]
    public async Task MoveAcrossChaptersIsAtomicAndManuscriptOrderMatchesBoard()
    {
        var (data, project, chapter, scene) = await Book();
        var part = await data.CreateNodeAsync(project, new(null, "part", "Part II", null, null, null));
        var target = await data.CreateNodeAsync(project, new(part.Id, "chapter", "Chapter 2", null, null, null));
        var second = await data.CreateNodeAsync(project, new(target.Id, "scene", "Second", null, null, null));
        var original = data.Tree(project).Nodes.Single(n => n.Id == scene);
        var before = LocalDocumentCodec.Encode(data.Document!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => data.MoveSceneAsync(project, scene,
            new(original.Title, target.Id, original.LinkedSectionId, original.MetadataJson, "scene"), new([scene])));
        Assert.Equal(before, LocalDocumentCodec.Encode(data.Document!));
        await data.MoveSceneAsync(project, scene, new(original.Title, target.Id, original.LinkedSectionId, original.MetadataJson, "scene"), new([second.Id, scene]));
        Assert.Equal(target.Id, data.Document!.Project!.Nodes.Single(n => n.NodeId == scene).ParentId);
        Assert.Equal(new[] { second.LinkedSectionId, original.LinkedSectionId }, data.Document.Sections.Select(s => (Guid?)s.SectionId));
        await data.ReorderAsync(project, target.Id, new([scene, second.Id]));
        Assert.Equal(original.LinkedSectionId, data.Document.Sections[0].SectionId);
        Assert.Equal("<p>Original writing 日本語</p>", data.Document.Sections[0].Pages[0].Content);
        await data.LoadAsync(project); Assert.Equal(scene, data.Tree(project).Nodes.Where(n => n.ParentId == target.Id).OrderBy(n => n.OrderIndex).First().Id);
    }
    [Fact]
    public async Task DuplicateGetsIndependentWritingAndDeleteRetainsRecoverableOriginal()
    {
        var (data, project, chapter, scene) = await Book(); var original = data.Document!.Sections[0];
        var duplicate = await data.DuplicateAsync(project, scene, new(false));
        var copy = Assert.Single(duplicate.CreatedNodes);
        Assert.NotEqual(original.SectionId, copy.LinkedSectionId);
        var copySection = data.Document!.Sections.Single(s => s.SectionId == copy.LinkedSectionId);
        Assert.Equal(original.Pages[0].Content, copySection.Pages[0].Content);
        Assert.NotEqual(original.Pages[0].PageId, copySection.Pages[0].PageId); Assert.Null(copySection.Pages[0].ServerPageId);
        await data.DeleteNodeAsync(project, scene);
        Assert.DoesNotContain(data.Tree(project).Nodes, n => n.Id == scene);
        Assert.Equal(original.Pages, data.Document.Sections.Single(s => s.SectionId == original.SectionId).Pages);
        await data.CommitAsync(d => LocalProjectStructure.Apply(d, new(LocalProjectAction.Restore, scene), DateTimeOffset.UtcNow));
        Assert.Contains(data.Tree(project).Nodes, n => n.Id == scene);
        await data.DeleteNodeAsync(project, chapter);
        Assert.Empty(data.Tree(project).Nodes); Assert.Equal(2, data.Document.Sections.Count);
        await data.LoadAsync(project); Assert.Equal(2, data.Document!.Sections.Count);
    }
    [Fact]
    public async Task StaleSourceAndInvalidHierarchyNeverOverwriteTheManuscript()
    {
        var (data, project, chapter, scene) = await Book(); var original = data.Tree(project).Nodes.Single(n => n.Id == scene);
        await Assert.ThrowsAsync<JsonException>(() => data.PatchNodeAsync(project, scene, new(original.Title, null, original.LinkedSectionId, null, "scene")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => data.PatchNodeAsync(project, scene, new(original.Title, chapter, Guid.NewGuid(), null, "scene")));
        var latest = await Repo.ChangeProjectAsync(data.Document!, new(LocalProjectAction.Rename, scene, Title: "Changed elsewhere"));
        await Assert.ThrowsAsync<LocalDocumentConflictException>(() => data.PatchNodeAsync(project, scene, new("Stale title", chapter, original.LinkedSectionId, null, "scene")));
        Assert.Equal(LocalDocumentCodec.Encode(latest), LocalDocumentCodec.Encode((await Repo.LoadAsync(latest.DocumentId))!));
    }
    [Fact]
    public async Task DraftGuardPreventsMutationsAndLocalOnlyAiSendsNothing()
    {
        var (data, project, chapter, scene) = await Book(); var before = LocalDocumentCodec.Encode(data.Document!);
        data.BeforeMutation = () => Task.FromResult(false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => data.DeleteNodeAsync(project, scene));
        Assert.Equal(before, LocalDocumentCodec.Encode(data.Document!));
        data.BeforeMutation = null;
        await Assert.ThrowsAsync<DeviceAiException>(() => data.ExecuteAiAsync(project, "storyboard.suggest-next-scene", new(data.Document!.DocumentId, data.Document.Sections[0].SectionId, null,null,null,null,null,null,null)));
        Assert.Equal(0, _api.Calls);
    }
    [Theory]
    [InlineData("storyboard.suggest-next-scene")]
    [InlineData("storyboard.detect-missing-scenes")]
    [InlineData("storyboard.check-subplot-continuity")]
    [InlineData("storyboard.analyze-pov-balance")]
    public async Task InsightsUseMappedCloudIdentitiesAndRejectStaleSuggestions(string key)
    {
        var (data, project, chapter, scene) = await Book(synced: true); await Account.SignInAsync();
        var d = data.Document!;
        var target = await data.ResolveTargetAsync(project, scene);
        var result = await data.ExecuteAiAsync(project, key, new(target!.DocumentId, target.SectionId, null,null,null,null,null,null,new() { ["storyboard_context"] = "saved board" }));
        Assert.Equal(key, result.ActionKey); Assert.Equal(d.ServerDocumentId, _api.Request!.DocumentId);
        Assert.Equal(d.Sections[0].ServerSectionId, _api.Request.SectionId); Assert.Equal("v1", _api.Request.ExpectedDocumentVersion);
        Assert.Equal("saved board", _api.Request.Parameters!["storyboard_context"]);
        await data.ValidateSuggestionAsync(project);
        await data.CreateNodeAsync(project, new(chapter, "scene", "Changed board",null,null,null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => data.ValidateSuggestionAsync(project));
        Assert.Equal("<p>Original writing 日本語</p>", data.Document!.Sections[0].Pages[0].Content);
    }
    [Fact]
    public async Task SharedBoardRendersExplicitNativeDragValuesForSceneSlotsAndHandles()
    {
        var (data, project, chapter, _) = await Book();
        await data.CreateNodeAsync(project, new(chapter, "scene", "Second", null, null, null));
        using var services = new ServiceCollection().AddLogging().AddSingleton<IStoryboardData>(data)
            .AddSingleton<NavigationManager, Nav>().AddSingleton<IJSRuntime, NoJs>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var tree = data.Tree(project);
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StoryboardBoard>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { ["Project"] = tree.Project, ["Nodes"] = tree.Nodes }))).ToHtmlString());
        var document = new HtmlParser().ParseDocument(html);
        var slots = document.QuerySelectorAll(".storyboard-scene-slot");
        Assert.Equal(2, slots.Length);
        Assert.All(slots, slot =>
        {
            Assert.Equal("true", slot.GetAttribute("draggable"));
            Assert.Equal("true", slot.QuerySelector(".scene-card-drag-handle")?.GetAttribute("draggable"));
        });

        html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<SceneCard>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { ["Title"] = "Disabled", ["EnableDragging"] = false }))).ToHtmlString());
        Assert.Null(new HtmlParser().ParseDocument(html).QuerySelector(".scene-card-drag-handle"));
    }
    [Fact]
    public async Task SharedBoardAndInsightsRenderNestedPartsAndBulkControls()
    {
        var (data, project, chapter, scene) = await Book();
        var part = await data.CreateNodeAsync(project, new(null,"part","Part II",null,null,null));
        var dto = data.Tree(project).Nodes.Single(n => n.Id == chapter);
        await data.PatchNodeAsync(project, chapter, new(dto.Title, part.Id, null, null, "chapter"));
        var second = await data.CreateNodeAsync(project, new(chapter,"scene","Second",null,null,null));
        using var services = new ServiceCollection().AddLogging().AddSingleton<IStoryboardData>(data)
            .AddSingleton<NavigationManager, Nav>().AddSingleton<IJSRuntime, NoJs>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var tree = data.Tree(project);
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StoryboardBoard>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            ["Project"] = tree.Project, ["Nodes"] = tree.Nodes, ["SelectedSceneIds"] = new[] { scene, second.Id }
        }))).ToHtmlString());
        Assert.Contains("Part II", html); Assert.Contains("Second", html); Assert.Contains("2 scenes selected", html);
        Assert.Contains("Add subplot tag", html); Assert.Contains("Filters", html); Assert.Contains("Add chapter", html);
        html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<StoryboardInsights>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            ["Project"] = tree.Project, ["ProjectId"] = project, ["Nodes"] = tree.Nodes
        }))).ToHtmlString());
        Assert.Contains("Storyboard Insights", html); Assert.Contains("Suggest next scene", html); Assert.Contains("POV", html);
    }
    private sealed class Nav : NavigationManager { public Nav() => Initialize("http://localhost/", "http://localhost/"); protected override void NavigateToCore(string uri, bool forceLoad) { } }
    [Fact]
    public async Task DesktopPageResolvesItsAdaptersAndShowsSelectedSceneActions()
    {
        var (_, project, _, scene) = await Book();
        var registrations = new ServiceCollection().AddLogging().AddSingleton<NavigationManager, Nav>().AddSingleton<IJSRuntime, NoJs>();
        registrations.AddWriterAppDeviceCore(new("Test",new Uri("https://test.invalid/")),_root);
        using var services = registrations.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<SelectedStoryboard>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            ["ProjectId"] = project, ["InitialScene"] = scene
        }))).ToHtmlString());
        Assert.Contains("Scene Detail",html); Assert.Contains("Duplicate scene",html); Assert.Contains("Move down",html);
        Assert.Contains("Generate summary",html); Assert.Contains("Suggest scene role/intent",html); Assert.Contains("Improve structure",html);
        Assert.Contains("Summary",html); Assert.Contains("Saved locally",html);
        Assert.DoesNotContain("Project unavailable",html);
    }
    private sealed class SelectedStoryboard : WriterApp.Device.Shared.Pages.Storyboard
    {
        [Parameter] public Guid InitialScene { get; set; }
        protected override Task OnParametersSetAsync() { RequestedScene = InitialScene; return base.OnParametersSetAsync(); }
    }
    [Fact]
    public async Task StructureCoachAppliesTypedPlanningWithoutChangingProse()
    {
        var (data, _, _, scene) = await Book(synced: true);
        var document = data.Document!;
        var prepared = AdvancedAiRequests.Build(document, document.Sections[0].SectionId, AdvancedAiAction.SceneCard);
        prepared = prepared with { Request = prepared.Request with { Key = "scene.refine" } };
        var proposal = new DeviceAiProposal(prepared.Request,"Structure suggestion",null,"",new(),
            SceneCard: new("Setup","Suspense","Discovery","Who?",PovCharacterId:"Alice",Summary:"Investigate",Status:"Revised",SubplotTags:["Mystery"],NarrativeRole:"Setup",NarrativeIntent:"Reveal"));
        var changed = AdvancedAiRequests.Apply(document,prepared,proposal);
        var card = changed.Project!.Nodes.Single(n => n.NodeId == scene).Card!;
        var original = document.Project!.Nodes.Single(n => n.NodeId == scene).Card ?? LocalPlanning.EmptyCard;
        Assert.Equal(original.Status,card.Status); Assert.Equal(original.PovCharacterId,card.PovCharacterId);
        Assert.Equal("Investigate",card.Summary); Assert.Equal("Reveal",card.NarrativeIntent);
        Assert.Equal(original.SubplotTagsJson,card.SubplotTagsJson);
        Assert.Equal(document.Sections,changed.Sections);
    }
    private sealed class NoJs : IJSRuntime
    { public ValueTask<T> InvokeAsync<T>(string identifier, object?[]? args) => ValueTask.FromResult(default(T)!); public ValueTask<T> InvokeAsync<T>(string identifier, CancellationToken ct, object?[]? args) => ValueTask.FromResult(default(T)!); }
    private sealed class Identity : IDeviceIdentityClient
    { public bool IsConfigured => true; public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("test",DateTimeOffset.UtcNow.AddHours(1),"Tester")); public Task SignOutAsync() => Task.CompletedTask; }
    private sealed class Api : IDeviceAiApi
    {
        public int Calls; public AiActionExecuteRequestDto? Request;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto { AiEnabled = true, UiEnabled = true, SupportsDocumentVersionChecks = true, QuotaRemaining = 10 });
        public Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto r, CancellationToken ct)
        { Calls++; Request = r; return Task.FromResult(new AiActionExecuteResponseDto(Guid.NewGuid(),null,"{\"title\":\"Next\"}",null,DateTimeOffset.UtcNow,key,SourceDocumentVersion:r.ExpectedDocumentVersion)); }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root,true); }
}
