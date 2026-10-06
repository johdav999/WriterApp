using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Users;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class DemoEntitlements : IUserEntitlementStore {
        public Task<UserEntitlement> GetOrCreateAsync(string owner,CancellationToken ct=default)=>Task.FromResult(new UserEntitlement {UserId=owner});
    }
    private sealed class DemoIdentity : IDeviceIdentityClient {
        public string User="ordinary-author";public bool IsConfigured=>true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive,CancellationToken ct)=>Task.FromResult<DeviceAccessToken?>(new("synthetic",DateTimeOffset.UtcNow.AddHours(1),"Writer",User));
        public Task SignOutAsync()=>Task.CompletedTask;
    }
    private sealed class DemoLostAck(TranslationFixture f) : DelegatingHandler(f.Server.GetTestServer().CreateHandler()) {
        public bool LoseBootstrap,LoseProgress,LoseMutation;public Func<Task>? AfterStatus;
        public int MutationRequests;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            if(request.RequestUri!.AbsolutePath=="/api/auth/me")return new(HttpStatusCode.OK){Content=JsonContent.Create(new{isAuthenticated=true,userId="ordinary-author",isPaidAccessActive=false})};
            var response=await base.SendAsync(request,ct);
            if(request.RequestUri.AbsolutePath.EndsWith("/operations"))MutationRequests++;
            if(request.RequestUri.AbsolutePath.EndsWith("/status") && AfterStatus is not null)await AfterStatus();
            if(request.RequestUri.AbsolutePath.EndsWith("/bootstrap") && LoseBootstrap || request.RequestUri.AbsolutePath.EndsWith("/progress") && LoseProgress
                || request.RequestUri.AbsolutePath.EndsWith("/operations") && LoseMutation) {
                LoseBootstrap=false;LoseProgress=false;LoseMutation=false;response.Dispose();throw new HttpRequestException("Lost acknowledgement after server persistence");
            }
            return response;
        }
    }
    private static async Task<TranslationFixture> DemoFixture() {
        var f=new TranslationFixture();await f.Start();f.Http.Dispose();f.Server.Dispose();
        f.Server=Host($"Data Source={Path.Combine(f.Root,"web.db")};Pooling=False",services=> {
            services.AddScoped<IDeletedUserIdentityService,DeletedUserIdentityService>();services.AddScoped<UserEventService>();
            services.AddScoped<IOnboardingBootstrapService,OnboardingBootstrapService>();services.AddScoped<IProjectSceneLinkingService,ProjectSceneLinkingService>();
            services.AddScoped<IOnboardingDemoEligibilityService,OnboardingDemoEligibilityService>();
            services.Configure<WriterAiOptions>(options=>options.Enabled=true);
            services.AddSingleton<IUserEntitlementStore,DemoEntitlements>();services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddScoped<ProjectDeletionService>();services.AddScoped<DocumentSyncService>();
        });
        f.Http=f.Server.GetTestClient();f.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");return f;
    }
    private static async Task<OnboardingDemoStatus> BootstrapDemo(HttpClient http) {
        using var response=await http.PostAsJsonAsync("/api/onboarding/demo/bootstrap",new OnboardingDemoBootstrapRequest(1,Guid.NewGuid(),"Other"));
        Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<OnboardingDemoStatus>())!;
    }

    [Fact]
    public async Task ProductionDemoBootstrapIsOwnedStableNeverAdoptsWritingAndProgressReceiptsReconcileWebCompletion() {
        await using var f=await DemoFixture();
        var status=(await f.Http.GetFromJsonAsync<OnboardingDemoStatus>("/api/onboarding/demo/status"))!;
        Assert.Null(status.Workspace);Assert.False(status.Available);
        var first=await BootstrapDemo(f.Http);var second=await BootstrapDemo(f.Http);
        Assert.Equal(first.Workspace,second.Workspace);Assert.True(first.Available);Assert.False(first.RequestUsed);
        var workspace=first.Workspace!;Assert.NotEqual(f.Document,workspace.DocumentId);
        Assert.Equal(HttpStatusCode.Forbidden,(await f.Http.GetAsync($"/api/sync/v4/documents/{workspace.DocumentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.Http.GetAsync($"/api/onboarding/demo/documents/{f.Document}")).StatusCode);
        using var foreign=f.Server.GetTestClient();foreign.DefaultRequestHeaders.Add("X-Test-Owner","other-author");
        Assert.Equal(HttpStatusCode.NotFound,(await foreign.GetAsync($"/api/onboarding/demo/documents/{workspace.DocumentId}")).StatusCode);
        using var anonymous=f.Server.GetTestClient();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync("/api/onboarding/demo/status")).StatusCode);
        var request=new OnboardingDemoProgressRequest(1,Guid.NewGuid(),first.Revision,2,false);
        var progressed=await f.Http.PostAsJsonAsync("/api/onboarding/demo/progress",request);Assert.Equal(HttpStatusCode.OK,progressed.StatusCode);
        var receipt=(await progressed.Content.ReadFromJsonAsync<OnboardingDemoStatus>())!;
        Assert.Equal(2,receipt.OnboardingStep);Assert.False(receipt.HasCompletedOnboarding);
        var replay=await f.Http.PostAsJsonAsync("/api/onboarding/demo/progress",request);Assert.Equal(receipt,await replay.Content.ReadFromJsonAsync<OnboardingDemoStatus>());
        Assert.Equal(HttpStatusCode.Conflict,(await f.Http.PostAsJsonAsync("/api/onboarding/demo/progress",request with{Step=3})).StatusCode);
        await f.Http.PostAsync("/api/onboarding/complete",null);await f.Http.PostAsJsonAsync("/api/onboarding/step",new{step=1});
        var complete=(await f.Http.GetFromJsonAsync<OnboardingDemoStatus>("/api/onboarding/demo/status"))!;
        Assert.True(complete.HasCompletedOnboarding);Assert.Equal(10,complete.OnboardingStep);Assert.False(complete.Available);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Http.PostAsJsonAsync("/api/onboarding/demo/progress",request with{OperationId=Guid.NewGuid()})).StatusCode);
        await using var db=new AppDbContext(f.Options);
        Assert.Equal(f.Html,(await db.Pages.Where(p=>p.DocumentId==f.Document).OrderBy(p=>p.OrderIndex).ThenBy(p=>p.Id).ToListAsync()).OrderBy(p=>Array.IndexOf(f.Pages,p.Id)).Select(p=>p.Content));
        Assert.Equal(f.Document,(await db.Projects.SingleAsync(p=>p.Id==f.Project)).PrimaryDocumentId);
        Assert.Equal(2,await db.Documents.CountAsync());
    }

    [Theory][InlineData("empty")][InlineData("altered")][InlineData("trashed")][InlineData("missing")][InlineData("expired")][InlineData("used")]
    public async Task ProductionDemoRetriesNeverReseedAndPolicyRejectsUnavailableGrants(string scenario) {
        await using var f=await DemoFixture();var first=await BootstrapDemo(f.Http);var workspace=first.Workspace!;
        await using(var db=new AppDbContext(f.Options)) {
            var page=await db.Pages.SingleAsync(p=>p.DocumentId==workspace.DocumentId);
            if(scenario is "empty" or "altered")page.Content=scenario=="empty" ? "" : "<p><strong>Authored 日本語 🧭</strong></p>";
            if(scenario=="trashed"){(await db.Documents.SingleAsync(d=>d.Id==workspace.DocumentId)).DeletedAtUtc=DateTime.UtcNow;}
            if(scenario=="missing")db.Pages.Remove(page);
            var grant=await db.OnboardingDemoWorkspaces.SingleAsync();
            if(scenario=="expired")grant.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1);
            if(scenario=="used")grant.RequestUsed=true;
            await db.SaveChangesAsync();
        }
        using var retried=await f.Http.PostAsJsonAsync("/api/onboarding/demo/bootstrap",new OnboardingDemoBootstrapRequest(1,Guid.NewGuid(),"Novel"));
        if(scenario=="trashed")Assert.Equal(HttpStatusCode.Conflict,retried.StatusCode);else Assert.Equal(HttpStatusCode.OK,retried.StatusCode);
        var current=(await f.Http.GetFromJsonAsync<OnboardingDemoStatus>("/api/onboarding/demo/status"))!;Assert.Equal(workspace,current.Workspace);
        if(scenario is "expired" or "used" or "trashed")Assert.False(current.Available);
        await using var verify=new AppDbContext(f.Options);
        if(scenario=="missing")Assert.Empty(await verify.Pages.Where(p=>p.DocumentId==workspace.DocumentId).ToListAsync());
        if(scenario is "empty" or "altered")Assert.Equal(scenario=="empty" ? "" : "<p><strong>Authored 日本語 🧭</strong></p>",(await verify.Pages.SingleAsync(p=>p.DocumentId==workspace.DocumentId)).Content);
        Assert.Equal(2,await verify.Documents.CountAsync());
    }

    [Fact]
    public async Task ActualDeviceDemoChoiceSurvivesLostAcknowledgementGuestSignInSyncAndCompletionWithoutAdoptingGuestWriting() {
        await using var f=await DemoFixture();using var handler=new DemoLostAck(f);using var http=new HttpClient(handler){BaseAddress=new("http://localhost/")};
        http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        var root=Path.Combine(f.Root,"local");var identity=new DemoIdentity();var account=new DeviceAccountService(identity);var host=new DeviceHostOptions("Test",new("http://localhost/"));
        var disk=new FileLocalDocumentStore(root);var store=new LocalOnboardingStore(Path.Combine(root,"guide"));var network=new DeviceConnectivity();var repository=new LocalDocumentRepository(disk);
        using var sync=new DeviceSyncEngine(disk,new(Path.Combine(root,"sync")),new DeviceSyncApi(http,store,host),account,network,repository,host);
        var service=new DeviceOnboarding(store,disk,account,host,http,sync,network);
        var guest=await service.OpenPracticeAsync(await service.ReadAsync());var guestBytes=LocalDocumentCodec.Encode(guest.Document);
        await account.SignInAsync();var source=await service.ReadAsync();Assert.Null(source.PracticeDocumentId);
        handler.LoseBootstrap=true;await Assert.ThrowsAsync<HttpRequestException>(()=>service.OpenServerDemoAsync(source));
        var retained=await service.ReadAsync();Assert.NotNull(retained.DemoBootstrapId);
        var opened=await service.OpenServerDemoAsync(retained);Assert.NotEqual(guest.Document.DocumentId,opened.Document.DocumentId);
        Assert.Equal(opened.Progress.Demo!.Workspace!.DocumentId,opened.Document.ServerDocumentId);Assert.Equal(2,opened.Progress.Demo.OnboardingStep);
        var again=await service.OpenServerDemoAsync(await service.ReadAsync());Assert.Equal(opened.Document.DocumentId,again.Document.DocumentId);
        Assert.Equal(guestBytes,LocalDocumentCodec.Encode((await disk.GetAsync(guest.Document.DocumentId))!));
        var edit=await repository.SaveAsync(again.Document with{Sections=again.Document.Sections.Select(s=>s with{Pages=s.Pages.Select(p=>p with{Content="<p><strong>My demo edit 日本語</strong></p>"}).ToArray()}).ToArray()});
        await sync.SyncAsync();Assert.Null(sync.LastError);
        var remote=(await f.Http.GetFromJsonAsync<SyncSnapshot>($"/api/onboarding/demo/documents/{edit.ServerDocumentId}"))!;
        Assert.Contains("My demo edit",remote.Document!.Sections[0].Pages[0].Content);
        handler.LoseProgress=true;await Assert.ThrowsAsync<HttpRequestException>(async ()=>await service.ReconcileProgressAsync(await service.ReadAsync(),true));
        var pending=await service.ReadAsync();Assert.NotNull(pending.PendingProgress);
        var completed=await service.ReconcileProgressAsync(pending,true);Assert.True(completed.Demo!.HasCompletedOnboarding);Assert.Null(completed.PendingProgress);Assert.Equal("Active",completed.Status);
        network.SetOnline(false);Assert.Equal(completed,await service.ReadAsync());await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RefreshDemoAsync(completed));
        network.SetOnline(true);await account.SignOutAsync();identity.User="other-author";await account.SignInAsync();
        Assert.Null((await service.ReadAsync()).Demo);await Assert.ThrowsAsync<InvalidOperationException>(()=>service.OpenServerDemoAsync(completed));
        Assert.Equal(guestBytes,LocalDocumentCodec.Encode((await disk.GetAsync(guest.Document.DocumentId))!));
    }

    [Fact]
    public async Task LateAccountStatusCannotPopulateAnotherScopeAndSecondWindowCannotReplacePendingIdentity() {
        await using var f=await DemoFixture();using var handler=new DemoLostAck(f);using var http=new HttpClient(handler){BaseAddress=new("http://localhost/")};
        http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        var root=Path.Combine(f.Root,"isolated");var account=new DeviceAccountService(new DemoIdentity());await account.SignInAsync();
        var disk=new FileLocalDocumentStore(root);var store=new LocalOnboardingStore(Path.Combine(root,"guide"));var host=new DeviceHostOptions("Test",new("http://localhost/"));
        var service=new DeviceOnboarding(store,disk,account,host,http);var source=await service.ReadAsync();
        handler.AfterStatus=()=>account.SignOutAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.RefreshDemoAsync(source));
        Assert.Null((await store.ReadAsync(source.Scope)).Demo);Assert.Null((await service.ReadAsync()).Demo);
        handler.AfterStatus=null;await account.SignInAsync();source=await service.ReadAsync();
        var other=new LocalOnboardingStore(Path.Combine(root,"guide"));
        var reserved=await other.UpdateAsync(source,value=>value with{DemoBootstrapId=Guid.NewGuid()});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.OpenServerDemoAsync(source));
        Assert.Equal(reserved,await service.ReadAsync());
        var backend=new DeviceOnboarding(store,disk,account,new("Test",new("http://other-backend/")),http);
        Assert.Null((await backend.ReadAsync()).DemoBootstrapId);await Assert.ThrowsAsync<InvalidOperationException>(()=>backend.RefreshDemoAsync(reserved));
        Assert.Empty((await disk.ListAsync()).Documents);
    }

    [Fact]
    public async Task SecondDeviceImportsSameOwnedDemoAndLostSyncAcknowledgementReplaysWithoutReseeding() {
        await using var f=await DemoFixture();using var handler=new DemoLostAck(f);using var http=new HttpClient(handler){BaseAddress=new("http://localhost/")};
        http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        var account=new DeviceAccountService(new DemoIdentity());await account.SignInAsync();var host=new DeviceHostOptions("Test",new("http://localhost/"));
        var root=Path.Combine(f.Root,"first-device");var disk=new FileLocalDocumentStore(root);var repository=new LocalDocumentRepository(disk);var network=new DeviceConnectivity();
        var guideStore=new LocalOnboardingStore(Path.Combine(root,"guide"));
        var api=new DeviceSyncApi(http,guideStore,host);
        await BootstrapDemo(f.Http); // A web-created grant alone cannot enroll this desktop.
        var refused=await Assert.ThrowsAsync<DeviceSyncApiException>(()=>api.GetOwnerAsync(default));Assert.Equal("demo_choice_required",refused.Code);
        Assert.Empty((await disk.ListAsync()).Documents);
        using var sync=new DeviceSyncEngine(disk,new(Path.Combine(root,"sync")),api,account,network,repository,host);
        var guide=new DeviceOnboarding(guideStore,disk,account,host,http,sync,network);
        var first=await guide.OpenServerDemoAsync(await guide.ReadAsync());
        await repository.SaveAsync(first.Document with{Sections=first.Document.Sections.Select(s=>s with{Pages=s.Pages.Select(p=>p with{Content="<p><strong>Persisted demo edit 日本語</strong></p>"}).ToArray()}).ToArray()});
        handler.LoseMutation=true;await sync.SyncAsync();Assert.False(handler.LoseMutation);Assert.True(handler.MutationRequests>=2);
        await sync.SyncAsync();Assert.Null(sync.LastError);
        var secondRoot=Path.Combine(f.Root,"second-device");var secondDisk=new FileLocalDocumentStore(secondRoot);var secondRepo=new LocalDocumentRepository(secondDisk);
        var secondStore=new LocalOnboardingStore(Path.Combine(secondRoot,"guide"));
        using var secondSync=new DeviceSyncEngine(secondDisk,new(Path.Combine(secondRoot,"sync")),new DeviceSyncApi(http,secondStore,host),account,network,secondRepo,host);
        var secondGuide=new DeviceOnboarding(secondStore,secondDisk,account,host,http,secondSync,network);
        var second=await secondGuide.OpenServerDemoAsync(await secondGuide.ReadAsync());
        Assert.Equal(first.Document.ServerDocumentId,second.Document.ServerDocumentId);Assert.NotEqual(first.Document.DocumentId,second.Document.DocumentId);
        Assert.Contains("Persisted demo edit",second.Document.Sections[0].Pages[0].Content);
        Assert.Equal(second.Document.DocumentId,(await secondGuide.OpenServerDemoAsync(await secondGuide.ReadAsync())).Document.DocumentId);
        await using var db=new AppDbContext(f.Options);Assert.Single(await db.OnboardingDemoWorkspaces.ToListAsync());Assert.Equal(2,await db.Documents.CountAsync());
        Assert.Equal(f.Html,(await db.Pages.Where(p=>p.DocumentId==f.Document).ToListAsync()).OrderBy(p=>Array.IndexOf(f.Pages,p.Id)).Select(p=>p.Content));
    }
}
