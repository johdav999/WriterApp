using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Client.Services;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Brainstorming;
using WriterApp.UI.Shared;
using Xunit;
using static WriterApp.Tests.BrainstormingKeepHostTests;

namespace WriterApp.Tests;

public sealed class BrainstormingPlanningHostTests
{
    #pragma warning disable BL0006 // Mount the actual handlers through a test-only renderer, without a native window.
    private sealed class Components:IComponentActivator {
        public BrainstormingPlanningPanel Panel=default!;
        public IComponent CreateInstance(Type type){var c=(IComponent)Activator.CreateInstance(type)!;if(c is BrainstormingPlanningPanel p)Panel=p;return c;}
    }
    private sealed class ReviewIdentityFixture:IBrainstormingReviewIdentity {public string Identity=>"synthetic-owner";public string? ActiveAccountKey=>"synthetic-owner";public event Action? Changed;public void Change()=>Changed?.Invoke();}
    [Fact]
    public async Task AccountChangeDuringCaptureClearsPrivateNotesAndRejectsLateReview() {
        var f=new BrainstormingPlanningTests.LocalFixture();await f.Start(false);var source=await f.Service.CaptureAsync(f.Cache.Session.Scope);var identity=new ReviewIdentityFixture();var components=new Components();
        using var services=new ServiceCollection().AddLogging().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var waiting=new TaskCompletionSource<BrainstormingPlanningSnapshot>();
        await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingPlanningPanel>(ParameterView.FromDictionary(new Dictionary<string,object?> { ["Cache"]=f.Cache,["Store"]=f.Sessions,["ReviewIdentity"]=identity,["Capture"]=(Func<BrainstormingScope,Task<BrainstormingPlanningSnapshot>>)(_=>waiting.Task) }));
            Set(components.Panel,"_decision","Private old account decision");Set(components.Panel,"_character","Private name");var pending=Call(components.Panel,"Prepare");identity.Change();waiting.SetResult(source);await pending;
            typeof(BrainstormingPlanningPanel).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Panel,null);
            Assert.DoesNotContain("Private old account",html.ToHtmlString());Assert.DoesNotContain("Private name",html.ToHtmlString());Assert.DoesNotContain("Review selected fields",html.ToHtmlString());Assert.Null(components.Panel.Cache);
        });
    }
    [Theory][InlineData("Web",BrainstormingPlanningKind.SceneIdea,false)][InlineData("Web",BrainstormingPlanningKind.StoryNote,false)][InlineData("Web",BrainstormingPlanningKind.SynopsisField,false)][InlineData("Web",BrainstormingPlanningKind.CharacterNote,false)]
    [InlineData("Device",BrainstormingPlanningKind.SceneIdea,false)][InlineData("Device",BrainstormingPlanningKind.StoryNote,false)][InlineData("Device",BrainstormingPlanningKind.SynopsisField,false)][InlineData("Device",BrainstormingPlanningKind.CharacterNote,false)]
    [InlineData("Web",BrainstormingPlanningKind.StoryNote,true)][InlineData("Device",BrainstormingPlanningKind.StoryNote,true)]
    public async Task SharedReviewPartialApprovalAndCancelReachBothActualProductionHosts(string surface,BrainstormingPlanningKind kind,bool spoken) {
        var components=new Components();using var services=new ServiceCollection().AddLogging().AddSingleton<IBrainstormingSpeechOutput,UnsupportedBrainstormingSpeechOutput>().AddSingleton<BrainstormingVoiceCoordinator>().AddSingleton<IComponentActivator>(components).BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        BrainstormingCache cache;IBrainstormingCacheStore store;Func<BrainstormingScope,Task<BrainstormingPlanningSnapshot>> capture;Func<WriterApp.Shared.Brainstorming.BrainstormingPlanningReview,Task<BrainstormingCache>> apply;
        Func<BrainstormingPlanningJournal,bool,Task<BrainstormingCache>> inspect;Func<Task> verify;BrainstormingConversationTests.Fixture? fixture=null;IHost? server=null;HttpClient? http=null;Mount? mount=null;LocalAutosaveCoordinator? autosave=null;
        if(surface=="Web") {
            fixture=new();var (_,session)=await BrainstormingPlanningTests.Server(fixture,kind==BrainstormingPlanningKind.SceneIdea);server=await Host(fixture);http=server.GetTestClient();http.BaseAddress=new("https://test.invalid/");http.DefaultRequestHeaders.Add("X-Test-Identity","author");
            var js=new Js();var adapter=new WebBrainstormingSessions(new WebBrainstormingStore(js),http,new AuthStateService(http,NullLogger<AuthStateService>.Instance,new(),new()));
            var scope=session.Scope with {Space=BrainstormingIdentitySpace.Local};cache=await adapter.WriteAsync(new(1,1,BrainstormingPersistence.ReScope(session,scope),"",new(1,scope,session.Scope,1,DateTimeOffset.UtcNow),null,new(1,Guid.NewGuid(),session.Scope,session.Revision,BrainstormingPersistence.Hash(session),DateTimeOffset.UtcNow),[],[]),0,Guid.NewGuid());store=adapter;
            var editor=new MountedWeb();Set(editor,"DocumentId",scope.DocumentId);Set(editor,"_brainstormContextScope",scope);Set(editor,"BrainstormSessions",adapter);Set(editor,"JSRuntime",js);
            capture=s=>Call<BrainstormingPlanningSnapshot>(editor,"CaptureBrainstormPlanning",s);apply=r=>Call<BrainstormingCache>(editor,"ApplyBrainstormPlanning",r);inspect=(j,u)=>Call<BrainstormingCache>(editor,"InspectBrainstormPlanning",j,u);
            verify=async()=>{Assert.Equal(1,await fixture.Db.BrainstormingPlanningSaves.CountAsync());Assert.Equal(1,editor.Reloads);if(kind==BrainstormingPlanningKind.SceneIdea)Assert.Null((await fixture.Db.ProjectNodes.SingleAsync(n=>n.Id==cache.PlanningOperations!.Single().Review.NewNodeId)).LinkedSectionId);else Assert.Empty((await fixture.Db.DocumentSynopses.SingleAsync()).Theme);};
        } else {
            var f=new BrainstormingPlanningTests.LocalFixture();await f.Start(kind==BrainstormingPlanningKind.SceneIdea);cache=f.Cache;store=f.Sessions;var d=(await f.Documents.LoadAsync(cache.Session.Scope.DocumentId))!;var editor=new MountedDevice();mount=new(services);mount.Attach(editor);var session=new LocalEditorSession(f.Documents,d);
            autosave=new(session,new LocalRecoveryStore(Path.Combine(Path.GetTempPath(),"P08-host",Guid.NewGuid().ToString())),()=>Task.CompletedTask);
            Set(editor,"DocumentId",d.DocumentId);Set(editor,"_session",session);Set(editor,"_selectedPage",d.Sections[0].Pages[0].PageId);Set(editor,"_autosave",autosave);Set(editor,"Documents",f.Documents);Set(editor,"BrainstormSessions",f.Sessions);Set(editor,"BrainstormPlanning",f.Service);Set(editor,"_brainstormContextScope",cache.Session.Scope);
            capture=s=>mount.Dispatcher.InvokeAsync(()=>Call<BrainstormingPlanningSnapshot>(editor,"CaptureBrainstormPlanningAsync",s));apply=r=>mount.Dispatcher.InvokeAsync(()=>Call<BrainstormingCache>(editor,"ApplyBrainstormPlanningAsync",r));inspect=(j,u)=>mount.Dispatcher.InvokeAsync(()=>Call<BrainstormingCache>(editor,"InspectBrainstormPlanningAsync",j,u));
            verify=async()=>{var saved=(await f.Documents.LoadAsync(d.DocumentId))!;Assert.Equal(BrainstormingPersistence.Json(d.Sections),BrainstormingPersistence.Json(saved.Sections));if(kind==BrainstormingPlanningKind.SceneIdea)Assert.Null(saved.Project!.Nodes.Single(n=>n.NodeId==cache.PlanningOperations!.Single().Review.NewNodeId).SectionId);else Assert.Empty(LocalBrainstormingPlanning.Synopsis(saved).Theme);};
        }
        try {await renderer.Dispatcher.InvokeAsync(async()=>{
            var html=await renderer.RenderComponentAsync<BrainstormingPlanningPanel>(ParameterView.FromDictionary(new Dictionary<string,object?> { ["Cache"]=cache,["Store"]=store,["Capture"]=capture,["Apply"]=apply,["InspectOrUndo"]=inspect,
                ["CacheChanged"]=EventCallback.Factory.Create<BrainstormingCache>(components,c=>{cache=c;Set(components.Panel,"Cache",c);}) }));
            Set(components.Panel,"_kind",kind);Set(components.Panel,"_decision","<script>window.providerRan=true</script> Åsa 日本語 🧭");Set(components.Panel,"_character","Åsa");
            await Call(components.Panel,"Prepare");if(kind==BrainstormingPlanningKind.SceneIdea)Set(components.Panel,"_parent",(await capture(cache.Session.Scope)).Chapters.Last().Id.ToString());
            if(kind==BrainstormingPlanningKind.SynopsisField){Set(components.Panel,"_addField","Theme");typeof(BrainstormingPlanningPanel).GetMethod("AddField",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Panel,null);typeof(BrainstormingPlanningPanel).GetMethod("SelectField",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Panel,["Theme",new ChangeEventArgs{Value=false}]);}
            await Call(components.Panel,"Review");typeof(BrainstormingPlanningPanel).GetMethod("StateHasChanged",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Panel,null);
            Assert.Contains("Approve selected planning fields",html.ToHtmlString());Assert.DoesNotContain("<script>",html.ToHtmlString());Assert.Equal(BrainstormingApplicationState.Unreviewed,cache.PlanningOperations!.Single().Receipt?.State??BrainstormingApplicationState.Unreviewed);
            if(Environment.GetEnvironmentVariable("WRITERAPP_BRAINSTORMING_CONTEXT_HTML") is {Length:>0} path && kind==BrainstormingPlanningKind.SynopsisField) {
                await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"planning-review-"+surface.ToLowerInvariant()+".html"),html.ToHtmlString());
                await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"planning-cache-"+surface.ToLowerInvariant()+".json"),BrainstormingPersistence.Json(cache));
                var item=cache.PlanningOperations!.Single();var uncertain=cache with {PlanningOperations=[item with {Receipt=new(1,item.Review.OperationId,item.Review.Scope,BrainstormingPlanningContract.Hash(item.Review),BrainstormingApplicationState.Unknown,DateTimeOffset.UtcNow,"Synthetic uncertain response; inspection required.")}]};
                BrainstormingPersistence.Validate(uncertain);await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(path)!,"planning-unknown-"+surface.ToLowerInvariant()+".json"),BrainstormingPersistence.Json(uncertain));
            }
            if(spoken){var voice=services.GetRequiredService<BrainstormingVoiceCoordinator>();var tickets=voice.CaptureReviews(cache.Session.Scope);Assert.Single(tickets);Assert.True(await voice.ApproveAsync(cache.Session.Scope,BrainstormingVoiceCommands.Classify("Approve these planning fields."),BrainstormingRecognitionQuality.Clear,tickets));}else await Call(components.Panel,"Approve");Assert.Equal(BrainstormingApplicationState.Committed,cache.PlanningOperations!.Single().Receipt!.State);await verify();
            await Call(components.Panel,"Inspect",cache.PlanningOperations!.Single());Assert.Equal(BrainstormingApplicationState.Committed,cache.PlanningOperations!.Single().Receipt!.State);
            await Call(components.Panel,"Undo",cache.PlanningOperations!.Single());Assert.Equal(BrainstormingApplicationState.Undone,cache.PlanningOperations!.Single().Receipt!.State);
            // Reviewing another decision and cancelling does not dispatch or alter confirmed destinations.
            await Call(components.Panel,"Prepare");typeof(BrainstormingPlanningPanel).GetMethod("Cancel",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(components.Panel,null);
        });}finally{autosave?.Dispose();mount?.Dispose();server?.Dispose();http?.Dispose();if(fixture is not null)await fixture.DisposeAsync();}
    }
    [Fact]
    public async Task RealAuthenticatedPlanningRoutesRefuseForeignReadsAndRecoverLostCommittedResponse() {
        await using var f=new BrainstormingConversationTests.Fixture();var (service,session)=await BrainstormingPlanningTests.Server(f);var r=BrainstormingPlanningTests.Review(session,await service.CaptureAsync(session.Scope),BrainstormingPlanningKind.StoryNote);
        using var host=await Host(f);using var http=host.GetTestClient();http.BaseAddress=new("https://test.invalid/");var path=$"api/brainstorming/{r.Scope.SessionId}/planning/apply";
        Assert.Equal(HttpStatusCode.Unauthorized,(await http.PostAsJsonAsync(path,r,BrainstormingValidation.Json)).StatusCode);http.DefaultRequestHeaders.Add("X-Test-Identity","other");Assert.Equal(HttpStatusCode.NotFound,(await http.PostAsJsonAsync(path,r,BrainstormingValidation.Json)).StatusCode);
        http.DefaultRequestHeaders.Remove("X-Test-Identity");http.DefaultRequestHeaders.Add("X-Test-Identity","author");Assert.Equal(HttpStatusCode.BadRequest,(await http.PostAsJsonAsync(path,r with {Scope=null!},BrainstormingValidation.Json)).StatusCode);var api=new BrainstormingPlanningHttp(http,_=>new(()=>{}));Assert.Equal(BrainstormingApplicationState.Failed,(await api.InspectAsync(r)).State);
        var local=r.Scope with {Space=BrainstormingIdentitySpace.Local};var files=new LocalBrainstormingStore(Path.Combine(Path.GetTempPath(),"P08-lost-web",Guid.NewGuid().ToString()));await files.WriteAsync(new(1,1,BrainstormingPersistence.ReScope(session,local),"",new(1,local,r.Scope,1,DateTimeOffset.UtcNow),null,null,[],[]),0,Guid.NewGuid());var flow=new BrainstormingPlanningWorkflow(files);
        await Assert.ThrowsAsync<IOException>(()=>flow.ApplyAsync(local,r,async review=>{await api.ApplyAsync(review);throw new IOException("Synthetic lost response");}));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>flow.ApplyAsync(local,r,_=>throw new Exception("No replay")));var confirmed=await api.InspectAsync(r);await flow.RememberAsync(local,r,confirmed);Assert.Equal(BrainstormingApplicationState.Committed,confirmed.State);
        await flow.ApplyAsync(local,r,_=>throw new Exception("No replay"));Assert.Equal(1,await f.Db.BrainstormingPlanningSaves.CountAsync());
        http.DefaultRequestHeaders.Remove("X-Test-Identity");http.DefaultRequestHeaders.Add("X-Test-Identity","other");Assert.Equal(HttpStatusCode.NotFound,(await http.PostAsJsonAsync(path.Replace("apply","inspect"),r,BrainstormingValidation.Json)).StatusCode);
    }
}
