using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Client.Pages;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class QualityTransport(TranslationFixture f):DelegatingHandler(f.Server.GetTestServer().CreateHandler()) {
        public string? Failure;public DocumentEditor? Editor;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            var response=await base.SendAsync(request,ct);
            if(!request.RequestUri!.AbsolutePath.EndsWith("quality-checks/run"))return response;
            if(Failure=="writing")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent writing"},ct);
            if(Failure=="planning") {await using var db=new AppDbContext(f.Options);(await db.ProjectNodes.SingleAsync(ct)).Title="Concurrent scene";await db.SaveChangesAsync(ct);}
            if(Failure=="account")TranslationSet(Editor!,"_webTranslationGeneration",1);
            if(Failure is "unchecked" or "foreign" or "malformed") {
                var result=(await response.Content.ReadFromJsonAsync<QualityCheckRunResultDto>(ct))!;response.Content.Dispose();
                response.Content=Failure=="malformed" ? new StringContent("{invalid") : JsonContent.Create(result with { WebSource=Failure=="unchecked"?null:result.WebSource! with { DocumentId=Guid.NewGuid() } });
            }
            return response;
        }
    }
    [Theory][InlineData(null)][InlineData("writing")][InlineData("planning")][InlineData("account")][InlineData("unchecked")][InlineData("foreign")][InlineData("malformed")]
    public async Task ActualWebQualityHandlerChecksDedicatedPageResultBeforeRetainingIssues(string? failure)
    {
        await using var f=new TranslationFixture { Configure=s=>s.AddScoped<IQualityCheckService,QualityCheckService>() };await f.Start();
        await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="<p>He was very very very tired.</p>"});
        using var provider=new CheckedProvider(f);using var editor=CheckedEditor(f,provider);using var transport=new QualityTransport(f){Failure=failure,Editor=editor};
        editor.Http.Dispose();editor.Http=new HttpClient(transport,false){BaseAddress=new("http://localhost/")};editor.Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        var page=(await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))!.Single(p=>p.Id==f.Pages[0]);TranslationSet(editor,"_activePage",page);
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new TranslationRenderer(services,services.GetRequiredService<ILoggerFactory>());await renderer.Attach(editor);
        await TranslationEvent(renderer,editor,"RunQualityChecksAsync");
        if(failure is null) {Assert.Null(TranslationField(editor,"_qualityError"));Assert.NotNull(TranslationField(editor,"_qualityCheckedSource"));Assert.All((IEnumerable<PageQualityIssueDto>)TranslationField(editor,"_qualityIssues")!,i=>Assert.Equal(f.Document,i.DocumentId));}
        else {Assert.NotNull(TranslationField(editor,"_qualityError"));Assert.Null(TranslationField(editor,"_qualityCheckedSource"));Assert.Empty((IEnumerable<PageQualityIssueDto>)TranslationField(editor,"_qualityIssues")!);}
    }
    [Theory][InlineData("stale")][InlineData("foreign")][InlineData("selection")][InlineData("size")]
    public async Task WebQualityServerRefusesInvalidCheckedSourcesBeforeIssuePersistence(string failure)
    {
        await using var f=new TranslationFixture { Configure=s=>s.AddScoped<IQualityCheckService,QualityCheckService>() };await f.Start();
        var source=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&pageId={f.Pages[0]}"))!;
        if(failure=="stale")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent"});
        if(failure=="foreign")source=source with { PageId=Guid.NewGuid() };
        var request=new QualityCheckRunRequest(failure=="selection"?"selection":"page",failure=="selection"?"Not in saved writing":failure=="size"?new string('x',100001):null,false,source);
        var response=await f.Http.PostAsJsonAsync($"api/pages/{f.Pages[0]}/quality-checks/run",request);
        Assert.False(response.IsSuccessStatusCode);await using var db=new AppDbContext(f.Options);Assert.Empty(db.PageQualityIssues);
    }
}
