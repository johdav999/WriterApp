using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceOnboardingTests
{
    private sealed class Identity : IDeviceIdentityClient {
        public string User = "account-1";
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic", DateTimeOffset.UtcNow.AddHours(1), "Test writer", User));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class Fixture : IDisposable {
        public string Root = Path.Combine(Path.GetTempPath(), "WriterApp.OnboardingTests", Guid.NewGuid().ToString("N"));
        public Identity Identity = new();
        public DeviceAccountService Account;
        public FileLocalDocumentStore Documents;
        public LocalOnboardingStore Store;
        public DeviceOnboarding Service;
        public DeviceHostOptions Host = new("Test", new("https://test.invalid/"));
        public Fixture() { Account = new(Identity); Documents = new(Root); Store = new(Root + "/guide"); Service = new(Store, Documents, Account, Host); }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    [Fact]
    public async Task VersionTwoGuideUpgradesOnExplicitChangeWithoutChangingPracticeIdentityOrWriting() {
        using var f=new Fixture();var opened=await f.Service.OpenPracticeAsync(await f.Service.ReadAsync());
        var original=LocalDocumentCodec.Encode(opened.Document);var legacy=opened.Progress with{Version=2,Status="Skipped",Step=3};
        await File.WriteAllBytesAsync(Path.Combine(f.Root,"guide",legacy.Scope+".json"),JsonSerializer.SerializeToUtf8Bytes(legacy));
        var read=await f.Service.ReadAsync();Assert.Equal(legacy,read);
        var updated=await f.Service.ChangeAsync(read,3,"Active");Assert.Equal(3,updated.Version);
        Assert.Equal(legacy.Id,updated.Id);Assert.Equal(legacy.PracticeDocumentId,updated.PracticeDocumentId);Assert.Null(updated.Demo);
        Assert.Equal(original,LocalDocumentCodec.Encode((await f.Documents.GetAsync(opened.Document.DocumentId))!));
    }
    [Theory]
    [InlineData("<p><strong>Räksmörgås 日本語</strong> — user writing.</p>")]
    [InlineData("<p></p>")]
    [InlineData("")]
    public async Task SkipResumeRestartAndCompletionNeverReseedOrTouchExistingWriting(string authored) {
        using var f = new Fixture();
        var existing = await f.Documents.CreateImportedAsync("User manuscript", authored);
        var sourceBytes = LocalDocumentCodec.Encode(existing);
        var progress = await f.Service.ReadAsync();
        Assert.Single((await f.Documents.ListAsync()).Documents);
        progress = await f.Service.ChangeAsync(progress, 2, "Skipped");
        progress = await new DeviceOnboarding(new(f.Root + "/guide"), new FileLocalDocumentStore(f.Root), f.Account, f.Host).ReadAsync();
        Assert.Equal(2, progress.Step); Assert.Equal("Skipped", progress.Status);
        progress = await f.Service.ChangeAsync(progress, 2, "Active");
        var opened = await f.Service.OpenPracticeAsync(progress);
        Assert.NotEqual(existing.DocumentId, opened.Document.DocumentId); Assert.NotNull(opened.Document.Project);
        var practice = await f.Documents.SaveAsync(opened.Document with { Sections = opened.Document.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = authored }).ToArray() }).ToArray() });
        var practiceBytes = LocalDocumentCodec.Encode(practice);
        progress = await f.Service.ChangeAsync(opened.Progress, 4, "Completed");
        Assert.Equal("Completed", (await new LocalOnboardingStore(f.Root + "/guide").ReadAsync(progress.Scope)).Status);
        progress = await f.Service.ChangeAsync(progress, 0, "Active");
        for (int i = 0; i < 3; i++) { opened = await f.Service.OpenPracticeAsync(progress); progress = opened.Progress; Assert.Equal(practiceBytes, LocalDocumentCodec.Encode(opened.Document)); }
        Assert.Equal(sourceBytes, LocalDocumentCodec.Encode((await f.Documents.GetAsync(existing.DocumentId))!));
        Assert.Equal(2, (await f.Documents.ListAsync()).Documents.Count);
    }
    [Fact]
    public async Task InterruptedCreationUsesReservedIdAndPreservesEditsOnRetry() {
        using var f = new Fixture(); var progress = await f.Service.ReadAsync();
        var id = Guid.NewGuid(); progress = await f.Store.UpdateAsync(progress, value => value with { PracticeDocumentId = id });
        var sample = await f.Documents.CreateOnboardingPracticeAsync(id, progress.Id);
        sample = await f.Documents.RenameAsync(sample.DocumentId, sample.LocalRevision, "My edited practice");
        var reopened = await new DeviceOnboarding(new(f.Root + "/guide"), new FileLocalDocumentStore(f.Root), f.Account, f.Host).OpenPracticeAsync(progress);
        Assert.Equal(sample.DocumentId, reopened.Document.DocumentId); Assert.Equal("My edited practice", reopened.Document.Title);
        Assert.True(reopened.Progress.PracticeCreated); Assert.Single((await f.Documents.ListAsync()).Documents);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task TrashedOrMissingPracticeIsNeverReseeded(bool delete) {
        using var f = new Fixture(); var opened = await f.Service.OpenPracticeAsync(await f.Service.ReadAsync());
        var trashed = await f.Documents.MoveToTrashAsync(opened.Document.DocumentId, opened.Document.LocalRevision);
        // Simulate a missing disk file; normal project deletion retains the manuscript in Trash.
        if (delete) File.Delete(Path.Combine(f.Root, $"{trashed.DocumentId:N}.json"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.OpenPracticeAsync(opened.Progress));
        var all = await f.Documents.ListAsync(LocalDocumentScope.All);
        if (delete) Assert.Empty(all.Documents); else Assert.NotNull(Assert.Single(all.Documents).DeletedAtUtc);
    }
    [Fact]
    public async Task ReservationCannotOverwriteAnUnlabelledDocument() {
        using var f = new Fixture(); var document = await f.Documents.CreateImportedAsync("Original", "<p>Original writing</p>");
        var bytes = LocalDocumentCodec.Encode(document);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Documents.CreateOnboardingPracticeAsync(document.DocumentId, Guid.NewGuid()));
        Assert.Equal(bytes, LocalDocumentCodec.Encode((await f.Documents.GetAsync(document.DocumentId))!));
        var progress = await f.Service.ReadAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ContinuePracticeAsync(progress, document.DocumentId));
    }
    [Fact]
    public async Task AccountAndBackendScopesAreSeparateAndGuestPracticeContinuationIsExplicit() {
        using var f = new Fixture(); var guest = await f.Service.OpenPracticeAsync(await f.Service.ReadAsync());
        var bytes = LocalDocumentCodec.Encode(guest.Document);
        await f.Account.SignInAsync(); var account = await f.Service.ReadAsync();
        Assert.NotEqual(guest.Progress.Scope, account.Scope); Assert.Null(account.PracticeDocumentId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ChangeAsync(guest.Progress, 1, "Active"));
        account = await f.Service.ContinuePracticeAsync(account, guest.Document.DocumentId);
        Assert.Equal(guest.Document.DocumentId, account.PracticeDocumentId);
        f.Identity.User = "account-2"; await f.Account.SignInAsync();
        var other = await f.Service.ReadAsync(); Assert.Null(other.PracticeDocumentId); Assert.NotEqual(account.Scope, other.Scope);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.OpenPracticeAsync(account));
        var backend = new DeviceOnboarding(f.Store, f.Documents, f.Account, new("Other", new("https://other.invalid/")));
        Assert.Null((await backend.ReadAsync()).PracticeDocumentId);
        Assert.Equal(bytes, LocalDocumentCodec.Encode((await f.Documents.GetAsync(guest.Document.DocumentId))!));
    }
    [Fact]
    public async Task ConcurrentWindowsRejectStaleStateInsteadOfLosingCompletionOrPracticeIdentity() {
        using var f = new Fixture(); var source = await f.Service.ReadAsync();
        var updated = await new LocalOnboardingStore(f.Root + "/guide").UpdateAsync(source, value => value with { Step = 4, Status = "Completed" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.ChangeAsync(source, 0, "Active"));
        Assert.Equal(updated, await f.Service.ReadAsync());
        var opened = await f.Service.OpenPracticeAsync(updated);
        await Assert.ThrowsAsync<InvalidDataException>(() => f.Store.UpdateAsync(opened.Progress, value => value with { PracticeDocumentId = Guid.NewGuid() }));
    }
    [Fact]
    public async Task VersionOneMigrationPreservesProgressPracticeAndBackupWithoutWritingToDocument() {
        using var f = new Fixture(); var opened = await f.Service.OpenPracticeAsync(await f.Service.ReadAsync());
        var bytes = LocalDocumentCodec.Encode(opened.Document);
        var old = opened.Progress with { Version = 1, Status = "Skipped", Step = 3, PracticeCreated = false };
        string path = Path.Combine(f.Root, "guide", old.Scope + ".json");
        var original = JsonSerializer.SerializeToUtf8Bytes(old); await File.WriteAllBytesAsync(path, original);
        var migrated = await f.Service.ReadAsync();
        Assert.Equal(2, migrated.Version); Assert.Equal(old.Id, migrated.Id); Assert.Equal(old.PracticeDocumentId, migrated.PracticeDocumentId);
        Assert.True(migrated.PracticeCreated);
        Assert.Equal(3, migrated.Step); Assert.Equal("Skipped", migrated.Status); Assert.Equal(original, await File.ReadAllBytesAsync(path + ".v1.bak"));
        Assert.Equal(bytes, LocalDocumentCodec.Encode((await f.Documents.GetAsync(opened.Document.DocumentId))!));
    }
    [Theory][InlineData("corrupt")][InlineData("future")][InlineData("identity")][InlineData("oversized")]
    public async Task InvalidStateIsPreservedAndCannotTriggerPracticeCreation(string failure) {
        using var f = new Fixture(); var progress = await f.Service.ReadAsync(); var path = Path.Combine(f.Root, "guide", progress.Scope + ".json");
        string payload = failure switch { "corrupt" => "{bad", "oversized" => new string('x', 20000), "identity" => JsonSerializer.Serialize(progress with { Scope = new string('b', 64) }), _ => JsonSerializer.Serialize(progress with { Version = 99 }) };
        await File.WriteAllTextAsync(path, payload);
        await Assert.ThrowsAnyAsync<Exception>(() => f.Service.ReadAsync());
        Assert.Equal(payload, await File.ReadAllTextAsync(path)); Assert.Empty((await f.Documents.ListAsync()).Documents);
    }
    private sealed class Navigation : NavigationManager {
        public Navigation() { Initialize("https://test.invalid/", "https://test.invalid/ai-guide"); }
        protected override void NavigateToCore(string uri, bool forceLoad) { Uri = ToAbsoluteUri(uri).AbsoluteUri; }
    }
    private sealed class Components : IComponentActivator {
        public DesktopAiGuide Guide = null!;
        public IComponent CreateInstance(Type type) { var component = (IComponent)Activator.CreateInstance(type)!; if (component is DesktopAiGuide guide) Guide = guide; return component; }
    }
    [Fact]
    public async Task ActualGuideRendersAllStepsSkipsRestartsAndOpensRealWorkflowsWithoutAutomaticRequests() {
        using var f = new Fixture(); var components = new Components(); var navigation = new Navigation();
        await using var services = new ServiceCollection().AddLogging().AddSingleton(f.Service).AddSingleton(f.Account)
            .AddSingleton<ILocalDocumentStore>(f.Documents).AddSingleton<NavigationManager>(navigation).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DesktopAiGuide>());
        Task Event(string method) => renderer.Dispatcher.InvokeAsync(() => EventCallback.Factory.Create(components.Guide,
            (Func<Task>)(() => (Task)typeof(DesktopAiGuide).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(components.Guide, null)!)).InvokeAsync());
        Task<string> Html() => renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        async Task Evidence(string name) { string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_P12_EVIDENCE"); if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence, name + ".html"), await Html()); }
        Assert.Contains("Opening this guide never sends", await Html()); Assert.Empty((await f.Documents.ListAsync()).Documents); await Evidence("guide-step0");
        await Event("Skip"); Assert.Contains("progress is saved", await Html()); await Evidence("guide-skipped");
        await Event("Resume"); await Event("OpenPractice"); Assert.Contains("?panel=Writing&view=tools", navigation.Uri);
        var sample = Assert.Single((await f.Documents.ListAsync()).Documents); var bytes = LocalDocumentCodec.Encode(sample);
        string? evidenceRoot = Environment.GetEnvironmentVariable("WRITERAPP_P12_EVIDENCE");
        if (evidenceRoot is not null) await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "practice-sample.html"), sample.Sections[0].Pages[0].Content);
        for (int step = 1; step <= 4; step++) {
            Assert.Contains($"Step {step + 1} of 5", await Html()); await Evidence("guide-step" + step);
            if (step == 4) { var html = await Html(); Assert.Contains("panel=Story&amp;view=synopsis", html); Assert.Contains("panel=Advanced&amp;view=prompts", html); Assert.Contains("/cover", html); }
            await Event("Next");
        }
        Assert.Contains("guide completed", await Html()); await Evidence("guide-completed");
        await Event("Restart"); await Event("OpenPractice"); Assert.Single((await f.Documents.ListAsync()).Documents);
        Assert.Equal(bytes, LocalDocumentCodec.Encode((await f.Documents.GetAsync(sample.DocumentId))!));
        await renderer.Dispatcher.InvokeAsync(() => f.Account.SignInAsync()); await root.QuiescenceTask;
        await (Task)typeof(DesktopAiGuide).GetField("_refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(components.Guide)!;
        Assert.Contains("Step 1 of 5", await Html()); Assert.DoesNotContain($"/documents/{sample.DocumentId}", await Html());
    }
    [Fact]
    public async Task ExistingPracticeGuideOffersExplicitContinuationAfterSignInAndShowsRecoveryErrors() {
        using var f = new Fixture(); var opened = await f.Service.OpenPracticeAsync(await f.Service.ReadAsync());
        var components = new Components(); await using var services = new ServiceCollection().AddLogging().AddSingleton(f.Service).AddSingleton(f.Account)
            .AddSingleton<ILocalDocumentStore>(f.Documents).AddSingleton<NavigationManager>(new Navigation()).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<DesktopAiGuide>(ParameterView.FromDictionary(new Dictionary<string,object?> { [nameof(DesktopAiGuide.DocumentId)] = opened.Document.DocumentId })));
        Task Event(string method) => renderer.Dispatcher.InvokeAsync(() => EventCallback.Factory.Create(components.Guide,
            (Func<Task>)(() => (Task)typeof(DesktopAiGuide).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(components.Guide, null)!)).InvokeAsync());
        Task Refresh() => (Task)typeof(DesktopAiGuide).GetField("_refresh", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(components.Guide)!;
        Task<string> Html() => renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        async Task Evidence(string name) { string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_P12_EVIDENCE"); if (evidence is not null) await File.WriteAllTextAsync(Path.Combine(evidence,name+".html"),await Html()); }
        await renderer.Dispatcher.InvokeAsync(() => f.Account.SignInAsync()); await Refresh();
        Assert.Contains("Continue guide on this practice project", await Html()); await Evidence("guide-account");
        await Event("ContinuePractice");Assert.Contains("Step 2 of 5",await Html());
        Assert.Single((await f.Documents.ListAsync()).Documents);
        await f.Documents.MoveToTrashAsync(opened.Document.DocumentId,opened.Document.LocalRevision);
        await Event("OpenPractice");Assert.Contains("role=\"alert\"",await Html());Assert.Contains("in Trash",await Html());await Evidence("guide-error");
        Assert.NotNull(Assert.Single((await f.Documents.ListAsync(LocalDocumentScope.All)).Documents).DeletedAtUtc);
    }
}
