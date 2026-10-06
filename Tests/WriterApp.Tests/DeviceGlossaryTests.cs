using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Quality;
using Xunit;

namespace WriterApp.Tests;

public sealed class DeviceGlossaryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.GlossaryTests", Guid.NewGuid().ToString("N"));
    private readonly Identity _identity = new();
    private readonly Handler _handler = new();
    private readonly DeviceConnectivity _network = new();
    private DeviceAccountService Account => _account ??= new(_identity);
    private DeviceAccountService? _account;
    private FileLocalDocumentStore Documents => new(Path.Combine(_root, "documents"));
    private LocalGlossaryStore Cache => new(Path.Combine(_root, "glossary"));
    private DeviceGlossaryService Service(string backend = "https://test.invalid/") => new(
        new(new HttpClient(_handler) { BaseAddress = new(backend) }), Cache, new(Documents), Account, _network, new("Test", new(backend)));
    private async Task<LocalDocument> Book(bool mapped = true)
    {
        await Account.SignInAsync();
        var doc = await new LocalDocumentRepository(Documents).CreateImportedAsync("Glossary fixture", "<p>🧭 elin visited Rostok.</p>");
        if (mapped) doc = await Documents.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced }, doc.LocalRevision, default);
        _handler.Id = doc.ServerDocumentId ?? Guid.Empty;
        return doc;
    }
    [Fact]
    public async Task FreshTermsPersistAcrossRestartOfflineAndUpdatesAndDeletionReplaceThemWithoutChangingWriting()
    {
        var doc = await Book(); var original = LocalDocumentCodec.Encode(doc);
        var fresh = await Service().PrepareAsync(doc);
        Assert.Equal(GlossaryAvailability.Verified, fresh.Availability); Assert.Equal(new[] { "Elin", "Rostock" }, fresh.Terms);
        Assert.Equal(fresh.AccountGeneration, Assert.Single(_handler.Generations));
        var page = doc.Sections[0].Pages[0]; const string plain = "🧭 elin visited Rostok.";
        var result = LocalQualityChecks.Analyze(doc, page.PageId, new(page.Content, plain, "", 0, 0, 1, 1, 0), false, fresh);
        Assert.Equal(2, result.Issues.Count(i => i.RuleId == "terminology.glossary"));
        _network.SetOnline(false);
        var offline = await Service().PrepareAsync(doc);
        Assert.Equal(GlossaryAvailability.CachedOffline, offline.Availability); Assert.Equal(fresh.CheckedAt, offline.CheckedAt);
        Assert.Equal(fresh.Terms, offline.Terms); Assert.Equal(1, _handler.Calls);
        _network.SetOnline(true); _handler.Terms = ["ELIN"];
        var updated = await Service().PrepareAsync(doc); Assert.Equal(new[] { "ELIN" }, updated.Terms);
        _handler.Terms = [];
        var empty = await Service().PrepareAsync(doc);
        Assert.Equal(GlossaryAvailability.Verified, empty.Availability); Assert.Empty(empty.Terms); Assert.Contains("verified empty", empty.Message);
        _network.SetOnline(false); var cachedEmpty = await Service().PrepareAsync(doc);
        Assert.Equal(GlossaryAvailability.CachedOffline, cachedEmpty.Availability); Assert.Empty(cachedEmpty.Terms);
        Assert.Equal(3, _handler.Calls);
        Assert.Equal(original, LocalDocumentCodec.Encode((await Documents.GetAsync(doc.DocumentId))!));
    }
    [Theory]
    [InlineData("network")][InlineData("500")][InlineData("malformed")][InlineData("wrong-document")]
    [InlineData("future-contract")][InlineData("oversized")][InlineData("timeout")]
    public async Task FailedRefreshUsesOnlyMatchingPreviousCacheAndNeverClaimsVerifiedEmpty(string failure)
    {
        var doc = await Book(); var first = await Service().PrepareAsync(doc);
        _handler.Failure = failure;
        var failed = await Service().PrepareAsync(doc);
        Assert.Equal(GlossaryAvailability.CachedRefreshFailed, failed.Availability); Assert.Equal(first.Terms, failed.Terms);
        Assert.Equal(first.CheckedAt, failed.CheckedAt); Assert.Contains("may be out of date", failed.Message);
        var other = await Book(); var missing = await Service().PrepareAsync(other);
        Assert.Equal(GlossaryAvailability.Unavailable, missing.Availability); Assert.Empty(missing.Terms); Assert.Null(missing.CheckedAt);
    }
    [Theory][InlineData(401)][InlineData(403)][InlineData(404)]
    public async Task RevokedAccessExcludesCachedTermsWithoutDeletingTheCache(int status)
    {
        var doc = await Book(); await Service().PrepareAsync(doc); _handler.Failure = status.ToString();
        var unavailable = await Service().PrepareAsync(doc); Assert.Empty(unavailable.Terms); Assert.Equal(GlossaryAvailability.Unavailable, unavailable.Availability);
        Assert.NotNull(await Cache.ReadAsync(LocalBibleStore.ScopeKey(new("https://test.invalid/"), "account-1"), doc));
    }
    [Fact]
    public async Task OfflineAccountBackendDocumentAndMappingSwitchesCannotReuseOtherTerms()
    {
        var doc = await Book(); await Service().PrepareAsync(doc); _network.SetOnline(false);
        Assert.Empty((await Service("https://other.invalid/").PrepareAsync(doc)).Terms);
        var other = await Book(); Assert.Empty((await Service().PrepareAsync(other)).Terms);
        _identity.Id = "account-2"; await Account.SignInAsync();
        Assert.Empty((await Service().PrepareAsync(doc)).Terms);
        await Account.SignOutAsync(); Assert.Empty((await Service().PrepareAsync(doc)).Terms);
        _identity.Id = "account-1"; await Account.SignInAsync(); Assert.NotEmpty((await Service().PrepareAsync(doc)).Terms);
        var remapped = await Documents.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid() }, doc.LocalRevision, default);
        Assert.Empty((await Service().PrepareAsync(remapped)).Terms);
        var page = remapped.Sections[0].Pages[0];
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().PrepareAsync(doc));
        var old = new LocalGlossaryContext(doc.DocumentId, doc.ServerDocumentId, ["Elin"], GlossaryAvailability.Verified, DateTimeOffset.UtcNow, "", Account.Generation);
        Assert.Throws<InvalidDataException>(() => LocalQualityChecks.Analyze(remapped, page.PageId, new(page.Content, "elin", "", 0, 0, 1, 1, 0), false, old));
        _network.SetOnline(true); _handler.Id = remapped.ServerDocumentId!.Value; _handler.Terms = ["NewMappingTerm"];
        Assert.Equal(new[] { "NewMappingTerm" }, (await Service().PrepareAsync(remapped)).Terms);
        Assert.Equal(new[] { "Elin", "Rostock" }, (await Cache.ReadAsync(LocalBibleStore.ScopeKey(new("https://test.invalid/"), "account-1"), doc))!.Snapshot.Terms);
    }
    [Theory][InlineData("account")][InlineData("mapping")][InlineData("cancel")][InlineData("delete")]
    public async Task LateRefreshCannotBeAdoptedOrCachedAfterIdentityChanges(string change)
    {
        var doc = await Book(); using var cancel = new CancellationTokenSource();
        _handler.BeforeReply = async () => {
            if (change == "account") { _identity.Id = "account-2"; await Account.SignInAsync(); }
            if (change == "mapping") await Documents.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid() }, doc.LocalRevision, default);
            if (change == "delete") await Documents.SaveAsync(doc with { DeletedAtUtc = DateTimeOffset.UtcNow });
            if (change == "cancel") cancel.Cancel();
        };
        var error = await Record.ExceptionAsync(() => Service().PrepareAsync(doc, cancel.Token));
        Assert.True(error is OperationCanceledException or InvalidOperationException);
        Assert.False(Directory.Exists(Path.Combine(_root, "glossary")));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task MissingCacheUpgradeLeavesExistingLocalDataLosslessAndUnavailableDistinctFromEmpty(bool mapped)
    {
        var doc = await Book(mapped); var original = LocalDocumentCodec.Encode(doc); _network.SetOnline(false);
        var missing = await Service().PrepareAsync(doc);
        Assert.Equal(GlossaryAvailability.Unavailable, missing.Availability); Assert.Null(missing.CheckedAt); Assert.Empty(missing.Terms);
        Assert.Equal(0, _handler.Calls); Assert.Equal(original, LocalDocumentCodec.Encode((await Documents.GetAsync(doc.DocumentId))!));
        Assert.False(Directory.Exists(Path.Combine(_root, "glossary")));
    }
    [Theory][InlineData("future-cache")][InlineData("corrupt")][InlineData("identity")]
    public async Task UnsupportedCacheIsPreservedAndNeverTreatedAsAnEmptyGlossary(string failure)
    {
        var doc = await Book(); await Service().PrepareAsync(doc);
        var file = Assert.Single(Directory.GetFiles(Path.Combine(_root, "glossary"), "*.json", SearchOption.AllDirectories));
        string text = await File.ReadAllTextAsync(file);
        text = failure switch { "future-cache" => text.Replace("\"version\":1", "\"version\":999"),
            "identity" => text.Replace(doc.ServerDocumentId!.Value.ToString(), Guid.NewGuid().ToString()), _ => "{ corrupt" };
        await File.WriteAllTextAsync(file, text);
        var result = await Service().PrepareAsync(doc);
        Assert.Equal(GlossaryAvailability.Unavailable, result.Availability); Assert.Empty(result.Terms); Assert.Null(result.CheckedAt);
        Assert.Equal(text, await File.ReadAllTextAsync(file)); Assert.Equal(1, _handler.Calls);
    }
    [Fact]
    public async Task LocallyEditedMappedDocumentCanCheckGlossaryWithoutUploadingItsWriting()
    {
        var doc = await Book(); doc = await Documents.SaveAsync(doc with { Title = "Unsynced title" });
        Assert.NotEqual(LocalSyncState.Synced, doc.SyncState);
        Assert.Equal(GlossaryAvailability.Verified, (await Service().PrepareAsync(doc)).Availability);
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Documents.GetAsync(doc.DocumentId))!));
    }
    [Fact]
    public async Task AuthenticatedSessionRejectionDiscardsPrivateContextAndPreservesOldScopedCache()
    {
        var doc = await Book(); var saved = await Service().PrepareAsync(doc); _handler.Failure = "401";
        using var http = new HttpClient(new DeviceAuthenticatedHandler(Account, new("https://test.invalid/")) { InnerHandler = _handler })
            { BaseAddress = new("https://test.invalid/") };
        var service = new DeviceGlossaryService(new(http), Cache, new(Documents), Account, _network, new("Test", new("https://test.invalid/")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PrepareAsync(doc));
        Assert.False(Account.IsSignedIn);
        Assert.Equal(saved.Terms, (await Cache.ReadAsync(LocalBibleStore.ScopeKey(new("https://test.invalid/"), "account-1"), doc))!.Snapshot.Terms);
        var anonymous = await Service().PrepareAsync(doc); Assert.Empty(anonymous.Terms); Assert.Equal(GlossaryAvailability.Unavailable, anonymous.Availability);
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
    private sealed class Identity : IDeviceIdentityClient
    {
        public string Id = "account-1";
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic-token", DateTimeOffset.UtcNow.AddHours(1), "Fixture", Id));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public Guid Id; public string[] Terms = ["Elin", "Rostock"]; public int Calls; public string? Failure; public Func<Task>? BeforeReply;
        public List<long> Generations = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; Assert.Equal(HttpMethod.Get, request.Method); Assert.EndsWith($"/{Id}/glossary/device", request.RequestUri!.AbsolutePath);
            Assert.True(request.Options.TryGetValue(DeviceAuthenticatedHandler.ExpectedAccountGeneration, out var generation)); Generations.Add(generation);
            if (BeforeReply is not null) await BeforeReply();
            if (Failure == "network") throw new HttpRequestException("Synthetic offline failure");
            if (Failure == "timeout") throw new OperationCanceledException();
            if (int.TryParse(Failure, out int status)) return new((HttpStatusCode)status);
            if (Failure is "malformed" or "oversized") return new(HttpStatusCode.OK) { Content = new StringContent(Failure == "malformed" ? "null" : new string('x', DeviceGlossarySnapshot.MaximumBytes + 1)) };
            var snapshot = DeviceGlossarySnapshot.Create(Failure == "wrong-document" ? Guid.NewGuid() : Id, Terms);
            if (Failure == "future-contract") snapshot = snapshot with { ContractVersion = 999 };
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(snapshot) };
        }
    }
}
