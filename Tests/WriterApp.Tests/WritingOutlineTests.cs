using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Data.Documents;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class AiActionsControllerTests
{
    [Theory]
    [InlineData("rewrite.selection")][InlineData("expand.section")][InlineData("propose.next-paragraph")]
    public async Task SavedWritingOutlineIsEquivalentAcrossHostsAndReachesActualProviderBuilder(string key)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        var project=db.Documents.Single().ProjectId;var node=Guid.NewGuid();
        db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});
        db.ProjectNodes.Add(new(){Id=node,ProjectId=project,DocumentId=id,NodeType=ProjectNodeType.Scene,Title="Åsa <script>日本語</script>",LinkedSectionId=section});await db.SaveChangesAsync();
        var ai=new WritingOrchestrator();var controller=BuildController(db,ai);
        var snapshot=Assert.IsType<WritingOutlineSnapshot>(Assert.IsType<OkObjectResult>((await controller.GetWritingOutline(id,default)).Result).Value);
        var localId=Guid.NewGuid();var localSection=Guid.NewGuid();var localPage=Guid.NewGuid();var localProject=Guid.NewGuid();
        var document=new LocalDocument{DocumentId=localId,ServerDocumentId=id,ServerProjectId=project,ServerVersion="v1",SyncState=LocalSyncState.Synced,
            Title="Doc",LocalRevision=1,CreatedAtUtc=DateTimeOffset.UtcNow,UpdatedAtUtc=DateTimeOffset.UtcNow,
            Sections=[new(){SectionId=localSection,ServerSectionId=section,Title="Scene",OrderIndex=0,CreatedAtUtc=default,UpdatedAtUtc=default,Pages=[new(){PageId=localPage,ServerPageId=page,Title="Page",OrderIndex=0,CreatedAtUtc=default,UpdatedAtUtc=default,Content="<p>Maya checked her phone.</p>",ContentFormat=LocalContentFormat.Html}]}],
            Project=new(){ProjectId=localProject,ServerProjectId=project,ManuscriptId=localId,Title="Project",Nodes=[new(){NodeId=Guid.NewGuid(),ServerNodeId=node,Title="Åsa <script>日本語</script>",NodeType="scene",SectionId=localSection}]}};
        Assert.Equal(WritingOutline.ProviderText(snapshot),WritingOutline.ProviderText(LocalWritingOutline.Capture(document)));
        var parameters=new Dictionary<string,object?>();
        if(key.EndsWith("section"))parameters[WritingActions.Parameter]=WritingActions.Serialize(new(1,id,section,[new(page,[new("0.0","Maya checked her phone at 08:05 and sighed.")])]));
        var request=new AiActionExecuteRequestDto(id,section,page,key=="rewrite.selection"?0:null,key=="rewrite.selection"?4:null,
            key=="rewrite.selection"?"Maya":null,"Maya checked her phone at 08:05 and sighed.","untrusted old outline",parameters,"v1",WritingOutline:snapshot);
        var response=Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>((await controller.ExecuteAction(key,request,default)).Result).Value);
        Assert.Equal(snapshot.Fingerprint,response.SourceOutlineFingerprint);Assert.Equal(WritingOutline.ProviderText(snapshot),ai.Request!.Context.OutlineText);
        Assert.Contains("\\u003Cscript\\u003E",ai.Request.Context.OutlineText);Assert.DoesNotContain("untrusted old outline",ai.Request.Context.OutlineText);
        Assert.Contains("Maya checked",db.Pages.Single().Content);
    }
    [Theory]
    [InlineData("title")][InlineData("order")][InlineData("delete")][InlineData("document")][InlineData("version")][InlineData("after-title")]
    [InlineData("node-title")][InlineData("node-order")][InlineData("node-delete")]
    public async Task WrongOrChangedSavedOutlineNeverReturnsApplicableGeneration(string change)
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out var page);
        db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});
        db.ProjectNodes.Add(new(){Id=Guid.NewGuid(),ProjectId=db.Documents.Single().ProjectId,DocumentId=id,Title="Saved node",LinkedSectionId=section});await db.SaveChangesAsync();
        var ai=new WritingOrchestrator();var controller=BuildController(db,ai);
        var snapshot=Assert.IsType<WritingOutlineSnapshot>(Assert.IsType<OkObjectResult>((await controller.GetWritingOutline(id,default)).Result).Value);
        if(change=="document")snapshot=WritingOutline.Create(Guid.NewGuid(),snapshot.ProjectId,"v1",snapshot.Sections,snapshot.Nodes);
        if(change=="version")snapshot=snapshot with{DocumentVersion="old"};
        if(change=="title")db.Sections.Single().Title="Later title";
        if(change=="order")db.Sections.Single().OrderIndex=3;
        if(change=="delete")db.DocumentSyncRecords.Single().IsTrashed=true;
        if(change=="node-title")db.ProjectNodes.Single().Title="Later node";
        if(change=="node-order")db.ProjectNodes.Single().OrderIndex=3;
        if(change=="node-delete")db.ProjectNodes.Single().SyncDeletionId=Guid.NewGuid();
        if(change=="after-title")ai.Before=()=>{db.Sections.Single().Title="Changed during provider";db.SaveChanges();};
        await db.SaveChangesAsync();
        var result=await controller.ExecuteAction("rewrite.selection",new(id,section,page,0,4,"Maya","Maya checked.",null,new(),"v1",WritingOutline:snapshot),default);
        Assert.IsType<ConflictObjectResult>(result.Result);Assert.Equal(change=="after-title"?1:0,ai.Calls);
    }
    [Fact]
    public async Task SavedOutlineEndpointUsesOwnedDocumentNotPrimaryOrOtherManuscript()
    {
        await using var db=BuildDbContext();SeedDocumentGraph(db,out var id,out var section,out _);
        db.DocumentSyncRecords.Add(new(){DocumentId=id,OwnerUserId="user-1",Version="v1",Sequence=1});
        var sibling=Guid.NewGuid();var project=db.Documents.Single().ProjectId;
        db.Documents.Add(new(){Id=sibling,ProjectId=project,OwnerUserId="user-1",Title="Sibling"});
        db.ProjectNodes.Add(new(){Id=Guid.NewGuid(),ProjectId=project,DocumentId=sibling,Title="Foreign sibling planning"});await db.SaveChangesAsync();
        var controller=BuildController(db,new WritingOrchestrator());
        var snapshot=Assert.IsType<WritingOutlineSnapshot>(Assert.IsType<OkObjectResult>((await controller.GetWritingOutline(id,default)).Result).Value);
        Assert.Empty(snapshot.Nodes);Assert.Equal(section,Assert.Single(snapshot.Sections).Id);
        db.Documents.Single(d=>d.Id==id).OwnerUserId="other-owner";await db.SaveChangesAsync();Assert.IsType<NotFoundResult>((await controller.GetWritingOutline(id,default)).Result);
    }
}

