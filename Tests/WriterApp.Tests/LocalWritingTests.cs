using WriterApp.Application.AI;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalWritingTests
{
    internal static LocalWritingPrepared Prepare(LocalDocument source, WritingScope scope, string? key = null, WritingSettings? settings = null) {
        var section = source.Sections[0]; var page = section.Pages[0]; string text = page.Content[3..^4];
        return LocalWriting.Prepare(source, section.SectionId, new(key ?? (scope switch { WritingScope.Section => "expand.section", WritingScope.Continuation => "propose.next-paragraph", _ => "rewrite.selection" }), scope, settings ?? new()),
            section.Pages.ToDictionary(p => p.PageId, p => new TranslationPageCapture([new("0.0", p.Content[3..^4])])),
            new(source, page.PageId, new(page.Content, text, text, 0, text.Length, 1, text.Length + 1, 0)),
            section.Pages.ToDictionary(p => p.PageId, p => p.Content[3..^4]), "Raise stakes");
    }
    internal sealed class Api : IDeviceAiApi {
        public bool Available = true, Structured = true, Presets = true, Outline=true, EchoOutline=true, Recommendations=true; public int Calls; public string? Invalid; public Func<CancellationToken, Task>? Before;
        public Func<CancellationToken, Task>? AvailabilityBefore;
        public bool Free;public OnboardingDemoStatus? Demo;
        public Func<CancellationToken, Task>? DemoBefore;
        public async Task<OnboardingDemoStatus?> GetOnboardingDemoAsync(CancellationToken ct) { if (DemoBefore is { } before) await before(ct); return Demo; }
        public AiActionExecuteRequestDto? Last; public string? LastKey; public string Paragraph = "A new visitor opened the door, carrying a sealed letter.";
        public async Task<WritingAvailability> GetWritingAvailabilityAsync(CancellationToken ct) {
            var result = new WritingAvailability(Available ? WritingActions.SelectionKeys.Concat(WritingActions.SectionKeys).Concat(["propose.next-paragraph","custom_transform"]).ToArray() : [], Structured, Presets, Outline, Recommendations);
            if (AvailabilityBefore is { } before) await before(ct);
            return result;
        }
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto { AiEnabled = !Free, UiEnabled = !Free, QuotaRemaining = Free ? 0 : 10, SupportsDocumentVersionChecks = true });
        public async Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct) {
            Calls++; Last = request; LastKey = key; if (Before is not null) await Before(ct);
            var result = request.Parameters!.TryGetValue(WritingActions.Parameter, out var json)
                ? WritingActions.Serialize(WritingActions.Parse(json!.ToString()!) with { Pages = WritingActions.Parse(json.ToString()!).Pages.Select(p => p with { Runs = p.Runs.Select(r => r with { Text = r.Text.Replace("Åsa", "Elin") }).ToArray() }).ToArray() })
                : key == "propose.next-paragraph" ? Paragraph : "Elin stepped forward.";
            if (RecommendedWriting.From(request.Parameters) is { } run) {
                if (!RecommendedWriting.Revises(run.ToolId)) result = System.Text.Json.JsonSerializer.Serialize(new RecommendedTextResult(Enumerable.Range(1, RecommendedWriting.ItemCount(run.ToolId)).Select(i => run.ToolId == "blog.generate_headlines" ? "Headline " + i : Paragraph).ToArray()), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
                else if (RecommendedWriting.Output(run.ToolId) == RecommendedOutput.OpeningRevision) {
                    var original = WritingActions.Parse(json!.ToString()!);
                    result = WritingActions.Serialize(original with { Pages = original.Pages.Select((p, i) => i == 0 ? p with { Runs = p.Runs.Select(r => r with { Text = r.Text.Replace("Åsa", "Elin") }).ToArray() } : p).ToArray() });
                }
            }
            return new(Guid.NewGuid(), request.OriginalText, Invalid ?? result, "", DateTimeOffset.UtcNow, key, SourceDocumentVersion: request.ExpectedDocumentVersion,SourceOutlineFingerprint:EchoOutline ? request.WritingOutline?.Fingerprint : null);
        }
    }
    [Theory]
    [InlineData("expand.section")][InlineData("tighten.section")][InlineData("change_tone.section")][InlineData("show_dont_tell.section")]
    public async Task EverySectionActionIncludesAllPagesAndSavesUndoRedoAfterRestart(string key) {
        using var f = new TranslationTestFixture(); var source = await f.Create(); var api = new Api(); var ai = new DeviceAiService(api, f.Account, f.Network);
        var actions = new LocalWritingActions(f.Repository, f.History, ai, api); var prepared = Prepare(source, WritingScope.Section, key);
        Assert.Equal(2, prepared.Structure!.Pages.Count); Assert.Equal(source.Sections[0].Pages.Select(p => p.ServerPageId), prepared.Structure.Pages.Select(p => (Guid?)p.Id));
        var proposal = await actions.ProposeAsync(prepared, default); var result = WritingActions.Result(proposal.ProposedText, prepared.Structure);
        var html = source.Sections[0].Pages.Select((p,i) => (p.PageId, Html:"<p>"+result.Pages[i].Runs[0].Text+"</p>")).ToDictionary(x => x.PageId, x => x.Html);
        var entry = LocalWriting.Entry(prepared, proposal, LocalWriting.Preview(prepared, html)); await f.History.SaveHistoryAsync(entry);
        Assert.Equal(LocalDocumentCodec.Encode(source),LocalDocumentCodec.Encode((await f.Repository.LoadAsync(source.DocumentId))!));
        await actions.ApplyAsync(entry, proposal, default);
        var after = (await f.Repository.LoadAsync(source.DocumentId))!; Assert.All(after.Sections[0].Pages,p => Assert.Contains("Elin",p.Content)); Assert.Equal(source.Sections[1].Pages[0].Content,after.Sections[1].Pages[0].Content);
        var restart = new LocalAiHistoryActions(f.Repository,new(f.Root+"/ai")); await restart.ChangeAsync(source.DocumentId,entry.Id,false);
        Assert.Equal(source.Sections[0].Pages.Select(p => p.Content),(await f.Repository.LoadAsync(source.DocumentId))!.Sections[0].Pages.Select(p => p.Content));
        await restart.ChangeAsync(source.DocumentId,entry.Id,true);
        Assert.Equal(after.Sections[0].Pages.Select(p => p.Content),(await f.Repository.LoadAsync(source.DocumentId))!.Sections[0].Pages.Select(p => p.Content));
    }
    [Theory][InlineData("rewrite.selection")][InlineData("change_tone.selection")][InlineData("show_dont_tell.selection")][InlineData("expand.selection")][InlineData("tighten.selection")]
    public async Task SelectionRequestsRetainExactScopeAndChosenParameters(string key) {
        using var f = new TranslationTestFixture(); var source = await f.Create(); var api = new Api(); var ai = new DeviceAiService(api,f.Account,f.Network);
        var prepared=Prepare(source,WritingScope.Selection,key,new("Friendly","Shorter",false)); await new LocalWritingActions(f.Repository,f.History,ai,api).ProposeAsync(prepared,default);
        Assert.Equal("Friendly",api.Last!.Parameters!["tone"]); Assert.Equal("Shorter",api.Last.Parameters["length"]); Assert.Equal(false,api.Last.Parameters["preserve_terms"]);
        Assert.Equal(source.Sections[0].Pages[0].ServerPageId,api.Last.PageId); Assert.Equal(prepared.Request.SelectedText,api.Last.OriginalText); Assert.Equal(0,api.Last.SelectionStart);
    }
    [Fact]
    public async Task ContinuationHasSavedCompleteContextSceneBeatsGenreAndLastPageTarget() {
        using var f=new TranslationTestFixture(); var source=await f.Create(); var prepared=Prepare(source,WritingScope.Continuation);
        Assert.Equal(source.Sections[0].Pages[^1].PageId,prepared.PageId); Assert.Null(prepared.Request.Request.SelectionStart); Assert.Null(prepared.Request.Request.OriginalText);
        Assert.Contains(source.Sections[0].Pages[0].Content[3..^4],prepared.Request.Request.SurroundingText); Assert.Contains(source.Sections[0].Pages[1].Content[3..^4],prepared.Request.Request.SurroundingText);
        Assert.Contains("Raise stakes",prepared.Request.Request.Parameters!["instruction"]!.ToString()); Assert.True(prepared.Request.Request.Parameters.ContainsKey("key_events"));
    }
    [Theory][InlineData("missing")][InlineData("capability")][InlineData("cancel")][InlineData("account")]
    public async Task AvailabilityCancellationAndAccountChecksPreventMutation(string failure) {
        using var f=new TranslationTestFixture();var source=await f.Create();var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);var actions=new LocalWritingActions(f.Repository,f.History,ai,api);
        if(failure=="missing")api.Available=false;if(failure=="capability")api.Structured=false;
        if(failure=="cancel")api.Before=ct=>Task.FromCanceled(ct.IsCancellationRequested ? ct : new CancellationToken(true));
        if(failure=="account")api.Before=_=>f.Account.SignOutAsync();
        await Assert.ThrowsAnyAsync<Exception>(()=>actions.ProposeAsync(Prepare(source,WritingScope.Section),default));
        if(failure is "missing" or "capability")Assert.Equal(0,api.Calls);
        Assert.Empty(await f.History.HistoryAsync(source.DocumentId));Assert.Equal(LocalDocumentCodec.Encode(source),LocalDocumentCodec.Encode((await f.Repository.LoadAsync(source.DocumentId))!));
    }
    [Theory][InlineData("source")][InlineData("account")][InlineData("proposal")][InlineData("cancel")][InlineData("section")][InlineData("page")]
    public async Task ApplyChecksReviewedSourceAccountTargetAndCancellation(string failure) {
        using var f=new TranslationTestFixture();var source=await f.Create();var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);var actions=new LocalWritingActions(f.Repository,f.History,ai,api);var prepared=Prepare(source,WritingScope.Section);
        var proposal=await actions.ProposeAsync(prepared,default);var entry=LocalWriting.Entry(prepared,proposal,LocalWriting.Preview(prepared,f.Html(source,"section")));await f.History.SaveHistoryAsync(entry);
        if(failure=="source")await f.Repository.SaveAsync(source with {Title="Later title"});if(failure=="account")await f.Account.SignOutAsync();if(failure=="proposal")proposal=proposal with{ProposedText="different"};
        if(failure=="section")proposal=proposal with { Prepared=proposal.Prepared with {Request=proposal.Prepared.Request with {SectionId=Guid.NewGuid()}}};
        if(failure=="page")proposal=proposal with { Prepared=proposal.Prepared with {Request=proposal.Prepared.Request with {PageId=Guid.NewGuid()}}};
        var before=(await f.Repository.LoadAsync(source.DocumentId))!;await Assert.ThrowsAnyAsync<Exception>(()=>actions.ApplyAsync(entry,proposal,failure=="cancel" ? new(true):default));
        Assert.Equal(LocalDocumentCodec.Encode(before),LocalDocumentCodec.Encode((await f.Repository.LoadAsync(source.DocumentId))!));Assert.Equal("Reviewed",Assert.Single(await f.History.HistoryAsync(source.DocumentId)).Status);
    }
    [Fact]
    public async Task ApprovedInterruptedSaveResumesIdempotentlyOfflineAndRefusesLaterEdits() {
        using var f=new TranslationTestFixture();var source=await f.Create();var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);var prepared=Prepare(source,WritingScope.Section);var proposal=await ai.ProposeAsync(prepared.Request,default);
        var entry=LocalWriting.Entry(prepared,proposal,LocalWriting.Preview(prepared,f.Html(source,"section"))) with{Status="Applying"};await f.History.SaveHistoryAsync(entry);
        await LocalWritingActions.ResumeAsync(f.Repository,new(f.Root+"/ai"),entry,default);var saved=(await f.Repository.LoadAsync(source.DocumentId))!;
        await LocalWritingActions.ResumeAsync(f.Repository,f.History,entry,default);Assert.Equal(saved.LocalRevision,(await f.Repository.LoadAsync(source.DocumentId))!.LocalRevision);Assert.Equal(1,api.Calls);
        await f.Repository.SaveAsync(saved with{Title="Later"});await Assert.ThrowsAsync<InvalidOperationException>(()=>LocalWritingActions.ResumeAsync(f.Repository,f.History,entry,default));
    }
    [Theory][InlineData("partial")][InlineData("reordered")][InlineData("wrong-document")][InlineData("unknown")][InlineData("duplicate")][InlineData("newlines")]
    public async Task StructuredOutputRejectsInvalidTargetsAndText(string failure) {
        using var f=new TranslationTestFixture();var source=await f.Create();var structure=Prepare(source,WritingScope.Section).Structure!;var text=WritingActions.Serialize(structure);
        text=failure switch {"partial"=>"{}","reordered"=>WritingActions.Serialize(structure with{Pages=structure.Pages.Reverse().ToArray()}),"wrong-document"=>text.Replace(structure.DocumentId.ToString(),Guid.NewGuid().ToString()),
            "unknown"=>text[..^1]+",\"extra\":true}","duplicate"=>text.Replace("\"version\":1","\"version\":1,\"version\":1"),_=>WritingActions.Serialize(structure with { Pages=structure.Pages.Select(p=>p with { Runs=p.Runs.Select(r=>r with { Text=r.Text+"\nInvalid" }).ToArray() }).ToArray() })};
        Assert.ThrowsAny<Exception>(()=>WritingActions.Result(text,structure));
    }
    [Fact]
    public void ContinuationSuppressesEstablishedLeadingEchoAndRejectsEmptyEchoMetaAndLargeOutput() {
        string source=new('a',100);Assert.Equal("New events.",WritingActions.Continuation(source+" New events.",source));
        Assert.Equal("New events. Another event.",WritingActions.Continuation("New events.\nAnother event.",source));
        foreach(var text in new[]{source,"As an AI, I suggest you rewrite this paragraph.",new string('x',20001),""}) Assert.ThrowsAny<Exception>(()=>WritingActions.Continuation(text,source));
    }
    [Fact]
    public void StructuredRevisionRetainsUnchangedPunctuationAndWhitespaceAroundMarks() {
        var source=new WritingStructure(1,Guid.NewGuid(),Guid.NewGuid(),[new(Guid.NewGuid(),[new("0.0","Original prose"),new("0.1","."),new("0.2"," ")])]);
        var result=source with{Pages=source.Pages.Select(p=>p with{Runs=p.Runs.Select(r=>r.Id=="0.0" ? r with{Text="Revised prose"}:r).ToArray()}).ToArray()};
        Assert.Equal(result.Pages[0].Runs,WritingActions.Result(WritingActions.Serialize(result),source).Pages[0].Runs);
    }
    [Fact]
    public async Task HistoryCannotReplacePlanningOrWrongPageAndUndoPreservesUnrelatedEdits() {
        using var f=new TranslationTestFixture();var source=await f.Create();var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);var prepared=Prepare(source,WritingScope.Section);var proposal=await ai.ProposeAsync(prepared.Request,default);
        var entry=LocalWriting.Entry(prepared,proposal,LocalWriting.Preview(prepared,f.Html(source,"section")));
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.History.SaveHistoryAsync(entry with{After=entry.After! with{Title="Wrong"}}));
        await new LocalWritingActions(f.Repository,f.History,ai,api).ApplyAsync(entry,proposal,default);var saved=(await f.Repository.LoadAsync(source.DocumentId))!;
        saved=await f.Repository.SaveAsync(saved with{Project=saved.Project! with{Synopsis=new(Stakes:"Later planning")},Sections=saved.Sections.Select((s,i)=>i==1 ? s with{Pages=s.Pages.Select(p=>p with{Content="<p>Later writing</p>"}).ToArray()}:s).ToArray()});
        var history=new LocalAiHistoryActions(f.Repository,f.History);await history.ChangeAsync(source.DocumentId,entry.Id,false);var undone=(await f.Repository.LoadAsync(source.DocumentId))!;
        Assert.Equal("Later planning",undone.Project!.Synopsis!.Stakes);Assert.Equal("<p>Later writing</p>",undone.Sections[1].Pages[0].Content);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SectionAggregateInterruptionRetainsEveryOriginalPageOrEveryReviewedPage(bool committed) {
        using var f=new TranslationTestFixture();var source=await f.Create();var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);var prepared=Prepare(source,WritingScope.Section);var proposal=await ai.ProposeAsync(prepared.Request,default);
        var entry=LocalWriting.Entry(prepared,proposal,LocalWriting.Preview(prepared,f.Html(source,"section"))) with {Status="Applying"};await f.History.SaveHistoryAsync(entry);
        if(committed)await f.Repository.SaveAsync(entry.After!);
        else {var failing=new LocalDocumentRepository(new FileLocalDocumentStore(f.Root,TimeProvider.System,new AtomicDocumentWriter(_=>throw new IOException("Injected after staging"))));await Assert.ThrowsAsync<IOException>(()=>failing.SaveAsync(entry.After!));Assert.Equal(LocalTranslation.Signature(source),LocalTranslation.Signature((await f.Repository.LoadAsync(source.DocumentId))!));}
        await LocalWritingActions.ResumeAsync(new(new FileLocalDocumentStore(f.Root)),new(f.Root+"/ai"),entry,default);Assert.Equal(LocalTranslation.Signature(entry.After!),LocalTranslation.Signature((await f.Repository.LoadAsync(source.DocumentId))!));
    }
    [Theory][InlineData(WritingScope.Selection)][InlineData(WritingScope.Continuation)]
    public async Task SelectionAndContinuationUndoRedoAfterRestartPreserveOtherPages(WritingScope scope) {
        using var f=new TranslationTestFixture();var source=await f.Create();var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);var prepared=Prepare(source,scope);var proposal=await ai.ProposeAsync(prepared.Request,default);
        var target=source.Sections[0].Pages.Single(p=>p.PageId==prepared.PageId);var html=scope==WritingScope.Continuation ? target.Content+"<p>New events.</p>":"<p>Changed selection.</p>";
        var entry=LocalWriting.Entry(prepared,proposal,LocalWriting.Preview(prepared,new Dictionary<Guid,string>{{prepared.PageId,html}}));await new LocalWritingActions(f.Repository,f.History,ai,api).ApplyAsync(entry,proposal,default);
        var actions=new LocalAiHistoryActions(new(new FileLocalDocumentStore(f.Root)),new(f.Root+"/ai"));await actions.ChangeAsync(source.DocumentId,entry.Id,false);Assert.Equal(target.Content,(await f.Repository.LoadAsync(source.DocumentId))!.Sections[0].Pages.Single(p=>p.PageId==target.PageId).Content);
        await actions.ChangeAsync(source.DocumentId,entry.Id,true);Assert.Equal(html,(await f.Repository.LoadAsync(source.DocumentId))!.Sections[0].Pages.Single(p=>p.PageId==target.PageId).Content);
    }
    [Fact]
    public async Task ConcurrentSnapshotReadersDoNotBlockAtomicHistoryAndCanonReplacement() {
        using var f=new TranslationTestFixture();var source=await f.Create();var prepared=Prepare(source,WritingScope.Section);
        var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);var proposal=await ai.ProposeAsync(prepared.Request,default);var entry=LocalWriting.Entry(prepared,proposal,LocalWriting.Preview(prepared,f.Html(source,"section")));await f.History.SaveHistoryAsync(entry);
        var canon=new LocalBibleStore(f.Root+"/canon");var scope=LocalBibleStore.ScopeKey(new("https://test.invalid"),"account-1");
        var snapshot=new WriterApp.Shared.Canon.DeviceBibleSnapshot(1,source.ServerDocumentId!.Value,WriterApp.Shared.Canon.CanonKind.Character,"token","v1","v1",new string('A',64),DateTimeOffset.UtcNow,"{\"schemaVersion\":\"1.0\",\"characters\":[]}",0,true);
        var cached=new LocalBibleCache(1,scope,source.DocumentId,source.ServerDocumentId.Value,WriterApp.Shared.Canon.CanonKind.Character,snapshot,DateTimeOffset.UtcNow,"Ready");await canon.SaveAsync(cached);
        var writer=Task.Run(async()=>{for(int i=0;i<40;i++){await f.History.SaveHistoryAsync(entry with{Status=i%2==0?"Applying":"Reviewed"});await canon.SaveAsync(cached with{CheckedAt=DateTimeOffset.UtcNow});}});
        var reader=Task.Run(async()=>{for(int i=0;i<40;i++){var saved=Assert.Single(await f.History.HistoryAsync(source.DocumentId));Assert.Contains(saved.Status,new[]{"Reviewed","Applying"});var value=await canon.ReadAsync(scope,source,WriterApp.Shared.Canon.CanonKind.Character);Assert.Equal("token",value!.Snapshot!.SnapshotVersion);}});
        await Task.WhenAll(writer,reader);
    }
    [Fact]
    public async Task AtomicRecoverySaveWaitsForBriefWindowsReaderWithoutTruncatingOriginal() {
        using var f=new TranslationTestFixture();Directory.CreateDirectory(f.Root);var path=Path.Combine(f.Root,"snapshot.json");await File.WriteAllTextAsync(path,"Complete original");
        var reader=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);var save=new AtomicDocumentWriter().WriteAsync(path,System.Text.Encoding.UTF8.GetBytes("Complete reviewed result"),default);
        await Task.Delay(60);if(OperatingSystem.IsWindows()){Assert.False(save.IsCompleted);Assert.Equal("Complete original",await File.ReadAllTextAsync(path));}
        await reader.DisposeAsync();await save;Assert.Equal("Complete reviewed result",await File.ReadAllTextAsync(path));
    }
}
