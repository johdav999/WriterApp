using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.AI;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    private sealed class TranslationFixture : IAsyncDisposable
    {
        public readonly string Root=Path.Combine(Path.GetTempPath(),"WriterApp.WebTranslation",Guid.NewGuid().ToString("N"));
        public readonly Guid Document=Guid.NewGuid(), Project=Guid.NewGuid(), Scene=Guid.NewGuid();
        public readonly Guid[] Sections=[Guid.NewGuid(),Guid.NewGuid()];
        public readonly Guid[] Pages=[Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()];
        public readonly string[] Html=["<h2>Åsa 日本語 🧭</h2><p><strong>Opening</strong> paragraph.</p>",
            "<blockquote><p><em>Second</em> page.</p></blockquote>","<p></p>","<table><tbody><tr><td><p>Other section</p></td></tr></tbody></table>"];
        public IHost Server=null!;public HttpClient Http=null!;
        public Action<Microsoft.Extensions.DependencyInjection.IServiceCollection>? Configure;
        public DbContextOptions<AppDbContext> Options => new DbContextOptionsBuilder<AppDbContext>().UseSqlite($"Data Source={Path.Combine(Root,"web.db")};Pooling=False").Options;
        public string Endpoint => $"/api/documents/{Document}/structured-translations";
        public async Task Start() {
            Directory.CreateDirectory(Root);
            await using(var db=new AppDbContext(Options)) {
                await db.Database.EnsureCreatedAsync();
                db.Projects.Add(new(){Id=Project,OwnerUserId="ordinary-author",Title="Non-demo book",PrimaryDocumentId=Document});
                db.Documents.Add(new(){Id=Document,ProjectId=Project,OwnerUserId="ordinary-author",Title="Rich book",LanguageCode="sv"});
                for(int i=0;i<2;i++)db.Sections.Add(new(){Id=Sections[i],DocumentId=Document,Title=$"Section {i+1}",NarrativePurpose="Authored purpose",OrderIndex=i,LanguageCode="sv"});
                for(int i=0;i<4;i++)db.Pages.Add(new(){Id=Pages[i],DocumentId=Document,SectionId=Sections[i<3?0:1],Title=$"Page {i+1}",OrderIndex=i<3?i:0,Content=Html[i]});
                db.ProjectNodes.Add(new(){Id=Scene,ProjectId=Project,DocumentId=Document,LinkedSectionId=Sections[0],Title="Scene",MetadataJson="{\"notesText\":\"Keep 日本語\",\"tags\":[\"authored\"]}"});
                await db.SaveChangesAsync();
                foreach(var sql in DocumentSyncSchemaV1.Install(false))await db.Database.ExecuteSqlRawAsync(sql);
            }
            Restart();
        }
        public void Restart() {
            Http?.Dispose();Server?.Dispose();
            Server=Host($"Data Source={Path.Combine(Root,"web.db")};Pooling=False",Configure);Http=Server.GetTestClient();
            Http.DefaultRequestHeaders.Add("X-Test-Owner","ordinary-author");
        }
        public async Task<WebTranslationSource> Source(string scope="document") =>
            (await Http.GetFromJsonAsync<WebTranslationSource>($"{Endpoint}/source?scope={scope}&sectionId={Sections[0]}"))!;
        public static TranslationStructure Capture(WebTranslationSource source) => new(1,source.DocumentId,source.Scope,"en",
            source.Sections.Select(s => new TranslationSection(s.Id,s.Pages.Select(p => new TranslationPage(p.Id,Runs(p.Content))).ToArray())).ToArray());
        public static IReadOnlyList<TranslationRun> Runs(string html) {
            var body=new HtmlParser().ParseDocument(html).Body!;var texts=new List<string>();
            void Visit(INode node) { if(node is IText t && t.Data.Length>0)texts.Add(t.Data);foreach(var child in node.ChildNodes)Visit(child); }
            Visit(body);return texts.Select((t,i) => new TranslationRun("0."+i,t)).ToArray();
        }
        public static TranslationStructure Translate(TranslationStructure source) => source with { Sections=source.Sections.Select(s => s with {
            Pages=s.Pages.Select(p => p with { Runs=p.Runs.Select(r => r with {
                Text=string.Concat(r.Text.TakeWhile(char.IsWhiteSpace))+"EN "+r.Text.Trim()+string.Concat(r.Text.Reverse().TakeWhile(char.IsWhiteSpace).Reverse())
            }).ToArray() }).ToArray() }).ToArray() };
        public async Task<WebTranslationApproval> Approval(string scope="document",string mode="replace") {
            var source=await Source(scope);var original=Capture(source);var translated=Translate(original);var proposal=Guid.NewGuid();
            await SaveProposal(source,original,translated,proposal);
            return new(Guid.NewGuid(),proposal,source,original,translated,mode,"auto");
        }
        public async Task SaveProposal(WebTranslationSource source,TranslationStructure original,TranslationStructure translated,Guid proposal,WebAiSource? webSource=null) {
            await using var db=new AppDbContext(Options);
            db.AiActionHistoryEntries.Add(new(){Id=proposal,OwnerUserId="ordinary-author",DocumentId=Document,SectionId=Sections[0],ActionKey="translate."+source.Scope,
                CreatedAt=DateTimeOffset.UtcNow,RequestJson=JsonSerializer.Serialize(new AiActionExecuteRequestDto(Document,Sections[0],Pages[0],null,null,null,null,null,
                    new(){[TranslationStructures.Parameter]=TranslationStructures.Serialize(original),["source_language"]="auto"},source.DocumentVersion),new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                ResultJson=JsonSerializer.Serialize(new AiActionExecuteResponseDto(proposal,TranslationStructures.Serialize(original),TranslationStructures.Serialize(translated),null,
                    DateTimeOffset.UtcNow,"translate."+source.Scope,SourceDocumentVersion:source.DocumentVersion,WebSource:webSource),new JsonSerializerOptions(JsonSerializerDefaults.Web))});
            await db.SaveChangesAsync();
        }
        public async Task<WebTranslationReceipt> Approve(WebTranslationApproval approval) {
            var response=await Http.PostAsJsonAsync(Endpoint+"/approve",approval);
            Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<WebTranslationReceipt>())!;
        }
        public async Task<WebTranslationReceipt> Commit(Guid operation) {
            var response=await Http.PostAsync($"{Endpoint}/operations/{operation}/commit",null);
            Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<WebTranslationReceipt>())!;
        }
        public ValueTask DisposeAsync() {Http?.Dispose();Server?.Dispose();Directory.Delete(Root,true);return ValueTask.CompletedTask;}
    }

    [Theory]
    [InlineData("section","replace")]
    [InlineData("document","replace")]
    [InlineData("section","duplicate-section")]
    [InlineData("document","duplicate-document")]
    public async Task StructuredWebTranslationCommitsCompleteGraphReplaysAfterRestartAndRecoversOriginal(string scope,string mode)
    {
        await using var f=new TranslationFixture();await f.Start();var approval=await f.Approval(scope,mode);
        var before=approval.Source;var receipt=await f.Approve(approval);
        Assert.Equal("Approved",receipt.State);
        Assert.Equal(JsonSerializer.Serialize(before),JsonSerializer.Serialize(await f.Source(scope)));
        Assert.Equal(receipt,await f.Approve(approval));
        f.Restart(); // Durable approval survives before the content transaction starts.
        var committed=await f.Commit(approval.OperationId);
        Assert.Equal("Committed",committed.State);
        f.Restart(); // Lost acknowledgement/reload must return the same IDs without another copy.
        Assert.Equal(committed,await f.Commit(approval.OperationId));
        Assert.Equal(committed,Assert.Single((await f.Http.GetFromJsonAsync<WebTranslationReceipt[]>(f.Endpoint+"/operations"))!));
        await using(var db=new AppDbContext(f.Options)) {
            var originals=await db.Pages.Where(p => p.DocumentId==f.Document).ToListAsync();
            Assert.All(f.Pages,p => Assert.Contains(originals,x => x.Id==p));
            var translated=mode=="duplicate-document" ? await db.Pages.Where(p => p.DocumentId==committed.ResultDocumentId).ToListAsync()
                : mode=="duplicate-section" ? await db.Pages.Where(p => p.SectionId==committed.ResultSectionId).ToListAsync()
                : originals.Where(p => scope=="document" || p.SectionId==f.Sections[0]).ToList();
            Assert.Equal(scope=="section" ? 3 : 4,translated.Count);
            Assert.Contains(translated,p => p.Content=="<p></p>");
            Assert.Contains(translated,p => p.Content.Contains("<strong>EN Opening</strong>") && p.Content.Contains("Åsa 日本語 🧭"));
            Assert.Equal(mode=="duplicate-document" ? 2 : 1,await db.Documents.CountAsync());
            Assert.Equal(mode=="duplicate-section" ? 3 : mode=="duplicate-document" ? 4 : 2,await db.Sections.CountAsync());
            if(mode!="replace")Assert.All(originals.Where(p => f.Pages.Contains(p.Id)),p => Assert.Equal(f.Html[Array.IndexOf(f.Pages,p.Id)],p.Content));
            if(scope=="section" && mode=="replace")Assert.Equal(f.Html[3],originals.Single(p => p.Id==f.Pages[3]).Content);
            Assert.Equal(f.Document,(await db.Projects.SingleAsync()).PrimaryDocumentId);
            Assert.Equal("{\"notesText\":\"Keep 日本語\",\"tags\":[\"authored\"]}",(await db.ProjectNodes.SingleAsync(n => n.Id==f.Scene)).MetadataJson);
            var history=await new EfCoreAiActionHistoryStore(db).ListAsync("ordinary-author",f.Document,default);
            var applied=Assert.Single(history);Assert.True(applied.IsApplied);Assert.False(applied.CanCloudUndo);Assert.False(applied.CanCloudRedo);
            Assert.Empty(await db.AiActionAppliedEvents.ToListAsync());
        }
        var recover=await f.Http.PostAsync($"{f.Endpoint}/operations/{committed.OperationId}/original",null);Assert.Equal(HttpStatusCode.OK,recover.StatusCode);
        f.Restart();Assert.Equal(HttpStatusCode.OK,(await f.Http.PostAsync($"{f.Endpoint}/operations/{committed.OperationId}/original",null)).StatusCode);
        await using(var db=new AppDbContext(f.Options)) {
            var recovered=await db.Documents.SingleAsync(d => d.Id==committed.OriginalCopyId);
            Assert.Equal("sv",recovered.LanguageCode);
            var sections=await db.Sections.Where(s => s.DocumentId==recovered.Id).OrderBy(s => s.OrderIndex).ToListAsync();Assert.Equal(2,sections.Count);
            var pages=await db.Pages.Where(p => p.DocumentId==recovered.Id).ToListAsync();
            Assert.Equal(f.Html,sections.SelectMany(s => pages.Where(p => p.SectionId==s.Id).OrderBy(p => p.OrderIndex)).Select(p => p.Content));
            Assert.Equal(4,pages.Count);Assert.DoesNotContain(pages,p => f.Pages.Contains(p.Id));
            Assert.Equal("Authored purpose",sections[0].NarrativePurpose);
        }
    }

    [Theory]
    [InlineData("missing")][InlineData("reordered")][InlineData("duplicate")][InlineData("language")]
    [InlineData("foreign")][InlineData("source-text")][InlineData("metadata")][InlineData("stale")][InlineData("markup")]
    [InlineData("planning-owner")][InlineData("planning-size")]
    public async Task InvalidTranslationApprovalsDoNotMutateOrJournal(string failure) {
        await using var f=new TranslationFixture();await f.Start();var approval=await f.Approval();
        var changed=approval.Translated;
        if(failure=="missing")changed=changed with { Sections=changed.Sections.Take(1).ToArray() };
        if(failure=="reordered")changed=changed with { Sections=changed.Sections.Reverse().ToArray() };
        if(failure=="duplicate")changed=changed with { Sections=[changed.Sections[0],changed.Sections[0]] };
        if(failure=="language")changed=changed with { TargetLanguage="ja" };
        if(failure=="foreign")changed=changed with { DocumentId=Guid.NewGuid() };
        if(failure=="source-text")approval=approval with { Original=TranslationFixture.Translate(approval.Original) };
        if(failure=="metadata")approval=approval with { Source=approval.Source with { ProjectId=Guid.NewGuid() } };
        if(failure is "planning-owner" or "planning-size") {
            await using var db=new AppDbContext(f.Options);var node=await db.ProjectNodes.SingleAsync();
            if(failure=="planning-size")node.MetadataJson=new string('x',2_000_001);
            else {var foreign=Guid.NewGuid();db.Projects.Add(new(){Id=foreign,OwnerUserId="other-owner"});node.ProjectId=foreign;}
            await db.SaveChangesAsync();
        }
        if(failure is "stale" or "markup") {
            await using var db=new AppDbContext(f.Options);var page=await db.Pages.SingleAsync(p => p.Id==f.Pages[0]);
            page.Content=failure=="markup" ? "<p><img src='/keep.png'></p>" : "<p>Later writing</p>";await db.SaveChangesAsync();
        }
        approval=approval with { Translated=changed };
        var response=await f.Http.PostAsJsonAsync(f.Endpoint+"/approve",approval);
        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict,await response.Content.ReadAsStringAsync());
        await using var verify=new AppDbContext(f.Options);
        Assert.Empty(await verify.WebTranslationOperations.ToListAsync());
        Assert.Equal(4,await verify.Pages.CountAsync());Assert.Equal(2,await verify.Sections.CountAsync());
        Assert.Equal(f.Html[1],(await verify.Pages.SingleAsync(p => p.Id==f.Pages[1])).Content);
    }

    [Fact]
    public async Task ApprovedStaleSaveAndInjectedPartialFailureLeaveAtomicOriginalAndRetryableEvidence() {
        await using var f=new TranslationFixture();await f.Start();var approval=await f.Approval();await f.Approve(approval);
        // DDL cannot bind trigger identifiers; the only substitution is a generated Guid.
#pragma warning disable EF1003 // Controlled generated Guid in test-only DDL; SQLite cannot bind a trigger condition parameter.
        await using(var db=new AppDbContext(f.Options)) await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER FailTranslation BEFORE UPDATE ON Pages WHEN NEW.Id='"+f.Pages[1].ToString("D").ToUpperInvariant()+"' BEGIN SELECT RAISE(ABORT,'injected page failure'); END;");
#pragma warning restore EF1003
        var response=await f.Http.PostAsync($"{f.Endpoint}/operations/{approval.OperationId}/commit",null);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        await using(var db=new AppDbContext(f.Options)) {
            Assert.Equal(f.Html,(await db.Pages.OrderBy(p => p.Id).ToListAsync()).OrderBy(p => Array.IndexOf(f.Pages,p.Id)).Select(p => p.Content));
            Assert.Equal("Approved",JsonSerializer.Deserialize<WebTranslationReceipt>((await db.WebTranslationOperations.SingleAsync()).ReceiptJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!.State);
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER FailTranslation");
            var page=await db.Pages.SingleAsync(p => p.Id==f.Pages[0]);page.Content="<p>Later source</p>";await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict,(await f.Http.PostAsync($"{f.Endpoint}/operations/{approval.OperationId}/commit",null)).StatusCode);
        Assert.Single((await f.Http.GetFromJsonAsync<WebTranslationReceipt[]>(f.Endpoint+"/operations"))!);
    }

    [Fact]
    public async Task DeletedOriginalRecoveryIdentityCannotBeReseededByRetry() {
        await using var f=new TranslationFixture();await f.Start();var approval=await f.Approval();var receipt=await f.Approve(approval);
        await using(var db=new AppDbContext(f.Options)) {
            var sequence=await db.DocumentSyncRecords.MaxAsync(x => x.Sequence);
            db.DocumentSyncRecords.Add(new(){DocumentId=receipt.OriginalCopyId,OwnerUserId="ordinary-author",Version="deleted",Sequence=sequence+1,IsDeleted=true});
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict,(await f.Http.PostAsync($"{f.Endpoint}/operations/{receipt.OperationId}/original",null)).StatusCode);
        await using var verify=new AppDbContext(f.Options);Assert.Single(await verify.Documents.ToListAsync());
        Assert.Single(await verify.WebTranslationOperations.ToListAsync());Assert.Equal(4,await verify.Pages.CountAsync());
    }

    [Fact]
    public async Task OperationsAreOwnedAndProposalCannotBeApprovedTwiceWithNewIdentity() {
        await using var f=new TranslationFixture();await f.Start();var approval=await f.Approval();await f.Approve(approval);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Http.PostAsJsonAsync(f.Endpoint+"/approve",approval with { OperationId=Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await f.Http.PostAsJsonAsync(f.Endpoint+"/approve",approval with { Mode="duplicate-document" })).StatusCode);
        f.Http.DefaultRequestHeaders.Remove("X-Test-Owner");
        Assert.Equal(HttpStatusCode.Unauthorized,(await f.Http.GetAsync(f.Endpoint+"/operations")).StatusCode);
        f.Http.DefaultRequestHeaders.Add("X-Test-Owner","other-owner");
        Assert.Equal(HttpStatusCode.NotFound,(await f.Http.GetAsync(f.Endpoint+"/operations")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.Http.PostAsync($"{f.Endpoint}/operations/{approval.OperationId}/commit",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await f.Http.PostAsync($"{f.Endpoint}/operations/{approval.OperationId}/original",null)).StatusCode);
    }
}
