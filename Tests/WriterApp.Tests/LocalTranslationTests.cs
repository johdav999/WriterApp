using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalTranslationTests
{
    [Fact]
    public async Task WholeScopePreflightRejectsOmittedPagesOversizedRunsAndAnnotatedSectionsBeforeGeneration()
    {
        using var f=new TranslationTestFixture(); var d=await f.Create(); var prepared=f.Prepare(d,"document");
        var captured=d.Sections.SelectMany(s => s.Pages).ToDictionary(p => p.PageId,p => new TranslationPageCapture([new("0.0","Text")]));
        captured.Remove(d.Sections[0].Pages[0].PageId);
        Assert.Throws<InvalidDataException>(() => LocalTranslation.Prepare(d,d.Sections[0].SectionId,"document","en",captured));
        captured[d.Sections[0].Pages[0].PageId]=new([new("0.0",new string('x',20001))]);
        Assert.Throws<InvalidDataException>(() => LocalTranslation.Prepare(d,d.Sections[0].SectionId,"document","en",captured));
        var node=d.Project!.Nodes.Single(n => n.NodeType=="scene");
        var annotation=new WriterApp.Shared.Sync.SyncSceneAnnotation(Guid.NewGuid(),"comment","open",0,5,"Quote","Source","",DateTimeOffset.UtcNow,null);
        var annotated=d with { Project=d.Project with { Nodes=d.Project.Nodes.Select(n => n.NodeId==node.NodeId ? n with { Annotations=[new(Guid.NewGuid(),null,annotation)] } : n).ToArray() } };
        Assert.Throws<InvalidOperationException>(() => LocalTranslation.Prepare(annotated,d.Sections[0].SectionId,"section","en",
            d.Sections[0].Pages.ToDictionary(p=>p.PageId,p=>new TranslationPageCapture([new("0.0","Text")]))));
        Assert.Equal(0,f.Api.Calls);
    }
    [Fact]
    public async Task VersionOneHistoryStillLoadsAndCopyRecoveryCannotTargetOriginalIdentity()
    {
        using var f=new TranslationTestFixture(); var d=await f.Create();
        var legacy=new LocalAiHistory(1,Guid.NewGuid(),d.DocumentId,"rewrite.selection","Manuscript replace",d.LocalRevision,d.ServerVersion,
            DateTimeOffset.UtcNow,"Reviewed",d,"New prose",d.Sections[0].Pages[0].PageId);
        await f.History.SaveHistoryAsync(legacy); Assert.Equal(1,Assert.Single(await f.History.HistoryAsync(d.DocumentId)).Version);
        var malformed=legacy with { Version=2,Id=Guid.NewGuid(),Action="translate.document",Target="Translation:duplicate-document",After=d };
        await Assert.ThrowsAsync<InvalidDataException>(() => f.History.SaveHistoryAsync(malformed));
        Assert.Single(await f.History.HistoryAsync(d.DocumentId));
    }
    [Theory]
    [InlineData("section", "replace")]
    [InlineData("document", "replace")]
    [InlineData("section", "duplicate-section")]
    [InlineData("document", "duplicate-document")]
    public async Task CompleteScopeApplyRestartUndoRedoPreservesOriginalsAndPlanning(string scope, string mode)
    {
        using var f = new TranslationTestFixture(); var before = await f.Create();
        var prepared = f.Prepare(before, scope); var proposal = await f.Ai.ProposeAsync(prepared.Request, default);
        var after = LocalTranslation.Preview(prepared, f.Html(before, scope), mode);
        var entry = f.Entry(before, after, proposal, mode); await f.History.SaveHistoryAsync(entry);
        Assert.Equal(LocalDocumentCodec.Encode(before), LocalDocumentCodec.Encode((await f.Repository.LoadAsync(before.DocumentId))!));
        await new LocalTranslationActions(f.Repository, f.History, f.Ai).ApplyAsync(entry, proposal);
        var saved = (await f.Repository.LoadAsync(after.DocumentId))!;
        Assert.Equal(LocalTranslation.Signature(after), LocalTranslation.Signature(saved));
        Assert.Equal("Applied", Assert.Single(await f.History.HistoryAsync(before.DocumentId)).Status);
        var restarted = new LocalDocumentRepository(new FileLocalDocumentStore(f.Root)); var history = new LocalAiStore(f.Root + "/ai");
        var actions = new LocalAiHistoryActions(restarted, history);
        await actions.ChangeAsync(before.DocumentId, entry.Id, false);
        var undone = (await restarted.LoadAsync(after.DocumentId))!;
        if (mode == "duplicate-document") {
            Assert.NotNull(undone.DeletedAtUtc);
            Assert.Equal(LocalTranslation.Signature(before), LocalTranslation.Signature((await restarted.LoadAsync(before.DocumentId))!));
            Assert.NotEqual(before.Project!.ProjectId, saved.Project!.ProjectId);
            Assert.Equal(saved.DocumentId, saved.Project.ManuscriptId);
            Assert.Equal(saved.Sections[0].SectionId, saved.Project.Nodes.Single(n => n.NodeType == "scene").SectionId);
        } else Assert.Equal(LocalTranslation.Signature(before), LocalTranslation.Signature(undone));
        await actions.ChangeAsync(before.DocumentId, entry.Id, true);
        Assert.Null((await restarted.LoadAsync(after.DocumentId))!.DeletedAtUtc);
        Assert.Equal(LocalTranslation.Signature(after), LocalTranslation.Signature((await restarted.LoadAsync(after.DocumentId))!));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedAggregateSaveOrHistoryConfirmationResumesWithoutPartialOverwrites(bool committed)
    {
        using var f = new TranslationTestFixture(); var before = await f.Create(); var p = f.Prepare(before, "document");
        var proposal = await f.Ai.ProposeAsync(p.Request, default); var after = LocalTranslation.Preview(p, f.Html(before,"document"),"replace");
        var entry = f.Entry(before, after, proposal, "replace") with { Status = "Applying" }; await f.History.SaveHistoryAsync(entry);
        if (committed) await f.Repository.SaveAsync(after);
        else {
            var broken = new LocalDocumentRepository(new FileLocalDocumentStore(f.Root, TimeProvider.System, new AtomicDocumentWriter(_ => throw new IOException("Injected after staging"))));
            await Assert.ThrowsAsync<IOException>(() => broken.SaveAsync(after));
            Assert.Equal(LocalTranslation.Signature(before), LocalTranslation.Signature((await f.Repository.LoadAsync(before.DocumentId))!));
        }
        await LocalTranslationActions.ResumeAsync(f.Repository, f.History, entry, default);
        Assert.Equal(LocalTranslation.Signature(after), LocalTranslation.Signature((await f.Repository.LoadAsync(before.DocumentId))!));
        Assert.Equal("Applied", Assert.Single(await f.History.HistoryAsync(before.DocumentId)).Status);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedCopyCreationUsesSavedIdentitiesAndNeverCreatesDuplicateCopies(bool committed)
    {
        using var f = new TranslationTestFixture(); var before = await f.Create(); var p = f.Prepare(before,"document");
        var proposal = await f.Ai.ProposeAsync(p.Request, default); var after = LocalTranslation.Preview(p, f.Html(before,"document"),"duplicate-document");
        var entry = f.Entry(before,after,proposal,"duplicate-document") with { Status = "Applying" }; await f.History.SaveHistoryAsync(entry);
        if (committed) await f.Repository.CreateTranslationCopyAsync(after);
        await LocalTranslationActions.ResumeAsync(f.Repository,f.History,entry,default);
        await f.Repository.CreateTranslationCopyAsync(after);
        Assert.Equal(2,(await f.Repository.ListAsync()).Documents.Count);
        var copy = (await f.Repository.LoadAsync(after.DocumentId))!;
        copy = await f.Repository.SaveAsync(copy with { Title = "Later copy edits" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Repository.CreateTranslationCopyAsync(after));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LocalAiHistoryActions(f.Repository,f.History).ChangeAsync(before.DocumentId,entry.Id,false));
        Assert.Equal("Later copy edits",(await f.Repository.LoadAsync(after.DocumentId))!.Title);
    }
    [Theory]
    [InlineData("missing-section")]
    [InlineData("duplicate-section")]
    [InlineData("missing-page")]
    [InlineData("duplicate-page")]
    [InlineData("missing-run")]
    [InlineData("duplicate-run")]
    [InlineData("language")]
    [InlineData("document")]
    [InlineData("reorder")]
    [InlineData("newline")]
    [InlineData("unicode")]
    [InlineData("spacing")]
    public async Task PartialWrongOrDuplicateMarkersAreRejected(string defect)
    {
        using var f = new TranslationTestFixture(); var before = await f.Create(); var source = f.Prepare(before,"document").Structure;
        var sections = source.Sections.ToArray(); var pages = sections[0].Pages.ToArray(); var runs = pages[0].Runs.ToArray(); var candidate = source;
        switch (defect) {
            case "missing-section": candidate = source with { Sections = sections.Skip(1).ToArray() }; break;
            case "duplicate-section": candidate = source with { Sections = [sections[0],sections[0]] }; break;
            case "missing-page": sections[0] = sections[0] with { Pages = pages.Skip(1).ToArray() }; break;
            case "duplicate-page": sections[0] = sections[0] with { Pages = [pages[0],pages[0]] }; break;
            case "missing-run": pages[0] = pages[0] with { Runs = [] }; break;
            case "duplicate-run": pages[0] = pages[0] with { Runs = [runs[0],runs[0]] }; break;
            case "language": candidate = source with { TargetLanguage = "fr" }; break;
            case "document": candidate = source with { DocumentId = Guid.NewGuid() }; break;
            case "reorder": candidate = source with { Sections = sections.Reverse().ToArray() }; break;
            case "newline": pages[0] = pages[0] with { Runs = [runs[0] with { Text = "New\nblock" }] }; break;
            case "unicode": pages[0] = pages[0] with { Runs = [runs[0] with { Text = "Broken \uD800" }] }; break;
            case "spacing": pages[0] = pages[0] with { Runs = [runs[0] with { Text = runs[0].Text + " " }] }; break;
        }
        if (defect is "missing-run" or "duplicate-run" or "newline" or "unicode" or "spacing") sections[0] = sections[0] with { Pages = pages };
        if (defect is "missing-page" or "duplicate-page" or "missing-run" or "duplicate-run" or "newline" or "unicode" or "spacing") candidate = source with { Sections = sections };
        Assert.ThrowsAny<Exception>(() => TranslationStructures.Result(TranslationStructures.Serialize(candidate),source));
        Assert.Throws<InvalidDataException>(() => TranslationStructures.Parse(TranslationStructures.Serialize(source).Replace("\"version\":1", "\"version\":1,\"version\":1")));
    }
    [Theory]
    [InlineData("stale")]
    [InlineData("account")]
    [InlineData("cancel")]
    [InlineData("target")]
    public async Task ApplyGuardsPreventMutationAndLeaveReviewedEvidence(string failure)
    {
        using var f = new TranslationTestFixture(); var before = await f.Create(); var p = f.Prepare(before,"document");
        var proposal = await f.Ai.ProposeAsync(p.Request,default); var entry = f.Entry(before,LocalTranslation.Preview(p,f.Html(before,"document"),"replace"),proposal,"replace");
        await f.History.SaveHistoryAsync(entry);
        if (failure == "stale") await f.Repository.SaveAsync(before with { Title = "Later title" });
        if (failure == "account") await f.Account.SignOutAsync();
        if (failure == "target") proposal = proposal with { Prepared = proposal.Prepared with {
            Request = proposal.Prepared.Request with { DocumentId = Guid.NewGuid() } } };
        var current = (await f.Repository.LoadAsync(before.DocumentId))!;
        await Assert.ThrowsAnyAsync<Exception>(() => new LocalTranslationActions(f.Repository,f.History,f.Ai).ApplyAsync(entry,proposal,
            failure == "cancel" ? new CancellationToken(true) : default));
        Assert.Equal(LocalDocumentCodec.Encode(current),LocalDocumentCodec.Encode((await f.Repository.LoadAsync(before.DocumentId))!));
        Assert.Equal("Reviewed",Assert.Single(await f.History.HistoryAsync(before.DocumentId)).Status);
    }
    [Fact]
    public async Task ScopeUndoPreservesLaterOtherPageAndPlanningEditsButRejectsChangedTranslatedPage()
    {
        using var f = new TranslationTestFixture(); var before = await f.Create(); var p = f.Prepare(before,"section");
        var proposal = await f.Ai.ProposeAsync(p.Request,default); var entry = f.Entry(before,LocalTranslation.Preview(p,f.Html(before,"section"),"replace"),proposal,"replace");
        await f.History.SaveHistoryAsync(entry); await new LocalTranslationActions(f.Repository,f.History,f.Ai).ApplyAsync(entry,proposal);
        var current = (await f.Repository.LoadAsync(before.DocumentId))!;
        current = await f.Repository.SaveAsync(current with { Sections = current.Sections.Select((s,index) => index == 1 ? s with { Pages = s.Pages.Select(p => p with { Content = "<p>Later other page</p>" }).ToArray() } : s).ToArray(),
            Project = current.Project! with { Synopsis = new(Stakes:"Later planning") } });
        var history = new LocalAiHistoryActions(f.Repository,f.History); await history.ChangeAsync(before.DocumentId,entry.Id,false);
        var undone = (await f.Repository.LoadAsync(before.DocumentId))!;
        Assert.Equal("<p>Later other page</p>",undone.Sections[1].Pages[0].Content); Assert.Equal("Later planning",undone.Project!.Synopsis!.Stakes);
        await history.ChangeAsync(before.DocumentId,entry.Id,true);
        current = (await f.Repository.LoadAsync(before.DocumentId))!;
        await f.Repository.SaveAsync(current with { Sections = current.Sections.Select((s,i) => i == 0 ? s with { Pages = s.Pages.Select(p => p with { Content = "<p>Later translated edit</p>" }).ToArray() } : s).ToArray() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => history.ChangeAsync(before.DocumentId,entry.Id,false));
    }
    [Fact]
    public async Task CapabilityAndAccountChangeDuringGenerationAreRejectedBeforeAnyApply()
    {
        using var f = new TranslationTestFixture(); var before = await f.Create(); var prepared = f.Prepare(before,"document");
        f.Api.Capability = false;
        await Assert.ThrowsAsync<DeviceAiException>(() => f.Ai.ProposeAsync(prepared.Request,default)); Assert.Equal(0,f.Api.Calls);
        f.Api.Capability = true; f.Api.Before = _ => f.Account.SignOutAsync();
        await Assert.ThrowsAsync<DeviceAiException>(() => f.Ai.ProposeAsync(prepared.Request,default));
        Assert.Empty(await f.History.HistoryAsync(before.DocumentId));
    }
}

internal sealed class TranslationTestFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(),"WriterApp.TranslationTests",Guid.NewGuid().ToString("N"));
    public FileLocalDocumentStore Store => new(Root);
    public LocalDocumentRepository Repository => new(Store);
    public LocalAiStore History => new(Root + "/ai");
    public DeviceAccountService Account { get; } = new(new Identity());
    public DeviceConnectivity Network { get; } = new();
    public TranslationApi Api { get; } = new();
    public DeviceAiService Ai => new(Api,Account,Network);
    public async Task<LocalDocument> Create() {
        await Account.SignInAsync(); var d = await Repository.CreateProjectAsync("Unicode book");
        d = LocalDocumentStructure.Apply(d,new(LocalStructureAction.CreatePage,TargetSectionId:d.Sections[0].SectionId,Title:"Page 2"),DateTimeOffset.UtcNow);
        d = LocalDocumentStructure.Apply(d,new(LocalStructureAction.CreateSection,Title:"Other section"),DateTimeOffset.UtcNow);
        d = await Repository.SaveAsync(d with { LanguageCode = "sv", Sections = d.Sections.Select(s => s with { LanguageCode = "sv",
            Pages = s.Pages.Select((p,i) => p with { Content = "<p>🧭 Åsa 日本語 " + i + "</p>" }).ToArray() }).ToArray() });
        var cloudDocument = Guid.NewGuid(); var cloudProject = Guid.NewGuid();
        return await Store.ApplySyncAsync(d with { ServerDocumentId = cloudDocument, ServerProjectId = cloudProject,
            Project = d.Project! with { ServerProjectId = cloudProject, ServerPrimaryDocumentId = cloudDocument, Nodes=d.Project.Nodes.Select(n=>n with{ServerNodeId=Guid.NewGuid()}).ToArray() }, ServerVersion = "v1", SyncState = LocalSyncState.Synced,
            Sections = d.Sections.Select(s => s with { ServerSectionId = Guid.NewGuid(), Pages = s.Pages.Select(p => p with { ServerPageId = Guid.NewGuid() }).ToArray() }).ToArray() },d.LocalRevision,default,projects:true);
    }
    public LocalTranslationPrepared Prepare(LocalDocument d,string scope) => LocalTranslation.Prepare(d,d.Sections[0].SectionId,scope,"en",
        LocalTranslation.Scope(d,d.Sections[0].SectionId,scope).SelectMany(s => s.Pages).ToDictionary(p => p.PageId,p => new TranslationPageCapture([new("0.0",p.Content[3..^4])])));
    public Dictionary<Guid,string> Html(LocalDocument d,string scope) => LocalTranslation.Scope(d,d.Sections[0].SectionId,scope).SelectMany(s => s.Pages).ToDictionary(p => p.PageId,p => p.Content.Replace("Åsa 日本語","Asa Japanese"));
    public LocalAiHistory Entry(LocalDocument before,LocalDocument after,DeviceAiProposal proposal,string mode) => new(2,Guid.NewGuid(),before.DocumentId,proposal.Prepared.Key,
        "Translation:"+mode,before.LocalRevision,before.ServerVersion,DateTimeOffset.UtcNow,"Reviewed",before,proposal.ProposedText,After:after);
    internal sealed class Identity : IDeviceIdentityClient {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive,CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic",DateTimeOffset.UtcNow.AddHours(1),"Writer","account-1"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    internal sealed class TranslationApi : IDeviceAiApi {
        public bool Capability = true; public int Calls; public Func<CancellationToken,Task>? Before; public string? Invalid; public AiActionExecuteRequestDto? Last;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto { AiEnabled=true,UiEnabled=true,QuotaRemaining=10,SupportsDocumentVersionChecks=true,SupportsStructuredTranslation=Capability });
        public async Task<AiActionExecuteResponseDto> ExecuteAsync(string key,AiActionExecuteRequestDto request,CancellationToken ct) {
            Calls++; Last = request; if (Before is not null) await Before(ct);
            var source = TranslationStructures.Parse(request.Parameters![TranslationStructures.Parameter]!.ToString()!);
            string text = TranslationStructures.Serialize(source with { Sections = source.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with {
                Runs = p.Runs.Select(r => r with { Text = r.Text.Replace("Åsa 日本語","Asa Japanese") }).ToArray() }).ToArray() }).ToArray() });
            return new(Guid.NewGuid(),null,Invalid ?? text,"",DateTimeOffset.UtcNow,key,SourceDocumentVersion:request.ExpectedDocumentVersion);
        }
    }
    public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root,true); }
}
