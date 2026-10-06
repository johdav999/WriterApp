using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.AI.StoryCoach;
using WriterApp.Application.Synopsis;
using WriterApp.Application.Subscriptions;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Client.State;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class CheckedSynopsisPlan : IEntitlementService {
        public Task<UserEntitlements> GetEntitlementsAsync(string userId)=>Task.FromResult(new UserEntitlements(userId,"professional","Professional",new Dictionary<string,string>{{"ai.enabled","true"}}));
        public WriterApp.Application.Subscriptions.PlanTier GetUserTier(UserEntitlements e)=>WriterApp.Application.Subscriptions.PlanTier.Professional;
        public Task<bool> HasAsync(string id,string key)=>Task.FromResult(true);public Task<int?> GetIntAsync(string id,string key)=>Task.FromResult<int?>(100);public void InvalidateForUser(string id) { }
    }
    private sealed class CheckedSynopsisProvider : IAiOrchestrator {
        public Action? Before=null;public int Calls;public string? Output=null;
        public IReadOnlyList<IAiAction> Actions {get;}=[new SynopsisEvaluateAction(),new SynopsisQuestionsAction(),new StoryCoachAction()];
        public IAiAction? GetAction(string key)=>Actions.SingleOrDefault(a=>a.ActionId==key);public bool CanRunAction(string key)=>true;
        public AiStreamingCapabilities GetStreamingCapabilities(string key)=>new(false,false);public AiStreamingSession StreamActionAsync(string key,AiActionInput input,CancellationToken ct)=>throw new NotSupportedException();
        public Task<AiExecutionResult> ExecuteActionAsync(string key,AiActionInput input,CancellationToken ct) {Calls++;Before?.Invoke();return Task.FromResult(AiExecutionResult.Success(new AiProposal(Guid.NewGuid(),Guid.Empty,"Synopsis",key,"synthetic",Guid.NewGuid(),DateTime.UtcNow,null,[],[],"Synopsis","synopsis",null,null,
            Output ?? (key==StoryCoachAction.ActionIdValue?"{\"proposedText\":\"Approved premise\",\"commentary\":\"Reasoning\"}":"Readable coaching questions and evaluation."))));}
    }
    private static void ConfigureCheckedSynopsis(IServiceCollection services) {
        services.AddSingleton<IAiOrchestrator>(new CheckedSynopsisProvider());services.AddSingleton<IEntitlementService>(new CheckedSynopsisPlan());
        services.AddSingleton<SynopsisAiContextBuilder>();services.AddSingleton<StoryCoachContextBuilder>();services.AddScoped<IAiActionHistoryStore,EfCoreAiActionHistoryStore>();
    }
    private sealed class SynopsisComponent : Synopsis {
        protected override Task OnParametersSetAsync()=>Task.CompletedTask;
        protected override void BuildRenderTree(RenderTreeBuilder b) { }
    }
    private static void SynopsisSet(Synopsis component,string name,object? value) {
        var property=typeof(Synopsis).GetProperty(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        if(property is not null)property.SetValue(component,value);else typeof(Synopsis).GetField(name,TranslationPrivate)!.SetValue(component,value);
    }
    private static Task SynopsisEvent(TranslationRenderer renderer,Synopsis component,string name,params object?[] args)=>renderer.Dispatcher.InvokeAsync(()=>(Task)typeof(Synopsis).GetMethod(name,TranslationPrivate)!.Invoke(component,args)!);
    [Theory][InlineData("evaluate")][InlineData("questions")][InlineData("suggest")]
    public async Task ActualWebSynopsisHandlersSaveThenCheckDedicatedModeAndApplyOnlyTheReviewedField(string mode)
    {
        await using var f=new TranslationFixture{Configure=ConfigureCheckedSynopsis};await f.Start();var component=new SynopsisComponent();
        SynopsisSet(component,"Http",f.Http);SynopsisSet(component,"DocumentId",f.Document);SynopsisSet(component,"CheckedAi",new WebCheckedAi(f.Http));
        SynopsisSet(component,"Logger",NullLogger<Synopsis>.Instance);
        SynopsisSet(component,"HistoryOutbox",new WebAiHistoryOutbox(f.Http,new HistoryMemoryModule(),new WebCheckedAi(f.Http)));
        SynopsisSet(component,"AuthMeStateService",new AuthMeStateService(f.Http,new DeletedAccountStateService(),new DuplicateAccountStateService()));
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(component);
        await SynopsisEvent(renderer,component,"LoadSynopsisAsync",f.Document);
        SynopsisSet(component,"_aiUserNotes","Author story intent");SynopsisSet(component,"_aiSelectedFieldKey","premise");
        await SynopsisEvent(renderer,component,"RunAiAsync",mode);
        Assert.Null(typeof(Synopsis).GetField("_aiError",TranslationPrivate)!.GetValue(component));Assert.NotNull(typeof(Synopsis).GetField("_synopsisAiLease",TranslationPrivate)!.GetValue(component));
        if(mode=="suggest") { await SynopsisEvent(renderer,component,"ApplyAiSuggestionAsync");var saved=(await f.Http.GetFromJsonAsync<DocumentSynopsisDto>($"api/documents/{f.Document}/synopsis"))!;Assert.Equal("Approved premise",saved.Premise);Assert.Equal("",saved.Logline);Assert.Equal("",saved.Notes); }
        else Assert.False((bool)typeof(Synopsis).GetProperty("CanApplySuggestion",TranslationPrivate)!.GetValue(component)!);
    }
    [Theory][InlineData("field")][InlineData("local")][InlineData("writing")][InlineData("planning")][InlineData("account")][InlineData("expired")]
    public async Task ActualWebSynopsisApplyRefusesChangedIdentityAndSource(string failure)
    {
        await using var f=new TranslationFixture{Configure=ConfigureCheckedSynopsis};await f.Start();var component=new SynopsisComponent();
        SynopsisSet(component,"Http",f.Http);SynopsisSet(component,"DocumentId",f.Document);SynopsisSet(component,"CheckedAi",new WebCheckedAi(f.Http));SynopsisSet(component,"Logger",NullLogger<Synopsis>.Instance);
        SynopsisSet(component,"AuthMeStateService",new AuthMeStateService(f.Http,new DeletedAccountStateService(),new DuplicateAccountStateService()));
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(component);
        await SynopsisEvent(renderer,component,"LoadSynopsisAsync",f.Document);SynopsisSet(component,"_aiUserNotes","Intent");SynopsisSet(component,"_aiSelectedFieldKey","premise");await SynopsisEvent(renderer,component,"RunAiAsync","suggest");
        if(failure=="field")SynopsisSet(component,"_aiSelectedFieldKey","notes");
        if(failure=="local") { var current=(DocumentSynopsisDto)typeof(Synopsis).GetField("_synopsis",TranslationPrivate)!.GetValue(component)!;SynopsisSet(component,"_synopsis",current with { Notes="Uncommitted note" }); }
        if(failure=="writing")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent prose"});
        if(failure=="planning") { await using var db=new AppDbContext(f.Options);(await db.ProjectNodes.SingleAsync()).Title="Concurrent planning";await db.SaveChangesAsync(); }
        if(failure=="account") { f.Http.DefaultRequestHeaders.Remove("X-Test-Owner");f.Http.DefaultRequestHeaders.Add("X-Test-Owner","other-owner"); }
        if(failure=="expired") { var result=(SynopsisAiResponseDto)typeof(Synopsis).GetField("_synopsisAiResponse",TranslationPrivate)!.GetValue(component)!;SynopsisSet(component,"_synopsisAiResponse",result with { CreatedUtc=DateTimeOffset.UtcNow.AddHours(-2) }); }
        await SynopsisEvent(renderer,component,"ApplyAiSuggestionAsync");Assert.NotNull(typeof(Synopsis).GetField("_aiError",TranslationPrivate)!.GetValue(component));
        await using var verify=new AppDbContext(f.Options);Assert.Equal("",(await verify.DocumentSynopses.SingleAsync()).Premise);
    }
}
