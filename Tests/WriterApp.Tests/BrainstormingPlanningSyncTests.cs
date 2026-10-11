using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Brainstorming;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DocumentSyncTests
{
    [Fact]
    public async Task BrainstormingPlanningIdeaRoundTripsWithoutCreatingOrRewritingProse() {
        var request=PlanningRequest();var project=request.Document!.Project!;var chapter=project.Nodes.Single(n=>n.NodeType=="chapter");var idea=new SyncProjectNode(Guid.NewGuid(),chapter.Id,"scene","Saved idea",1,null,
            MetadataJson:BrainstormingPlanningContract.SceneIdeaMetadata,Notes:"Her brother must remain unaware",Card:new(null,null,null,null,null,null,"Suspicious stranger","Draft",null,null,null,null,"[\"untouched\"]",null,null));
        request=request with {Document=request.Document with {Project=project with {Nodes=[..project.Nodes,idea]}}};var id=Guid.NewGuid();
        var created=await _sync.MutateAsync("paid",id,request,projects:true,planning:true);var snapshot=await _sync.DownloadAsync("paid",id,projects:true,planning:true);
        var local=DeviceSyncMapping.Download(snapshot,Guid.NewGuid(),null,DateTimeOffset.UtcNow);var planned=local.Project!.Nodes.Single(n=>n.ServerNodeId==idea.Id);Assert.Null(planned.SectionId);Assert.Equal("[\"untouched\"]",planned.Card!.TagsJson);
        Assert.Equal(1,await _db.Sections.CountAsync());Assert.Equal(1,await _db.Pages.CountAsync());Assert.Equal("<p>Räksmörgås 日本語</p>",(await _db.Pages.SingleAsync()).Content);
        var upload=DeviceSyncMapping.Upload(local);await _sync.MutateAsync("paid",id,new(Guid.NewGuid(),created.State.Version,"upload",upload),projects:true,planning:true);
        Assert.Null((await _db.ProjectNodes.SingleAsync(n=>n.Id==idea.Id)).LinkedSectionId);Assert.Equal("Her brother must remain unaware",(await _db.SceneNotes.SingleAsync(n=>n.SceneNodeId==idea.Id)).NotesText);
        var invalid=upload with {Project=upload.Project! with {Nodes=upload.Project!.Nodes.Select(n=>n.Id==idea.Id?n with {MetadataJson=null}:n).ToArray()}};
        var latest=await _sync.DownloadAsync("paid",id,projects:true,planning:true);Assert.Equal(400,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",id,new(Guid.NewGuid(),latest.State.Version,"upload",invalid),projects:true,planning:true))).Status);
    }
}
