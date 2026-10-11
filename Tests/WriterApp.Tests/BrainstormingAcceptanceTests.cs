using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared.Brainstorming;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Controllers;

namespace WriterApp.Tests;

public sealed class BrainstormingAcceptanceTests {
    [Fact]
    public async Task UneditedNotesFlushDoesNotCreateRecordsOrInvalidateReviewedContext() {
        var editor=new WriterApp.Client.Pages.DocumentEditor();
        BrainstormingKeepHostTests.Set(editor,"_activeSection",new SectionDto(Guid.NewGuid(),Guid.NewGuid(),"Existing section",null,0,DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,null,null));
        // No HTTP client or renderer: an unedited flush must perform no save at all.
        await BrainstormingKeepHostTests.Call(editor,"FlushNotesSaveAsync");
        Assert.Equal(0,(int)typeof(WriterApp.Client.Pages.DocumentEditor).GetField("_notesEditVersion",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(editor)!);
    }
    public class UnusedSearch : System.Reflection.DispatchProxy {
        protected override object? Invoke(System.Reflection.MethodInfo? method,object?[]? args)=>throw new InvalidOperationException("A protected multi-page save must not rewrite the page search index.");
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task MultiPageSceneSavePreservesIndividualPagesAndLanguageOnlyUpdates(bool languageOnly) {
        await using var data=new BrainstormingPersistenceTests.Database();await data.Start(true);await using var db=new AppDbContext(data.Options);
        var scene=Guid.NewGuid();db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=data.Document,SectionId=data.Section,OrderIndex=1,Content="<p><em>Later page.</em></p>"});
        db.ProjectNodes.Add(new(){Id=scene,ProjectId=data.Project,DocumentId=data.Document,NodeType=ProjectNodeType.Scene,LinkedSectionId=data.Section,Title="Linked scene"});
        var aggregate="<p><strong>Åsa 日本語 🧭</strong></p>\n\n<p><em>Later page.</em></p>";db.SceneContents.Add(new(){SceneNodeId=scene,ContentJson=aggregate,LanguageCode="en",UpdatedAtUtc=DateTimeOffset.UtcNow});await db.SaveChangesAsync();
        var before=await db.Pages.AsNoTracking().OrderBy(p=>p.Id).Select(p=>p.Content).ToArrayAsync();
        var controller=new ProjectSceneContentController(db,new UserIdResolver(NullLogger<UserIdResolver>.Instance),new ProjectWordCountService(db),System.Reflection.DispatchProxy.Create<ISearchIndexService,UnusedSearch>(),NullLogger<ProjectSceneContentController>.Instance) {ControllerContext=new(){HttpContext=new DefaultHttpContext{User=new ClaimsPrincipal(new ClaimsIdentity([new Claim("oid","author")],"test"))}}};
        var result=await controller.Put(data.Project,scene,new(languageOnly?aggregate:aggregate.Replace("\n\n",""),"sv"),default);
        if(languageOnly)Assert.IsType<OkObjectResult>(result.Result);else Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(before,await db.Pages.AsNoTracking().OrderBy(p=>p.Id).Select(p=>p.Content).ToArrayAsync());Assert.Equal(aggregate,(await db.SceneContents.AsNoTracking().SingleAsync()).ContentJson);
    }
    [Fact]
    public async Task IntegratedFixtureHasOwnedMultiPageChaptersStandaloneAndExplicitConflictingNotes() {
        await using var data=new BrainstormingPersistenceTests.Database();await data.Start(true);await using var db=new AppDbContext(data.Options);var now=DateTimeOffset.UtcNow;
        var chapterOne=Guid.NewGuid();var chapterTwo=Guid.NewGuid();var preceding=Guid.NewGuid();var sceneOne=Guid.NewGuid();var sceneTwo=Guid.NewGuid();var standalone=Guid.NewGuid();var standaloneSection=Guid.NewGuid();var standalonePage=Guid.NewGuid();var standaloneProject=Guid.NewGuid();
        (await db.Documents.SingleAsync()).Title="P12 synthetic arrival manuscript";(await db.Projects.SingleAsync()).PrimaryDocumentId=data.Document;(await db.Sections.SingleAsync()).Title="Chapter 2 existing opening";(await db.Sections.SingleAsync()).OrderIndex=1;
        (await db.Pages.SingleAsync()).Title="Chapter 2 first page";(await db.Pages.SingleAsync()).Content="<p>Åsa watched the station. Her brother knew nothing.</p><p><strong>Keep this authored paragraph.</strong> 日本語 🧭</p>";
        db.Sections.Add(new(){Id=preceding,DocumentId=data.Document,Title="Chapter 1 departure",OrderIndex=0});
        db.Pages.AddRange(new(){Id=Guid.NewGuid(),DocumentId=data.Document,SectionId=preceding,Title="Letter",OrderIndex=0,Content="<p>The sealed letter arrived at the harbor.</p>"},new(){Id=Guid.NewGuid(),DocumentId=data.Document,SectionId=preceding,Title="Departure",OrderIndex=1,Content="<p>Åsa boarded the train. Her brother remained unaware.</p>"},new(){Id=Guid.NewGuid(),DocumentId=data.Document,SectionId=data.Section,Title="Chapter 2 later page",OrderIndex=1,Content="<h2>Later arrival</h2><p><em>Preserve this later page.</em></p>"});
        db.ProjectNodes.AddRange(new(){Id=chapterOne,ProjectId=data.Project,DocumentId=data.Document,Title="Chapter 1",NodeType=ProjectNodeType.Chapter,OrderIndex=0},new(){Id=chapterTwo,ProjectId=data.Project,DocumentId=data.Document,Title="Chapter 2",NodeType=ProjectNodeType.Chapter,OrderIndex=1},new(){Id=sceneOne,ProjectId=data.Project,DocumentId=data.Document,ParentId=chapterOne,Title="Departure",NodeType=ProjectNodeType.Scene,LinkedSectionId=preceding,OrderIndex=0},new(){Id=sceneTwo,ProjectId=data.Project,DocumentId=data.Document,ParentId=chapterTwo,Title="Existing arrival",NodeType=ProjectNodeType.Scene,LinkedSectionId=data.Section,OrderIndex=0});
        db.DocumentSynopses.Add(new(){DocumentId=data.Document,Premise="A sealed letter makes Åsa suspicious of a stranger.",EndingIntent="Her brother must remain unaware until Chapter 4.",Notes="Character: Åsa distrusts easy promises. Place: a busy railway station.",UpdatedAt=now});
        db.PageNotes.Add(new(){PageId=data.Page,Notes="Optional conflicting plan: her brother already learns the secret in Chapter 2. Treat this as an idea, not established manuscript fact.",UpdatedAt=now});
        db.Projects.Add(new(){Id=standaloneProject,OwnerUserId="author",Title="P12 standalone backing record"});db.Documents.Add(new(){Id=standalone,ProjectId=standaloneProject,OwnerUserId="author",Title="P12 synthetic standalone"});db.Sections.Add(new(){Id=standaloneSection,DocumentId=standalone,Title="Standalone opening"});db.Pages.Add(new(){Id=standalonePage,DocumentId=standalone,SectionId=standaloneSection,Content="<p>Standalone author text.</p>"});
        // Disposable local development entitlement. It still uses the real policy,
        // quota and usage services; no production account/subscription is modified.
        db.UserEntitlements.Add(new(){UserId="author",PlanKey="Professional",SubscriptionStatus="Active",CreatedAt=now,PeriodStartUtc=now,UpdatedUtc=now,AiMonthlyTokenBudget=100_000});await db.SaveChangesAsync();
        var source=await new BrainstormingStorySourceReader(db,"author").ReadAsync(data.Document);Assert.Equal(4,source.Pages.Count);var scope=new BrainstormingScope(1,Guid.NewGuid(),data.Document,data.Project,BrainstormingIdentitySpace.Server,data.Backend,BrainstormingValidation.Fingerprint("author"),null);
        var projected=source.Pages.Select(p=>new BrainstormingProjectedPage(p.Id,BrainstormingValidation.Fingerprint(p.Content),BrainstormingDraftContent.PlainText(p.Content))).ToArray();var preparation=BrainstormingContextPolicy.Prepare(scope,source,projected,new(ChapterNumber:2,NewFirstSection:true),[]);Assert.True(preparation.Context!.Policy!.Ready);Assert.Equal(BrainstormingTargetKind.CreateSection,preparation.Resolution.Target!.Kind);Assert.Contains("sealed letter",BrainstormingContextPolicy.GenerationData(preparation.Context));Assert.DoesNotContain("Optional conflicting plan",BrainstormingContextPolicy.GenerationData(preparation.Context));
        var note=source.References.Single(r=>r.Kind==BrainstormingSourceKind.StoryNote);var included=BrainstormingContextPolicy.Prepare(scope,source,projected,new(ChapterNumber:2),[new(note.Id,true)]);Assert.Contains("Optional conflicting plan",BrainstormingContextPolicy.GenerationData(included.Context!));Assert.Null((await new BrainstormingStorySourceReader(db,"author").ReadAsync(standalone)).Outline.ProjectId);await Assert.ThrowsAnyAsync<Exception>(()=>new BrainstormingStorySourceReader(db,"foreign").ReadAsync(data.Document));
        Guid restoredSessionId=Guid.NewGuid();
        if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_ACCEPTANCE_DB") is {Length:>0} path){path=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(path)!);if(File.Exists(path))throw new IOException("Refusing to replace an existing acceptance database.");using var copy=new SqliteConnection("Data Source="+path);await copy.OpenAsync();data.Connection.BackupDatabase(copy);
            await using(var launch=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(copy).Options)){foreach(var d in await launch.Documents.ToListAsync())d.OwnerUserId="dev-oid";foreach(var p in await launch.Projects.ToListAsync())p.OwnerUserId="dev-oid";var entitlement=await launch.UserEntitlements.SingleAsync();launch.UserEntitlements.Remove(entitlement);await launch.SaveChangesAsync();entitlement.UserId="dev-oid";launch.UserEntitlements.Add(entitlement);launch.UserProfiles.Add(new(){UserId="dev-oid",DisplayName="Dev User",Email="dev@local",HasCompletedOnboarding=true,HasOnboarded=true,OnboardingStep=10,CreatedUtc=now.UtcDateTime,UpdatedUtc=now.UtcDateTime});await launch.SaveChangesAsync();}
            await using(var launch=new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(copy).Options)) {
                var restoredScope=scope with {SessionId=restoredSessionId,BackendKey=BrainstormingValidation.Fingerprint("http://127.0.0.1:5399/"),AccountKey=BrainstormingValidation.Fingerprint("dev-oid")};
                var restoredSource=await new BrainstormingStorySourceReader(launch,"dev-oid").ReadAsync(data.Document);
                var placementFixture=Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_PLACEMENT_FIXTURE")=="1";
                var prepared=BrainstormingContextPolicy.Prepare(restoredScope,restoredSource,projected,placementFixture?new(SectionId:data.Section):new(ChapterNumber:2,NewFirstSection:true),[]);
                var fixture=BrainstormingPersistence.ReScope(BrainstormingContractTests.Fixture(),restoredScope);
                var restoredContext=prepared.Context! with {Id=fixture.Contexts[0].Id};var restoredTarget=prepared.Resolution.Target! with {Id=fixture.Targets[0].Id};
                var restoredDraft=fixture.Drafts[0] with {ManuallyEdited=true,PlainText="She kept her distance. 日本語 🧭",EditorHtml="<p><strong>She kept her distance.</strong> 日本語 🧭</p>"};
                fixture=fixture with {Contexts=[restoredContext],Targets=[restoredTarget],Drafts=[restoredDraft],Approvals=[],Applications=[]};
                await new BrainstormingSessions(launch,"dev-oid",restoredScope.BackendKey).WriteAsync(new(1,Guid.NewGuid(),0,fixture),default);
            }
            await File.WriteAllTextAsync(Path.ChangeExtension(path,".json"),System.Text.Json.JsonSerializer.Serialize(new{scope="Disposable migrated SQLite fixture; built-in LocalDev dev-oid identity and synthetic entitlement, not an External ID login. Restored draft is synthetic, not provider evidence.",restoredSessionId,documentId=data.Document,sectionId=data.Section,pageId=data.Page,projectId=data.Project,chapterOne,chapterTwo,standaloneDocumentId=standalone,standaloneSectionId=standaloneSection,standalonePageId=standalonePage,sourcePages=source.Pages.Select(p=>new{p.Id,p.Content}),lastMigration=(await db.Database.GetAppliedMigrationsAsync()).Last()},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
    }
}
