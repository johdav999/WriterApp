using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingPlanningTests
{
    internal static BrainstormingPlanningReview Review(BrainstormingSession session,BrainstormingPlanningSnapshot snapshot,BrainstormingPlanningKind kind) {
        var turn=session.Turns.Last(t=>t.State==BrainstormingTurnState.Complete && t.ContextId!=null);
        BrainstormingPlanningField[] fields=kind==BrainstormingPlanningKind.SceneIdea?[new("Title","","Suspicious stranger"),new("Summary","","She keeps her distance. 日本語 🧭")]:
            kind==BrainstormingPlanningKind.SynopsisField?[new("Premise",snapshot.Synopsis.Premise,"The letter hides a secret.")]:
            [new("Notes",snapshot.Synopsis.Notes,snapshot.Synopsis.Notes+"\n"+(kind==BrainstormingPlanningKind.CharacterNote?"Character: Åsa\n":"")+"Her brother must remain unaware until Chapter 4.")];
        return new(1,Guid.NewGuid(),Guid.NewGuid(),snapshot.Scope,turn.Id,turn.ContextId!.Value,session.Drafts.LastOrDefault(d=>d.TurnId==turn.Id)?.Id,kind,
            kind==BrainstormingPlanningKind.SceneIdea?snapshot.Chapters.Last().Id:null,Guid.NewGuid(),snapshot.Source,fields,DateTimeOffset.UtcNow,
            kind==BrainstormingPlanningKind.SceneIdea?snapshot.Chapters.Last().NextChildOrder:0);
    }
    internal static async Task<(BrainstormingPlanningSaves Service,BrainstormingSession Session)> Server(BrainstormingConversationTests.Fixture f,bool project=false,bool migrated=false) {
        if(project)await BrainstormingKeepTests.Server(f,"new");else {await f.Start(migrated);await f.Run();}
        var scope=project?(await f.Db.BrainstormingSessions.OrderByDescending(s=>s.UpdatedUnixMilliseconds).FirstAsync()).Id:f.Send.Scope.SessionId;
        var session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,scope,default))!;
        return(new(f.Db,"author",f.Data.Backend),session);
    }
    [Theory][InlineData(BrainstormingPlanningKind.SceneIdea)][InlineData(BrainstormingPlanningKind.StoryNote)][InlineData(BrainstormingPlanningKind.SynopsisField)][InlineData(BrainstormingPlanningKind.CharacterNote)]
    public async Task AllServerDestinationsCommitExactlyOnceReopenPreserveWritingContextAndUndo(BrainstormingPlanningKind kind) {
        await using var f=new BrainstormingConversationTests.Fixture();var (service,session)=await Server(f,kind==BrainstormingPlanningKind.SceneIdea);
        f.Db.DocumentSynopses.Add(new(){DocumentId=f.Data.Document,Logline="Untouched logline",Theme="Untouched theme",Notes="Author's prior note"});await f.Db.SaveChangesAsync();
        var r=Review(session,await service.CaptureAsync(session.Scope),kind);var oldPages=await f.Db.Pages.AsNoTracking().Select(p=>new {p.Id,p.Content}).ToArrayAsync();var oldSections=await f.Db.Sections.CountAsync();
        Assert.Equal(BrainstormingApplicationState.Failed,(await service.InspectAsync(r)).State);
        var receipt=await service.ApplyAsync(r);Assert.Equal(BrainstormingApplicationState.Committed,receipt.State);Assert.Equal(receipt,await service.ApplyAsync(r));
        await using var reopened=new AppDbContext(f.Data.Options);service=new(reopened,"author",f.Data.Backend);Assert.Equal(receipt,await service.InspectAsync(r));
        Assert.Equal(oldSections,await reopened.Sections.CountAsync());foreach(var p in oldPages)Assert.Equal(p.Content,(await reopened.Pages.SingleAsync(x=>x.Id==p.Id)).Content);
        if(kind==BrainstormingPlanningKind.SceneIdea){var node=await reopened.ProjectNodes.SingleAsync(n=>n.Id==r.NewNodeId);Assert.Null(node.LinkedSectionId);Assert.Null(await reopened.SceneContents.SingleOrDefaultAsync(s=>s.SceneNodeId==node.Id));}
        var synopsis=await reopened.DocumentSynopses.SingleAsync();Assert.Equal("Untouched logline",synopsis.Logline);Assert.Equal("Untouched theme",synopsis.Theme);
        var current=(await new BrainstormingSessions(reopened,"author",f.Data.Backend).ReadAsync(session.Scope.DocumentId,session.Scope.SessionId,default))!;
        Assert.Equal(BrainstormingPersistence.Json(session.Contexts),BrainstormingPersistence.Json(current.Contexts));Assert.Contains(current.Applications,a=>a.OperationId==r.OperationId && a.State==BrainstormingApplicationState.Committed);Assert.Contains(current.Planning,p=>p.Id==r.CandidateId);
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.ApplyAsync(r with {OperationId=Guid.NewGuid()}));
        synopsis=await reopened.DocumentSynopses.SingleAsync();synopsis.Theme="Later unrelated planning";await reopened.SaveChangesAsync();var undo=await service.UndoAsync(r);Assert.Equal(BrainstormingApplicationState.Undone,undo.State);Assert.Equal(undo,await service.UndoAsync(r));
        Assert.Equal("Later unrelated planning",(await reopened.DocumentSynopses.SingleAsync()).Theme);
    }
    [Theory][InlineData("stale")][InlineData("deleted")][InlineData("foreign")][InlineData("backend")][InlineData("cancelled")][InlineData("malformed")][InlineData("turn")][InlineData("wrong-document")]
    public async Task InvalidApprovalsCannotChangePlanningOrProduceReceipt(string mode) {
        await using var f=new BrainstormingConversationTests.Fixture();var (service,session)=await Server(f);var r=Review(session,await service.CaptureAsync(session.Scope),BrainstormingPlanningKind.StoryNote);
        if(mode=="stale"){(await f.Db.Pages.SingleAsync()).Content="<p>Later writing</p>";await f.Db.SaveChangesAsync();}
        if(mode=="deleted"){(await f.Db.Documents.SingleAsync()).DeletedAtUtc=DateTime.UtcNow;await f.Db.SaveChangesAsync();}
        if(mode=="foreign")service=new(f.Db,"other",f.Data.Backend);if(mode=="backend")service=new(f.Db,"author",new string('F',64));
        if(mode=="cancelled")r=r with {TurnId=session.Turns.First().Id,ContextId=Guid.NewGuid()};
        if(mode=="malformed")r=r with {Version=2};if(mode=="turn")r=r with {TurnId=Guid.NewGuid()};if(mode=="wrong-document")r=r with {Scope=r.Scope with {DocumentId=Guid.NewGuid()}};
        await Assert.ThrowsAnyAsync<Exception>(()=>service.ApplyAsync(r));Assert.Empty(await f.Db.DocumentSynopses.ToArrayAsync());Assert.Empty(await f.Db.BrainstormingPlanningSaves.ToArrayAsync());
    }
    [Fact]
    public async Task MigratedSqliteRollsBackFieldMutationAndReceiptOnFailureThenExplicitRetryWorks() {
        await using var f=new BrainstormingConversationTests.Fixture();var (service,session)=await Server(f,migrated:true);var r=Review(session,await service.CaptureAsync(session.Scope),BrainstormingPlanningKind.SynopsisField);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailPlanningSave BEFORE INSERT ON BrainstormingPlanningSaves BEGIN SELECT RAISE(ABORT, 'synthetic failure'); END");
        await Assert.ThrowsAsync<DbUpdateException>(()=>service.ApplyAsync(r));f.Db.ChangeTracker.Clear();Assert.Empty(await f.Db.DocumentSynopses.ToArrayAsync());Assert.Equal(BrainstormingApplicationState.Failed,(await service.InspectAsync(r)).State);
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailPlanningSave");Assert.Equal(BrainstormingApplicationState.Committed,(await service.ApplyAsync(r)).State);
        var synopsis=await f.Db.DocumentSynopses.SingleAsync();synopsis.Premise="Later planning";await f.Db.SaveChangesAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>service.UndoAsync(r));Assert.Equal("Later planning",synopsis.Premise);
    }
    internal sealed class LocalFixture {
        public LocalDocumentRepository Documents=default!;public DeviceBrainstormingSessions Sessions=default!;public LocalBrainstormingPlanningSaves Service=default!;public BrainstormingCache Cache=default!;public DeviceAccountService Account=default!;
        public async Task Start(bool project) {
            var root=Path.Combine(Path.GetTempPath(),"BrainstormingP08",Guid.NewGuid().ToString());Documents=new(new FileLocalDocumentStore(Path.Combine(root,"docs")));
            var d=project?await Documents.CreateProjectAsync("Book"):await Documents.CreateAsync("Standalone");Account=new(new UnconfiguredDeviceIdentityClient());var host=new DeviceHostOptions("Fixture",new("https://test.invalid/"));var online=new DeviceConnectivity();online.SetOnline(false);
            Sessions=new(new LocalBrainstormingStore(Path.Combine(root,"sessions")),Account,host,online,Documents,new HttpClient{BaseAddress=host.ApiBaseAddress});
            var scope=await Sessions.ScopeAsync(d.DocumentId);var session=BrainstormingKeepTests.LocalSession(d,scope,false);Cache=await Sessions.WriteAsync(new(1,1,session,"",null,null,null,[],[]),0,Guid.NewGuid());Service=new(Documents,Sessions);
        }
    }
    [Theory][InlineData(BrainstormingPlanningKind.SceneIdea)][InlineData(BrainstormingPlanningKind.StoryNote)][InlineData(BrainstormingPlanningKind.SynopsisField)][InlineData(BrainstormingPlanningKind.CharacterNote)]
    public async Task AtomicOfflineLocalPlanningReopensDeduplicatesAndRestoresSelectedFields(BrainstormingPlanningKind kind) {
        var f=new LocalFixture();await f.Start(kind==BrainstormingPlanningKind.SceneIdea);var scope=f.Cache.Session.Scope;var before=(await f.Documents.LoadAsync(scope.DocumentId))!;
        var r=Review(f.Cache.Session,await f.Service.CaptureAsync(scope),kind);var flow=new BrainstormingPlanningWorkflow(f.Sessions);var cache=await flow.ApplyAsync(scope,r,review=>f.Service.ApplyAsync(review));Assert.Equal(BrainstormingApplicationState.Committed,cache.PlanningOperations!.Single().Receipt!.State);
        var reopened=new LocalBrainstormingPlanningSaves(f.Documents,f.Sessions);Assert.Equal(BrainstormingApplicationState.Committed,(await reopened.InspectAsync(r)).State);
        await flow.ApplyAsync(scope,r,_=>throw new Exception("Must never replay a confirmed planning save"));
        var after=(await f.Documents.LoadAsync(scope.DocumentId))!;Assert.Equal(BrainstormingPersistence.Json(before.Sections),BrainstormingPersistence.Json(after.Sections));
        if(kind==BrainstormingPlanningKind.SceneIdea){Assert.Null(after.Project!.Nodes.Single(n=>n.NodeId==r.NewNodeId).SectionId);LocalProjectStructure.Validate(after);}
        Assert.Equal(BrainstormingApplicationState.Undone,(await reopened.UndoAsync(r)).State);Assert.Equal(BrainstormingApplicationState.Undone,(await reopened.UndoAsync(r)).State);
        Assert.Equal(BrainstormingPersistence.Json(before.Sections),BrainstormingPersistence.Json((await f.Documents.LoadAsync(scope.DocumentId))!.Sections));
        Assert.Equal(BrainstormingPersistence.Json(before.Project?.Synopsis??new SyncSynopsis()),BrainstormingPersistence.Json(LocalBrainstormingPlanning.Synopsis((await f.Documents.LoadAsync(scope.DocumentId))!)));
    }
    [Fact]
    public async Task LostLocalResponseRequiresInspectionAndNeverReplaysConfirmedSave() {
        var f=new LocalFixture();await f.Start(false);var scope=f.Cache.Session.Scope;var r=Review(f.Cache.Session,await f.Service.CaptureAsync(scope),BrainstormingPlanningKind.StoryNote);var flow=new BrainstormingPlanningWorkflow(f.Sessions);
        await Assert.ThrowsAsync<IOException>(()=>flow.ApplyAsync(scope,r,async review=>{await f.Service.ApplyAsync(review);throw new IOException("Lost response");}));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>flow.ApplyAsync(scope,r,_=>throw new Exception("Must inspect")));
        var receipt=await f.Service.InspectAsync(r);Assert.Equal(BrainstormingApplicationState.Committed,receipt.State);await flow.RememberAsync(scope,r,receipt);await flow.ApplyAsync(scope,r,_=>throw new Exception("Must not replay"));
        var changed=r with {OperationId=Guid.NewGuid()};await Assert.ThrowsAsync<InvalidDataException>(()=>flow.RememberAsync(scope,changed));
        await Assert.ThrowsAnyAsync<Exception>(()=>f.Service.InspectAsync(r with {Scope=scope with {AccountKey=new string('A',64),LocalOwnerId=null}}));
    }
    [Fact]
    public async Task LocalStaleApprovalAndLaterAffectedPlanningArePreserved() {
        var f=new LocalFixture();await f.Start(true);var scope=f.Cache.Session.Scope;var review=Review(f.Cache.Session,await f.Service.CaptureAsync(scope),BrainstormingPlanningKind.SynopsisField);
        var d=(await f.Documents.LoadAsync(scope.DocumentId))!;await f.Documents.SaveAsync(LocalPlanning.Synopsis(d,new(Theme:"Later theme")));
        var flow=new BrainstormingPlanningWorkflow(f.Sessions);await Assert.ThrowsAsync<InvalidOperationException>(()=>flow.ApplyAsync(scope,review,r=>f.Service.ApplyAsync(r)));
        await flow.RememberAsync(scope,review,await f.Service.InspectAsync(review));
        review=Review(f.Cache.Session,await f.Service.CaptureAsync(scope),BrainstormingPlanningKind.SynopsisField);await flow.ApplyAsync(scope,review,r=>f.Service.ApplyAsync(r));
        d=(await f.Documents.LoadAsync(scope.DocumentId))!;await f.Documents.SaveAsync(LocalPlanning.Synopsis(d,d.Project!.Synopsis! with {Premise="Later affected field",Theme="Still retained"}));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.UndoAsync(review));Assert.Equal("Later affected field",(await f.Documents.LoadAsync(scope.DocumentId))!.Project!.Synopsis!.Premise);
    }
    [Theory][InlineData("missing")][InlineData("deleted")][InlineData("order")][InlineData("tags")]
    public async Task PlanningIdeaRefusesMissingChapterAndLaterSceneChanges(string mode) {
        var f=new LocalFixture();await f.Start(true);var scope=f.Cache.Session.Scope;var r=Review(f.Cache.Session,await f.Service.CaptureAsync(scope),BrainstormingPlanningKind.SceneIdea);var flow=new BrainstormingPlanningWorkflow(f.Sessions);
        var before=(await f.Documents.LoadAsync(scope.DocumentId))!;
        if(mode is "missing" or "deleted") {
            var nodes=before.Project!.Nodes.Select(n=>n.NodeId==r.ParentId || n.ParentId==r.ParentId?n with {DeletionId=Guid.NewGuid()}:n).ToArray();
            await f.Documents.SaveAsync(before with {Project=before.Project with {Nodes=nodes}});
            await Assert.ThrowsAsync<InvalidOperationException>(()=>flow.ApplyAsync(scope,r,review=>f.Service.ApplyAsync(review)));
        } else {
            await flow.ApplyAsync(scope,r,review=>f.Service.ApplyAsync(review));var d=(await f.Documents.LoadAsync(scope.DocumentId))!;
            await f.Documents.SaveAsync(d with {Project=d.Project! with {Nodes=d.Project!.Nodes.Select(n=>n.NodeId==r.NewNodeId?mode=="order"?n with {OrderIndex=n.OrderIndex+2}:n with {Card=n.Card! with {TagsJson="[\"later\"]"}}:n).ToArray()}});
            await Assert.ThrowsAsync<InvalidOperationException>(()=>f.Service.UndoAsync(r));
        }
        Assert.Equal(BrainstormingPersistence.Json(before.Sections),BrainstormingPersistence.Json((await f.Documents.LoadAsync(scope.DocumentId))!.Sections));
    }
    [Fact]
    public async Task StandaloneAuthoredNotesInvalidateMetadataRetryAndCannotBecomeOlderServerContext() {
        var f=new LocalFixture();await f.Start(false);var scope=f.Cache.Session.Scope;var before=(await f.Documents.LoadAsync(scope.DocumentId))!;
        var next=LocalPlanning.Synopsis(before,new(Notes:"Authored local planning"));Assert.NotEqual(DeviceSyncMapping.Fingerprint(before),DeviceSyncMapping.Fingerprint(next));
        Assert.Null(DeviceSyncMapping.Upload(next).Project);Assert.Equal(BrainstormingPersistence.Json(DeviceSyncMapping.Upload(before).Sections),BrainstormingPersistence.Json(DeviceSyncMapping.Upload(next).Sections));
        var source=DeviceBrainstormingContext.Capture(next,next);Assert.Contains(source.References,s=>s.Kind==BrainstormingSourceKind.Synopsis && s.Text.Contains("Authored local planning",StringComparison.Ordinal));
    }
}
