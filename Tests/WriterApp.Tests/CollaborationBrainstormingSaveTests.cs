using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WriterApp.Application.AI;
using WriterApp.Application.Collaboration;
using WriterApp.Data.Documents;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Collaboration;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class CollaborationConversionTests
{
    [Fact]
    public async Task SharedBrainstormPlanningMigrationRoundTripPreservesModelAndFences()
    {
        var migrator=_db.GetService<IMigrator>();
        await migrator.MigrateAsync(_db.Database.IsSqlServer()?"20261018220001_CollaborationStructureCommandsSqlServer":"20261018220000_CollaborationStructureCommands");
        await _db.Database.MigrateAsync();Assert.False(_db.Database.HasPendingModelChanges());
        await PrepareBrainstormBook();
        await Assert.ThrowsAnyAsync<Exception>(()=>_db.DocumentSynopses.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Notes,"Unreviewed")));
        await Assert.ThrowsAnyAsync<Exception>(()=>_db.ProjectNodes.ExecuteUpdateAsync(n=>n.SetProperty(x=>x.Title,"Unreviewed")));
    }
    private async Task PrepareBrainstormBook()
    {
        var section=await _db.Sections.SingleAsync();var project=(await _db.Documents.SingleAsync()).ProjectId;
        _db.ProjectNodes.Add(new(){Id=Guid.NewGuid(),DocumentId=_document,ProjectId=project,NodeType=ProjectNodeType.Chapter,Title="First chapter"});
        _db.DocumentSynopses.Add(new(){DocumentId=_document,Logline="Untouched logline",Theme="Untouched theme",Notes="Original author note"});
        await _db.Pages.Where(p=>p.Id==_page).ExecuteUpdateAsync(p=>p.SetProperty(x=>x.Content,"<p><em>Åsa</em> waited. 日本語 🧭</p>"));
        await _db.SaveChangesAsync();_version=(await _db.DocumentSyncRecords.AsNoTracking().SingleAsync()).Version;
        await Convert();await InviteMember("author",CollaborationRole.Editor);
    }
    private async Task<(BrainstormingSession Session,BrainstormingStorySnapshot Source)> SharedDraft(HttpClient http,string actor,bool newSection=false)
    {
        var story=(await http.GetFromJsonAsync<BrainstormingStorySnapshot>($"/api/brainstorming/sources/{_document}",BrainstormingValidation.Json))!;
        var scope=new BrainstormingScope(1,Guid.NewGuid(),_document,story.Outline.ProjectId,BrainstormingIdentitySpace.Server,BrainstormingValidation.Fingerprint(http.BaseAddress!.AbsoluteUri),BrainstormingValidation.Fingerprint(actor),null);
        var target=newSection?new BrainstormingTargetQuery(ChapterNumber:1,NewFirstSection:true):new BrainstormingTargetQuery(SectionId:story.Pages.Single(p=>p.Id==_page).SectionId);
        using var prepared=await http.PostAsJsonAsync("/api/brainstorming/conversation/context",new BrainstormingServerContextRequest(1,scope,target,[],[],60_000),BrainstormingValidation.Json);
        Assert.True(prepared.IsSuccessStatusCode,await prepared.Content.ReadAsStringAsync());
        var context=(await prepared.Content.ReadFromJsonAsync<BrainstormingServerContext>(BrainstormingValidation.Json))!.Preparation;
        var session=BrainstormingPersistence.Empty(scope) with {State=BrainstormingConversationState.Saved,Contexts=[context.Context!],Targets=[context.Resolution.Target!]};
        using var saved=await http.PostAsJsonAsync("/api/brainstorming/sessions",new BrainstormingWrite(1,Guid.NewGuid(),0,session),BrainstormingValidation.Json);Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        using var response=await http.PostAsJsonAsync("/api/brainstorming/conversation/stream",new BrainstormingSend(1,scope,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,context.Context!.Id,context.Resolution.Target!.Id,null,"Draft a quiet meeting."),BrainstormingValidation.Json);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var events=(await response.Content.ReadAsStringAsync()).Split('\n',StringSplitOptions.RemoveEmptyEntries).Select(line=>System.Text.Json.JsonSerializer.Deserialize<BrainstormingConversationEvent>(line,BrainstormingValidation.Json)!).ToArray();
        Assert.True(events[^1].Kind==BrainstormingEventKind.Completed,BrainstormingPersistence.Json(events[^1]));
        session=(await http.GetFromJsonAsync<BrainstormingSession>($"/api/brainstorming/sessions/{scope.SessionId}?documentId={_document}",BrainstormingValidation.Json))!;Assert.Single(session.Drafts);
        return(session,story);
    }
    private static BrainstormingConversationTests.Provider DraftProvider()=>new(){Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Draft ready.",1,BrainstormingIntent.Draft,new("Hon tvivlade. 日本語 🧭","Understated",[]),[],[]))};
    private static async Task<T> PostBrainstorm<T>(HttpClient http,string route,object value)
    {
        using var result=await http.PostAsJsonAsync(route,value,BrainstormingValidation.Json);
        Assert.True(result.IsSuccessStatusCode,$"{route}: {result.StatusCode}: {await result.Content.ReadAsStringAsync()}");
        return (await result.Content.ReadFromJsonAsync<T>(BrainstormingValidation.Json))!;
    }
    [Theory]
    [InlineData("owner","insert")][InlineData("author","replace")][InlineData("author","new")][InlineData("owner","after")]
    public async Task SharedBrainstormKeepFullHttpFlowRestartsInspectsUndoRedoAndPreservesLaterWriting(string actor,string mode)
    {
        await PrepareBrainstormBook();var legacy=await _db.Pages.AsNoTracking().OrderBy(p=>p.Id).Select(p=>p.Content).ToArrayAsync();
        using var api=Api(true,configure:s=>BrainstormServices(s,DraftProvider(),actor));using var http=api.CreateClient();http.DefaultRequestHeaders.Add("X-Test-Owner",actor);
        var (session,story)=await SharedDraft(http,actor,mode=="new");var scope=session.Scope;
        var r=BrainstormingKeepContract.Review(scope,session.Drafts.Single(),session.Targets.Single(),BrainstormingKeepContract.Writing(story),mode=="replace"?0:null,mode=="replace"?3:null,"Quiet meeting");
        if(mode=="after")r=BrainstormingPlacements.ReviewAt(r,new(BrainstormingPlacementMode.NewSectionAfter,story.Pages[0].SectionId),"Quiet meeting");
        string root=$"/api/brainstorming/{scope.SessionId}/keeps/{r.OperationId}";
        var approved=await PostBrainstorm<BrainstormingKeepReceipt>(http,root,r);Assert.Equal(BrainstormingApplicationState.Approved,approved.State);
        var receipt=await PostBrainstorm<BrainstormingKeepReceipt>(http,root+"/commit",scope);Assert.Equal(BrainstormingApplicationState.Committed,receipt.State);
        _db.ChangeTracker.Clear();Assert.Equal(receipt,await PostBrainstorm<BrainstormingKeepReceipt>(http,root+"/inspect",r));Assert.Equal(receipt,await PostBrainstorm<BrainstormingKeepReceipt>(http,root+"/commit",scope));
        var pageId=r.Kind==BrainstormingTargetKind.CreateSection?r.NewPageId:BrainstormingPlacements.Destination(r).PageId!.Value;
        var shared=await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access);Assert.Equal(new AngleSharp.Html.Parser.HtmlParser().ParseDocument(r.AfterHtml).Body!.InnerHtml,shared.Pages.Single(p=>p.PageId==pageId).Html);
        Assert.Equal(legacy,await _db.Pages.AsNoTracking().Where(p=>p.Id!=r.NewPageId).OrderBy(p=>p.Id).Select(p=>p.Content).ToArrayAsync());
        string moveRoute=root+$"/move?documentId={_document}&projectId={scope.ProjectId}";
        var move=new BrainstormingKeepMove(Guid.NewGuid(),1,false);var undone=await PostBrainstorm<BrainstormingKeepReceipt>(http,moveRoute,move);Assert.Equal(BrainstormingApplicationState.Undone,undone.State);
        Assert.Equal(undone,await PostBrainstorm<BrainstormingKeepReceipt>(http,moveRoute,move));
        shared=await CollaborationProjectionService.Read(_db,"author",_document,new(),default,Access);
        if(r.Kind==BrainstormingTargetKind.CreateSection)Assert.DoesNotContain(shared.Pages,p=>p.PageId==pageId);else Assert.Equal(story.Pages.Single(p=>p.Id==pageId).Content,shared.Pages.Single(p=>p.PageId==pageId).Html);
        Assert.Equal(BrainstormingApplicationState.Committed,(await PostBrainstorm<BrainstormingKeepReceipt>(http,moveRoute,new BrainstormingKeepMove(Guid.NewGuid(),2,true))).State);
        shared=await CollaborationProjectionService.Read(_db,"owner",_document,new(),default,Access);var page=shared.Pages.Single(p=>p.PageId==pageId);
        await new CollaborationCheckedEdits(_db,_codec,Access).Apply("owner",_document,new(Guid.NewGuid(),await new WebAiSourceService(_db,"owner",Access).Capture(_document,page.SectionId,pageId,null,default),page.Html,page.Html+"<p>Later shared writing.</p>"),default);
        using var conflict=await http.PostAsJsonAsync(moveRoute,new BrainstormingKeepMove(Guid.NewGuid(),3,false),BrainstormingValidation.Json);Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);
        var copy=await PostBrainstorm<Guid>(http,root+$"/original?documentId={_document}&projectId={scope.ProjectId}",new{});Assert.Equal(actor,(await _db.Documents.AsNoTracking().SingleAsync(d=>d.Id==copy)).OwnerUserId);
        Assert.Contains("Later shared writing",(await CollaborationProjectionService.Read(_db,"author",_document,new(),default,Access)).Pages.Single(p=>p.PageId==pageId).Html);
        Assert.Equal(3,await _db.WebAiHistoryOperations.CountAsync());Assert.Single(await _db.AiActionHistoryEntries.ToArrayAsync());
        Assert.Equal(0,await _db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM CollaborationPlanningPermits").SingleAsync());
    }
    [Theory]
    [InlineData(BrainstormingPlanningKind.SceneIdea)][InlineData(BrainstormingPlanningKind.StoryNote)][InlineData(BrainstormingPlanningKind.CharacterNote)][InlineData(BrainstormingPlanningKind.SynopsisField)]
    public async Task SharedBrainstormAllPlanningKindsCommitInspectUndoAndRetainProse(BrainstormingPlanningKind kind)
    {
        await PrepareBrainstormBook();using var api=Api(true,configure:s=>BrainstormServices(s,DraftProvider(),"author"));using var http=api.CreateClient();http.DefaultRequestHeaders.Add("X-Test-Owner","author");
        var (session,story)=await SharedDraft(http,"author");var scope=session.Scope;string root=$"/api/brainstorming/{scope.SessionId}/planning";
        var snapshot=await PostBrainstorm<BrainstormingPlanningSnapshot>(http,root+"/snapshot",scope);var r=BrainstormingPlanningTests.Review(session,snapshot,kind);
        var receipt=await PostBrainstorm<BrainstormingPlanningReceipt>(http,root+"/apply",r);Assert.Equal(BrainstormingApplicationState.Committed,receipt.State);_db.ChangeTracker.Clear();
        Assert.Equal(receipt,await PostBrainstorm<BrainstormingPlanningReceipt>(http,root+"/inspect",r));Assert.Equal(receipt,await PostBrainstorm<BrainstormingPlanningReceipt>(http,root+"/apply",r));
        var saved=await new BrainstormingStorySourceReader(_db,"owner",Access).ReadAsync(_document);Assert.NotEqual(story.Stamp,saved.Stamp);Assert.Equal(story.Pages.Select(p=>p.Content),saved.Pages.Select(p=>p.Content));
        Assert.Equal("Untouched theme",(await _db.DocumentSynopses.AsNoTracking().SingleAsync()).Theme);
        if(kind==BrainstormingPlanningKind.SceneIdea)Assert.Contains(saved.Outline.Nodes,n=>n.Id==r.NewNodeId && n.SectionId==null);
        else Assert.Equal(r.Fields.Single().After,BrainstormingPlanningContract.Value((await PostBrainstorm<BrainstormingPlanningSnapshot>(http,root+"/snapshot",scope)).Synopsis,r.Fields.Single().Field));
        var undone=await PostBrainstorm<BrainstormingPlanningReceipt>(http,root+"/undo",r);Assert.Equal(BrainstormingApplicationState.Undone,undone.State);Assert.Equal(undone,await PostBrainstorm<BrainstormingPlanningReceipt>(http,root+"/undo",r));
        Assert.Equal("Original author note",(await _db.DocumentSynopses.AsNoTracking().SingleAsync()).Notes);Assert.False(await _db.ProjectNodes.AsNoTracking().AnyAsync(n=>n.Id==r.NewNodeId));
        // Ordinary legacy/raw SQL remains fenced after the reviewed transaction ends.
        await Assert.ThrowsAnyAsync<Exception>(()=>_db.DocumentSynopses.ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Notes,"Stale legacy save")));
        await Assert.ThrowsAnyAsync<Exception>(()=>_db.ProjectNodes.ExecuteUpdateAsync(n=>n.SetProperty(x=>x.Title,"Stale outline save")));
    }
    [Theory][InlineData("stale")][InlineData("viewer")][InlineData("revoked")][InlineData("failure")]
    public async Task SharedBrainstormApprovedSavesRejectChangedAccessAndSourcesAndRollbackAtomically(string failure)
    {
        await PrepareBrainstormBook();using var api=Api(true,configure:s=>BrainstormServices(s,DraftProvider(),"author"));using var http=api.CreateClient();http.DefaultRequestHeaders.Add("X-Test-Owner","author");
        var (session,story)=await SharedDraft(http,"author",true);var scope=session.Scope;var service=new BrainstormingKeeps(_db,"author",scope.BackendKey,Access,_codec);
        var r=BrainstormingKeepContract.Review(scope,session.Drafts.Single(),session.Targets.Single(),BrainstormingKeepContract.Writing(story));await service.PrepareAsync(r);
        var planning=new BrainstormingPlanningSaves(_db,"author",scope.BackendKey,Access);var p=BrainstormingPlanningTests.Review(session,await planning.CaptureAsync(scope),BrainstormingPlanningKind.StoryNote);
        if(failure=="stale") {
            var page=story.Pages.Single(x=>x.Id==_page);await new CollaborationCheckedEdits(_db,_codec,Access).Apply("owner",_document,new(Guid.NewGuid(),await new WebAiSourceService(_db,"owner",Access).Capture(_document,page.SectionId,page.Id,null,default),page.Content,"<p>Later collaborator writing.</p>"),default);
        } else if(failure is "viewer" or "revoked")await Sharing.Change("owner",_document,"author",new(1,failure=="viewer"?CollaborationRole.Viewer:CollaborationRole.Editor,failure=="revoked"),default);
        else {
            // Fail the durable CRDT insert after reviewed structure has been added.
            await _db.Database.ExecuteSqlRawAsync(_db.Database.IsSqlServer()
                ? "CREATE TRIGGER FailBrainstormSnapshot ON CollaborationSnapshots AFTER INSERT AS THROW 51099,'fixture snapshot failure',1;"
                : "CREATE TRIGGER FailBrainstormSnapshot BEFORE INSERT ON CollaborationSnapshots BEGIN SELECT RAISE(ABORT,'fixture snapshot failure'); END;");
        }
        await Assert.ThrowsAnyAsync<Exception>(()=>service.CommitAsync(scope,r.OperationId));_db.ChangeTracker.Clear();
        Assert.False(await _db.Sections.AnyAsync(s=>s.Id==r.NewSectionId));Assert.False(await _db.ProjectNodes.AnyAsync(n=>n.Id==r.NewNodeId));Assert.Empty(await _db.AiActionHistoryEntries.ToArrayAsync());
        Assert.Equal(0,await _db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM CollaborationPlanningPermits").SingleAsync());
        if(failure!="failure")await Assert.ThrowsAnyAsync<Exception>(()=>planning.ApplyAsync(p));
        else {
            await _db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailBrainstormSnapshot");
            Assert.Equal(BrainstormingApplicationState.Approved,(await service.InspectAsync(scope,r.OperationId))!.State);
            Assert.Equal(BrainstormingApplicationState.Committed,(await service.CommitAsync(scope,r.OperationId)).State);
        }
    }
    [Theory][InlineData(BrainstormingPlanningKind.StoryNote)][InlineData(BrainstormingPlanningKind.SceneIdea)]
    public async Task SharedBrainstormPlanningUndoPreservesLaterAuthorChanges(BrainstormingPlanningKind kind)
    {
        await PrepareBrainstormBook();using var api=Api(true,configure:s=>BrainstormServices(s,DraftProvider(),"author"));using var http=api.CreateClient();http.DefaultRequestHeaders.Add("X-Test-Owner","author");
        var (session,_)=await SharedDraft(http,"author");var service=new BrainstormingPlanningSaves(_db,"author",session.Scope.BackendKey,Access);
        var r=BrainstormingPlanningTests.Review(session,await service.CaptureAsync(session.Scope),kind);await service.ApplyAsync(r);
        if(kind==BrainstormingPlanningKind.StoryNote) {
            var later=r with {OperationId=Guid.NewGuid(),CandidateId=Guid.NewGuid(),Source=(await service.CaptureAsync(session.Scope)).Source,Fields=[new("Notes",r.Fields.Single().After,"Later author planning.")]};await service.ApplyAsync(later);
        } else {
            await using var tx=await _db.Database.BeginTransactionAsync();
            var operation=Guid.NewGuid();await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CollaborationPlanningPermits(DocumentId,OperationId) VALUES({_document},{operation})");
            await _db.SceneCards.Where(c=>c.SceneNodeId==r.NewNodeId).ExecuteUpdateAsync(c=>c.SetProperty(x=>x.Summary,"Later scene planning."));
            await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM CollaborationPlanningPermits WHERE DocumentId={_document}");await tx.CommitAsync();
        }
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.UndoAsync(r));
        if(kind==BrainstormingPlanningKind.StoryNote)Assert.Equal("Later author planning.",(await _db.DocumentSynopses.AsNoTracking().SingleAsync()).Notes);
        else Assert.Equal("Later scene planning.",(await _db.SceneCards.AsNoTracking().SingleAsync(c=>c.SceneNodeId==r.NewNodeId)).Summary);
        Assert.Equal(BrainstormingApplicationState.Committed,(await service.InspectAsync(r)).State);
    }
}
