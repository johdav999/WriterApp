using System.Net.Http.Json;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Client.Pages;
using WriterApp.Data;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private static void ConfigureCheckedScenes(IServiceCollection services)=>services.AddSingleton<IStructureCommandProcessor,StructureCommandProcessor>();
    [Theory][InlineData("scene.suggest",null)][InlineData("scene.refine",null)][InlineData("scene.find-open-questions",null)][InlineData("scene.suggest","writing")][InlineData("scene.suggest","planning")][InlineData("scene.suggest","local")]
    public async Task ActualWebSceneHandlerSavesCheckedPlanningAndAppliesOnlyTheReviewedField(string key,string? failure)
    {
        await using var f=new TranslationFixture{Configure=ConfigureCheckedScenes};await f.Start();
        await using(var db=new AppDbContext(f.Options)){db.SectionSceneCards.Add(new(){SectionId=f.Sections[0],Summary="Authored summary",Status="Final",EmotionalBeat="Authored beat",TagsJson="[\"Authored\"]"});await db.SaveChangesAsync();}
        using var provider=new CheckedProvider(f);using var editor=CheckedEditor(f,provider);
        var auth=new WriterApp.Client.State.AuthMeStateService(editor.Http,new WriterApp.Client.State.DeletedAccountStateService(),new WriterApp.Client.State.DuplicateAccountStateService());await auth.RefreshAsync(force:true);
        editor.AuthMeStateService=auth;
        var featureType=typeof(DocumentEditor).Assembly.GetType("WriterApp.Client.Services.FeatureAccessService")!;
        var logs=typeof(Microsoft.Extensions.Logging.Abstractions.NullLogger<>).MakeGenericType(featureType).GetField("Instance")!.GetValue(null);
        typeof(DocumentEditor).GetProperty("FeatureAccessService",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(editor,Activator.CreateInstance(featureType,auth,editor.Navigation,logs));
        TranslationSet(editor,"_activePage",(await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))!.Single(p=>p.Id==f.Pages[0]));
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"LoadSceneCardAsync",f.Sections[0]);
        var method=typeof(DocumentEditor).GetMethod("RunSceneAiAsync",TranslationPrivate,null,[typeof(string),typeof(string),typeof(string)],null)!;
        await renderer.Dispatcher.InvokeAsync(()=>(Task)method.Invoke(editor,[key,"Improve only the summary","summary"])!);
        Assert.Null(TranslationField(editor,"_sceneAiError"));Assert.NotNull(provider.Request!.ExpectedCanonVersions);Assert.Equal("1",provider.Request.Parameters!["scene_coaching_version"]!.ToString());
        if(failure=="writing")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent writing"});
        if(failure=="planning"){await using var db=new AppDbContext(f.Options);(await db.SectionSceneCards.SingleAsync()).Summary="Concurrent planning";await db.SaveChangesAsync();}
        if(failure=="local")TranslationSet(editor,"_sceneSummary","Local revised summary");
        await TranslationEvent(renderer,editor,"ApplySceneAiProposalAsync");
        await using var verify=new AppDbContext(f.Options);var card=await verify.SectionSceneCards.SingleAsync();
        Assert.Equal(failure==null?"Reviewed summary":failure=="planning"?"Concurrent planning":"Authored summary",card.Summary);
        Assert.Equal("Final",card.Status);Assert.Equal("Authored beat",card.EmotionalBeat);Assert.Equal("[\"Authored\"]",card.TagsJson);
        var history = await verify.WebAiHistoryOperations.ToListAsync();
        if (failure is null)
        {
            var operation = Assert.Single(history);
            Assert.NotNull(operation.CommittedAt); Assert.NotNull(operation.ReportedAt);
            Assert.Equal("SectionCard", WriterApp.Application.AI.WebAiHistoryOperations.Intent(operation).TargetKind);
            var source = (await editor.Http.GetFromJsonAsync<WriterApp.Shared.WebAiSource>(
                $"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&pageId={f.Pages[0]}"))!;
            using var move = await editor.Http.PostAsJsonAsync("api/ai/actions/history/web/move",
                new WriterApp.Shared.WebAiHistoryMoveRequest(source, "Undone"));
            Assert.Equal(System.Net.HttpStatusCode.NoContent, move.StatusCode);
        }
        else Assert.Empty(history);
        if(failure is not null)Assert.NotNull(TranslationField(editor,"_sceneAiError"));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task WebSceneSaveRefusesAConcurrentCardBeforeGenerationWithoutOverwritingPlanning(bool section)
    {
        await using var f=new TranslationFixture{Configure=ConfigureCheckedScenes};await f.Start();
        await using(var db=new AppDbContext(f.Options)) { (await db.ProjectNodes.SingleAsync()).NodeType=WriterApp.Data.Documents.ProjectNodeType.Scene;await db.SaveChangesAsync(); }
        string endpoint=section?$"api/sections/{f.Sections[0]}/scene-card":$"api/scenes/{f.Scene}/scene-card";
        string fingerprint=section?WriterApp.Shared.WebSceneCardSources.Fingerprint((await f.Http.GetFromJsonAsync<SectionSceneCardDto>(endpoint))!) : WriterApp.Shared.WebSceneCardSources.Fingerprint((await f.Http.GetFromJsonAsync<SceneCardDto>(endpoint))!);
        var concurrent=new SceneCardUpdateRequest("", "","","",Summary:"Concurrent planning");
        Assert.True((await f.Http.PutAsJsonAsync($"api/scenes/{f.Scene}/scene-card",concurrent)).IsSuccessStatusCode);
        if(section){await using var db=new AppDbContext(f.Options);db.SectionSceneCards.Add(new(){SectionId=f.Sections[0],Summary="Concurrent planning"});await db.SaveChangesAsync();}
        object payload=section ? new SectionSceneCardUpdateRequest("", "","","",Summary:"Older draft",ExpectedCardFingerprint:fingerprint) : concurrent with {Summary="Older draft",ExpectedCardFingerprint=fingerprint};
        var response=await f.Http.PutAsJsonAsync(endpoint,payload);Assert.Equal(System.Net.HttpStatusCode.Conflict,response.StatusCode);
        await using var verify=new AppDbContext(f.Options);Assert.Equal("Concurrent planning",(await verify.SceneCards.SingleAsync()).Summary);
    }
}
