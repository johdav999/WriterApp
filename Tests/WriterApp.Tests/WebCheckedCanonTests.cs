using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data.Documents;
using WriterApp.Shared.Canon;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DocumentBiblesControllerTests
{
    [Theory][InlineData(CanonKind.Character)][InlineData(CanonKind.Place)][InlineData(CanonKind.Timeline)]
    public async Task WebCanonRefreshAllowsMetadataOnlyAcknowledgementForEachKind(CanonKind kind)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section);SeedVersion(db,id);
        var source=await new WebAiSourceService(db,"user-1").Capture(id,section,null,null,default);
        var controller=BuildController(db,Payload(kind),async ()=>{db.DocumentSyncRecords.Single().Version="ack-only";await db.SaveChangesAsync();});
        var response=await controller.RefreshDeviceSnapshot(id,kind.ToString(),new("v1","missing",true,section,source),default);
        Assert.IsType<OkObjectResult>(response.Result);Assert.Single(db.BibleSnapshots);
    }
    [Theory][InlineData(CanonKind.Character)][InlineData(CanonKind.Place)][InlineData(CanonKind.Timeline)]
    public async Task WebCanonRefreshChecksWritingFingerprintBeforeCommittingEachKind(CanonKind kind)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section);SeedVersion(db,id);
        var source=await new WebAiSourceService(db,"user-1").Capture(id,section,null,null,default);
        int calls=0;var controller=BuildController(db,Payload(kind),async ()=>{calls++;db.Pages.Single().Content="<p>Concurrent prose without sync metadata update</p>";await db.SaveChangesAsync();});
        var response=await controller.RefreshDeviceSnapshot(id,kind.ToString(),new("v1","missing",true,section,source),default);
        Assert.IsType<ConflictObjectResult>(response.Result);Assert.Equal(1,calls);Assert.Empty(db.BibleSnapshots);
    }
    [Theory][InlineData("target")][InlineData("planning")][InlineData("owner")]
    public async Task WebCanonRefreshRefusesChangedPlanningOrWrongSourceBeforeProvider(string failure)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section);SeedVersion(db,id);
        var source=await new WebAiSourceService(db,"user-1").Capture(id,section,null,null,default);
        if(failure=="target")source=source with { DocumentId=Guid.NewGuid() };
        if(failure=="planning"){db.SectionSceneCards.Add(new(){SectionId=section,Summary="Concurrent"});await db.SaveChangesAsync();}
        if(failure=="owner"){db.Documents.Single().OwnerUserId="foreign-owner";await db.SaveChangesAsync();}
        int calls=0;var controller=BuildController(db,Payload(CanonKind.Character),()=>{calls++;return Task.CompletedTask;});
        var response=await controller.RefreshDeviceSnapshot(id,"character",new("v1","missing",true,section,source),default);
        if(failure=="owner")Assert.IsType<NotFoundResult>(response.Result);else Assert.IsType<ConflictObjectResult>(response.Result);
        Assert.Equal(0,calls);Assert.Empty(db.BibleSnapshots);
    }
}