public sealed class WritingOutlineContractTests
{
    [Fact]
    public async Task DeviceApprovalRejectsAnotherBackendAndPreservesOfflineOutlineCapture()
    {
        using var f=new TranslationTestFixture();var source=await f.Create();var api=new LocalWritingTests.Api();
        var first=new DeviceAiService(api,f.Account,f.Network,new("test",new("https://first-backend.test/")));
        var prepared=LocalWritingTests.Prepare(source,WritingScope.Selection);
        var proposal=await new LocalWritingActions(f.Repository,f.History,first,api).ProposeAsync(prepared,default);
        var second=new DeviceAiService(api,f.Account,f.Network,new("test",new("https://second-backend.test/")));
        Assert.Throws<DeviceAiException>(()=>second.RequireCurrentAccount(proposal));
        var before=LocalDocumentCodec.Encode(source);f.Network.SetOnline(false);
        Assert.Equal(prepared.Request.Request.WritingOutline!.Fingerprint,LocalWritingOutline.Capture(source).Fingerprint);
        Assert.Equal(before,LocalDocumentCodec.Encode(source));
    }
    [Theory][InlineData("standalone")][InlineData("empty")][InlineData("oversize")][InlineData("cycle")][InlineData("foreign")][InlineData("reorder")]
    public void DeterministicBoundedOutlinePreservesIdentityAndNeverSilentlyTruncates(string scenario)
    {
        var id=Guid.NewGuid();var section=Guid.NewGuid();var node=Guid.NewGuid();
        WritingOutlineSnapshot Make()=>WritingOutline.Create(id,scenario=="standalone"?null:Guid.NewGuid(),"v1",
            scenario=="empty"?[]:scenario=="oversize"?Enumerable.Range(0,101).Select(i=>new WritingOutlineSection(Guid.NewGuid(),i,new string('x',200))):[new(section,0,"日本語 🧭")],
            scenario is "standalone" or "empty" or "oversize"?[]:[new(node,scenario=="cycle"?node:null,0,"scene","Title",scenario=="foreign"?Guid.NewGuid():section)]);
        if(scenario is "oversize" or "cycle" or "foreign")Assert.Throws<InvalidDataException>(Make);
        else {var value=Make();WritingOutline.Validate(value);Assert.Equal(value.Fingerprint,(value with{DocumentVersion="v2"}).Fingerprint);
            if(scenario=="reorder")Assert.NotEqual(value.Fingerprint,WritingOutline.Create(id,value.ProjectId,"v1",value.Sections,[value.Nodes[0] with{Order=2}]).Fingerprint);}
    }
    [Theory][InlineData("capability")][InlineData("receipt")][InlineData("stale-before")][InlineData("stale-after")]
    public async Task DeviceOutlineCapabilityReceiptAndPlanningAreRequired(string failure)
    {
        using var f=new TranslationTestFixture();var source=await f.Create();var api=new LocalWritingTests.Api();var ai=new DeviceAiService(api,f.Account,f.Network);
        var actions=new LocalWritingActions(f.Repository,f.History,ai,api);var prepared=LocalWritingTests.Prepare(source,WritingScope.Section,"tighten.section");
        if(failure=="capability")api.Outline=false;if(failure=="receipt")api.EchoOutline=false;
        async Task Change()=>await f.Repository.SaveAsync(source with{Project=source.Project! with{Nodes=source.Project.Nodes.Select(n=>n with{Title="Changed title"}).ToArray()}});
        if(failure=="stale-before")await Change();if(failure=="stale-after")api.Before=async _=>await Change();
        await Assert.ThrowsAnyAsync<Exception>(()=>actions.ProposeAsync(prepared,default));Assert.Equal(failure is "capability" or "stale-before"?0:1,api.Calls);
        Assert.Empty(await f.History.HistoryAsync(source.DocumentId));
    }
}
