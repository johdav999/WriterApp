using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingPlacementTests
{
    [Theory][InlineData("continue")][InlineData("boundary")][InlineData("new")][InlineData("other-section")][InlineData("standalone-new")]
    public async Task ServerPlacementKeepsExactDestinationAndSupportsDuplicateUndoRedo(string mode)
    {
        await using var f = new BrainstormingConversationTests.Fixture();
        var original = await BrainstormingKeepTests.Server(f,mode=="standalone-new"?"insert":"new");
        var last = original.Before.Pages.Where(p => p.SectionId == f.Data.Section).OrderBy(p => p.Order).Last();
        var elsewhere = original.Before.Pages.FirstOrDefault(p => p.SectionId != f.Data.Section)??last;
        var placement = mode switch {
            "new" or "standalone-new" => new BrainstormingPlacement(BrainstormingPlacementMode.NewSectionAfter,f.Data.Section),
            "boundary" => new(BrainstormingPlacementMode.PageBoundary,f.Data.Section,last.Id,1),
            "other-section" => new(BrainstormingPlacementMode.PageBoundary,elsewhere.SectionId,elsewhere.Id,1),
            _ => new(BrainstormingPlacementMode.ContinueSection,f.Data.Section)
        };
        var review = BrainstormingPlacements.ReviewAt(original,placement,"Next encounter");
        Assert.Equal(original.Target,review.Target); // Generation provenance stays immutable.
        var destination = BrainstormingPlacements.Destination(review);
        if(mode == "continue") { Assert.Equal(last.Id,destination.PageId); Assert.Equal(last.Content+review.Candidate.EditorHtml,review.AfterHtml); }
        if(mode == "boundary") Assert.Equal("<h2>Later page</h2>"+review.Candidate.EditorHtml+"<p><em>Keep this prose.</em></p>",review.AfterHtml);
        var service = new BrainstormingKeeps(f.Db,"author",f.Data.Backend);
        await service.PrepareAsync(review);
        var committed = await service.CommitAsync(review.Scope,review.OperationId);
        Assert.Equal(BrainstormingApplicationState.Committed,committed.State);
        Assert.Equal(committed,await service.CommitAsync(review.Scope,review.OperationId));
        Assert.Equal(review.AfterHtml,(await f.Db.Pages.AsNoTracking().SingleAsync(p => p.Id == (review.Kind == BrainstormingTargetKind.CreateSection ? review.NewPageId : destination.PageId))).Content);
        foreach(var page in original.Before.Pages.Where(p => p.Id != destination.PageId)) Assert.Equal(page.Content,(await f.Db.Pages.AsNoTracking().SingleAsync(p => p.Id == page.Id)).Content);
        if(mode == "new") {
            var outline = BrainstormingKeepContract.AfterOutline(review);
            Assert.Equal(outline.Nodes.Single(n => n.Id == review.NewNodeId).Order,(await f.Db.ProjectNodes.AsNoTracking().SingleAsync(n => n.Id == review.NewNodeId)).OrderIndex);
            Assert.Equal(1,(await f.Db.ProjectNodes.AsNoTracking().SingleAsync(n => n.Id == review.NewNodeId)).OrderIndex);
            var sections = await f.Db.Sections.AsNoTracking().OrderBy(s => s.OrderIndex).ToArrayAsync();
            Assert.Equal(review.NewSectionId,sections[Array.FindIndex(sections,s => s.Id == f.Data.Section)+1].Id);
        }
        if(mode=="standalone-new")Assert.False(await f.Db.ProjectNodes.AnyAsync(n=>n.Id==review.NewNodeId));
        Assert.Equal(BrainstormingApplicationState.Undone,(await service.MoveAsync(review.Scope,review.OperationId,new(Guid.NewGuid(),1,false))).State);
        Assert.Equal(BrainstormingApplicationState.Committed,(await service.MoveAsync(review.Scope,review.OperationId,new(Guid.NewGuid(),2,true))).State);
        var affected = await f.Db.Pages.SingleAsync(p => p.Id == (review.Kind == BrainstormingTargetKind.CreateSection ? review.NewPageId : destination.PageId));
        affected.Content += "<p>Later author writing.</p>"; await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.MoveAsync(review.Scope,review.OperationId,new(Guid.NewGuid(),3,false)));
        if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path)
            await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"placement-"+mode+".json"),BrainstormingPersistence.Json(review));
    }

    [Theory][InlineData(false,"continue")][InlineData(false,"new")][InlineData(false,"boundary")][InlineData(true,"continue")][InlineData(true,"new")][InlineData(true,"boundary")]
    public async Task DevicePlacementPersistsAcrossRestartAndGuardsUndo(bool project,string mode)
    {
        var root = Path.Combine(Path.GetTempPath(),"BrainstormingPlacement",Guid.NewGuid().ToString());
        var documents = new LocalDocumentRepository(new FileLocalDocumentStore(Path.Combine(root,"documents")));
        var document = project ? await documents.CreateProjectAsync("Book") : await documents.CreateAsync("Book");
        var first = document.Sections[0].Pages[0];
        document = await documents.SaveAsync(document with { Sections = document.Sections.Select((s,i) => i == 0 ? s with { Pages = [first with {Content="<p>Opening.</p>"},first with {PageId=Guid.NewGuid(),OrderIndex=1,Content="<p><em>Before.</em></p><p>After. 日本語 🧭</p>"}] } : s).ToArray() });
        var account = new DeviceAccountService(new UnconfiguredDeviceIdentityClient());var host = new DeviceHostOptions("Fixture",new("https://test.invalid/"));
        var network = new DeviceConnectivity();network.SetOnline(false);using var http = new HttpClient {BaseAddress=host.ApiBaseAddress};
        var files = new LocalBrainstormingStore(Path.Combine(root,"sessions"));var sessions = new DeviceBrainstormingSessions(files,account,host,network,documents,http);
        var scope = await sessions.ScopeAsync(document.DocumentId);var conversation = BrainstormingKeepTests.LocalSession(document,scope,false);
        await files.WriteAsync(new(1,1,conversation,"",null,null,null,[],[]),0,Guid.NewGuid());
        var original = BrainstormingKeepContract.Review(scope,conversation.Drafts[0],conversation.Targets[0],LocalBrainstormingWriting.Capture(document));
        var last = document.Sections[0].Pages[1];var section = document.Sections[0].SectionId;
        var placement = mode switch {
            "new" => new BrainstormingPlacement(BrainstormingPlacementMode.NewSectionAfter,section),
            "boundary" => new(BrainstormingPlacementMode.PageBoundary,section,last.PageId,1),
            _ => new(BrainstormingPlacementMode.ContinueSection,section)
        };
        var review = BrainstormingPlacements.ReviewAt(original,placement,"Next encounter");
        var history = new LocalAiStore(Path.Combine(root,"history"));var keeps = new LocalBrainstormingKeeps(documents,history,sessions,account,host);
        Assert.Equal(BrainstormingApplicationState.Committed,(await keeps.ApplyAsync(review)).State);
        var saved = (await documents.LoadAsync(document.DocumentId))!;
        Assert.Equal("<p>Opening.</p>",saved.Sections[0].Pages[0].Content);
        Assert.Equal(review.AfterHtml,saved.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == (mode == "new" ? review.NewPageId : last.PageId)).Content);
        keeps = new(documents,new LocalAiStore(Path.Combine(root,"history")),sessions,account,host);
        Assert.Equal(BrainstormingApplicationState.Committed,(await keeps.ApplyAsync(review)).State);
        Assert.Equal(BrainstormingApplicationState.Undone,(await keeps.MoveAsync(review,false)).State);
        Assert.Equal(BrainstormingApplicationState.Committed,(await keeps.MoveAsync(review,true)).State);
        saved = (await documents.LoadAsync(document.DocumentId))!;
        await documents.SaveAsync(saved with {Sections=saved.Sections.Select(s => s with {Pages=s.Pages.Select(p => p.PageId == (mode == "new" ? review.NewPageId : last.PageId) ? p with {Content=p.Content+"<p>Later author writing.</p>"} : p).ToArray()}).ToArray()});
        await Assert.ThrowsAsync<InvalidOperationException>(() => keeps.MoveAsync(review,false));
    }

    [Fact]
    public async Task ForeignOrForgedPlacementAndStaleSourceCannotApply()
    {
        await using var f = new BrainstormingConversationTests.Fixture();var original = await BrainstormingKeepTests.Server(f,"insert");
        Assert.Throws<InvalidDataException>(() => BrainstormingPlacements.ReviewAt(original,new(BrainstormingPlacementMode.ContinueSection,Guid.NewGuid()),"Section"));
        Assert.Throws<InvalidDataException>(() => BrainstormingPlacements.ReviewAt(original,new(BrainstormingPlacementMode.PageBoundary,f.Data.Section,f.Data.Page,9999),"Section"));
        var review = BrainstormingPlacements.ReviewAt(original,new(BrainstormingPlacementMode.ContinueSection,f.Data.Section),"Section");
        Assert.Throws<InvalidDataException>(() => BrainstormingKeepContract.Validate(review with {AfterHtml=review.Candidate.EditorHtml!}));
        var service = new BrainstormingKeeps(f.Db,"author",f.Data.Backend);await service.PrepareAsync(review);
        (await f.Db.Pages.SingleAsync()).Content += "<p>Intervening writing.</p>";await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CommitAsync(review.Scope,review.OperationId));
        Assert.Empty(await f.Db.AiActionHistoryEntries.ToArrayAsync());
    }
}
