using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingKeepTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("replace")]
    [InlineData("new")]
    public async Task ApprovedKeepCannotWriteAfterManuscriptBecomesCollaborative(string mode)
    {
        await using var f = new BrainstormingConversationTests.Fixture();
        var review = await Server(f, mode);
        var service = new BrainstormingKeeps(f.Db, "author", f.Data.Backend);
        await service.PrepareAsync(review);
        var original = await f.Db.Pages.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Content).ToArrayAsync();
        await f.Db.Documents.Where(d => d.Id == f.Data.Document).ExecuteUpdateAsync(s => s.SetProperty(d => d.CollaborationMode, WriterApp.Shared.Collaboration.CollaborationMode.Collaborative));
        await Assert.ThrowsAsync<WriterApp.Application.Collaboration.CollaborationWriteException>(() => service.CommitAsync(review.Scope, review.OperationId));
        f.Db.ChangeTracker.Clear();
        Assert.Equal(original, await f.Db.Pages.AsNoTracking().OrderBy(p => p.Id).Select(p => p.Content).ToArrayAsync());
        Assert.Empty(await f.Db.WebAiHistoryOperations.ToArrayAsync());
        Assert.Equal(BrainstormingApplicationState.Approved, (await service.InspectAsync(review.Scope, review.OperationId))!.State);
    }
    internal static async Task<BrainstormingKeepReview> Server(BrainstormingConversationTests.Fixture f,string mode)
    {
        await f.Start(true);
        if(mode=="empty") {(await f.Db.Pages.SingleAsync()).Content="<p></p>";await f.Db.SaveChangesAsync();}
        if(mode=="new") {
            var first=Guid.NewGuid();var second=Guid.NewGuid();var previous=Guid.NewGuid();
            f.Db.Sections.Add(new(){Id=previous,DocumentId=f.Data.Document,Title="Chapter one section",OrderIndex=0});
            (await f.Db.Sections.SingleAsync(s=>s.Id==f.Data.Section)).OrderIndex=1;
            f.Db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=f.Data.Document,SectionId=previous,Title="Preceding page",Content="<p>The letter arrived.</p>"});
            f.Db.Pages.Add(new(){Id=Guid.NewGuid(),DocumentId=f.Data.Document,SectionId=f.Data.Section,Title="Chapter 2 continuation",OrderIndex=1,Content="<h2>Later page</h2><p><em>Keep this prose.</em></p>"});
            f.Db.ProjectNodes.AddRange(new(){Id=first,ProjectId=f.Data.Project,DocumentId=f.Data.Document,Title="Chapter 1",NodeType=WriterApp.Data.Documents.ProjectNodeType.Chapter,OrderIndex=0},
                new(){Id=second,ProjectId=f.Data.Project,DocumentId=f.Data.Document,Title="Chapter 2",NodeType=WriterApp.Data.Documents.ProjectNodeType.Chapter,OrderIndex=1},
                new(){Id=Guid.NewGuid(),ProjectId=f.Data.Project,DocumentId=f.Data.Document,ParentId=first,Title="Earlier",NodeType=WriterApp.Data.Documents.ProjectNodeType.Scene,LinkedSectionId=previous,OrderIndex=0},
                new(){Id=Guid.NewGuid(),ProjectId=f.Data.Project,DocumentId=f.Data.Document,ParentId=second,Title="Existing",NodeType=WriterApp.Data.Documents.ProjectNodeType.Scene,LinkedSectionId=f.Data.Section,OrderIndex=0});
            await f.Db.SaveChangesAsync();f.Send=f.Send with {Scope=f.Send.Scope with {SessionId=Guid.NewGuid(),ProjectId=f.Data.Project}};
        }
        var source=await new BrainstormingStorySourceReader(f.Db,"author").ReadAsync(f.Data.Document);
        var preparation=await f.Sources.PrepareAsync(new(1,f.Send.Scope,mode=="new"?new(ChapterNumber:2,NewFirstSection:true):new(SectionId:f.Data.Section),[],[],60_000),default);
        var scope=f.Send.Scope with {SessionId=Guid.NewGuid()};var context=preparation.Preparation.Context! with {Scope=scope};var target=preparation.Preparation.Resolution.Target!;
        var session=BrainstormingPersistence.Empty(scope) with {State=BrainstormingConversationState.Saved,Targets=[target],Contexts=[context]};
        await new BrainstormingSessions(f.Db,"author",f.Data.Backend).WriteAsync(new(1,Guid.NewGuid(),0,session),default);
        f.Send=f.Send with {Scope=scope,ExpectedRevision=1,ContextId=context.Id,TargetId=target.Id};
        f.Provider.Wire=BrainstormingPersistence.Json(new BrainstormingAnswer("Draft ready.",1,BrainstormingIntent.Draft,new("Hon tvivlade. 日本語 🧭","Understated",[]),[],[]));await f.Run();
        session=(await new BrainstormingSessions(f.Db,"author",f.Data.Backend).ReadAsync(f.Data.Document,scope.SessionId,default))!;
        var draft=session.Drafts.Single();var manual=draft with {Id=Guid.NewGuid(),ParentDraftId=draft.Id,Revision=2,ManuallyEdited=true,EditorHtml="<p><strong>Hon tvivlade.</strong> 日本語 🧭</p>"};
        session=session with {Revision=session.Revision+1,UpdatedAt=DateTimeOffset.UtcNow,Drafts=[draft,manual]};
        await new BrainstormingSessions(f.Db,"author",f.Data.Backend).WriteAsync(new(1,Guid.NewGuid(),session.Revision-1,session),default);
        return BrainstormingKeepContract.Review(scope,manual,target,BrainstormingKeepContract.Writing(source),mode=="replace"?0:null,mode=="replace"?3:null,"Suspicious arrival");
    }
    [Theory][InlineData("empty")][InlineData("insert")][InlineData("replace")][InlineData("new")]
    public async Task ServerAtomicKeepRestartDuplicateAndGuardedUndoRedo(string mode)
    {
        await using var f=new BrainstormingConversationTests.Fixture();var r=await Server(f,mode);var service=new BrainstormingKeeps(f.Db,"author",f.Data.Backend);var originals=await f.Db.Pages.AsNoTracking().ToArrayAsync();
        var approval=await service.PrepareAsync(r);Assert.Equal(BrainstormingApplicationState.Approved,approval.State);Assert.Empty(await f.Db.AiActionHistoryEntries.ToArrayAsync());
        var receipt=await service.CommitAsync(r.Scope,r.OperationId);Assert.Equal(BrainstormingApplicationState.Committed,receipt.State);
        Assert.Equal(r.AfterHtml,(await f.Db.Pages.AsNoTracking().SingleAsync(p=>p.Id==(mode=="new"?r.NewPageId:r.Target.PageId))).Content);
        foreach(var original in originals.Where(p=>mode=="new" || p.Id!=r.Target.PageId))Assert.Equal(original.Content,(await f.Db.Pages.AsNoTracking().SingleAsync(p=>p.Id==original.Id)).Content);
        Assert.Equal(1,await f.Db.AiActionHistoryEntries.CountAsync());Assert.Equal(1,await f.Db.WebAiHistoryOperations.CountAsync());
        if(mode=="new"){var after=await f.Db.Sections.AsNoTracking().OrderBy(s=>s.OrderIndex).ToArrayAsync();Assert.Equal(r.NewSectionId,after[1].Id);Assert.Equal(f.Data.Section,after[2].Id);}
        await using var reopened=new AppDbContext(f.Data.Options);service=new(reopened,"author",f.Data.Backend);
        Assert.Equal(receipt,await service.PrepareAsync(r));Assert.Equal(receipt,await service.CommitAsync(r.Scope,r.OperationId));Assert.Equal(receipt,await service.InspectAsync(r.Scope,r.OperationId));
        await Assert.ThrowsAsync<InvalidDataException>(()=>service.PrepareAsync(r with {OperationId=Guid.NewGuid()}));
        var undo=new BrainstormingKeepMove(Guid.NewGuid(),1,false);var undone=await service.MoveAsync(r.Scope,r.OperationId,undo);Assert.Equal(BrainstormingApplicationState.Undone,undone.State);Assert.Equal(undone,await service.MoveAsync(r.Scope,r.OperationId,undo));
        if(mode=="new")Assert.False(await reopened.Sections.AnyAsync(s=>s.Id==r.NewSectionId));else Assert.Equal(originals.Single(p=>p.Id==r.Target.PageId).Content,(await reopened.Pages.AsNoTracking().SingleAsync(p=>p.Id==r.Target.PageId)).Content);
        var redone=await service.MoveAsync(r.Scope,r.OperationId,new(Guid.NewGuid(),2,true));Assert.Equal(BrainstormingApplicationState.Committed,redone.State);
        var page=await reopened.Pages.SingleAsync(p=>p.Id==(mode=="new"?r.NewPageId:r.Target.PageId));page.Content+="<p>Later user writing.</p>";await reopened.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.MoveAsync(r.Scope,r.OperationId,new(Guid.NewGuid(),3,false)));Assert.Contains("Later user",(await reopened.Pages.AsNoTracking().SingleAsync(p=>p.Id==page.Id)).Content);
        var copy=await service.RecoverAsync(r.Scope,r.OperationId);Assert.Equal(copy,await service.RecoverAsync(r.Scope,r.OperationId));Assert.Equal(originals.Length,await reopened.Pages.CountAsync(p=>p.DocumentId==copy));
        Assert.Equal(BrainstormingApplicationState.Committed,(await new BrainstormingSessions(reopened,"author",f.Data.Backend).ReadAsync(r.Scope.DocumentId,r.Scope.SessionId,default))!.Applications.Single().State);
        if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path)await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"keep-"+mode+".json"),BrainstormingPersistence.Json(r));
    }
    [Theory][InlineData("content")][InlineData("order")][InlineData("deleted")][InlineData("account")][InlineData("backend")][InlineData("candidate")]
    public async Task StaleAndForeignReviewsCannotMutateOrConfirmHistory(string change)
    {
        await using var f=new BrainstormingConversationTests.Fixture();var r=await Server(f,"insert");var service=new BrainstormingKeeps(f.Db,"author",f.Data.Backend);
        if(change=="candidate")r=r with {Candidate=r.Candidate with {PlainText="Forged",EditorHtml="<p>Forged</p>"},AfterHtml="<p>Forged</p>"+r.Before.Pages.Single().Content};
        if(change=="account")service=new(f.Db,"other",f.Data.Backend);if(change=="backend")service=new(f.Db,"author",new string('F',64));
        if(change=="content"){(await f.Db.Pages.SingleAsync()).Content="<p>Later writing.</p>";await f.Db.SaveChangesAsync();}
        if(change=="order"){(await f.Db.Sections.SingleAsync()).OrderIndex=8;await f.Db.SaveChangesAsync();}
        if(change=="deleted"){(await f.Db.Documents.SingleAsync()).DeletedAtUtc=DateTime.UtcNow;await f.Db.SaveChangesAsync();}
        await Assert.ThrowsAnyAsync<Exception>(()=>service.PrepareAsync(r));Assert.Empty(await f.Db.BrainstormingKeeps.ToArrayAsync());Assert.Empty(await f.Db.AiActionHistoryEntries.ToArrayAsync());
    }
    [Fact]
    public async Task TransactionFailureAfterCreatingStructureLeavesNoEmptySectionOrFalseReceipt()
    {
        await using var f=new BrainstormingConversationTests.Fixture();var r=await Server(f,"new");var service=new BrainstormingKeeps(f.Db,"author",f.Data.Backend);await service.PrepareAsync(r);
        await f.Db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailKeepPage BEFORE INSERT ON Pages BEGIN SELECT RAISE(ABORT, 'fixture save failure'); END");
        await Assert.ThrowsAsync<DbUpdateException>(()=>service.CommitAsync(r.Scope,r.OperationId));f.Db.ChangeTracker.Clear();
        Assert.False(await f.Db.Sections.AnyAsync(s=>s.Id==r.NewSectionId));Assert.False(await f.Db.ProjectNodes.AnyAsync(n=>n.Id==r.NewNodeId));Assert.Empty(await f.Db.AiActionHistoryEntries.ToArrayAsync());Assert.Equal(BrainstormingApplicationState.Approved,(await service.InspectAsync(r.Scope,r.OperationId))!.State);
        await f.Db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailKeepPage");Assert.Equal(BrainstormingApplicationState.Committed,(await service.CommitAsync(r.Scope,r.OperationId)).State);
    }
    [Theory][InlineData("<p style=\"color:red\">x</p>")][InlineData("<script>x</script>")][InlineData("<p><img src=\"/a.png\"></p>")]
    public void UnsupportedSourceOrProposalNeverBecomesAnApplication(string bad)
    {
        Assert.ThrowsAny<Exception>(()=>BrainstormingKeepContract.Transform(bad,"<p>New</p>",BrainstormingTargetKind.ReplaceRange,0,1,"x"));
    }
    [Fact]
    public void RichPassageMappingPreservesUntouchedMarksNodesAndUnicode()
    {
        var source="<p><em>Åsa</em> <strong>日本語 🧭</strong> waited.</p><ul><li><p>Untouched list</p></li></ul><p><a href=\"https://example.com\">Link</a></p>";
        var after=BrainstormingKeepContract.Transform(source,"<p><strong>Hon</strong></p>",BrainstormingTargetKind.ReplaceRange,0,3,"Åsa");
        Assert.Contains("<strong>Hon</strong>",after);Assert.Contains("<strong>日本語 🧭</strong>",after);Assert.Contains("<ul><li><p>Untouched list</p></li></ul>",after);Assert.Contains("href=\"https://example.com\"",after);
        var plain=BrainstormingDraftContent.PlainText(source);int split=plain.IndexOf("🧭",StringComparison.Ordinal)+1;
        Assert.Throws<InvalidDataException>(()=>BrainstormingKeepContract.Transform(source,"<p>New</p>",BrainstormingTargetKind.ReplaceRange,split,split+1,plain.Substring(split,1)));
    }
    private static string Root()=>Path.Combine(Path.GetTempPath(),"BrainstormingP07",Guid.NewGuid().ToString());
    internal static BrainstormingSession LocalSession(LocalDocument d,BrainstormingScope scope,bool create)
    {
        var fixture=BrainstormingPersistence.ReScope(BrainstormingContractTests.Fixture(),scope);var snapshot=DeviceBrainstormingContext.Capture(d,d);
        var resolution=BrainstormingTargetResolver.Resolve(snapshot,create?new(ChapterNumber:1,NewFirstSection:true):new(SectionId:d.Sections[0].SectionId));var target=resolution.Target! with {Id=fixture.Targets[0].Id};
        var original=fixture.Drafts[0] with {ManuallyEdited=true,PlainText="Hon tvivlade. 日本語 🧭",EditorHtml="<p><strong>Hon tvivlade.</strong> 日本語 🧭</p>"};
        return fixture with {State=BrainstormingConversationState.LocalOnly,Targets=[target],Drafts=[original],Planning=[],Approvals=[],Applications=[],Contexts=fixture.Contexts.Select(c=>c with {Sources=c.Sources.Select(s=>s with {DocumentId=d.DocumentId,SectionId=d.Sections[0].SectionId,PageId=d.Sections[0].Pages[0].PageId}).ToArray()}).ToArray()};
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task DeviceAtomicKeepJournalRestartUndoAndRecoveryPreserveExistingWriting(bool create)
    {
        var root=Root();var docs=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"docs")));var d=create?await docs.CreateProjectAsync("Book"):await docs.CreateAsync("Book");
        d=await docs.SaveAsync(d with {Sections=d.Sections.Select(s=>s with {Pages=s.Pages.Select(p=>p with {Content="<p><em>Åsa</em> waited.</p>"}).ToArray()}).ToArray()});
        var account=new DeviceAccountService(new UnconfiguredDeviceIdentityClient());var host=new DeviceHostOptions("Fixture",new("https://test.invalid/"));var network=new DeviceConnectivity();network.SetOnline(false);using var http=new HttpClient{BaseAddress=host.ApiBaseAddress};
        var files=new LocalBrainstormingStore(Path.Combine(root,"sessions"));var sessions=new DeviceBrainstormingSessions(files,account,host,network,docs,http);var scope=await sessions.ScopeAsync(d.DocumentId);
        var session=LocalSession(d,scope,create);await files.WriteAsync(new(1,1,session,"",null,null,null,[],[]),0,Guid.NewGuid());var r=BrainstormingKeepContract.Review(scope,session.Drafts[0],session.Targets[0],LocalBrainstormingWriting.Capture(d));
        var history=new LocalAiStore(Path.Combine(root,"history"));var keeps=new LocalBrainstormingKeeps(docs,history,sessions,account,host);var flow=new BrainstormingKeepWorkflow(sessions);
        var cache=await flow.ApplyAsync(scope,r,review=>keeps.ApplyAsync(review));Assert.Equal(BrainstormingApplicationState.Committed,cache.Keeps!.Single().Receipt!.State);
        Assert.Equal("Applied",(await history.HistoryAsync(d.DocumentId)).Single().Status);var saved=(await docs.LoadAsync(d.DocumentId))!;
        Assert.Equal(r.AfterHtml,saved.Sections.SelectMany(s=>s.Pages).Single(p=>p.PageId==(create?r.NewPageId:r.Target.PageId)).Content);
        if(create){Assert.Equal(r.NewSectionId,saved.Sections.OrderBy(s=>s.OrderIndex).First().SectionId);Assert.Equal(d.Sections[0].Pages[0].Content,saved.Sections.Single(s=>s.SectionId==d.Sections[0].SectionId).Pages[0].Content);}
        keeps=new(docs,new LocalAiStore(Path.Combine(root,"history")),sessions,account,host);Assert.Equal(BrainstormingApplicationState.Committed,(await keeps.ApplyAsync(r)).State);Assert.NotNull((await new LocalBrainstormingStore(Path.Combine(root,"sessions")).ReadAsync(scope))!.Keeps);
        Assert.Equal(BrainstormingApplicationState.Undone,(await keeps.MoveAsync(r,false)).State);Assert.Equal(BrainstormingApplicationState.Committed,(await keeps.MoveAsync(r,true)).State);
        var latest=(await docs.LoadAsync(d.DocumentId))!;latest=await docs.SaveAsync(latest with {Sections=latest.Sections.Select(s=>s with {Pages=s.Pages.Select(p=>p.PageId==(create?r.NewPageId:r.Target.PageId)?p with {Content=p.Content+"<p>Later writing.</p>"}:p).ToArray()}).ToArray()});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>keeps.MoveAsync(r,false));await keeps.RecoverAsync(r);Assert.Contains((await docs.ListAsync()).Documents,x=>x.DocumentId!=d.DocumentId);Assert.Contains("Later writing",(await docs.LoadAsync(d.DocumentId))!.Sections.SelectMany(s=>s.Pages).Single(p=>p.PageId==(create?r.NewPageId:r.Target.PageId)).Content);
    }
    [Fact]
    public async Task UnknownDispatchSurvivesRestartAndCannotAllocateAnotherIdentityForTheCandidate()
    {
        var root=Root();var docs=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"docs")));var d=await docs.CreateAsync("Book");var files=new LocalBrainstormingStore(Path.Combine(root,"sessions"));var scope=new BrainstormingScope(1,Guid.NewGuid(),d.DocumentId,null,BrainstormingIdentitySpace.Local,new string('A',64),null,Guid.NewGuid());var session=LocalSession(d,scope,false);
        await files.WriteAsync(new(1,1,session,"",null,null,null,[],[]),0,Guid.NewGuid());var r=BrainstormingKeepContract.Review(scope,session.Drafts[0],session.Targets[0],LocalBrainstormingWriting.Capture(d));var flow=new BrainstormingKeepWorkflow(files);var calls=0;
        await Assert.ThrowsAsync<HttpRequestException>(()=>flow.ApplyAsync(scope,r,_=>{calls++;throw new HttpRequestException("Uncertain transport");}));var reopened=(await new LocalBrainstormingStore(Path.Combine(root,"sessions")).ReadAsync(scope))!;Assert.Equal(BrainstormingApplicationState.Unknown,reopened.Keeps!.Single().Receipt!.State);Assert.Equal(1,calls);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>flow.RememberAsync(scope,r with {OperationId=Guid.NewGuid()}));Assert.Equal(d.Sections[0].Pages[0].Content,(await docs.LoadAsync(d.DocumentId))!.Sections[0].Pages[0].Content);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>flow.ApplyAsync(scope,r,_=>{calls++;throw new Exception("Must inspect first");}));Assert.Equal(1,calls);
    }
    [Theory][InlineData("source")][InlineData("saved")][InlineData("diverged")]
    public async Task InterruptedDeviceJournalRequiresInspectionAndPreservesLaterWriting(string outcome)
    {
        var root=Root();var docs=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"docs")));var d=await docs.CreateAsync("Book");var account=new DeviceAccountService(new UnconfiguredDeviceIdentityClient());var host=new DeviceHostOptions("Fixture",new("https://test.invalid/"));var network=new DeviceConnectivity();network.SetOnline(false);using var http=new HttpClient{BaseAddress=host.ApiBaseAddress};var files=new LocalBrainstormingStore(Path.Combine(root,"sessions"));var sessions=new DeviceBrainstormingSessions(files,account,host,network,docs,http);var scope=await sessions.ScopeAsync(d.DocumentId);var conversation=LocalSession(d,scope,false);
        await files.WriteAsync(new(1,1,conversation,"",null,null,null,[],[]),0,Guid.NewGuid());var r=BrainstormingKeepContract.Review(scope,conversation.Drafts[0],conversation.Targets[0],LocalBrainstormingWriting.Capture(d));var after=LocalBrainstormingWriting.Apply(d,r);var history=new LocalAiStore(Path.Combine(root,"history"));
        await history.SaveHistoryAsync(new(6,r.OperationId,d.DocumentId,"brainstorming.keep","Brainstorming:manuscript",d.LocalRevision,d.ServerVersion,r.ReviewedAt,"Applying",d,r.Candidate.PlainText,After:after,Brainstorming:r));
        if(outcome!="source")await docs.SaveAsync(outcome=="saved"?after:after with {Sections=after.Sections.Select(s=>s with {Pages=s.Pages.Select(p=>p with {Content=p.Content+"<p>Later writing.</p>"}).ToArray()}).ToArray()});
        var keeps=new LocalBrainstormingKeeps(docs,new LocalAiStore(Path.Combine(root,"history")),sessions,account,host);var receipt=await keeps.InspectAsync(r);Assert.Equal(outcome=="source"?BrainstormingApplicationState.Approved:outcome=="saved"?BrainstormingApplicationState.Committed:BrainstormingApplicationState.Unknown,receipt.State);
        if(outcome=="source")Assert.Equal(BrainstormingApplicationState.Committed,(await keeps.ResumeAsync(r)).State);
        if(outcome=="diverged"){Assert.Equal(BrainstormingApplicationState.Unknown,(await keeps.ResumeAsync(r)).State);Assert.Contains("Later writing",(await docs.LoadAsync(d.DocumentId))!.Sections[0].Pages[0].Content);}
        Assert.Single(await history.HistoryAsync(d.DocumentId));
    }
    [Theory][InlineData("added-order")][InlineData("annotation")][InlineData("stale-after-approval")]
    public async Task ServerGuardsInterveningOrderAnnotationsAndApprovedSource(string change)
    {
        await using var f=new BrainstormingConversationTests.Fixture();var r=await Server(f,change=="added-order"?"new":"insert");var service=new BrainstormingKeeps(f.Db,"author",f.Data.Backend);await service.PrepareAsync(r);
        if(change=="added-order"){await service.CommitAsync(r.Scope,r.OperationId);f.Db.ChangeTracker.Clear();(await f.Db.Sections.SingleAsync(s=>s.Id==r.NewSectionId)).OrderIndex=9;await f.Db.SaveChangesAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>service.MoveAsync(r.Scope,r.OperationId,new(Guid.NewGuid(),1,false)));Assert.True(await f.Db.Sections.AnyAsync(s=>s.Id==r.NewSectionId));}
        else {if(change=="annotation")f.Db.PageAnnotations.Add(new(){Id=Guid.NewGuid(),PageId=f.Data.Page,DocumentId=f.Data.Document,AnchorText="Åsa",Content="Author comment",AuthorUserId="author",CreatedAt=DateTimeOffset.UtcNow});else (await f.Db.Pages.SingleAsync()).Content="<p>Later author writing.</p>";await f.Db.SaveChangesAsync();await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CommitAsync(r.Scope,r.OperationId));Assert.Empty(await f.Db.AiActionHistoryEntries.AsNoTracking().ToArrayAsync());Assert.Equal(BrainstormingApplicationState.Approved,(await service.InspectAsync(r.Scope,r.OperationId))!.State);if(change=="annotation")Assert.Equal("Author comment",(await f.Db.PageAnnotations.SingleAsync()).Content);}
    }
    [Theory][InlineData(false,false)][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public async Task InterruptedUndoRedoInspectionChangesOnlyProvenHistoryMetadata(bool redo,bool saved)
    {
        var root=Root();var docs=new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"docs")));var d=await docs.CreateAsync("Book");var account=new DeviceAccountService(new UnconfiguredDeviceIdentityClient());var host=new DeviceHostOptions("Fixture",new("https://test.invalid/"));var network=new DeviceConnectivity();network.SetOnline(false);using var http=new HttpClient{BaseAddress=host.ApiBaseAddress};var files=new LocalBrainstormingStore(Path.Combine(root,"sessions"));var sessions=new DeviceBrainstormingSessions(files,account,host,network,docs,http);var scope=await sessions.ScopeAsync(d.DocumentId);var conversation=LocalSession(d,scope,false);
        await files.WriteAsync(new(1,1,conversation,"",null,null,null,[],[]),0,Guid.NewGuid());var r=BrainstormingKeepContract.Review(scope,conversation.Drafts[0],conversation.Targets[0],LocalBrainstormingWriting.Capture(d));var after=LocalBrainstormingWriting.Apply(d,r);var history=new LocalAiStore(Path.Combine(root,"history"));
        await history.SaveHistoryAsync(new(6,r.OperationId,d.DocumentId,"brainstorming.keep","Brainstorming:manuscript",d.LocalRevision,d.ServerVersion,r.ReviewedAt,redo?"Redoing":"Undoing",d,r.Candidate.PlainText,After:after,Brainstorming:r));
        var current=await docs.SaveAsync(redo==saved?after:d);var receipt=await new LocalBrainstormingKeeps(docs,history,sessions,account,host).InspectAsync(r);
        Assert.Equal(redo==saved?BrainstormingApplicationState.Committed:BrainstormingApplicationState.Undone,receipt.State);Assert.Equal(current.LocalRevision,(await docs.LoadAsync(d.DocumentId))!.LocalRevision);Assert.Equal(current.Sections[0].Pages[0].Content,(await docs.LoadAsync(d.DocumentId))!.Sections[0].Pages[0].Content);
    }
}
