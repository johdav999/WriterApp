using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.Application.Collaboration;
using WriterApp.Application.Documents;
using WriterApp.Application.Importing;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Collaboration;
using WriterApp.Data.Documents;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed class CollaborationFoundationTests : IAsyncLifetime
{
    private AppDbContext _db = null!;
    private DbContextOptions<AppDbContext>? _sqlite;
    private DbContextOptions<SqlServerMigrationsDbContext>? _sql;
    private string? _database;
    private readonly string _file = Path.Combine(Path.GetTempPath(), "WriterApp_CollaborationTests_" + Guid.NewGuid().ToString("N") + ".db");
    private readonly Guid _project = Guid.NewGuid(), _document = Guid.NewGuid(), _section = Guid.NewGuid(), _page = Guid.NewGuid(), _scene = Guid.NewGuid();
    private bool SqlServer => _sql is not null;
    private AppDbContext Open() => SqlServer ? new SqlServerMigrationsDbContext(_sql!) : new AppDbContext(_sqlite!);

    public async Task InitializeAsync()
    {
        string? server = Environment.GetEnvironmentVariable("WRITERAPP_SYNC_SQLSERVER_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server)) _sqlite = new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={_file};Pooling=False").Options;
        else
        {
            _database = "WriterApp_CollaborationTests_" + Guid.NewGuid().ToString("N");
            _sql = new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(new SqlConnectionStringBuilder
            { DataSource = server, InitialCatalog = _database, IntegratedSecurity = true, TrustServerCertificate = true }.ConnectionString).Options;
        }
        _db = Open(); await _db.Database.EnsureCreatedAsync();
        foreach (string sql in DocumentSyncSchemaV1.Install(SqlServer)) await _db.Database.ExecuteSqlRawAsync(sql);
        foreach (string sql in MultiDocumentSyncSchemaV4.Install(SqlServer)) await _db.Database.ExecuteSqlRawAsync(sql);
        foreach (string sql in CollaborationWriteFenceSchema.Install(SqlServer)) await _db.Database.ExecuteSqlRawAsync(sql);
        var now = DateTimeOffset.UtcNow;
        _db.Projects.Add(new() { Id = _project, OwnerUserId = "owner", Title = "Novel", CreatedUtc = now, UpdatedUtc = now });
        _db.Documents.Add(new() { Id = _document, ProjectId = _project, OwnerUserId = "owner", Title = "Manuscript", CreatedAt = now, UpdatedAt = now });
        _db.Sections.Add(new() { Id = _section, DocumentId = _document, Title = "Chapter", CreatedAt = now, UpdatedAt = now });
        _db.Pages.Add(new() { Id = _page, DocumentId = _document, SectionId = _section, Title = "Page", Content = "<p>Original 日本語</p>", CreatedAt = now, UpdatedAt = now });
        _db.ProjectNodes.Add(new() { Id = _scene, ProjectId = _project, DocumentId = _document, LinkedSectionId = _section, Title = "Scene", NodeType = ProjectNodeType.Scene, UpdatedUtc = now });
        _db.SceneContents.Add(new() { SceneNodeId = _scene, ContentJson = "<p>Original 日本語</p>", UpdatedAtUtc = now });
        await _db.SaveChangesAsync(); _db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        // Only the random database created by this fixture can be deleted.
        if (_database is not null && _database.StartsWith("WriterApp_CollaborationTests_", StringComparison.Ordinal)
            && _db.Database.GetDbConnection().Database == _database) await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
        if (!SqlServer && File.Exists(_file)) File.Delete(_file);
    }

    private async Task Protect(CollaborationMode mode = CollaborationMode.Collaborative)
    {
        await _db.Documents.Where(d => d.Id == _document).ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, mode));
        _db.ChangeTracker.Clear();
    }

    [Theory]
    [InlineData(CollaborationMode.Migrating)]
    [InlineData(CollaborationMode.Collaborative)]
    public async Task DatabaseFenceBlocksRawWritesBulkDeletesAndStructureChanges(CollaborationMode mode)
    {
        await Protect(mode);
        foreach (var statement in new[]
        {
            $"UPDATE Pages SET Content='overwritten' WHERE Id={{0}}",
            $"DELETE FROM Pages WHERE Id={{0}}",
            $"UPDATE Sections SET Title='changed' WHERE Id={{0}}",
            $"DELETE FROM Sections WHERE Id={{0}}",
            $"UPDATE SceneContents SET ContentJson='overwritten' WHERE SceneNodeId={{0}}",
            $"UPDATE ProjectNodes SET Title='changed' WHERE Id={{0}}",
            $"UPDATE Projects SET Title='changed' WHERE Id={{0}}",
            $"UPDATE Documents SET Title='changed' WHERE Id={{0}}",
            $"DELETE FROM Documents WHERE Id={{0}}"
        }.Select((sql, index) => (sql, id: index switch { 0 or 1 => _page, 2 or 3 => _section, 4 or 5 => _scene, 6 => _project, _ => _document })))
        {
            var failure = await Assert.ThrowsAnyAsync<Exception>(() => _db.Database.ExecuteSqlRawAsync(statement.sql, statement.id));
            // SQL Server may enforce a restrictive FK before its AFTER trigger can run.
            Assert.True(CollaborationWriteException.FromStorage(failure) is not null || failure is SqlException { Number: 547 }, failure.ToString());
        }
        await Assert.ThrowsAnyAsync<Exception>(() => _db.Pages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.Content, "bulk overwrite")));
        Assert.Equal("<p>Original 日本語</p>", (await _db.Pages.AsNoTracking().SingleAsync()).Content);
    }

    [Fact]
    public async Task StaleTrackedEditorCannotWriteAfterAnotherContextChangesMode()
    {
        var page = await _db.Pages.SingleAsync();
        await using var migration = Open();
        await migration.Documents.Where(d => d.Id == _document).ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, CollaborationMode.Migrating));
        page.Content = "stale editor";
        var failure = await Assert.ThrowsAsync<CollaborationWriteException>(() => _db.SaveChangesAsync());
        Assert.Equal(CollaborationErrors.MigrationInProgress, failure.Error.Code);
        await using var observer = Open();
        Assert.Equal("<p>Original 日本語</p>", (await observer.Pages.SingleAsync()).Content);
    }

    [Fact]
    public async Task ModeTransitionSerializesWithOutstandingLegacyWrite()
    {
        await using var writer = Open();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await writer.Pages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.Content, "committed before migration"));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var transition = Task.Run(async () =>
        {
            await using var migration = Open(); started.SetResult();
            await migration.Documents.Where(d => d.Id == _document).ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, CollaborationMode.Migrating));
        });
        await started.Task; await transaction.CommitAsync(); await transition.WaitAsync(TimeSpan.FromSeconds(15));
        await Assert.ThrowsAnyAsync<Exception>(() => writer.Pages.Where(p => p.Id == _page).ExecuteUpdateAsync(s => s.SetProperty(p => p.Content, "too late")));
        Assert.Equal("committed before migration", (await _db.Pages.AsNoTracking().SingleAsync()).Content);
    }

    [Fact]
    public async Task ModeCannotBeDowngradedOrCombinedWithLegacyContentMutation()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(() => _db.Database.ExecuteSqlRawAsync(
            "UPDATE Documents SET CollaborationMode=1,Title='changed' WHERE Id={0}", _document));
        Assert.NotNull(CollaborationWriteException.FromStorage(error));
        Assert.Equal(CollaborationMode.Legacy, (await _db.Documents.AsNoTracking().SingleAsync()).CollaborationMode);
        await Protect();
        await Assert.ThrowsAnyAsync<Exception>(() => _db.Documents.Where(d => d.Id == _document).ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, CollaborationMode.Legacy)));
    }

    [Fact]
    public async Task LedgerUniquenessAndIndependentReceiptRetentionAreEnforced()
    {
        Guid generation = Guid.NewGuid(), installation = Guid.NewGuid(), update = Guid.NewGuid();
        _db.CollaborationPages.Add(new() { PageId = _page, DocumentId = _document, Generation = generation, ContentContractVersion = 2, ProtocolVersion = 1, SchemaFingerprint = new('a', 64), CommittedSequence = 1 });
        _db.CollaborationUpdates.Add(new() { PageId = _page, Generation = generation, Sequence = 1, InstallationId = installation, UpdateId = update, Sha256 = new('b', 64), AuthorUserId = "owner", Update = [1, 2, 3] });
        _db.CollaborationReceipts.Add(new() { PageId = _page, Generation = generation, InstallationId = installation, UpdateId = update, CommitSequence = 1, Sha256 = new('b', 64) });
        await _db.SaveChangesAsync();
        await _db.CollaborationUpdates.ExecuteDeleteAsync();
        Assert.Single(await _db.CollaborationReceipts.ToArrayAsync());
        _db.CollaborationReceipts.Add(new() { PageId = _page, Generation = generation, InstallationId = Guid.NewGuid(), UpdateId = Guid.NewGuid(), CommitSequence = 1, Sha256 = new('c', 64) });
        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    private TestServer Host()
    {
        return new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            services.AddLogging(); services.AddSingleton(_db);
            services.AddSingleton<IUserIdResolver, TestUsers>();
            services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
            services.AddAuthorization();
            services.AddScoped<CollaborationWriteFilter>();
            services.AddControllers(o => o.Filters.AddService<CollaborationWriteFilter>()).AddApplicationPart(typeof(PagesController).Assembly);
            var config = new ConfigurationBuilder().Build(); services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<IDocumentRepository>(new DocumentRepository(_db, NullLogger<DocumentRepository>.Instance));
            services.AddSingleton<IPageRepository>(new PageRepository(_db));
            services.AddSingleton<ISectionRepository>(new SectionRepository(_db, NullLogger<SectionRepository>.Instance, config));
            services.AddSingleton(Noop<ISearchIndexService>()); services.AddSingleton(Noop<IVersionHistoryService>());
            services.AddSingleton(Noop<IProjectWordCountService>()); services.AddSingleton(Noop<IProjectGoalsService>());
            services.AddSingleton(Noop<IVersionHistoryPolicyService>()); services.AddSingleton(Noop<IPageVersionDiffService>());
            services.AddSingleton<ISectionImportService, SectionImportService>();
        }).Configure(app => { app.UseRouting(); app.UseAuthentication(); app.UseAuthorization(); app.UseEndpoints(e => e.MapControllers()); }));
    }

    [Fact]
    public async Task CapabilitiesAreOwnerScopedAndFailClosedForUnknownSchemaAndGeneration()
    {
        using var host = Host(); using var client = host.CreateClient();
        string uri = $"/api/documents/{_document}/collaboration/capabilities";
        var legacy = await client.GetFromJsonAsync<CollaborationCapabilities>(uri);
        Assert.True(legacy!.CanWriteLegacy); Assert.False(legacy.FeatureAvailable);
        await Protect();
        var capability = await client.GetFromJsonAsync<CollaborationCapabilities>(uri);
        Assert.False(capability!.CanWriteLegacy); Assert.False(capability.CanWriteCollaboratively);
        Assert.Equal(CollaborationErrors.UpgradeRequired, capability.BlockedReason!.Code);
        var wrongGeneration = await client.GetFromJsonAsync<CollaborationCapabilities>($"{uri}?pageId={_page}&generation={Guid.NewGuid()}");
        Assert.Equal(CollaborationErrors.GenerationChanged, wrongGeneration!.BlockedReason!.Code);
        client.DefaultRequestHeaders.Add("X-Test-User", "other");
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(uri)).StatusCode);
        client.DefaultRequestHeaders.Remove("X-Test-User"); client.DefaultRequestHeaders.Add("X-Test-User", "anonymous");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(uri)).StatusCode);
    }

    [Theory]
    [InlineData(CollaborationMode.Migrating)]
    [InlineData(CollaborationMode.Collaborative)]
    public async Task RealHttpSaveSceneImportAndDeleteReturnStructuredConflict(CollaborationMode mode)
    {
        await Protect(mode);
        using var host = Host(); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-WriterApp-Internal-Projection", "true"); // Untrusted headers grant no bypass.
        var responses = new List<HttpResponseMessage>
        {
            await client.PutAsJsonAsync($"/api/pages/{_page}", new { content = "<p>Do not overwrite</p>" }),
            await client.PutAsJsonAsync($"/api/projects/{_project}/scenes/{_scene}/content", new { contentJson = "<p>Do not overwrite</p>" }),
            await client.DeleteAsync($"/api/pages/{_page}"),
            await client.PostAsync($"/api/pages/{_page}/versions/{Guid.NewGuid()}/restore", null),
            await client.PostAsJsonAsync($"/api/pages/{_page}/move", new { targetSectionId = _section, targetOrderIndex = 1 }),
            await client.PostAsJsonAsync($"/api/sections/{_section}/pages", new { title = "New page", content = "<p>Do not seed</p>" })
        };
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("Imported text")), "file", "writing.txt");
        form.Add(new StringContent("replace"), "mode"); form.Add(new StringContent(_section.ToString()), "targetSectionId");
        responses.Add(await client.PostAsync($"/api/documents/{_document}/sections/{_section}/import", form));
        // Checked translation routes authorize through their operation ledger;
        // a fabricated operation has no receipt and must not reach a write.
        using(var missing=await client.PostAsync($"/api/documents/{_document}/structured-translations/operations/{Guid.NewGuid()}/commit",null))
            Assert.Equal(HttpStatusCode.NotFound,missing.StatusCode);
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
                var error = await response.Content.ReadFromJsonAsync<CollaborationError>();
                Assert.True(error!.PreserveLocalDraft);
                Assert.Equal(mode == CollaborationMode.Migrating ? CollaborationErrors.MigrationInProgress : CollaborationErrors.LegacyWriteBlocked, error.Code);
            }
        }
        Assert.Equal("<p>Original 日本語</p>", (await _db.Pages.AsNoTracking().SingleAsync()).Content);
    }

    [Fact]
    public async Task LegacyRealHttpPageSaveStillWorks()
    {
        using var host = Host(); using var client = host.CreateClient();
        var response = await client.PutAsJsonAsync($"/api/pages/{_page}", new { content = "<p>Legacy changed</p>" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<p>Legacy changed</p>", (await _db.Pages.AsNoTracking().SingleAsync()).Content);
    }

    private sealed class TestUsers : IUserIdResolver
    { public string ResolveUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier)!; }

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string user = Request.Headers["X-Test-User"].FirstOrDefault() ?? "owner";
            return Task.FromResult(user == "anonymous" ? AuthenticateResult.NoResult() : AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "Test")), "Test")));
        }
    }

    private static T Noop<T>() where T : class => DispatchProxy.Create<T, NoopProxy>();
    public class NoopProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            Type type = method!.ReturnType;
            if (type == typeof(Task)) return Task.CompletedTask;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var result = type.GenericTypeArguments[0];
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(result).Invoke(null, [result.IsValueType ? Activator.CreateInstance(result) : null]);
            }
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }
    }
}
