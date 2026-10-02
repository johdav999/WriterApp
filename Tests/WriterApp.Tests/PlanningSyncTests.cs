using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DocumentSyncTests
{
    private static SyncMutation PlanningRequest()
    {
        var r = ProjectRequest();
        return r with { Document = r.Document! with { Project = r.Document.Project! with { Version = 2,
            Synopsis = new("Hook", "Premise", "Theme", "Arc", "Conflict", "Stakes", "Setting", "Ending", "Questions", "Notes"),
            Nodes = r.Document.Project.Nodes.Select(n => n.NodeType != "scene" ? n : n with { Annotations = [new(Guid.NewGuid(), "todo", "open", 0, 0,
                "Räksmörgås", "Research this", "untrusted", DateTimeOffset.UtcNow)] }).ToArray() } } };
    }
    [Fact]
    public async Task PlanningSyncRoundTripsWebRecordsAndRejectsOlderClients()
    {
        Guid id = Guid.NewGuid(); var r=PlanningRequest();
        var created=await _sync.MutateAsync("paid",id,r,projects:true,planning:true);
        Assert.Equal(created,await _sync.MutateAsync("paid",id,r,projects:true,planning:true));
        Assert.Equal("paid",(await _db.SceneAnnotations.SingleAsync()).AuthorUserId);
        Assert.Equal("Hook",(await _db.DocumentSynopses.SingleAsync()).Logline);
        Assert.Equal(426,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.DownloadAsync("paid",id,projects:true))).Status);
        Assert.Equal(426,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",id,new(Guid.NewGuid(),created.State.Version,"rename",Title:"Old"),projects:true))).Status);
        var snapshot=await _sync.DownloadAsync("paid",id,projects:true,planning:true);
        var local=DeviceSyncMapping.Download(snapshot,Guid.NewGuid(),null,DateTimeOffset.UtcNow);
        var node=local.Project!.Nodes.Single(n=>n.NodeType=="scene"); var task=node.Annotations.Single();
        Assert.NotEqual(task.LocalId,task.ServerId);
        local=LocalPlanning.Resolve(local,node.NodeId,task.LocalId,true);
        await _sync.MutateAsync("paid",id,new(Guid.NewGuid(),snapshot.State.Version,"upload",DeviceSyncMapping.Upload(local)),projects:true,planning:true);
        Assert.Equal("resolved",(await _db.SceneAnnotations.SingleAsync()).Status);
        Assert.Equal("<p>Räksmörgås 日本語</p>",(await _db.Pages.SingleAsync()).Content);
        Assert.Equal("Notes",(await _db.DocumentSynopses.SingleAsync()).Notes);
    }
    [Theory]
    [InlineData("synopsis")]
    [InlineData("annotation")]
    [InlineData("delete")]
    public async Task WebPlanningChangesConflictWithOfflineWriting(string change)
    {
        Guid id=Guid.NewGuid(); var r=PlanningRequest();
        var created=await _sync.MutateAsync("paid",id,r,projects:true,planning:true);
        if(change=="synopsis") (await _db.DocumentSynopses.SingleAsync()).Logline="Web hook";
        else if(change=="annotation") (await _db.SceneAnnotations.SingleAsync()).Content="Web task";
        else _db.SceneAnnotations.Remove(await _db.SceneAnnotations.SingleAsync());
        await _db.SaveChangesAsync();
        Assert.NotEqual(created.State.Version,(await _sync.DownloadAsync("paid",id,projects:true,planning:true)).State.Version);
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",id,r with {OperationId=Guid.NewGuid(),ExpectedVersion=created.State.Version},projects:true,planning:true))).Status);
    }
    [Fact]
    public async Task TwoDevicesPreserveConcurrentPlanningAndWritingAndRestart()
    {
        var api=new ProjectApi(_sync); using var a=new ProjectDevice(api); using var b=new ProjectDevice(api);
        var d=await a.Store.CreateProjectAsync("Planning"); var node=d.Project!.Nodes.Single(n=>n.NodeType=="scene");
        d=await a.Store.SaveAsync(LocalPlanning.AddAnnotation(LocalPlanning.Synopsis(d,new(Logline:"Hook")),node.NodeId,"todo","Task",""));
        await a.Engine.EnableAsync(d.DocumentId); await b.Engine.SyncAsync();
        var other=Assert.Single((await b.Store.ListAsync()).Documents);
        Assert.Equal("Hook",other.Project!.Synopsis!.Logline);
        a.Restart(); d=(await a.Store.GetAsync(d.DocumentId))!;
        await a.Store.SaveAsync(LocalPlanning.Synopsis(d,new(Logline:"Offline hook")));
        await b.Store.SaveAsync(other with {Sections=other.Sections.Select(s=>s with {Pages=s.Pages.Select(p=>p with {Content="<p>Other writing</p>"}).ToArray()}).ToArray()});
        await b.Engine.SyncAsync(); await a.Engine.SyncAsync();
        Assert.True(a.Engine.Status(d.DocumentId)!.Conflict);
        await a.Engine.ResolveAsync(d.DocumentId,false);
        Assert.Contains((await a.Store.ListAsync()).Documents,x=>x.Project?.Synopsis?.Logline=="Offline hook");
        Assert.Contains((await a.Store.ListAsync()).Documents,x=>x.Sections[0].Pages[0].Content=="<p>Other writing</p>");
        Assert.All((await a.Store.ListAsync()).Documents,x=>Assert.Single(x.Project!.Nodes.Single(n=>n.NodeType=="scene").Annotations));
    }
    [Fact]
    public async Task PlanningOmissionAndCrossSceneIdentityCannotCommitPartialWriting()
    {
        Guid id=Guid.NewGuid(); var r=PlanningRequest();
        var created=await _sync.MutateAsync("paid",id,r,projects:true,planning:true);
        var bad=r with {OperationId=Guid.NewGuid(),ExpectedVersion=created.State.Version,Document=r.Document! with {Title="Must not commit",Project=r.Document.Project! with {Nodes=r.Document.Project.Nodes.Select(n=>n with {Annotations=[]}).ToArray()}}};
        Assert.Equal(400,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",id,bad,projects:true,planning:true))).Status);
        Assert.Equal("Book",(await _sync.DownloadAsync("paid",id,projects:true,planning:true)).Document!.Title);
        Assert.Single(await _db.SceneAnnotations.ToListAsync());
    }
}
