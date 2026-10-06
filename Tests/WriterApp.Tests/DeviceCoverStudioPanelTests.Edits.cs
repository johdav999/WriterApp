using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DeviceCoverStudioPanelTests
{
    [Theory][InlineData("variation")][InlineData("darker")][InlineData("brighter")][InlineData("cinematic")][InlineData("minimal")]
    public async Task ActualDeviceCoverPageEditsReviewsSelectsSavesAndRestores(string operation) {
        await using var h=new Harness();h.Fixture.OwnedInline=true;h.Fixture.Provider.Values=[CoverTestFixture.Image];await h.Start();
        await h.Event("Generate");await h.Event("Edit",operation);Assert.Equal(operation,h.Fixture.Provider.Operation);Assert.Contains("Original is concept 1",await h.Html());
        var dir=Environment.GetEnvironmentVariable("WRITERAPP_P18_EVIDENCE");if(dir is not null)await File.WriteAllTextAsync(Path.Combine(dir,"p18","device-"+operation+".html"),await h.Html());
        Assert.Null((await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
        await h.Event("Select",1);await h.Offline();await h.Event("Save");Assert.Equal(CoverTestFixture.SecondImage,(await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
        await h.Event("Restore");Assert.Null((await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
    }
    [Fact] public async Task ActualDevicePageUnsupportedAndLateCancelledEditPreservesEarlierConcepts() {
        await using var h=new Harness();h.Fixture.OwnedInline=true;h.Fixture.Provider.Values=[CoverTestFixture.Image];await h.Start();await h.Event("Generate");
        h.Fixture.Provider.Unsupported=true;await h.Event("RefreshEdits");Assert.Contains("unsupported provider",await h.Html());await h.Event("Edit","minimal");Assert.Equal(0,h.Fixture.Provider.Edits);
        h.Fixture.Provider.Unsupported=false;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var resume=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Fixture.Provider.During=async _=>{entered.TrySetResult();await resume.Task;};var editing=h.Event("Edit","darker");await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("Working",await h.Html());await h.Event("Cancel");resume.TrySetResult();await editing;
        Assert.DoesNotContain("Original is concept 1",await h.Html());Assert.Contains("Canceled",await h.Html());Assert.Null((await h.Fixture.Documents.LoadAsync(h.Fixture.Source.DocumentId))!.Project!.CoverImageUrl);
    }
}
