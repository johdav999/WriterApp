using System.Net;
using System.Net.Http.Json;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Quality;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceQualityDismissalTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.QualityDecisions", Guid.NewGuid().ToString("N"));
    private readonly Identity _identity = new();
    private readonly DeviceAccountService _account;
    private readonly DeviceConnectivity _network = new();
    private readonly Handler _handler = new();
    private readonly LocalQualityDismissalStore _store;
    private readonly FileLocalDocumentStore _documents;
    private readonly LocalDocumentRepository _repository;
    private const string Plain = "🧭 Elin carried carried her heavy leather suitcase through the crowded square. Anna carried carried the box.";
    public DeviceQualityDismissalTests()
    {
        _account = new(_identity); _store = new(Path.Combine(_root, "decisions"));
        _documents = new(Path.Combine(_root, "documents")); _repository = new(_documents);
    }
    private DeviceQualityDismissals Service(string backend = "https://test.invalid/", LocalQualityDismissalStore? store = null) => new(store ?? _store,
        new(new HttpClient(_handler) { BaseAddress = new(backend) }), _repository, _account, _network, new("Test", new(backend)));
    private async Task<LocalDocument> Book(bool mapped = true)
    {
        await _account.SignInAsync();
        var doc = await _repository.CreateImportedAsync("Synthetic quality", "<p>" + Plain + "</p>");
        if (mapped) doc = await _documents.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced,
            Sections = doc.Sections.Select(s => s with { ServerSectionId = Guid.NewGuid(), Pages = s.Pages.Select(p => p with { ServerPageId = Guid.NewGuid() }).ToArray() }).ToArray() }, doc.LocalRevision, default);
        return doc;
    }
    private LocalQualityAnalysis Analyze(LocalDocument doc, string text = Plain, string[]? glossary = null, bool selection = false)
    {
        var page = doc.Sections[0].Pages[0]; int from = selection ? text.IndexOf("Anna", StringComparison.Ordinal) : 0;
        var a = LocalQualityChecks.Analyze(doc, page.PageId, new(page.Content, text, text[from..], from, text.Length, from + 1, text.Length + 1, 0), selection,
            new(doc.DocumentId, doc.ServerDocumentId, glossary ?? [], GlossaryAvailability.Verified, DateTimeOffset.UtcNow, "Synthetic verified glossary", _account.Generation));
        _handler.Analysis = a; return a;
    }
    [Fact]
    public async Task OfflineOccurrenceSurvivesRestartReconnectAndRestoreRetriesWithoutDuplicatingServerDecisions()
    {
        var doc = await Book(); var a = Analyze(doc); var keys = a.Issues.Where(i => i.RuleId == "style.repeated_words").Select(i => i.IssueKey).ToArray();
        Assert.Equal(2, keys.Length); _network.SetOnline(false);
        var hidden = await Service().DismissAsync(a, keys[0]); Assert.True(hidden.Items.Single().Pending); Assert.Equal(new[] { keys[0] }, hidden.Hidden);
        var restarted = Service(store: new(Path.Combine(_root, "decisions")));
        Assert.Equal(hidden.Hidden, (await restarted.LoadAsync(a)).Hidden); Assert.Equal(0, _handler.Calls);
        _network.SetOnline(true); var synced = await restarted.LoadAsync(a);
        Assert.False(synced.Items.Single().Pending); Assert.Equal("Synced", synced.Items[0].Status); Assert.Single(_handler.Server);
        await restarted.LoadAsync(a); Assert.Single(_handler.Server);
        _network.SetOnline(false); var restored = await restarted.RestoreAsync(a, synced.Items[0].Id);
        Assert.Empty(restored.Hidden); Assert.True(restored.Items.Single().Pending); Assert.False(restored.Items[0].Dismissed);
        _network.SetOnline(true); _handler.Failure = "503";
        var failed = await restarted.LoadAsync(a); Assert.Empty(failed.Hidden); Assert.Equal("Failed", failed.Items.Single().Status);
        _handler.Failure = null; var delivered = await restarted.LoadAsync(a); Assert.Empty(delivered.Items); Assert.Empty(_handler.Server);
        Assert.All(_handler.Generations, g => Assert.Equal(_account.Generation, g));
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await _documents.GetAsync(doc.DocumentId))!));
    }
    [Theory][InlineData("page")][InlineData("glossary")][InlineData("selection")][InlineData("rule")]
    public async Task ChangedPageContextSelectionOrRuleCannotInheritEarlierDismissal(string change)
    {
        var doc = await Book(); var a = Analyze(doc); _network.SetOnline(false);
        var key = a.Issues.Last(i => i.RuleId == "style.repeated_words").IssueKey;
        await Service().DismissAsync(a, key);
        LocalQualityAnalysis next;
        if (change == "page")
        {
            string changed = Plain.Replace("box", "cat");
            doc = await _documents.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>" + changed + "</p>" }).ToArray() }).ToArray() });
            next = Analyze(doc, changed);
            Assert.Contains(next.Issues, i => i.IssueKey == key); // Even unchanged key cannot inherit changed source.
        }
        else if (change == "glossary") next = Analyze(doc, glossary: ["Elin"]);
        else if (change == "selection") next = Analyze(doc, selection: true);
        else next = a with { Issues = a.Issues.Select(i => i with { IssueKey = QualityDismissalIdentity.Hash("new rule:" + i.IssueKey), RuleId = i.RuleId + ".new" }).ToArray() };
        var result = await Service().LoadAsync(next); Assert.Empty(result.Hidden); Assert.Single(result.Items);
        Assert.Equal(0, _handler.Calls);
    }
    [Theory][InlineData("network")][InlineData("503")][InlineData("409")][InlineData("404")][InlineData("malformed")]
    [InlineData("foreign-document")][InlineData("foreign-page")][InlineData("foreign-source")][InlineData("missing-issue")][InlineData("wrong-decision")]
    public async Task FailedOrUnverifiedSynchronizationPreservesDurableLocalDecisionAndRetries(string failure)
    {
        var a = Analyze(await Book()); _handler.Failure = failure;
        var key = a.Issues.First().IssueKey; var result = await Service().DismissAsync(a, key);
        Assert.Contains(key, result.Hidden); Assert.True(result.Items.Single().Pending);
        Assert.Equal(failure == "409" ? "Conflict" : "Failed", result.Items[0].Status);
        _network.SetOnline(false);
        Assert.Contains(key, (await Service(store: new(Path.Combine(_root, "decisions"))).LoadAsync(a)).Hidden);
        _handler.Failure = null; _network.SetOnline(true);
        var success = await Service().LoadAsync(a); Assert.Contains(key, success.Hidden); Assert.False(success.Items.Single().Pending);
    }
    [Theory][InlineData("account")][InlineData("backend")][InlineData("document")][InlineData("page-mapping")][InlineData("document-mapping")][InlineData("signed-out")]
    public async Task DecisionsCannotBleedAcrossAccountsBackendsDocumentsOrMappings(string change)
    {
        var doc = await Book(); var a = Analyze(doc); _network.SetOnline(false); await Service().DismissAsync(a, a.Issues[0].IssueKey);
        var service = Service();
        if (change == "account") { await _account.SignOutAsync(); _identity.Account = "second-writer"; await _account.SignInAsync(); }
        if (change == "signed-out") await _account.SignOutAsync();
        if (change == "backend") service = Service("https://another.invalid/");
        if (change == "document") doc = await Book();
        if (change is "page-mapping" or "document-mapping")
        {
            doc = await _documents.ApplySyncAsync(doc with { ServerDocumentId = change == "document-mapping" ? Guid.NewGuid() : doc.ServerDocumentId,
                Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { ServerPageId = change == "page-mapping" ? Guid.NewGuid() : p.ServerPageId }).ToArray() }).ToArray() }, doc.LocalRevision, default);
        }
        Assert.Empty((await service.LoadAsync(Analyze(doc))).Hidden); Assert.Equal(0, _handler.Calls);
    }
    [Fact]
    public async Task LocalOnlyAndSelectionDecisionsNeverGuessCloudPageIds()
    {
        var a = Analyze(await Book(false)); var result = await Service().DismissAsync(a, a.Issues[0].IssueKey);
        Assert.Equal("Unmapped", result.Items[0].Status); Assert.Equal(0, _handler.Calls);
        var mapped = Analyze(await Book(), selection: true); result = await Service().DismissAsync(mapped, mapped.Issues[0].IssueKey);
        Assert.Equal("Local", result.Items[0].Status); Assert.False(result.Items[0].Pending); Assert.Equal(0, _handler.Calls);
    }
    [Fact]
    public async Task LateAccountChangeCannotAcknowledgeOrImportForeignDecisions()
    {
        var a = Analyze(await Book()); _handler.BeforeReply = () => _account.SignOutAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().DismissAsync(a, a.Issues[0].IssueKey));
        _handler.BeforeReply = null; _network.SetOnline(false); Assert.Empty((await Service().LoadAsync(a)).Hidden);
        await _account.SignInAsync(); var result = await Service().LoadAsync(a); Assert.True(result.Items.Single().Pending);
    }
    [Theory][InlineData("version")][InlineData("identity")][InlineData("corrupt")]
    public async Task UnknownOrCorruptJournalIsPreservedWithoutInventingDismissals(string failure)
    {
        var a = Analyze(await Book()); _network.SetOnline(false); await Service().DismissAsync(a, a.Issues[0].IssueKey);
        var path = Assert.Single(Directory.GetFiles(Path.Combine(_root, "decisions"), "*.json", SearchOption.AllDirectories));
        var original = await File.ReadAllTextAsync(path);
        string broken = failure switch { "version" => original.Replace("\"version\":1", "\"version\":999"),
            "identity" => original.Replace(a.Source.DocumentId.ToString(), Guid.NewGuid().ToString()), _ => "{broken" };
        await File.WriteAllTextAsync(path, broken);
        await Assert.ThrowsAnyAsync<Exception>(() => Service().LoadAsync(a)); Assert.Equal(broken, await File.ReadAllTextAsync(path));
        Assert.Equal(0, _handler.Calls);
    }
    [Fact]
    public async Task FailedAtomicJournalWritePreservesPreviousDecisionAndNeverContactsBackend()
    {
        var a = Analyze(await Book()); _network.SetOnline(false);
        await Service().DismissAsync(a, a.Issues[0].IssueKey);
        var faulted = new LocalQualityDismissalStore(Path.Combine(_root, "decisions"), _ => throw new IOException("Synthetic atomic replacement failure"));
        await Assert.ThrowsAsync<IOException>(() => Service(store: faulted).DismissAsync(a, a.Issues[1].IssueKey));
        Assert.Equal(new[] { a.Issues[0].IssueKey }, (await Service().LoadAsync(a)).Hidden);
        Assert.Equal(0, _handler.Calls); Assert.Empty(Directory.GetFiles(Path.Combine(_root, "decisions"), "*.tmp", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task InactiveRestoreRemainsQueuedAndSourceReversionCannotResurrectServerDismissal()
    {
        var doc = await Book(); var a = Analyze(doc); var key = a.Issues[0].IssueKey;
        var first = await Service().DismissAsync(a, key); Assert.False(first.Items.Single().Pending);
        _network.SetOnline(false);
        var changed = Plain.Replace("box", "cat");
        doc = await _documents.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>" + changed + "</p>" }).ToArray() }).ToArray() });
        var later = Analyze(doc, changed);
        var restored = await Service().RestoreAsync(later, first.Items[0].Id);
        Assert.Empty(restored.Hidden); Assert.False(restored.Items.Single().Dismissed); Assert.True(restored.Items[0].Pending);
        doc = await _documents.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = "<p>" + Plain + "</p>" }).ToArray() }).ToArray() });
        doc = await _documents.ApplySyncAsync(doc with { SyncState = LocalSyncState.Synced }, doc.LocalRevision, default);
        a = Analyze(doc); Assert.Empty((await Service().LoadAsync(a)).Hidden);
        _network.SetOnline(true); Assert.Empty((await Service().LoadAsync(a)).Hidden); Assert.Empty(_handler.Server);
    }
    [Fact]
    public async Task VerifiedRemappingNeverDeletesOldMappedDecisionEvidence()
    {
        var doc = await Book(); var a = Analyze(doc); var first = await Service().DismissAsync(a, a.Issues[0].IssueKey);
        doc = await _documents.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), Sections = doc.Sections.Select(s => s with {
            Pages = s.Pages.Select(p => p with { ServerPageId = Guid.NewGuid() }).ToArray() }).ToArray() }, doc.LocalRevision, default);
        _handler.Server.Clear(); a = Analyze(doc); Assert.Empty((await Service().LoadAsync(a)).Hidden);
        await Service().DismissAsync(a, a.Issues[0].IssueKey);
        var scope = LocalBibleStore.ScopeKey(new("https://test.invalid/"), "account:" + _account.AccountId);
        var all = await _store.ReadAsync(scope, doc.DocumentId, a.PageId);
        Assert.Equal(2, all.Items.Count); Assert.Contains(first.Items[0], all.Items);
    }
    private sealed class Identity : IDeviceIdentityClient
    {
        public string Account = "writer-1"; public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic", DateTimeOffset.UtcNow.AddHours(1), "Writer", Account));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public LocalQualityAnalysis Analysis = null!; public int Calls; public string? Failure; public Func<Task>? BeforeReply;
        public HashSet<string> Server = []; public List<long> Generations = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.True(request.Options.TryGetValue(DeviceAuthenticatedHandler.ExpectedAccountGeneration, out long generation)); Generations.Add(generation);
            if (Failure == "network") throw new HttpRequestException("Synthetic network failure");
            if (int.TryParse(Failure, out int code)) return new((HttpStatusCode)code);
            var value = (await request.Content!.ReadFromJsonAsync<QualityDismissalRequest>(ct))!;
            foreach (var d in value.Decisions) { if (d.Dismissed) Server.Add(d.IssueKey); else Server.Remove(d.IssueKey); }
            if (BeforeReply is not null) await BeforeReply();
            if (Failure == "malformed") return new(HttpStatusCode.OK) { Content = new StringContent("{bad") };
            var receipt = new QualityDismissalReceipt(1, value.DocumentId, Analysis.Source.Sections[0].Pages[0].ServerPageId!.Value,
                value.SourceHash, Analysis.Issues.Select(i => new QualityDismissalDecision(i.IssueKey, Server.Contains(i.IssueKey))).ToArray());
            receipt = Failure switch { "foreign-document" => receipt with { DocumentId = Guid.NewGuid() }, "foreign-page" => receipt with { PageId = Guid.NewGuid() },
                "foreign-source" => receipt with { SourceHash = new string('f', 64) }, "missing-issue" => receipt with { Decisions = [] },
                "wrong-decision" => receipt with { Decisions = receipt.Decisions.Select(d => d with { Dismissed = false }).ToArray() }, _ => receipt };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(receipt) };
        }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
