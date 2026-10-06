using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Application.Synopsis;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;
public sealed class LocalSynopsisCoachingTests
{
    public static IEnumerable<object[]> Fields()=>SynopsisCoaching.Fields.Select(f=>new object[]{f.Key});
    internal sealed class Api : IDeviceAiApi {
        public bool Capability=true;public long Quota=10;public bool Enabled=true;public int Calls;public string? Mode;public SynopsisAiRequestDto? Last;
        public string Failure="";public Func<CancellationToken,Task>? Before;
        public string? Feedback, Proposed;
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct)=>Task.FromResult(new AiUsageStatusDto{AiEnabled=Enabled,UiEnabled=true,QuotaRemaining=Quota,SupportsDocumentVersionChecks=true,SupportsSynopsisCoaching=Capability});
        public Task<AiActionExecuteResponseDto> ExecuteAsync(string key,AiActionExecuteRequestDto r,CancellationToken ct)=>throw new InvalidOperationException("Synopsis must use its dedicated route.");
        public async Task<SynopsisAiResponseDto> ExecuteSynopsisAsync(Guid id,string mode,SynopsisAiRequestDto r,CancellationToken ct) {
            Calls++;Mode=mode;Last=r;if(Before is not null)await Before(ct);
            var response=new SynopsisAiResponseDto(mode,mode=="suggest"?"Explains the field only.":mode=="evaluate"?"Strengths:\n- Clear intent\nPotential weaknesses:\n- Unclear stakes\nMissing elements:\n- Audience\nClarity issues:\n- Motive":"1. What does Åsa want?\n2. Who resists?",r.FocusFieldKey,mode=="suggest"?"New field text Åsa 日本語":null,1,Guid.NewGuid(),id,r.ExpectedDocumentVersion,r.SourceSynopsis);
            response = response with { OutputText = Feedback ?? response.OutputText, ProposedText = mode == "suggest" ? Proposed ?? response.ProposedText : null };
            return Failure switch {
                "mode"=>response with{Mode="other"},"field"=>response with{FocusFieldKey="notes"},"version"=>response with{SourceDocumentVersion="old"},
                "source"=>response with{SourceSynopsis=new(Logline:"Older synopsis")},"document"=>response with{DocumentId=Guid.NewGuid()},"id"=>response with{ProposalId=Guid.Empty},
                "capability"=>response with{ContractVersion=0},"analysis-proposal"=>response with{ProposedText="Unexpected writing"},
                "empty"=>response with{OutputText="",ProposedText=mode=="suggest"?"":null},"large"=>response with{ProposedText=new('x',20_001)},_=>response};
        }
    }
    internal static LocalDocument Source(LocalDocument d)=>d with{Project=d.Project! with{Synopsis=new("Original logline","Premise","Theme","Arc","Conflict","Stakes","Setting","Ending","Questions","Authored notes")}};
    [Theory][MemberData(nameof(Fields))]
    public async Task EveryFieldPersistsReopensAndUndoesWithoutChangingOtherWriting(string field) {
        using var f=new TranslationTestFixture();var source=Source(await f.Create());
        var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);
        await f.Repository.SaveAsync(source);source=(await f.Repository.LoadAsync(source.DocumentId))!;
        // Re-pin the durable source after this test's author save and its synthetic sync acknowledgment.
        await f.Store.ApplySyncAsync(source with{SyncState=LocalSyncState.Synced},source.LocalRevision,default,projects:true);
        source=(await f.Repository.LoadAsync(source.DocumentId))!;var p=LocalSynopsisCoaching.Prepare(source,"suggest",field,"Author coaching notes");var proposal=await ai.ProposeAsync(p.Request,default);
        var after=LocalSynopsisCoaching.Preview(source,p,proposal);var entry=Entry(source,p,proposal) with{Status="Applying",After=after};
        await f.History.SaveHistoryAsync(entry);await LocalSynopsisCoaching.ResumeAsync(f.Repository,f.History,entry);await LocalSynopsisCoaching.ResumeAsync(f.Repository,f.History,entry);
        var saved=(await f.Repository.LoadAsync(source.DocumentId))!;Assert.Equal(proposal.ProposedText,SynopsisCoaching.Value(saved.Project!.Synopsis!,field));
        foreach(var other in SynopsisCoaching.Fields.Where(x=>x.Key!=field))Assert.Equal(SynopsisCoaching.Value(source.Project!.Synopsis!,other.Key),SynopsisCoaching.Value(saved.Project.Synopsis!,other.Key));
        Assert.Equal(JsonSerializer.Serialize(source.Sections),JsonSerializer.Serialize(saved.Sections));
        var history=new LocalAiHistoryActions(f.Repository,f.History);await history.ChangeAsync(source.DocumentId,entry.Id,false);Assert.Equal(source.Project!.Synopsis,(await f.Repository.LoadAsync(source.DocumentId))!.Project!.Synopsis);
        await history.ChangeAsync(source.DocumentId,entry.Id,true);Assert.Equal(saved.Project.Synopsis,(await f.Repository.LoadAsync(source.DocumentId))!.Project!.Synopsis);
        Assert.Equal("Author coaching notes",Assert.Single(await f.History.HistoryAsync(source.DocumentId)).SynopsisUserNotes);
        await Assert.ThrowsAsync<InvalidDataException>(()=>f.History.SaveHistoryAsync(entry with{After=after with{Title="Forged writing"}}));
    }
    internal static LocalAiHistory Entry(LocalDocument d,AdvancedAiPrepared p,DeviceAiProposal proposal)=>new(5,Guid.NewGuid(),d.DocumentId,p.Request.Key,p.Target+":"+p.Field,d.LocalRevision,d.ServerVersion,DateTimeOffset.UtcNow,"Reviewed",d,proposal.ProposedText,
        OriginalText:p.Target==LocalAiTarget.SynopsisField?SynopsisCoaching.Value(d.Project!.Synopsis!,p.Field):"",SynopsisCommentary:p.Target==LocalAiTarget.SynopsisField?proposal.Summary:null,SynopsisUserNotes:p.Request.SynopsisRequest!.UserNotes);
    [Theory][InlineData("mode")][InlineData("field")][InlineData("version")][InlineData("source")][InlineData("document")][InlineData("id")][InlineData("capability")][InlineData("empty")][InlineData("large")]
    public async Task WrongAndMalformedResponseCannotBecomeProposal(string failure) {
        using var f=new TranslationTestFixture();var d=Source(await f.Create());var api=new Api{Failure=failure};var ai=new DeviceAiService(api,f.Account,f.Network);
        await Assert.ThrowsAsync<InvalidDataException>(()=>ai.ProposeAsync(LocalSynopsisCoaching.Prepare(d,"suggest","logline","").Request,default));Assert.Empty(await f.History.HistoryAsync(d.DocumentId));
    }
    [Theory][InlineData("evaluate")][InlineData("questions")]
    public async Task FeedbackNeedsNoScenesAndCannotApplyEvenWhenHandlerIsInvoked(string mode) {
        using var f=new TranslationTestFixture();var original=Source(await f.Create());var d=original with{Sections=[],Project=original.Project! with{Nodes=original.Project!.Nodes.Where(n=>n.NodeType!="scene").ToArray()}};var p=LocalSynopsisCoaching.Prepare(d,mode,"invalid-unused-field","");var api=new Api();var proposal=await new DeviceAiService(api,f.Account,f.Network).ProposeAsync(p.Request,default);
        Assert.Equal(mode,api.Mode);Assert.Null(api.Last!.FocusFieldKey);Assert.Equal(d.Project!.Synopsis,api.Last.SourceSynopsis);Assert.Equal(Guid.Empty,p.SectionId);
        Assert.Throws<InvalidDataException>(()=>LocalSynopsisCoaching.Preview(d,p,proposal));await f.History.SaveHistoryAsync(Entry(d,p,proposal));
        Assert.Throws<InvalidDataException>(()=>LocalSynopsisCoaching.ValidateEntry(Entry(d,p,proposal) with{Status="Applying",After=d}));
        api.Failure="analysis-proposal";await Assert.ThrowsAsync<InvalidDataException>(()=>new DeviceAiService(api,f.Account,f.Network).ProposeAsync(p.Request,default));
    }
    [Theory][InlineData("offline")][InlineData("plan")][InlineData("quota")][InlineData("capability")][InlineData("account")][InlineData("cancel")]
    public async Task ExistingAiPoliciesPreserveSource(string reason) {
        using var f=new TranslationTestFixture();var d=Source(await f.Create());var api=new Api();var ai=new DeviceAiService(api,f.Account,f.Network);using var cancel=new CancellationTokenSource();
        if(reason=="offline")f.Network.SetOnline(false);if(reason=="plan")api.Enabled=false;if(reason=="quota")api.Quota=0;if(reason=="capability")api.Capability=false;
        if(reason=="account")api.Before=_=>f.Account.SignOutAsync();if(reason=="cancel")api.Before=_=>{cancel.Cancel();return Task.CompletedTask;};
        await Assert.ThrowsAnyAsync<Exception>(()=>ai.ProposeAsync(LocalSynopsisCoaching.Prepare(d,"evaluate","logline","").Request,cancel.Token));
        if(reason is "offline" or "plan" or "quota" or "capability")Assert.Equal(0,api.Calls);Assert.Empty(await f.History.HistoryAsync(d.DocumentId));
    }
    [Theory][InlineData("{}")] [InlineData("[]")][InlineData("not JSON")][InlineData("{\"proposedText\":\"x\",\"proposedText\":\"y\"}")][InlineData("{\"proposedText\":3,\"commentary\":\"x\"}")][InlineData("{\"proposedText\":\"\",\"commentary\":\"x\"}")]
    public void SuggestionRequiresSeparateBoundedFieldTextAndCommentary(string json)=>Assert.ThrowsAny<Exception>(()=>SynopsisCoaching.ParseSuggestion(json));
    [Fact]
    public async Task EmptyStandaloneInvalidScopesAndLaterChangesFailExplicitly() {
        using var f=new TranslationTestFixture();var d=await f.Create();Assert.Throws<InvalidDataException>(()=>LocalSynopsisCoaching.Prepare(d,"suggest","logline",""));
        Assert.NotNull(LocalSynopsisCoaching.Prepare(d,"questions","logline",""));Assert.NotNull(LocalSynopsisCoaching.Prepare(d,"suggest","logline","The story is about ambition."));
        Assert.Throws<InvalidDataException>(()=>LocalSynopsisCoaching.Prepare(d with{Project=null},"questions","logline",""));Assert.Throws<InvalidDataException>(()=>LocalSynopsisCoaching.Prepare(d,"suggest","outline_draft","notes"));
        var p=LocalSynopsisCoaching.Prepare(Source(d),"suggest","logline","");var proposal=await new DeviceAiService(new Api(),f.Account,f.Network).ProposeAsync(p.Request,default);
        Assert.Throws<InvalidOperationException>(()=>LocalSynopsisCoaching.Preview(p.Source with{Title="Changed"},p,proposal));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task InterruptedApprovalReopensOfflineAndRejectsLaterChanges(bool alreadySaved) {
        using var f=new TranslationTestFixture();var original=await f.Create();var source=await f.Repository.SaveAsync(Source(original));source=await f.Store.ApplySyncAsync(source with{SyncState=LocalSyncState.Synced},source.LocalRevision,default,projects:true);
        var p=LocalSynopsisCoaching.Prepare(source,"suggest","logline","");var proposal=await new DeviceAiService(new Api(),f.Account,f.Network).ProposeAsync(p.Request,default);
        var approved=Entry(source,p,proposal) with{Status="Applying",After=LocalSynopsisCoaching.Preview(source,p,proposal)};await f.History.SaveHistoryAsync(approved);
        if(alreadySaved)await f.Repository.SaveAsync(approved.After!);f.Network.SetOnline(false);await f.Account.SignOutAsync();
        var reopened=Assert.Single(await new LocalAiStore(f.Root+"/ai").HistoryAsync(source.DocumentId));await LocalSynopsisCoaching.ResumeAsync(f.Repository,f.History,reopened);
        var saved=(await f.Repository.LoadAsync(source.DocumentId))!;Assert.Equal(proposal.ProposedText,saved.Project!.Synopsis!.Logline);Assert.Equal("Applied",Assert.Single(await f.History.HistoryAsync(source.DocumentId)).Status);
        saved=await f.Repository.SaveAsync(saved with{Project=saved.Project! with{Synopsis=saved.Project!.Synopsis! with{Notes="Later unrelated note"}}});
        var history=new LocalAiHistoryActions(f.Repository,f.History);await history.ChangeAsync(source.DocumentId,approved.Id,false);
        var undone=(await f.Repository.LoadAsync(source.DocumentId))!;Assert.Equal("Later unrelated note",undone.Project!.Synopsis!.Notes);Assert.Equal(source.Project!.Synopsis!.Logline,undone.Project.Synopsis.Logline);
        await history.ChangeAsync(source.DocumentId,approved.Id,true);saved=(await f.Repository.LoadAsync(source.DocumentId))!;
        await f.Repository.SaveAsync(saved with{Project=saved.Project! with{Synopsis=saved.Project!.Synopsis! with{Logline="Later authored logline"}}});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>history.ChangeAsync(source.DocumentId,approved.Id,false));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>LocalSynopsisCoaching.ResumeAsync(f.Repository,f.History,approved));
    }
}
