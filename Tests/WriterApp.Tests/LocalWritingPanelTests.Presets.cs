using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class LocalWritingPanelTests
{
    [Theory][InlineData(WritingScope.Selection,"custom")][InlineData(WritingScope.Section,"custom")][InlineData(WritingScope.Selection,"builtin")][InlineData(WritingScope.Section,"builtin")]
    public async Task ActualPresetReviewApplyRestartUndoAndRedoUseDeclaredTargetAndRetainParameters(WritingScope scope,string kind) {
        await using var h=new Harness();await h.Start();var preset=ReusablePromptTransferTests.Custom("Reviewed preset") with{Scope=scope,Pinned=false,ProjectId=h.Source.ServerProjectId};
        if(kind=="builtin")preset=preset with{Kind=kind,Template=null,ActionKey="change_tone.selection",Parameters=new(){["tone"]="dramatic"}};
        await h.Renderer.Dispatcher.InvokeAsync(()=>h.Components.Panel.RunPresetAsync(preset));var review=await h.Html();Assert.Contains("Reviewed preset",review);Assert.Contains("Review writing proposal",review);
        Assert.Equal("dramatic",h.Api.Last!.Parameters!["tone"]);if(kind=="custom"){Assert.Equal(scope.ToString().ToLowerInvariant(),h.Api.Last.Parameters["scope"]);Assert.Equal(true,h.Api.Last.Parameters["strictTokens"]);Assert.Contains("Åsa",h.Api.Last.Parameters["context"]!.ToString());}
        Assert.Equal(ReusablePrompts.Canonical(preset),ReusablePrompts.Canonical(ReusablePrompts.Parse(h.Api.Last.Parameters[ReusablePrompts.Parameter]!.ToString()!)));
        Assert.Equal(LocalWritingOutline.Capture(h.Source).Fingerprint,h.Api.Last.WritingOutline!.Fingerprint);
        Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        await h.Event("Apply");var after=(await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
        Assert.Equal(h.Source.Sections[1].Pages.Select(p=>p.Content),after.Sections[1].Pages.Select(p=>p.Content));if(scope==WritingScope.Selection)Assert.Equal(h.Source.Sections[0].Pages[1],after.Sections[0].Pages[1]);
        var entry=Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));Assert.Equal("Applied",entry.Status);Assert.Equal(ReusablePrompts.Canonical(preset),ReusablePrompts.Canonical(entry.Preset));
        var restarted=new LocalAiHistoryActions(h.Fixture.Repository,new(h.Fixture.Root+"/ai"));await restarted.ChangeAsync(h.Source.DocumentId,entry.Id,false);
        Assert.Equal(h.Source.Sections[0].Pages.Select(p=>p.Content),(await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections[0].Pages.Select(p=>p.Content));
        await restarted.ChangeAsync(h.Source.DocumentId,entry.Id,true);Assert.Equal(after.Sections[0].Pages.Select(p=>p.Content),(await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections[0].Pages.Select(p=>p.Content));
        var evidence=Environment.GetEnvironmentVariable("WRITERAPP_P09_EVIDENCE");if(evidence is not null)await File.WriteAllTextAsync(Path.Combine(evidence,kind+"-"+scope.ToString().ToLowerInvariant()+"-review.html"),review);
    }
    [Theory][InlineData("source")][InlineData("selection")][InlineData("account")][InlineData("parameters")][InlineData("capability")][InlineData("project")][InlineData("unsupported")]
    public async Task PresetFailuresNeverMutateWritingOrRetainedIntent(string failure) {
        await using var h=new Harness();await h.Start();var scope=failure=="selection"?WritingScope.Selection:WritingScope.Section;var preset=ReusablePromptTransferTests.Custom() with{Scope=scope};
        if(failure=="capability")h.Api.Presets=false;if(failure=="project")preset=preset with{ProjectId=Guid.NewGuid()};if(failure=="unsupported")preset=preset with{Parameters=new(){["ignored"]=true}};
        await h.Renderer.Dispatcher.InvokeAsync(()=>h.Components.Panel.RunPresetAsync(preset));
        if(failure is "source" or "selection" or "account" or "parameters") {
            Assert.Contains("Review writing proposal",await h.Html());
            if(failure=="source")h.Source=await h.Fixture.Repository.SaveAsync(h.Source with{Title="Later writing"});
            if(failure=="selection")h.Selection=h.Selection with{Editor=h.Selection.Editor with{SelectedText="Other selection"}};
            if(failure=="account")await h.Renderer.Dispatcher.InvokeAsync(()=>h.Fixture.Account.SignOutAsync());
            if(failure=="parameters") {var entry=(LocalAiHistory)typeof(WriterApp.Device.Shared.Components.LocalWritingPanel).GetField("_entry",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(h.Components.Panel)!;h.Set("_entry",entry with{Preset=preset with{Name="Altered approval"}});}
            await h.Event("Apply");
        }
        Assert.DoesNotContain(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");Assert.Equal(LocalDocumentCodec.Encode(h.Source),LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        if(failure is "capability" or "project" or "unsupported")Assert.Equal(0,h.Api.Calls);
    }
}
