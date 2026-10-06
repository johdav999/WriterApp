using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WriterApp.Client.Services;
using WriterApp.Data;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed partial class DesktopAiWebPersistenceTests
{
    [Theory][InlineData("current")][InlineData("ack")][InlineData("stale")][InlineData("owner")][InlineData("target")]
    public async Task CheckedPageSaveConfirmsItsTransactionalSourceAndRejectsConcurrentOrForeignWrites(string scenario)
    {
        await using var f=new TranslationFixture();await f.Start();
        var source=(await f.Http.GetFromJsonAsync<WebAiSource>($"api/ai/actions/web-source/{f.Document}?sectionId={f.Sections[0]}&pageId={f.Pages[0]}"))!;
        if(scenario=="stale")await f.Http.PutAsJsonAsync($"api/pages/{f.Pages[0]}",new{content="Concurrent writing"});
        if(scenario=="ack") { await using var db=new AppDbContext(f.Options);(await db.DocumentSyncRecords.SingleAsync(r=>r.DocumentId==f.Document)).Version="metadata-ack";await db.SaveChangesAsync(); }
        if(scenario=="owner"){f.Http.DefaultRequestHeaders.Remove("X-Test-Owner");f.Http.DefaultRequestHeaders.Add("X-Test-Owner","other-owner");}
        using var request=new HttpRequestMessage(HttpMethod.Put,$"api/pages/{(scenario=="target"?f.Pages[1]:f.Pages[0])}"){Content=JsonContent.Create(new{content="<p><em>Explicit approved prose</em></p>"})};
        request.Headers.Add("X-WriterApp-AI-Source",JsonSerializer.Serialize(source));using var response=await f.Http.SendAsync(request);
        await using var verify=new AppDbContext(f.Options);
        if(scenario is "current" or "ack") {Assert.Equal(HttpStatusCode.OK,response.StatusCode);WebCheckedAi.RequireMutationReceipt(response);Assert.Contains("Explicit approved prose",(await verify.Pages.SingleAsync(p=>p.Id==f.Pages[0])).Content);}
        else {Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);Assert.All(await verify.Pages.ToListAsync(),p=>Assert.DoesNotContain("Explicit approved prose",p.Content));}
        Assert.Equal(f.Html[3],(await verify.Pages.SingleAsync(p=>p.Id==f.Pages[3])).Content);
    }
    [Fact]
    public void UncheckedSaveAcknowledgementCannotClaimCheckedPersistence()
    {
        using var response=new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{content="Prose"})};
        Assert.Throws<InvalidDataException>(()=>WebCheckedAi.RequireMutationReceipt(response));
    }
    [Fact]
    public async Task StoryboardApprovalCannotBorrowTheSourceOrExpiryOfAnotherAnalysis()
    {
        await using var f=new TranslationFixture();await f.Start();using var provider=new CheckedProvider(f);using var http=new HttpClient(provider){BaseAddress=new("http://localhost/")};http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        var data=new HttpStoryboardData(http);var first=await data.ExecuteAiAsync(f.Project,"storyboard.suggest-next-scene",new(f.Document,f.Sections[0],null,null,null,null,null,null,new()),f.Document);
        var receipts=(Dictionary<Guid,(WebCheckedAi.Lease Lease,DateTimeOffset Created)>)typeof(HttpStoryboardData).GetField("_suggestions",TranslationPrivate)!.GetValue(data)!;
        receipts[first.ProposalId]=(receipts[first.ProposalId].Lease,DateTimeOffset.UtcNow.AddHours(-2));
        await data.ExecuteAiAsync(f.Project,"storyboard.analyze-pov-balance",new(f.Document,f.Sections[0],null,null,null,null,null,null,new()),f.Document);
        await Assert.ThrowsAsync<InvalidDataException>(()=>data.ValidateSuggestionAsync(f.Project,f.Document,first.ProposalId));
        await using var verify=new AppDbContext(f.Options);Assert.Single(await verify.ProjectNodes.ToListAsync());
    }
}
