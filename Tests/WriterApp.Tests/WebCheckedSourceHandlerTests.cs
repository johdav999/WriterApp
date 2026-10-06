using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class CheckedProvider(TranslationFixture f) : DelegatingHandler(f.Server.GetTestServer().CreateHandler())
    {
        public int Calls;public string? Failure;public Func<Task>? After;public AiActionExecuteRequestDto? Request;public Guid Proposal;
        public int ReferenceRefreshes;
        public string? ReferenceFailure;
        public string? ConsistencyOutput;
        public Func<int, string, string>? OutputForCall;
        public int TokensUsed;
        public SectionSceneCardProposalDto? SceneProposal;
        public bool SceneSaveFailure;
        public Func<Task>? BeforeSceneSave;
        public int SceneSaves;
        public bool ResolveScene;
        public string? LastHttpError;
        private readonly HashSet<WriterApp.Shared.Canon.CanonKind> _preparedReferences = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message,CancellationToken ct)
        {
            string path=message.RequestUri!.AbsolutePath;
            if (ResolveScene && path.EndsWith("/open-scene"))return new(HttpStatusCode.OK){Content=JsonContent.Create(new ProjectSceneOpenTargetDto(f.Project,f.Scene,f.Document,f.Sections[0]))};
            if(message.Method==HttpMethod.Put && path.EndsWith("/scene-card")) {
                SceneSaves++;
                if(BeforeSceneSave is not null)await BeforeSceneSave();
                if(SceneSaveFailure)return new(HttpStatusCode.ServiceUnavailable);
            }
            if(path=="/api/auth/me")return new(HttpStatusCode.OK){Content=JsonContent.Create(new{isAuthenticated=true,userId="ordinary-author",planKey="professional",effectivePlanKey="professional",subscriptionStatus="active",isPaidAccessActive=true,aiMonthlyTokenBudget=1000,aiTokensUsedThisPeriod=TokensUsed})};
            if(path.Contains("/writing-outline/")) {
                var source=await f.Source("section");
                return new(HttpStatusCode.OK){Content=JsonContent.Create(WritingOutline.Create(f.Document,f.Project,source.DocumentVersion,source.Sections.Select(s=>new WritingOutlineSection(s.Id,s.OrderIndex,s.Title)),[]))};
            }
            if(path.Contains("/web-source/") && Failure=="capability")return new(HttpStatusCode.NotFound);
            if(path.Contains("/bibles/") && path.EndsWith("/device")) {
                var type=path.Split('/')[5];var kind=Enum.Parse<WriterApp.Shared.Canon.CanonKind>(type,true);
                string version=Uri.UnescapeDataString(message.RequestUri.Query.Split('=')[1]);
                bool ready = _preparedReferences.Contains(kind);
                return new(HttpStatusCode.OK){Content=JsonContent.Create(new WriterApp.Shared.Canon.DeviceBibleSnapshot(1,f.Document,kind,ready ? "ready" : "missing",ready ? version : null,version,"",null,$"{{\"schemaVersion\":\"1.0\",\"{WriterApp.Shared.Canon.CanonContent.Collection(kind)}\":[]}}",0,ready))};
            }
            if (path.Contains("/bibles/") && path.EndsWith("/device/refresh")) {
                ReferenceRefreshes++;
                if (ReferenceFailure == "unavailable") return new(HttpStatusCode.ServiceUnavailable);
                if (ReferenceFailure == "stale") return new(HttpStatusCode.Conflict);
                var request = (await message.Content!.ReadFromJsonAsync<WriterApp.Shared.Canon.DeviceBibleRefreshRequest>(ct))!;
                var kind = Enum.Parse<WriterApp.Shared.Canon.CanonKind>(path.Split('/')[5], true);
                _preparedReferences.Add(kind);
                return new(HttpStatusCode.OK) { Content = JsonContent.Create(new WriterApp.Shared.Canon.DeviceBibleSnapshot(1, f.Document, kind, "ready",
                    request.ExpectedDocumentVersion, request.ExpectedDocumentVersion, "", DateTimeOffset.UtcNow,
                    $"{{\"schemaVersion\":\"1.0\",\"{WriterApp.Shared.Canon.CanonContent.Collection(kind)}\":[]}}", 0, true)) };
            }
            if(path.Contains("/history") && !path.Contains("/history/web"))return new(HttpStatusCode.OK){Content=JsonContent.Create(Array.Empty<AiActionHistoryEntryDto>())};
            if(!path.EndsWith("/execute")) {
                var returned=await base.SendAsync(message,ct);
                if(!returned.IsSuccessStatusCode)LastHttpError=path+" "+returned.StatusCode+" "+await returned.Content.ReadAsStringAsync(ct);
                return returned;
            }
            Calls++;Request=(await message.Content!.ReadFromJsonAsync<AiActionExecuteRequestDto>(ct))!;
            string key=path.Split('/')[4],output="Reviewed selection prose.";
            Proposal=Guid.NewGuid();
            if(Request.Parameters?.TryGetValue(WritingActions.Parameter,out var json)==true) {
                var structure=WritingActions.Parse(json!.ToString()!);
                output=WritingActions.Serialize(structure with { Pages=structure.Pages.Select(p=>p with { Runs=p.Runs.Select(r=>r with { Text=string.IsNullOrWhiteSpace(r.Text)?r.Text:string.Concat(r.Text.TakeWhile(char.IsWhiteSpace))+"Reviewed "+r.Text.Trim()+string.Concat(r.Text.Reverse().TakeWhile(char.IsWhiteSpace).Reverse()) }).ToArray() }).ToArray() });
            }
            var result=new AiActionExecuteResponseDto(Proposal,null,Failure=="malformed"?"{}":output,null,
                Failure=="expired"?DateTimeOffset.UtcNow.AddHours(-2):DateTimeOffset.UtcNow,key,SourceDocumentVersion:Request.ExpectedDocumentVersion,
                SourceOutlineFingerprint:Request.WritingOutline?.Fingerprint,WebSource:Failure=="unchecked"?null:Failure=="foreign"?Request.WebSource! with { DocumentId=Guid.NewGuid() }:Request.WebSource);
            if(key.StartsWith("scene.",StringComparison.Ordinal))result=result with { ProposedSceneCard=SceneProposal ?? new SectionSceneCardProposalDto(null,null,null,null,Summary:"Reviewed summary",Status:null) };
            if(key=="continuity.check_section")result=result with { ProposedText=ConsistencyOutput ?? "{\"schemaVersion\":\"1.0\",\"issues\":[]}" };
            if (OutputForCall is not null) result = result with { ProposedText = OutputForCall(Calls, result.ProposedText ?? "") };
            if(Request.WebSource is not null) {
                await using var db=new AppDbContext(f.Options);
                db.AiActionHistoryEntries.Add(new(){Id=Proposal,OwnerUserId="ordinary-author",DocumentId=f.Document,SectionId=f.Sections[0],ActionKey=key,
                    CreatedAt=DateTimeOffset.UtcNow,RequestJson=JsonSerializer.Serialize(Request,new JsonSerializerOptions(JsonSerializerDefaults.Web)),ResultJson=JsonSerializer.Serialize(result,new JsonSerializerOptions(JsonSerializerDefaults.Web))});
                await db.SaveChangesAsync();
            }
            if(After is not null)await After();
            return new(HttpStatusCode.OK){Content=JsonContent.Create(result)};
        }
    }
    private static TranslationEditor CheckedEditor(TranslationFixture f,CheckedProvider provider) {
        var editor=new TranslationEditor { Http=new HttpClient(provider,false){BaseAddress=new("http://localhost/")},JSRuntime=new TranslationJs(),AiCommandStatusService=new(),Navigation=new TranslationNavigation(),Logger=Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentEditor>.Instance };
        editor.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        typeof(DocumentEditor).GetProperty(nameof(DocumentEditor.DocumentId))!.SetValue(editor,f.Document);
        TranslationSet(editor,"_activeSection",new SectionDto(f.Sections[0],f.Document,"Section 1","Authored purpose",0,default,default,"sv",null));return editor;
    }
    private static Task<HttpResponseMessage> CheckedPost(DocumentEditor editor,string key,AiActionExecuteRequestDto request,CancellationToken ct=default)=>
        (Task<HttpResponseMessage>)typeof(DocumentEditor).GetMethod("PostAiActionAsync",TranslationPrivate)!.Invoke(editor,[key,request,false,null,ct])!;
    [Theory][InlineData("expand.section")][InlineData("tighten.section")][InlineData("change_tone.section")][InlineData("show_dont_tell.section")][InlineData("custom_transform")]
    public async Task ActualWebCheckedSectionWritingCommitsAllMappedPagesAndReopensRecovery(string key)
    {
        await using var f=new TranslationFixture();await f.Start();using var provider=new CheckedProvider(f);using var editor=CheckedEditor(f,provider);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        var parameters=new Dictionary<string,object?>();
        if(key=="custom_transform") { var preset=new PromptDefinition("Custom",null,"custom",null,"Revise {context}",new(),WritingScope.Section);parameters=ReusablePrompts.ExecutionParameters(preset);parameters[ReusablePrompts.Parameter]=ReusablePrompts.Serialize(preset); }
        using var response=await renderer.Dispatcher.InvokeAsync(()=>CheckedPost(editor,key,new(f.Document,f.Sections[0],f.Pages[0],null,null,null,null,null,parameters)));
        var result=(await response.Content.ReadFromJsonAsync<AiActionExecuteResponseDto>())!;
        Assert.NotNull(provider.Request!.WebSource);Assert.NotNull(provider.Request.ExpectedDocumentVersion);Assert.Contains("Reviewed",result.ProposedText);Assert.DoesNotContain("\"pages\"",result.ProposedText);
        var pending=Activator.CreateInstance(typeof(DocumentEditor).GetNestedType("PendingAiProposal",BindingFlags.NonPublic)!,[result.ProposalId,key,"Section revision",result.OriginalText,result.ProposedText,null,null,result.CreatedUtc,null])!;
        TranslationSet(editor,"_pendingAiProposal",pending);
        await TranslationEvent(renderer,editor,"OnApplyPendingAiProposal");
        Assert.Null(TranslationField(editor,"_webTranslationError"));Assert.Equal("Committed",Assert.Single((IEnumerable<WebTranslationReceipt>)TranslationField(editor,"_webTranslationReceipts")!).State);
        f.Restart();var pages=await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages");
        Assert.Equal(3,pages!.Length);Assert.Contains("<strong>Reviewed Opening</strong>",pages[0].Content);Assert.Contains("<em>Reviewed Second</em>",pages[1].Content);Assert.Equal(f.Html[3],(await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[1]}/pages"))![0].Content);
        Assert.All((await f.Http.GetFromJsonAsync<SectionDto[]>($"api/documents/{f.Document}/sections"))!,s=>Assert.Equal("sv",s.LanguageCode));
        var receipt=Assert.Single((IEnumerable<WebTranslationReceipt>)TranslationField(editor,"_webTranslationReceipts")!);
        Assert.Equal(HttpStatusCode.OK,(await f.Http.PostAsync($"{f.Endpoint}/operations/{receipt.OperationId}/original",null)).StatusCode);
        var originals=await f.Http.GetFromJsonAsync<SectionDto[]>($"api/documents/{receipt.OriginalCopyId}/sections");
        Assert.Equal(2,originals!.Length);
    }
    [Theory][InlineData("capability")][InlineData("unchecked")][InlineData("expired")][InlineData("foreign")][InlineData("writing")][InlineData("planning")][InlineData("account")][InlineData("cancel")][InlineData("malformed")]
    public async Task ActualWebCheckedWritingCannotRetainInvalidOrLateSource(string failure)
    {
        await using var f=new TranslationFixture();await f.Start();using var provider=new CheckedProvider(f){Failure=failure};using var editor=CheckedEditor(f,provider);using var cancel=new CancellationTokenSource();
        provider.After=async ()=> {
            if(failure=="writing")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent writing"});
            if(failure=="planning") { await using var db=new AppDbContext(f.Options);(await db.ProjectNodes.SingleAsync()).Title="Concurrent planning";await db.SaveChangesAsync(); }
            if(failure=="account")TranslationSet(editor,"_webTranslationGeneration",1);
            if(failure=="cancel")cancel.Cancel();
        };
        string key=failure=="malformed"?"expand.section":"rewrite.selection";
        await Assert.ThrowsAnyAsync<Exception>(()=>CheckedPost(editor,key,new(f.Document,f.Sections[0],f.Pages[0],key.EndsWith("selection")?0:null,key.EndsWith("selection")?4:null,"Åsa",null,null,new()),cancel.Token));
        Assert.Empty((System.Collections.IDictionary)TranslationField(editor,"_checkedProposals")!);Assert.Equal(failure=="capability"?0:1,provider.Calls);
    }
    [Theory][InlineData("storyboard.suggest-next-scene")][InlineData("storyboard.detect-missing-scenes")][InlineData("storyboard.check-subplot-continuity")][InlineData("storyboard.analyze-pov-balance")]
    public async Task ActualWebStoryboardAdapterRetainsFourActionsAndRejectsChangedPlanningBeforeCreate(string key)
    {
        await using var f=new TranslationFixture();await f.Start();using var provider=new CheckedProvider(f);using var http=new HttpClient(provider){BaseAddress=new("http://localhost/")};http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        var data=new HttpStoryboardData(http);var result=await data.ExecuteAiAsync(f.Project,key,new(f.Document,f.Sections[0],null,null,null,null,null,null,new()),f.Document);
        Assert.Equal(key,result.ActionKey);Assert.NotNull(result.WebSource);await data.ValidateSuggestionAsync(f.Project,f.Document);
        await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent source"});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>data.ValidateSuggestionAsync(f.Project,f.Document));
    }
    [Theory][InlineData("writing")][InlineData("planning")][InlineData("owner")][InlineData("target")]
    public async Task CheckedPlanningMutationRejectsConcurrentOrForeignApprovalInsideItsTransaction(string change)
    {
        await using var f=new TranslationFixture{Configure=ConfigureCheckedSynopsis};await f.Start();
        var source=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}"))!;
        if(change=="writing")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent"});
        if(change=="planning") { await using var db=new AppDbContext(f.Options);(await db.ProjectNodes.SingleAsync()).MetadataJson="{}";await db.SaveChangesAsync(); }
        using var request=new HttpRequestMessage(HttpMethod.Put,$"api/documents/{(change=="target"?Guid.NewGuid():f.Document)}/synopsis") { Content=JsonContent.Create(new WriterApp.Application.Synopsis.DocumentSynopsisDto(f.Document,"Approved", "","","","","","","","","",DateTimeOffset.UtcNow)) };
        request.Headers.Add("X-WriterApp-AI-Source",JsonSerializer.Serialize(source));
        if(change=="owner") { f.Http.DefaultRequestHeaders.Remove("X-Test-Owner");f.Http.DefaultRequestHeaders.Add("X-Test-Owner","other-owner"); }
        var response=await f.Http.SendAsync(request);
        Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        await using var verify=new AppDbContext(f.Options);Assert.Empty(verify.DocumentSynopses);
    }
}
