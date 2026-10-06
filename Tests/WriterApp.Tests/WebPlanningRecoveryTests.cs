using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.Synopsis;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Client.State;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private static async Task<(WebRecoveryRequest Request,WebRecoveryReceipt Receipt)> ReplayRecovery(TranslationFixture f,WebRecoveryItem item,string outcome,Guid? operation=null) {
        var lease=await new WebCheckedAi(f.Http).Capture(f.Document,item.SectionId,page:item.PageId,scene:item.SceneId);
        var request=new WebRecoveryRequest(operation??Guid.NewGuid(),item.ApplicationId,outcome,lease.Source);
        using var response=await f.Http.PostAsJsonAsync("api/ai/actions/history/recovery/replay",request);
        Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
        return(request,(await response.Content.ReadFromJsonAsync<WebRecoveryReceipt>())!);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WebPlanningRecoveryRestoresOnlyApprovedSceneFieldsAndTheirExactLinkedMirrors(bool inspector) {
        await using var h=new SceneApprovalHarness();await h.Start(inspector);
        await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.Summary,SceneCoachingField.SubplotTags});await h.Event("ApplySceneAiProposalAsync");Assert.True(string.IsNullOrEmpty(h.Error),h.Error);
        var item=Assert.Single((await h.Fixture.Http.GetFromJsonAsync<WebRecoveryItem[]>($"api/ai/actions/history/recovery/{h.Fixture.Document}"))!,i=>i.CanUndo);
        await using(var db=new AppDbContext(h.Fixture.Options)) {
            var card=await db.SceneCards.SingleAsync();card.Status="Revised";card.TagsJson="[ \"Later 日本語\" ]";
            var section=await db.SectionSceneCards.SingleAsync();section.Status="Revised";await db.SaveChangesAsync();
        }
        var undo=await ReplayRecovery(h.Fixture,item,"Undone");
        using(var repeated=await h.Fixture.Http.PostAsJsonAsync("api/ai/actions/history/recovery/replay",undo.Request)) {Assert.Equal(HttpStatusCode.OK,repeated.StatusCode);Assert.Equal(undo.Receipt.Intent.OperationId,(await repeated.Content.ReadFromJsonAsync<WebRecoveryReceipt>())!.Intent.OperationId);}
        h.Fixture.Restart();
        await using(var db=new AppDbContext(h.Fixture.Options)) {var card=await db.SceneCards.SingleAsync();Assert.Equal("Authored summary",card.Summary);Assert.Equal("[\"Plot\"]",card.SubplotTagsJson);Assert.Equal("[ \"Later 日本語\" ]",card.TagsJson);Assert.Equal("Revised",card.Status);if(!inspector)Assert.Equal("Authored summary",(await db.SectionSceneCards.SingleAsync()).Summary);}
        await ReplayRecovery(h.Fixture,item,"Redone");await ReplayRecovery(h.Fixture,item,"Undone");
        await using var verify=new AppDbContext(h.Fixture.Options);Assert.Equal(4,await verify.WebAiHistoryOperations.CountAsync(r=>r.CommittedAt!=null));Assert.Equal("Authored summary",(await verify.SceneCards.SingleAsync()).Summary);Assert.Empty(await verify.UsageEvents.ToListAsync());
    }
    [Fact]
    public async Task WebPlanningRecoveryIntentOnlyApprovalSurvivesActualEditorPreflushAndReopen() {
        await using var h=new SceneApprovalHarness();await h.Start(false);
        await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.NarrativeIntent});
        await h.Event("ApplySceneAiProposalAsync");Assert.True(string.IsNullOrEmpty(h.Error),h.Error);
        var f=h.Fixture;
        var item=Assert.Single((await f.Http.GetFromJsonAsync<WebRecoveryItem[]>($"api/ai/actions/history/recovery/{f.Document}"))!,i=>i.CanUndo);
        await h.Event("LoadSceneCardAsync",f.Sections[0]);
        await h.Event("SaveBeforeScopedRecovery");
        await using(var db=new AppDbContext(f.Options)) {
            var card=await db.SectionSceneCards.SingleAsync();
            Assert.True(string.IsNullOrEmpty(card.NarrativePurpose),"An unchanged preflush must not materialize the DTO's legacy projection.");
        }
        await ReplayRecovery(f,item,"Undone");
        await h.Event("LoadSceneCardAsync",f.Sections[0]);
        Assert.Equal(string.Empty,h.Get("_sceneNarrativeIntent"));
        Assert.True(string.IsNullOrEmpty((await f.Http.GetFromJsonAsync<SectionSceneCardDto>($"api/sections/{f.Sections[0]}/scene-card"))!.NarrativeIntent));
        await h.Event("SaveBeforeScopedRecovery");await ReplayRecovery(f,item,"Redone");
        await h.Event("LoadSceneCardAsync",f.Sections[0]);Assert.Equal("Proposed intent",h.Get("_sceneNarrativeIntent"));
        // A real pending manual edit must still be flushed before a replay captures its source.
        h.Set("_sceneSummary","Later manual summary");await h.Event("SaveBeforeScopedRecovery");
        Assert.Equal("Later manual summary",(await f.Http.GetFromJsonAsync<SectionSceneCardDto>($"api/sections/{f.Sections[0]}/scene-card"))!.Summary);
        await ReplayRecovery(f,item,"Undone");await h.Event("LoadSceneCardAsync",f.Sections[0]);
        Assert.Equal(string.Empty,h.Get("_sceneNarrativeIntent"));
        Assert.Equal("Later manual summary",h.Get("_sceneSummary"));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WebPlanningRecoveryUnrelatedManualSaveDoesNotResurrectAnUndoneIntent(bool inspector) {
        await using var h=new SceneApprovalHarness();await h.Start(inspector);var f=h.Fixture;
        await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.NarrativeIntent});await h.Event("ApplySceneAiProposalAsync");
        var item=Assert.Single((await f.Http.GetFromJsonAsync<WebRecoveryItem[]>($"api/ai/actions/history/recovery/{f.Document}"))!,i=>i.CanUndo);
        string endpoint=inspector?$"api/scenes/{f.Scene}/scene-card":$"api/sections/{f.Sections[0]}/scene-card";
        var dto=await f.Http.GetFromJsonAsync<SceneCardDto>(endpoint);
        var request=new SceneCardUpdateRequest(dto!.NarrativePurpose,dto.EmotionalBeat,dto.KeyEvents,dto.OpenQuestions,dto.PovCharacterId,dto.PlaceId,dto.TimelineEventId,dto.TimeRef,dto.Tags,dto.References,"Later manual summary",dto.Status,dto.SubplotTags,dto.NarrativeRole,dto.NarrativeIntent);
        using var save=await f.Http.PutAsJsonAsync(endpoint,request);Assert.True(save.IsSuccessStatusCode,await save.Content.ReadAsStringAsync());
        await ReplayRecovery(f,item,"Undone");
        var reopened=(await f.Http.GetFromJsonAsync<SceneCardDto>(endpoint))!;
        Assert.True(string.IsNullOrEmpty(reopened.NarrativeIntent));Assert.Equal("Later manual summary",reopened.Summary);
        await using var db=new AppDbContext(f.Options);
        Assert.True(string.IsNullOrEmpty(inspector?(await db.SceneCards.SingleAsync()).NarrativePurpose:(await db.SectionSceneCards.SingleAsync()).NarrativePurpose));
    }
    [Theory][InlineData("affected")][InlineData("mirror")][InlineData("deleted")][InlineData("account")][InlineData("legacy")][InlineData("storage")]
    public async Task WebPlanningRecoveryRefusesConflictsDeletedTargetsForeignAccountsAndMissingSnapshotsAtomically(string failure) {
        await using var h=new SceneApprovalHarness();await h.Start(false);await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.Summary});await h.Event("ApplySceneAiProposalAsync");
        var item=Assert.Single((await h.Fixture.Http.GetFromJsonAsync<WebRecoveryItem[]>($"api/ai/actions/history/recovery/{h.Fixture.Document}"))!,i=>i.CanUndo);
        var lease=await new WebCheckedAi(h.Fixture.Http).Capture(h.Fixture.Document,item.SectionId,scene:item.SceneId);
        await using(var db=new AppDbContext(h.Fixture.Options)) {
            if(failure=="affected")(await db.SectionSceneCards.SingleAsync()).Summary="Later affected field";
            if(failure=="mirror")(await db.SceneCards.SingleAsync()).Summary="Later mirror field";
            if(failure=="deleted")db.SectionSceneCards.Remove(await db.SectionSceneCards.SingleAsync());
            if(failure=="legacy")(await db.WebAiHistoryOperations.SingleAsync()).RecoveryJson=null;
            if(failure=="storage")await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailReplay BEFORE INSERT ON WebAiHistoryOperations WHEN NEW.Sequence>1 BEGIN SELECT RAISE(ABORT,'injected replay history failure'); END;");
            await db.SaveChangesAsync();
        }
        if(failure is "affected" or "mirror" or "legacy" or "storage")lease=await new WebCheckedAi(h.Fixture.Http).Capture(h.Fixture.Document,item.SectionId,scene:item.SceneId);
        if(failure=="account"){h.Fixture.Http.DefaultRequestHeaders.Remove("X-Test-Owner");h.Fixture.Http.DefaultRequestHeaders.Add("X-Test-Owner","foreign");}
        using var refused=await h.Fixture.Http.PostAsJsonAsync("api/ai/actions/history/recovery/replay",new WebRecoveryRequest(Guid.NewGuid(),item.ApplicationId,"Undone",lease.Source));Assert.False(refused.IsSuccessStatusCode);
        await using var verify=new AppDbContext(h.Fixture.Options);Assert.Single(await verify.WebAiHistoryOperations.ToListAsync());Assert.Equal(failure=="mirror"?"Later mirror field":"Proposed summary",(await verify.SceneCards.SingleAsync()).Summary);
    }
    [Fact]
    public async Task WebPlanningRecoverySnapshotFailureRollsBackAuthoredApplyAndRetainsPreparedIntent() {
        await using var h=new SceneApprovalHarness();await h.Start(false);await h.Event("ApprovedSceneFieldsChanged",new[]{SceneCoachingField.Summary});
        await using(var db=new AppDbContext(h.Fixture.Options))await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailSnapshot BEFORE UPDATE ON WebAiHistoryOperations WHEN NEW.RecoveryJson IS NOT NULL BEGIN SELECT RAISE(ABORT,'injected snapshot failure'); END;");
        await h.Event("ApplySceneAiProposalAsync");Assert.NotNull(h.Error);
        await using var verify=new AppDbContext(h.Fixture.Options);Assert.Equal("Authored summary",(await verify.SceneCards.SingleAsync()).Summary);var row=Assert.Single(await verify.WebAiHistoryOperations.ToListAsync());Assert.Null(row.CommittedAt);Assert.Null(row.RecoveryJson);
    }
    public static IEnumerable<object[]> SynopsisRecoveryFields=>SynopsisCoaching.Fields.Select(f=>new object[]{f.Key});
    [Theory][MemberData(nameof(SynopsisRecoveryFields))]
    public async Task WebPlanningRecoverySupportsEverySynopsisFieldAndKeepsUnrelatedLaterPlanning(string field) {
        await using var f=new TranslationFixture{Configure=ConfigureCheckedSynopsis};await f.Start();var component=new SynopsisComponent();
        SynopsisSet(component,"Http",f.Http);SynopsisSet(component,"DocumentId",f.Document);SynopsisSet(component,"CheckedAi",new WebCheckedAi(f.Http));SynopsisSet(component,"HistoryOutbox",new WebAiHistoryOutbox(f.Http,new HistoryMemoryModule(),new WebCheckedAi(f.Http)));SynopsisSet(component,"Logger",NullLogger<Synopsis>.Instance);
        SynopsisSet(component,"AuthMeStateService",new AuthMeStateService(f.Http,new DeletedAccountStateService(),new DuplicateAccountStateService()));
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(component);
        await SynopsisEvent(renderer,component,"LoadSynopsisAsync",f.Document);SynopsisSet(component,"_aiUserNotes","Intent");SynopsisSet(component,"_aiSelectedFieldKey",field);await SynopsisEvent(renderer,component,"RunAiAsync","suggest");await SynopsisEvent(renderer,component,"ApplyAiSuggestionAsync");
        Assert.Null(typeof(Synopsis).GetField("_aiError",TranslationPrivate)!.GetValue(component));
        var item=Assert.Single((await f.Http.GetFromJsonAsync<WebRecoveryItem[]>($"api/ai/actions/history/recovery/{f.Document}"))!,i=>i.CanUndo);
        string property=SynopsisCoaching.Fields.Single(v=>v.Key==field).Property;
        string other=property=="Notes"?"Logline":"Notes";
        await using(var db=new AppDbContext(f.Options)){typeof(WriterApp.Data.Documents.DocumentSynopsisRecord).GetProperty(other)!.SetValue(await db.DocumentSynopses.SingleAsync(),"Later untouched 日本語");await db.SaveChangesAsync();}
        await ReplayRecovery(f,item,"Undone");f.Restart();
        await using(var db=new AppDbContext(f.Options)){var synopsis=await db.DocumentSynopses.SingleAsync();Assert.Equal("",synopsis.GetType().GetProperty(property)!.GetValue(synopsis));Assert.Equal("Later untouched 日本語",synopsis.GetType().GetProperty(other)!.GetValue(synopsis));}
        await ReplayRecovery(f,item,"Redone");await using var verify=new AppDbContext(f.Options);Assert.Equal("Approved premise",typeof(WriterApp.Data.Documents.DocumentSynopsisRecord).GetProperty(property)!.GetValue(await verify.DocumentSynopses.SingleAsync()));
    }
}
