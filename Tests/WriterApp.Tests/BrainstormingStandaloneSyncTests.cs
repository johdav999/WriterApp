using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared.Sync;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DocumentSyncTests
{
    [Fact]
    public async Task BrainstormingStandaloneSynopsisSyncV5RoundTripsWithoutProjectOrLostWritingAndRejectsOldOrStaleUploads() {
        var id=Guid.NewGuid();var request=Create("<p><strong>Original prose 日本語</strong></p>");request=request with {Document=request.Document! with {Synopsis=new(Logline:"Suspicious stranger",Notes:"Character: Åsa\nHer brother remains unaware.")}};
        Assert.Equal(426,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",id,request,projects:true,planning:true,multipleDocuments:true))).Status);
        var first=await _sync.MutateAsync("paid",id,request,projects:true,planning:true,multipleDocuments:true,standalonePlanning:true);
        var snapshot=await _sync.DownloadAsync("paid",id,projects:true,planning:true,multipleDocuments:true,standalonePlanning:true);Assert.Null(snapshot.Document!.Project);Assert.Equal(request.Document.Synopsis,snapshot.Document.Synopsis);
        var device1=DeviceSyncMapping.Download(snapshot,Guid.NewGuid(),null,DateTimeOffset.UtcNow);var device2=DeviceSyncMapping.Download(snapshot,Guid.NewGuid(),null,DateTimeOffset.UtcNow);Assert.Null(device1.Project);Assert.Equal(request.Document.Synopsis,LocalBrainstormingPlanning.Synopsis(device1));
        var upload=DeviceSyncMapping.Upload(device1);upload=upload with {Synopsis=upload.Synopsis! with {Theme="Updated web-visible theme"}};await _sync.MutateAsync("paid",id,new(Guid.NewGuid(),first.State.Version,"upload",upload),projects:true,planning:true,multipleDocuments:true,standalonePlanning:true);
        Assert.Equal(409,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.MutateAsync("paid",id,new(Guid.NewGuid(),first.State.Version,"upload",DeviceSyncMapping.Upload(device2)),projects:true,planning:true,multipleDocuments:true,standalonePlanning:true))).Status);
        snapshot=await _sync.DownloadAsync("paid",id,projects:true,planning:true,multipleDocuments:true,standalonePlanning:true);var latest=DeviceSyncMapping.Download(snapshot,device2.DocumentId,device2,DateTimeOffset.UtcNow);Assert.Equal("Updated web-visible theme",LocalBrainstormingPlanning.Synopsis(latest).Theme);Assert.Equal(request.Document.Sections.Single().Pages.Single().Content,latest.Sections.Single().Pages.Single().Content);
        Assert.Equal(1,await _db.Sections.CountAsync());Assert.Equal(1,await _db.Pages.CountAsync());Assert.Empty(await _db.ProjectNodes.ToArrayAsync());Assert.Equal("Updated web-visible theme",(await _db.DocumentSynopses.SingleAsync()).Theme);
        var story=await new BrainstormingStorySourceReader(_db,"paid").ReadAsync(id);var scope=BrainstormingContractTests.Fixture().Scope with {DocumentId=id,ProjectId=null,Space=BrainstormingIdentitySpace.Server};
        var prepared=BrainstormingContextPolicy.Prepare(scope,story,await BrainstormingStorySources.ProjectAsync(story,new BrainstormingServerProjector(),default),new(SectionId:story.Outline.Sections.Single().Id),[]);
        Assert.Equal(BrainstormingPersistence.Json(snapshot.Document!.Synopsis),prepared.Context!.Sources.Single(s=>s.Kind==BrainstormingSourceKind.Synopsis && s.Included).Text);Assert.True(prepared.Context.Policy!.Ready);
        Assert.Equal(404,(await Assert.ThrowsAsync<DocumentSyncException>(()=>_sync.DownloadAsync("foreign",id,standalonePlanning:true))).Status);
    }
}
