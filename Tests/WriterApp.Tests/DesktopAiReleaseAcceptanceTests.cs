using WriterApp.Application.AI;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Canon;
using WriterApp.Shared.Sync;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    [Fact]
    public void ActualWebPageProjectionKeepsIdentityAndPreviewIncludesEveryPageWithoutMergingEditorWriting()
    {
        using var editor=new TranslationEditor{Http=new HttpClient(),AiCommandStatusService=new()};
        var section=Guid.NewGuid();var document=Guid.NewGuid();
        var first=new PageDto(Guid.NewGuid(),document,section,"First","<p><strong>Åsa</strong></p>",0,default,default);
        var second=first with{Id=Guid.NewGuid(),Title="Second",Content="<blockquote><p>日本語</p></blockquote>",OrderIndex=1};
        var pages=(Dictionary<Guid,List<PageDto>>)typeof(WriterApp.Client.Pages.DocumentEditor).GetField("_pagesBySection",TranslationPrivate)!.GetValue(editor)!;pages[section]=[first,second];
        var sections=(List<SectionDto>)typeof(WriterApp.Client.Pages.DocumentEditor).GetField("_sections",TranslationPrivate)!.GetValue(editor)!;sections.Add(new(section,document,"Opening",null,0,default,default,null,null));
        var selected=(PageDto)typeof(WriterApp.Client.Pages.DocumentEditor).GetMethod("GetPrimaryPage",TranslationPrivate)!.Invoke(editor,[section])!;
        Assert.Equal(first,selected);Assert.Equal(2,pages[section].Count);
        string preview=System.Text.RegularExpressions.Regex.Unescape(JsonSerializer.Serialize(typeof(WriterApp.Client.Pages.DocumentEditor).GetMethod("GetPreviewSections",TranslationPrivate)!.Invoke(editor,null)));
        Assert.Contains("Åsa",preview);Assert.Contains("日本語",preview);Assert.Equal(second,pages[section][1]);
    }
    [Fact]
    public async Task ActualWebPageSwitchKeepsActivePageAndDraftWhenSaveIsUnconfirmed()
    {
        using var editor=new TranslationEditor{Http=new HttpClient(),AiCommandStatusService=new(),Logger=Microsoft.Extensions.Logging.Abstractions.NullLogger<WriterApp.Client.Pages.DocumentEditor>.Instance};
        var section=Guid.NewGuid();var document=Guid.NewGuid();var page=new PageDto(Guid.NewGuid(),document,section,"First","<p>Authored</p>",0,default,default);
        var draft=new WriterApp.Client.Components.Editor.PageEditor();typeof(WriterApp.Client.Components.Editor.PageEditor).GetField("_isDirty",TranslationPrivate)!.SetValue(draft,true);
        TranslationSet(editor,"_activePage",page);TranslationSet(editor,"_activeSection",new SectionDto(section,document,"Opening",null,0,default,default,null,null));TranslationSet(editor,"_pageEditor",draft);
        await (Task)typeof(WriterApp.Client.Pages.DocumentEditor).GetMethod("SelectManuscriptPageAsync",TranslationPrivate)!.Invoke(editor,[new Microsoft.AspNetCore.Components.ChangeEventArgs{Value=Guid.NewGuid().ToString()}])!;
        Assert.Equal(page,typeof(WriterApp.Client.Pages.DocumentEditor).GetField("_activePage",TranslationPrivate)!.GetValue(editor));
        Assert.Contains("draft is retained",(string)typeof(WriterApp.Client.Pages.DocumentEditor).GetField("_manuscriptPageError",TranslationPrivate)!.GetValue(editor)!);
    }
    [Fact]
    public void SqlServerCoverAndNarrativeRepairsAreDiscoverableAndRetainExistingColumns()
    {
        using var db=new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer("Server=localhost;Database=ScriptOnly;Integrated Security=true;TrustServerCertificate=true").Options);
        Assert.Contains("20260316113000_AddProjectCoverImageUrlSqlServer",db.Database.GetMigrations());
        string sql=db.GetService<IMigrator>().GenerateScript(null,null,MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("COL_LENGTH(N'Projects', N'CoverImageUrl') IS NULL",sql);
        foreach(string table in new[]{"SceneCards","SectionSceneCards"})foreach(string column in new[]{"Summary","Status","SubplotTagsJson"})Assert.Contains($"COL_LENGTH(N'{table}', N'{column}') IS NULL",sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }
    // Optional SQL execution is restricted to a new local database. Supplied catalogs are never used.
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(true,true)]
    public async Task ReleaseDatabaseUpgradeAndCheckedReceiptConcurrencyPreserveOrdinaryWriting(bool sqlServer,bool preexistingCoverColumn)
    {
        string? configured=Environment.GetEnvironmentVariable("WRITERAPP_P22_SQLSERVER");if(sqlServer && string.IsNullOrWhiteSpace(configured))return;
        string name="WriterAppP22_"+Guid.NewGuid().ToString("N"),folder=Path.Combine(Path.GetTempPath(),"WriterAppP22_"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string connection=$"Data Source={Path.Combine(folder,"release.db")};Pooling=False";SqlConnectionStringBuilder? master=null;
        if(sqlServer) {
            master=new(configured);Assert.Contains(master.DataSource.Split('\\')[0].ToLowerInvariant(),new[]{".","localhost","(local)","127.0.0.1",Environment.MachineName.ToLowerInvariant()});Assert.True(master.IntegratedSecurity);master.InitialCatalog="master";master.Pooling=false;
            await using var admin=new SqlConnection(master.ConnectionString);await admin.OpenAsync();await using var create=admin.CreateCommand();create.CommandText=$"CREATE DATABASE [{name}]";await create.ExecuteNonQueryAsync();connection=new SqlConnectionStringBuilder(master.ConnectionString){InitialCatalog=name}.ConnectionString;
        }
        AppDbContext Db()=>sqlServer?new SqlServerMigrationsDbContext(new DbContextOptionsBuilder<SqlServerMigrationsDbContext>().UseSqlServer(connection).Options):new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        var project=Guid.NewGuid();var document=Guid.NewGuid();var section=Guid.NewGuid();var page=Guid.NewGuid();var other=Guid.NewGuid();
        const string before="<h2>Åsa 日本語 🧭</h2><p><strong>Authored</strong> writing.</p>",after="<h2>Åsa 日本語 🧭</h2><p><strong>Reviewed</strong> writing.</p>";
        try {
            await using(var db=Db()) {
                if(preexistingCoverColumn){await db.GetService<IMigrator>().MigrateAsync("20260304062920_InitialSqlServerBaseline_SqlServerSafe2");await db.Database.ExecuteSqlRawAsync("ALTER TABLE [Projects] ADD [CoverImageUrl] nvarchar(max) NULL;");}
                await db.GetService<IMigrator>().MigrateAsync(sqlServer?"20261003183237_WebTranslationOperationsSqlServer":"20261003183221_WebTranslationOperations");
                db.Projects.Add(new(){Id=project,OwnerUserId="ordinary-author",Title="Non-demo release",PrimaryDocumentId=document,CoverImageUrl=CoverTestFixture.Image});db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="ordinary-author",Title="Rich book"});db.Sections.Add(new(){Id=section,DocumentId=document,Title="Opening"});
                db.Pages.AddRange(new PageRecord{Id=page,DocumentId=document,SectionId=section,Content=before},new PageRecord{Id=other,DocumentId=document,SectionId=section,OrderIndex=1,Content="<p><em>Keep other page</em></p>"});db.DocumentSynopses.Add(new(){DocumentId=document,Logline="Keep authored synopsis 日本語"});await db.SaveChangesAsync();
                string version=(await db.DocumentSyncRecords.SingleAsync()).Version;await db.Database.MigrateAsync();db.ChangeTracker.Clear();
                Assert.Equal(version,(await db.DocumentSyncRecords.SingleAsync()).Version);Assert.Equal(before,(await db.Pages.SingleAsync(p=>p.Id==page)).Content);Assert.Equal(CoverTestFixture.Image,(await db.Projects.SingleAsync()).CoverImageUrl);Assert.Empty(await db.OnboardingDemoWorkspaces.ToListAsync());Assert.False(db.Database.HasPendingModelChanges());
                if(sqlServer) {
                    await using var read=new SqlConnection(connection);await read.OpenAsync();var columns=read.GetSchema("Columns").Rows.Cast<System.Data.DataRow>().Select(r=>$"{r["TABLE_NAME"]}.{r["COLUMN_NAME"]}").ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var missing=db.Model.GetEntityTypes().SelectMany(e=>e.GetProperties().Select(p=>$"{e.GetTableName()}.{p.GetColumnName(Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(e.GetTableName()!,e.GetSchema()))}")).Distinct().Where(c=>!columns.Contains(c)).Order().ToArray();Assert.True(missing.Length==0,"Missing SQL Server model columns: "+string.Join(", ",missing));
                }
            }
            WebAiHistoryIntent first;
            using(var server=Host(connection,sqlServer:sqlServer))using(var http=server.GetTestClient()) {
                Assert.Equal(HttpStatusCode.Unauthorized,(await http.GetAsync($"api/sections/{section}/pages")).StatusCode);http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
                var source=(await http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{document}?sectionId={section}&pageId={page}"))!;var proposal=Guid.NewGuid();
                await using(var db=Db()){db.AiActionHistoryEntries.Add(new(){Id=proposal,OwnerUserId="ordinary-author",DocumentId=document,SectionId=section,PageId=page,ActionKey="rewrite.selection",CreatedAt=DateTimeOffset.UtcNow,ResultJson=JsonSerializer.Serialize(new AiActionExecuteResponseDto(proposal,"Authored","Reviewed",null,DateTimeOffset.UtcNow,"rewrite.selection",WebSource:source),WebAiHistoryContracts.Json)});await db.SaveChangesAsync();}
                var id=Guid.NewGuid();first=new(1,id,id,proposal,1,null,"Applied",source,"Page",before,after);var secondId=Guid.NewGuid();var second=first with{OperationId=secondId,ApplicationId=secondId,AfterContent="<p>Competing approval</p>"};
                foreach(var intent in new[]{first,second}){using var prepared=await http.PostAsJsonAsync("api/ai/actions/history/web/operations",intent);Assert.True(prepared.IsSuccessStatusCode,await prepared.Content.ReadAsStringAsync());}
                async Task<HttpResponseMessage> Save(WebAiHistoryIntent intent){using var request=new HttpRequestMessage(HttpMethod.Put,$"api/pages/{page}"){Content=JsonContent.Create(new{Content=intent.AfterContent})};request.Headers.Add("X-WriterApp-AI-Source",JsonSerializer.Serialize(intent.Source));request.Headers.Add("X-WriterApp-AI-Operation",intent.OperationId.ToString());return await http.SendAsync(request);}
                var saves=await Task.WhenAll(Save(first),Save(second));Assert.Single(saves,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(saves,r=>r.StatusCode==HttpStatusCode.Conflict);first=saves[0].IsSuccessStatusCode?first:second;foreach(var response in saves)response.Dispose();using var duplicate=await Save(first);Assert.Equal(HttpStatusCode.OK,duplicate.StatusCode);
            }
            using(var server=Host(connection,sqlServer:sqlServer))using(var http=server.GetTestClient()) {
                http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");for(int retry=0;retry<2;retry++){using var report=await http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{first.OperationId}/report",first);Assert.True(report.IsSuccessStatusCode,await report.Content.ReadAsStringAsync());}
                http.DefaultRequestHeaders.Remove("X-Test-Owner");http.DefaultRequestHeaders.Add("X-Test-Owner","other-author");Assert.Equal(HttpStatusCode.NotFound,(await http.GetAsync($"api/sections/{section}/pages")).StatusCode);Assert.Equal(HttpStatusCode.Conflict,(await http.PostAsJsonAsync($"api/ai/actions/history/web/operations/{first.OperationId}/report",first)).StatusCode);
            }
            await using(var db=Db()) {
                Assert.Equal(first.AfterContent,(await db.Pages.SingleAsync(p=>p.Id==page)).Content);Assert.Equal("<p><em>Keep other page</em></p>",(await db.Pages.SingleAsync(p=>p.Id==other)).Content);Assert.Equal("Keep authored synopsis 日本語",(await db.DocumentSynopses.SingleAsync()).Logline);Assert.Single(await db.WebAiHistoryOperations.Where(o=>o.CommittedAt!=null).ToListAsync());Assert.Single(await db.WebAiHistoryOperations.Where(o=>o.ReportedAt!=null).ToListAsync());
                if(Environment.GetEnvironmentVariable("WRITERAPP_P22_EVIDENCE") is {} root)await File.WriteAllTextAsync(Path.Combine(root,sqlServer?preexistingCoverColumn?"sqlserver-existing-cover-acceptance.json":"sqlserver-acceptance.json":"sqlite-acceptance.json"),JsonSerializer.Serialize(new{Provider=db.Database.ProviderName,IsolatedDatabase=name,Authentication="Synthetic HTTP test scheme",Demo=false,AppliedMigrations=await db.Database.GetAppliedMigrationsAsync(),UpgradePreserved=true,ConcurrentWinnerCount=1,DuplicateSave=true,RestartReportRetry=true,OwnershipRejected=true,OtherWritingAndSynopsisPreserved=true,ExternalDatabase=false}));
            }
        }finally{
            if(master is not null){Assert.Matches("^WriterAppP22_[a-f0-9]{32}$",name);await using var admin=new SqlConnection(master.ConnectionString);await admin.OpenAsync();await using var drop=admin.CreateCommand();drop.CommandText=$"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";await drop.ExecuteNonQueryAsync();}Directory.Delete(folder,true);
        }
    }
    [Fact]
    public async Task ReleaseEquivalentNormalWebAndDeviceFixtureRetainsRichGraphAndCoverBytes()
    {
        string? evidence=Environment.GetEnvironmentVariable("WRITERAPP_P22_EVIDENCE");string root=Path.Combine(evidence??Path.GetTempPath(),"acceptance-data-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var project=Guid.NewGuid();var document=Guid.NewGuid();var sections=new[]{Guid.NewGuid(),Guid.NewGuid()};var pages=new[]{Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()};var scene=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        string[] html=["<h2>Åsa 日本語 🧭</h2><p><strong>Opening</strong> paragraph.</p>","<blockquote><p><em>Second</em> page.</p></blockquote><ul><li><p>Keep list</p></li></ul>","<table><tbody><tr><td><p>Other section</p></td></tr></tbody></table>"];
        const string metadata="{\"notesText\":\"Keep 日本語\",\"tags\":[\"authored\"]}";string database=Path.Combine(root,"web.db");
        var canon=Enum.GetValues<CanonKind>().ToDictionary(k=>k,k=>JsonSerializer.Serialize(new Dictionary<string,object>{["schemaVersion"]="1.0",[CanonContent.Collection(k)]=new[]{new Dictionary<string,string>{["id"]="entry-1",[k==CanonKind.Timeline?"title":"name"]=k==CanonKind.Character?"Åsa":k==CanonKind.Place?"Harbour 日本語":"Letter arrives"}}}));
        await using(var db=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={database};Pooling=False").Options)){
            await db.Database.MigrateAsync();db.Projects.Add(new(){Id=project,OwnerUserId="dev-oid",Title="Prompt 22 normal Unicode project",PrimaryDocumentId=document,CoverImageUrl=CoverTestFixture.Image,AuthorName="Åsa",Language="sv",CreatedUtc=now,UpdatedUtc=now});db.Documents.Add(new(){Id=document,ProjectId=project,OwnerUserId="dev-oid",Title="Unicode book",LanguageCode="sv",CreatedAt=now,UpdatedAt=now});
            for(int i=0;i<2;i++)db.Sections.Add(new(){Id=sections[i],DocumentId=document,Title=$"Section {i+1}",OrderIndex=i,LanguageCode="sv",CreatedAt=now,UpdatedAt=now});for(int i=0;i<3;i++)db.Pages.Add(new(){Id=pages[i],DocumentId=document,SectionId=sections[i==2?1:0],Title=$"Page {i+1}",OrderIndex=i==2?0:i,Content=html[i],CreatedAt=now,UpdatedAt=now});
            db.ProjectNodes.Add(new(){Id=scene,ProjectId=project,DocumentId=document,NodeType=ProjectNodeType.Scene,LinkedSectionId=sections[1],Title="Harbour scene",MetadataJson=metadata});db.SceneCards.Add(new(){SceneNodeId=scene,Summary="Authored opening 日本語"});db.DocumentSynopses.Add(new(){DocumentId=document,Logline="Åsa finds the letter",Notes="Preserve authored synopsis",UpdatedAt=now});
            foreach(var (kind,content) in canon)db.BibleSnapshots.Add(new(){Id=Guid.NewGuid(),DocumentId=document,BibleType=kind.ToString().ToLowerInvariant(),SchemaVersion=1,ContentJson=content,CreatedUtc=now,UpdatedUtc=now,LastRefreshUtc=now});db.UserEntitlements.Add(new(){UserId="dev-oid",PlanKey="Professional",SubscriptionStatus="Active",CurrentPeriodEndUtc=now.AddDays(10),AiMonthlyTokenBudget=1000000,PeriodStartUtc=now,CreatedAt=now,UpdatedUtc=now});await db.SaveChangesAsync();Assert.Empty(await db.OnboardingDemoWorkspaces.ToListAsync());
        }
        var store=new FileLocalDocumentStore(Path.Combine(root,"native","documents"));var local=await store.CreateProjectAsync("Prompt 22 normal Unicode project");var chapter=local.Project!.Nodes.First(n=>n.NodeType=="chapter").NodeId;
        local=await store.SaveAsync(local with{Title="Unicode book",LanguageCode="sv",Sections=sections.Select((id,i)=>new LocalSection{SectionId=id,Title=$"Section {i+1}",OrderIndex=i,LanguageCode="sv",CreatedAtUtc=now,UpdatedAtUtc=now,Pages=pages.Select((p,j)=>(p,j)).Where(x=>(x.j==2?1:0)==i).Select(x=>new LocalPage{PageId=x.p,Title=$"Page {x.j+1}",OrderIndex=x.j==2?0:x.j,CreatedAtUtc=now,UpdatedAtUtc=now,ContentFormat=LocalContentFormat.Html,Content=html[x.j]}).ToArray()}).ToArray(),Project=local.Project with{AuthorName="Åsa",Language="sv",Synopsis=new SyncSynopsis(Logline:"Åsa finds the letter",Notes:"Preserve authored synopsis"),Nodes=[new(){NodeId=chapter,NodeType="chapter",Title="Chapter 1"},new(){ParentId=chapter,NodeId=scene,NodeType="scene",Title="Harbour scene",SectionId=sections[1],MetadataJson=metadata,Card=new(null,null,null,null,null,null,"Authored opening 日本語",null,null,null,null,null,null,null,null)}]}});
        local=await store.SetProjectCoverAsync(local,CoverTestFixture.Image,Guid.NewGuid());var reopened=(await new FileLocalDocumentStore(Path.Combine(root,"native","documents")).GetAsync(local.DocumentId))!;
        Assert.Equal(html,reopened.Sections.SelectMany(s=>s.Pages).Select(p=>p.Content));Assert.Equal(pages,reopened.Sections.SelectMany(s=>s.Pages).Select(p=>p.PageId));Assert.Equal(metadata,Assert.Single(reopened.Project!.Nodes,n=>n.NodeType=="scene").MetadataJson);Assert.Equal(CoverTestFixture.Image,reopened.Project.CoverImageUrl);
        // Prepared canon is scoped to a synthetic fixture account; native launch/sign-in is untested.
        var cache=new LocalBibleStore(Path.Combine(root,"native","canon"));var scope=LocalBibleStore.ScopeKey(new Uri("http://127.0.0.1:5390/"),"p22-native-fixture-account");foreach(var (kind,content) in canon)await cache.SaveAsync(new(1,scope,reopened.DocumentId,document,kind,new(1,document,kind,"fixture-v1","fixture-v1","fixture-v1",new string('A',64),now,content,0,true),now,"Ready"));
        await File.WriteAllBytesAsync(Path.Combine(root,"source-cover.png"),Convert.FromBase64String(CoverTestFixture.Png));
        if(evidence is not null)await File.WriteAllTextAsync(Path.Combine(evidence,"acceptance-fixture.json"),JsonSerializer.Serialize(new{Root=root,Database=database,Project=project,Document=document,Sections=sections,Pages=pages,Scene=scene,NativeDocument=reopened.DocumentId,Html=html,Cover=CoverTestFixture.Image,CoverSha256=Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(CoverTestFixture.Png))),Demo=false,Authentication="Existing Development LocalDev",NativePrepared=true,NativeLaunched=false},WebAiHistoryContracts.Json));else Directory.Delete(root,true);
    }
}
