using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    // Synthetic authentication applies only to this in-process host; production authorization attributes remain active.
    private sealed class Authentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder) {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
            var owner = Request.Headers["X-Test-Owner"].ToString();
            return Task.FromResult(string.IsNullOrWhiteSpace(owner) ? AuthenticateResult.NoResult() : AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,owner)],Scheme.Name)),Scheme.Name)));
        }
    }
    private sealed class Owner : IUserIdResolver {
        public string ResolveUserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("Authenticated test owner required.");
    }
    private sealed class HistoryPolicy(int maxVersions = 100) : IVersionHistoryPolicyService {
        public Task<VersionHistoryPolicy> GetPolicyAsync(string userId) => Task.FromResult(new VersionHistoryPolicy("Professional","Test plan",true,maxVersions,30,true,true,true,true));
    }
    private static IHost Host(string connection,Action<IServiceCollection>? configure=null,bool sqlServer=false) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services => {
        if(sqlServer) {
            services.AddDbContext<SqlServerMigrationsDbContext>(options=>options.UseSqlServer(connection));
            services.AddScoped<AppDbContext>(sp=>sp.GetRequiredService<SqlServerMigrationsDbContext>());
        } else services.AddDbContext<AppDbContext>(options=>options.UseSqlite(connection));
        services.AddSingleton<WriterApp.Application.Subscriptions.IEntitlementService>(new CheckedSynopsisPlan());
        services.AddScoped<WriterApp.Application.AI.WebAiMutationFilter>();
        services.AddControllers(options=>options.Filters.AddService<WriterApp.Application.AI.WebAiMutationFilter>()).AddApplicationPart(typeof(PagesController).Assembly);
        services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions,Authentication>("Test",_=>{});
        services.AddAuthorization();
        services.AddScoped<IUserIdResolver,Owner>();
        services.AddScoped<IDocumentRepository,DocumentRepository>();services.AddScoped<ISectionRepository,SectionRepository>();services.AddScoped<IPageRepository,PageRepository>();
        services.AddSingleton<ISearchIndexBackfillQueue,SearchIndexBackfillQueue>();services.AddScoped<ISearchIndexService,SearchIndexService>();
        services.AddSingleton<IVersionHistoryPolicyService>(new HistoryPolicy());services.AddScoped<IVersionHistoryService,VersionHistoryService>();
        services.AddScoped<IProjectWordCountService,ProjectWordCountService>();services.AddScoped<IProjectGoalsService,ProjectGoalsService>();
        services.AddScoped<IDocumentLifecycleService,DocumentLifecycleService>();
        services.AddScoped<WriterApp.Application.Importing.ISectionImportService,WriterApp.Application.Importing.SectionImportService>();
        services.AddScoped<IProjectSceneLinkingService,ProjectSceneLinkingService>();services.AddScoped<IProjectDeletionService,ProjectDeletionService>();
        services.AddScoped<ManuscriptScopeFilter>();
        services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?>{{"Workflow:ProjectsEnabled","true"}}).Build());
        configure?.Invoke(services);
    }).Configure(app=>{app.UseRouting();app.UseAuthentication();app.UseAuthorization();app.UseEndpoints(endpoints=>endpoints.MapControllers());})).Start();

    [Fact]
    public async Task SqliteHistoryPrunesByUtcTimeAndRetentionWithoutPruningAnotherOwnersPage() {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=:memory:").Options);
        await db.Database.OpenConnectionAsync();await db.Database.EnsureCreatedAsync();
        var project=Guid.NewGuid();var document=Guid.NewGuid();var section=Guid.NewGuid();var page=Guid.NewGuid();
        db.Projects.Add(new(){Id=project,OwnerUserId="author"});
        db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="author"});
        db.Sections.Add(new(){Id=section,DocumentId=document});db.Pages.Add(new(){Id=page,DocumentId=document,SectionId=section});
        var now=DateTimeOffset.UtcNow;
        var versions=new[]{now.AddDays(-40),now.AddMinutes(-3).ToOffset(TimeSpan.FromHours(5)),now.AddMinutes(-2).ToOffset(TimeSpan.FromHours(-5)),now.AddMinutes(-1)}
            .Select(time=>new PageVersionRecord{Id=Guid.NewGuid(),PageId=page,DocumentId=document,CreatedAt=time}).ToArray();
        db.PageVersions.AddRange(versions);await db.SaveChangesAsync();
        var history=new VersionHistoryService(db,new HistoryPolicy(2),Microsoft.Extensions.Logging.Abstractions.NullLogger<VersionHistoryService>.Instance);
        await history.PruneAsync("other-author",page,default);Assert.Equal(4,await db.PageVersions.CountAsync());
        await history.PruneAsync("author",page,default);
        Assert.Equal(versions.Skip(2).Reverse().Select(v=>v.Id),(await history.ListVersionsAsync("author",page,default)).Select(v=>v.Id));
        Assert.Empty(await history.ListVersionsAsync("other-author",page,default));
    }

    [Fact]
    public async Task NormalWebPageSaveReopensAfterHostRestartAndPreservesRichContentIdsPlanningAndOwnership() {
        var root=Path.Combine(Path.GetTempPath(),"WriterApp.WebPersistenceTests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var connection=$"Data Source={Path.Combine(root,"web.db")};Pooling=False";
        var options=new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var projectId=Guid.NewGuid();var documentId=Guid.NewGuid();var sections=new[]{Guid.NewGuid(),Guid.NewGuid()};var pages=new[]{Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()};var sceneId=Guid.NewGuid();
        var source=new[]{"<h2>Åsa 日本語 🧭</h2><p><strong>Opening</strong> paragraph.</p>","<blockquote><p>Second page with <em>voice</em>.</p></blockquote>","<p>Other section stays intact.</p>"};
        const string metadata="{\"summary\":\"Authored scene\",\"notesText\":\"Keep notes 日本語\",\"tags\":[\"keep\"]}";
        const string revised="<h2>Åsa 日本語 🧭</h2><p><strong>Approved opening</strong> paragraph.</p>";
        try {
            await using(var db=new AppDbContext(options)) {
                await db.Database.EnsureCreatedAsync();
                db.Projects.Add(new(){Id=projectId,OwnerUserId="ordinary-author",Title="Non-demo manuscript",PrimaryDocumentId=documentId});
                db.Documents.Add(new(){Id=documentId,ProjectId=projectId,OwnerUserId="ordinary-author",Title="Unicode book",LanguageCode="sv"});
                for(int i=0;i<sections.Length;i++)db.Sections.Add(new(){Id=sections[i],DocumentId=documentId,Title=$"Section {i+1}",OrderIndex=i,LanguageCode="sv"});
                for(int i=0;i<pages.Length;i++)db.Pages.Add(new(){Id=pages[i],DocumentId=documentId,SectionId=sections[i==2?1:0],Title=$"Page {i+1}",OrderIndex=i==2?0:i,Content=source[i]});
                db.ProjectNodes.Add(new(){Id=sceneId,ProjectId=projectId,DocumentId=documentId,LinkedSectionId=sections[0],NodeType=ProjectNodeType.Scene,Title="Authored opening",MetadataJson=metadata});
                await db.SaveChangesAsync();
            }
            using(var server=Host(connection))using(var http=server.GetTestClient()) {
                Assert.Equal(HttpStatusCode.Unauthorized,(await http.GetAsync($"/api/sections/{sections[0]}/pages")).StatusCode);
                http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
                var response=await http.PutAsJsonAsync($"/api/pages/{pages[0]}",new{title="Reviewed opening",content=revised});
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                var saved=await response.Content.ReadFromJsonAsync<PageDto>();Assert.Equal(revised,saved!.Content);Assert.Equal(pages[0],saved.Id);
            }
            using(var restarted=Host(connection))using(var http=restarted.GetTestClient()) {
                http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
                var first=await http.GetFromJsonAsync<PageDto[]>($"/api/sections/{sections[0]}/pages");
                var second=await http.GetFromJsonAsync<PageDto[]>($"/api/sections/{sections[1]}/pages");
                Assert.NotNull(first);
                Assert.Equal(pages.Take(2),first.Select(p=>p.Id));Assert.Equal(new[]{revised,source[1]},first.Select(p=>p.Content));
                Assert.Equal(pages[2],Assert.Single(second!).Id);Assert.Equal(source[2],second![0].Content);
                http.DefaultRequestHeaders.Remove("X-Test-Owner");http.DefaultRequestHeaders.Add("X-Test-Owner","different-author");
                Assert.Equal(HttpStatusCode.NotFound,(await http.PutAsJsonAsync($"/api/pages/{pages[0]}",new{content="Wrong owner overwrite"})).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"/api/sections/{sections[0]}/pages")).StatusCode);
            }
            await using(var db=new AppDbContext(options)) {
                Assert.Equal(3,await db.Pages.CountAsync());Assert.Equal(2,await db.Sections.CountAsync());
                Assert.Equal(metadata,(await db.ProjectNodes.SingleAsync()).MetadataJson);Assert.Equal(documentId,(await db.Projects.SingleAsync()).PrimaryDocumentId);
                Assert.Equal(revised,(await db.Pages.SingleAsync(p=>p.Id==pages[0])).Content);
                Assert.Equal(revised+"\n\n"+source[1],(await db.SceneContents.SingleAsync()).ContentJson);
                string? evidence=Environment.GetEnvironmentVariable("WRITERAPP_P13_EVIDENCE");
                if(evidence is not null)await File.WriteAllTextAsync(Path.Combine(evidence,"normal-web-persistence.json"),JsonSerializer.Serialize(new{Host="In-process TestServer",Authentication="Synthetic test scheme",Storage="Isolated SQLite file",Project=projectId,Document=documentId,Sections=sections,Pages=pages,RichContent=revised,OtherPagesPreserved=true,PlanningPreserved=true,SceneMirrorPreserved=true,OwnershipRejected=true,HostRestart=true,Demo=false}));
            }
        } finally { Directory.Delete(root,true); }
    }
}
