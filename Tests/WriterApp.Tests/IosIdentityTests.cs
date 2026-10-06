using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using System.Net;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.iOS;
using WriterApp.iOS.Authentication;
using Xunit;

namespace WriterApp.Tests;

public sealed class IosIdentityTests
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111", Client = "22222222-2222-2222-2222-222222222222";
    private static IosIdentityOptions Options(string backend="https://api.example/", string environment="Production", string? redirect=null) => new() {
        Authentication=new() {TenantId=Tenant, ClientId=Client, Authority="https://login.microsoftonline.com/"+Tenant,
            RedirectUri=redirect??IosIdentityOptions.Callback, Scopes=["api://api/access_as_user"]},
        Environment=DeviceEnvironmentConfiguration.Create(environment,backend,null)
    };
    private sealed class Store : IIosIdentitySelectionStore {
        public Dictionary<string,string> Values=new(); public bool FailRead,FailWrite,FailRemove;
        public Task<string?> ReadAsync(string key) => FailRead?throw new IOException("Synthetic Keychain locked"):Task.FromResult(Values.GetValueOrDefault(key));
        public Task WriteAsync(string key,string accountId) {if(FailWrite)throw new IOException("Synthetic Keychain locked");Values[key]=accountId;return Task.CompletedTask;}
        public Task RemoveAsync(string key) {if(FailRemove)throw new IOException("Synthetic Keychain locked");Values.Remove(key);return Task.CompletedTask;}
    }
    private sealed class Session : IIosMsalSession {
        public List<string> Accounts=[]; public int Acquisitions,Removals,Callbacks;public bool UiRequired,Offline,Expired,WrongTenant,WrongAccount,FailRemove;
        public string NextAccount="account-a";public string? LastSelected;public bool LastInteractive;public bool CallbackAccepted=true,CallbackThrows;
        public TaskCompletionSource<IosIdentityResult>? Pending;public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<IReadOnlyList<string>> AccountsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<string>>(Accounts.ToArray());
        public async Task<IosIdentityResult> AcquireAsync(bool interactive,string? accountId,CancellationToken ct) {
            Acquisitions++;LastInteractive=interactive;LastSelected=accountId;Started.TrySetResult();
            if(Pending is not null)return await Pending.Task; // Deliberately ignores cancellation, testing the adapter boundary.
            if(UiRequired)throw new DeviceSignInRequiredException();if(Offline)throw new HttpRequestException("Synthetic offline");
            if(!Accounts.Contains(NextAccount))Accounts.Add(NextAccount);
            return Result(WrongAccount?"foreign":NextAccount,Expired,WrongTenant);
        }
        public static IosIdentityResult Result(string id="account-a",bool expired=false,bool wrongTenant=false)=>new(new("synthetic-access-token",DateTimeOffset.UtcNow.AddMinutes(expired?-1:60),"Synthetic author",id),wrongTenant?Client:Tenant);
        public Task RemoveAccountsAsync(){Removals++;if(FailRemove)throw new IOException("Synthetic Keychain removal failure");Accounts.Clear();return Task.CompletedTask;}
        public bool Continue(Uri callback){Callbacks++;if(CallbackThrows)throw new FormatException("Synthetic malformed callback");return CallbackAccepted;}
    }
    [Theory][InlineData("http://localhost")][InlineData("https://evil.example/auth")][InlineData("msauth.other://auth")][InlineData("msauth.com.prosa.writer.ios://other")]
    public void IosConfigurationCannotAcceptWindowsOrForeignCallback(string redirect) {
        Assert.False(Options(redirect:redirect).IsConfigured);Assert.True(Options().IsConfigured);
        Assert.False(Options().Authentication.IsConfigured);Assert.True(Options().Authentication.IsProviderConfigured);
    }
    [Fact]
    public void BundledIosConfigurationHasNoInventedClientRegistrationAndMatchesCallbackAndKeychainEntitlements() {
        using var settings=typeof(IosIdentityTests).Assembly.GetManifestResourceStream("Prosa.IosAuthTestSettings")!;
        var auth=JsonSerializer.Deserialize<DeviceAuthOptions>(settings)!;Assert.Empty(auth.ClientId);Assert.Equal(IosIdentityOptions.Callback,auth.RedirectUri);
        using var info=typeof(IosIdentityTests).Assembly.GetManifestResourceStream("Prosa.IosInfoTestSettings")!;
        using var entitlement=typeof(IosIdentityTests).Assembly.GetManifestResourceStream("Prosa.IosEntitlementsTestSettings")!;
        Assert.Contains(XDocument.Load(info).Descendants("string"),s=>s.Value=="msauth."+IosIdentityOptions.BundleId);
        Assert.Contains(XDocument.Load(entitlement).Descendants("string"),s=>s.Value=="$(AppIdentifierPrefix)"+IosIdentityOptions.KeychainGroup);
    }
    [Theory][InlineData("Production",false)][InlineData("Production",true)][InlineData("Staging",true)][InlineData("Development",true)]
    public void SettingsOverridesAreDevelopmentOnlyAndBackendIsBuildSelected(string environment,bool debug) {
        using var stream=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(Options().Authentication));
        var metadata=new Dictionary<string,string?>{{"ProsaEnvironment",environment},{"ProsaApiBaseUrl","https://chosen.example/"}};
        var result=IosBuildConfiguration.ReadSettings(stream,metadata,debug,key=>key=="WRITERAPP_AUTH_CLIENT_ID"?Tenant:null,IosIdentityOptions.BundleId);
        Assert.Equal(environment=="Development"&&debug?Tenant:Client,result.Authentication.ClientId);
        Assert.Equal("chosen.example",result.Environment.ApiBaseAddress.Host);
    }
    [Fact]
    public void SelectionScopeSeparatesBackendEnvironmentAndNativeClient() {
        Assert.NotEqual(Options().SelectionKey,Options("https://other.example/").SelectionKey);
        Assert.NotEqual(Options().SelectionKey,Options(environment:"Staging").SelectionKey);
        Assert.Throws<ArgumentException>(()=>Options("http://api.example/"));
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes("{}"));
        Assert.Throws<InvalidOperationException>(()=>IosBuildConfiguration.ReadSettings(stream,new Dictionary<string,string?>(),false,_=>null,"foreign.bundle"));
    }
    [Fact]
    public async Task SignInPersistsOnlySelectedAccountAndRestartRenewsSilentlyWithoutGuessingAnotherBackend() {
        var store=new Store();var session=new Session();var options=Options();var identity=new IosDeviceIdentityClient(options,session,store);
        var first=await identity.AcquireAsync(true,default);Assert.Equal("account-a",first!.AccountId);Assert.Single(store.Values);Assert.DoesNotContain(first.Value,store.Values.Values);
        session.Accounts.Add("old-account");var reopened=new IosDeviceIdentityClient(options,session,store);
        Assert.Equal(first.AccountId,(await reopened.AcquireAsync(false,default))!.AccountId);Assert.False(session.LastInteractive);Assert.Equal(first.AccountId,session.LastSelected);
        var other=new IosDeviceIdentityClient(Options("https://other.example/"),session,store);Assert.Null(await other.AcquireAsync(false,default));Assert.Equal(2,session.Acquisitions);
        Assert.DoesNotContain("synthetic-access-token",Session.Result().ToString());
    }
    [Theory][InlineData("tenant")][InlineData("account")][InlineData("expired")][InlineData("revoked")]
    public async Task InvalidSilentResultsInvalidateSharedUiGenerationWithoutChangingDocuments(string failure) {
        var session=new Session();var identity=new IosDeviceIdentityClient(Options(),session,new Store());var account=new DeviceAccountService(identity);
        await account.SignInAsync();var generation=account.Generation;int changes=0;account.Changed+=()=>changes++;
        session.WrongTenant=failure=="tenant";session.WrongAccount=failure=="account";session.Expired=failure=="expired";session.UiRequired=failure=="revoked";
        await Assert.ThrowsAsync<DeviceSignInRequiredException>(()=>account.GetTokenAsync(default));
        Assert.False(account.IsSignedIn);Assert.Null(account.AccountId);Assert.True(account.Generation>generation);Assert.True(changes>0);
    }
    [Theory][InlineData("read")][InlineData("write")][InlineData("offline")]
    public async Task KeychainOrNetworkFailureIsActionableAndDoesNotInventSignedInIdentity(string failure) {
        var store=new Store();var session=new Session();var identity=new IosDeviceIdentityClient(Options(),session,store);var account=new DeviceAccountService(identity);
        if(failure=="read"){await account.SignInAsync();store.FailRead=true;}else {store.FailWrite=failure=="write";session.Offline=failure=="offline";}
        await Assert.ThrowsAsync<DeviceIdentityUnavailableException>(()=>failure=="read"?account.GetTokenAsync(default):account.SignInAsync());
        if(failure!="read"){Assert.False(account.IsSignedIn);Assert.Empty(store.Values);}else Assert.True(account.IsSignedIn);
        Assert.Contains("local editing",account.Message);Assert.DoesNotContain("Synthetic",account.Message);
    }
    [Fact]
    public async Task BackgroundHandoffAndDuplicateCallbacksCannotStartAnotherAcquisitionOrAcceptForeignUrl() {
        var store=new Store();var session=new Session{Pending=new(TaskCreationOptions.RunContinuationsAsynchronously)};
        var identity=new IosDeviceIdentityClient(Options(),session,store);var account=new DeviceAccountService(identity);var lifecycle=new IosIdentityLifecycle(identity,account);
        var signIn=account.SignInAsync();await session.Started.Task; lifecycle.Background();await lifecycle.ResumeAsync();Assert.Equal(1,session.Acquisitions);
        Assert.False(identity.HandleCallback(new("https://foreign.example/auth?code=x")));Assert.False(identity.HandleCallback(new(IosIdentityOptions.Callback+":123?code=x")));
        session.CallbackAccepted=false;Assert.False(identity.HandleCallback(new(IosIdentityOptions.Callback+"?state=old")));
        session.CallbackThrows=true;Assert.False(identity.HandleCallback(new(IosIdentityOptions.Callback+"?state=malformed")));session.CallbackThrows=false;
        session.CallbackAccepted=true;Assert.True(identity.HandleCallback(new(IosIdentityOptions.Callback+"?state=current")));Assert.False(identity.HandleCallback(new(IosIdentityOptions.Callback+"?state=current")));
        session.Pending.SetResult(Session.Result());await signIn;Assert.False(identity.HandleCallback(new(IosIdentityOptions.Callback+"?state=current")));
    }
    [Theory][InlineData("cancel")][InlineData("destroy")][InlineData("signout")]
    public async Task IgnoredCancellationOrSignOutCannotRestoreLateProviderAccount(string boundary) {
        var store=new Store();var session=new Session{Pending=new(TaskCreationOptions.RunContinuationsAsynchronously)};
        var identity=new IosDeviceIdentityClient(Options(),session,store);var account=new DeviceAccountService(identity);using var ct=new CancellationTokenSource();
        var signIn=account.SignInAsync(ct.Token);await session.Started.Task;Task? signOut=null;
        if(boundary=="cancel")ct.Cancel();else if(boundary=="destroy")new IosIdentityLifecycle(identity,account).Destroy();else {signOut=account.SignOutAsync();Assert.False(account.IsSignedIn);}
        Assert.False(identity.HandleCallback(new(IosIdentityOptions.Callback+"?state=late")));
        session.Pending.SetResult(Session.Result());await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>signIn);if(signOut is not null)await signOut;
        Assert.False(account.IsSignedIn);Assert.Empty(store.Values);
    }
    [Theory][InlineData(null)][InlineData("selection")][InlineData("native")]
    public async Task SignOutClearsNativeAccountsAndSelectionAndPreservesLocalWritingEvenIfOneStoreFails(string? failure) {
        var root=Path.Combine(Path.GetTempPath(),"WriterApp.IosIdentity",Guid.NewGuid().ToString("N"));
        try {
            var repository=new LocalDocumentRepository(new FileLocalDocumentStore(root));var document=await repository.CreateAsync("Authored 日本語");
            var store=new Store();var session=new Session();var identity=new IosDeviceIdentityClient(Options(),session,store);var account=new DeviceAccountService(identity);await account.SignInAsync();store.FailRemove=failure=="selection";session.FailRemove=failure=="native";
            if(failure is not null)await Assert.ThrowsAsync<DeviceIdentityUnavailableException>(()=>account.SignOutAsync());else await account.SignOutAsync();
            Assert.False(account.IsSignedIn);if(failure!="native")Assert.Empty(session.Accounts);Assert.Equal(1,session.Removals);Assert.NotNull(await repository.LoadAsync(document.DocumentId));
            await Assert.ThrowsAsync<DeviceSignInRequiredException>(()=>account.GetTokenAsync(default));
            if(failure is not null){store.FailRemove=false;session.FailRemove=false;await account.SignOutAsync();}Assert.Empty(store.Values);Assert.Empty(session.Accounts);
        } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]
    public async Task ResumeRestoresOnceAndSwitchAccountInvalidatesGenerationWhileSameAccountRenewalDoesNot() {
        var store=new Store();var session=new Session();var identity=new IosDeviceIdentityClient(Options(),session,store);var account=new DeviceAccountService(identity);await account.SignInAsync();var first=account.Generation;
        session.Pending=new(TaskCreationOptions.RunContinuationsAsynchronously);session.Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var lifecycle=new IosIdentityLifecycle(identity,account);var resume=lifecycle.ResumeAsync();await session.Started.Task;await lifecycle.ResumeAsync();session.Pending.SetResult(Session.Result());await resume;
        Assert.Equal(2,session.Acquisitions);Assert.Equal(first,account.Generation);
        session.Pending=null;session.NextAccount="account-b";await account.SignInAsync();Assert.True(account.Generation>first);Assert.Equal("account-b",account.AccountId);Assert.Equal("account-b",store.Values[Options().SelectionKey]);
    }
    [Fact]
    public async Task BackgroundWithoutActiveBrowserRefusesInteractivePresentationAndGuestRestoreRemainsSilent() {
        var identity=new IosDeviceIdentityClient(Options(),new Session(),new Store());var account=new DeviceAccountService(identity);var lifecycle=new IosIdentityLifecycle(identity,account);
        lifecycle.Background();await Assert.ThrowsAsync<DeviceIdentityUnavailableException>(()=>account.SignInAsync());await lifecycle.ResumeAsync();Assert.False(account.IsSignedIn);
    }
    [Theory][InlineData("{")][InlineData("null")][InlineData("{\"Scopes\":null}")]
    public void InvalidAuthSettingsDisableSignInAndRetainUsableOfflineHostConfiguration(string json) {
        using var stream=new MemoryStream(Encoding.UTF8.GetBytes(json));var metadata=new Dictionary<string,string?>{{"ProsaEnvironment","Development"},{"ProsaApiBaseUrl","http://localhost:5387/"}};
        var options=IosBuildConfiguration.ReadSettings(stream,metadata,true,_=>null,IosIdentityOptions.BundleId);
        Assert.False(options.IsConfigured);Assert.True(options.Environment.ApiBaseAddress.IsLoopback);
    }
    private sealed class UncooperativeIdentity : IDeviceIdentityClient {
        public bool IsConfigured=>true;public TaskCompletionSource<DeviceAccessToken?> Pending=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive,CancellationToken cancellationToken)=>Pending.Task;
        public Task SignOutAsync()=>Task.CompletedTask;
    }
    [Fact]
    public async Task SharedAccountServiceRefusesLateIdentityEvenWhenAdapterIgnoresCancellation() {
        var identity=new UncooperativeIdentity();var account=new DeviceAccountService(identity);var signIn=account.SignInAsync();var old=account.Generation;
        var signOut=account.SignOutAsync();Assert.True(account.Generation>old);Assert.False(account.IsSignedIn);
        identity.Pending.SetResult(Session.Result().Token);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>signIn);await signOut;
        Assert.False(account.IsSignedIn);Assert.Null(account.AccountId);Assert.Null(account.DisplayName);
    }
    [Theory][InlineData("switch")][InlineData("revoked")][InlineData("destroy")]
    public async Task ExistingProductionAiApprovalGuardRejectsIosAccountLifecycleChanges(string boundary) {
        var session=new Session();var identity=new IosDeviceIdentityClient(Options(),session,new Store());var account=new DeviceAccountService(identity);await account.SignInAsync();
        var prepared=new DeviceAiPrepared("rewrite.selection",new(null,null,null,null,null,null,null,null,null),DeviceAiAction.Rewrite,Guid.NewGuid(),"<p>Original</p>",0,8,"Original","replace");
        var proposal=new DeviceAiProposal(prepared,"Approved",null,"Original",new(),Guid.NewGuid(),AccountGeneration:account.Generation,PreparedAt:DateTimeOffset.UtcNow);
        using var http=new HttpClient(new NoNetwork());var ai=new DeviceAiService(new DeviceAiApi(http),account,new());ai.RequireCurrentAccount(proposal);
        if(boundary=="switch"){session.NextAccount="account-b";await account.SignInAsync();}
        else if(boundary=="revoked")account.RejectSession(account.Generation);
        else new IosIdentityLifecycle(identity,account).Destroy();
        Assert.Equal(DeviceAiFailure.Authentication,Assert.Throws<DeviceAiException>(()=>ai.RequireCurrentAccount(proposal)).Kind);
        Assert.Equal("<p>Original</p>",prepared.BaseHtml);
    }
    private sealed class NoNetwork : HttpMessageHandler {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){Calls++;throw new InvalidOperationException("No network is allowed in this guest/offline fixture.");}
    }
    [Theory][InlineData("guest")][InlineData("unconfigured")][InlineData("offline")][InlineData("rejected")]
    public async Task ActualAccountMenuShowsIosGuestConfigurationOfflineAndRejectedStates(string state) {
        var options=state=="unconfigured"?new IosIdentityOptions{Authentication=new(),Environment=Options().Environment}:Options();
        var session=new Session();var identity=new IosDeviceIdentityClient(options,session,new Store());var account=new DeviceAccountService(identity);
        if(state is "offline" or "rejected")await account.SignInAsync();session.Offline=state=="offline";session.UiRequired=state=="rejected";
        var connectivity=new DeviceConnectivity();connectivity.SetOnline(false);using var transport=new NoNetwork();using var http=new HttpClient(transport){BaseAddress=new("https://api.example/")};
        using var services=new ServiceCollection().AddLogging().AddSingleton(account).AddSingleton(connectivity).AddSingleton(http).AddSingleton<DeviceAccountOverview>().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var html=await renderer.Dispatcher.InvokeAsync(async()=> (await renderer.RenderComponentAsync<DeviceAccountMenu>()).ToHtmlString());
        Assert.Contains("Local writing",html);Assert.DoesNotContain("synthetic-access-token",html);Assert.Equal(0,transport.Calls);
        if(state=="unconfigured"){Assert.Contains("Native sign-in is not configured",html);await account.SignOutAsync();Assert.Equal(0,session.Removals);}
        if(state=="rejected")Assert.False(account.IsSignedIn);
        if(state=="offline")Assert.Contains("Offline",html,StringComparison.OrdinalIgnoreCase);
        var root=System.Environment.GetEnvironmentVariable("WRITERAPP_P21_EVIDENCE");if(root is not null){Directory.CreateDirectory(Path.Combine(root,"p21"));await File.WriteAllTextAsync(Path.Combine(root,"p21","identity-"+state+".html"),html);}
    }
}
