using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalWritingPanelTests
{
    [Theory][InlineData("available")][InlineData("used")][InlineData("foreign")][InlineData("stale-apply")]
    public async Task ActualWritingDemoHandlerUsesVerifiedPolicyKeepsRealQuotaAndExplicitReviewApply(string scenario) {
        await using var h=new Harness();await h.Start(practice:true);
        h.Api.Free=true;h.Api.Available=false;
        var section=h.Source.Sections[0];
        h.Api.Demo=new(1,new string('a',64),false,2,new(Guid.NewGuid(),scenario=="foreign" ? Guid.NewGuid() : h.Source.ServerDocumentId!.Value,
            section.ServerSectionId!.Value,Guid.NewGuid()),scenario!="used","demo-scene",OnboardingAiDemoRequest.ActionKey,"section",scenario=="used",null,DateTimeOffset.UtcNow.AddDays(1),true);
        await h.Event("Refresh");
        Assert.Equal(0,(await h.Api.GetUsageAsync(default)).QuotaRemaining);
        await h.Event("Generate","rewrite.selection");Assert.Equal(0,h.Api.Calls);
        var before=LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!);
        await h.Event("GenerateDemo");
        if(scenario is "used" or "foreign"){Assert.Equal(0,h.Api.Calls);Assert.Equal(before,LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));return;}
        Assert.Equal(1,h.Api.Calls);Assert.True(OnboardingAiDemoRequest.IsRequested(OnboardingAiDemoRequest.ActionKey,h.Api.Last!.Parameters));
        Assert.Contains("Review before applying",await h.Html());Assert.Equal(before,LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        if(Environment.GetEnvironmentVariable("WRITERAPP_P15_EVIDENCE") is { } evidence) {
            var folder=Path.Combine(evidence,"p15");Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"demo-writing-review.html"),await h.Html());
        }
        if(scenario=="stale-apply"){
            var current=(await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
            await h.Fixture.Repository.SaveAsync(current with{Sections=current.Sections.Select(s=>s with{Pages=s.Pages.Select(p=>p with{Content="<p>Later authored edit.</p>"}).ToArray()}).ToArray()});
            await h.Event("Apply");Assert.Contains("Later authored edit",(await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections[0].Pages[0].Content);
            Assert.DoesNotContain(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");
        }else {
            await h.Event("Apply");var entry=Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId),e=>e.Status=="Applied");
            await new LocalAiHistoryActions(h.Fixture.Repository,h.Fixture.History).ChangeAsync(h.Source.DocumentId,entry.Id,false);
            Assert.Equal(h.Source.Sections.SelectMany(s=>s.Pages).Select(p=>p.Content),(await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s=>s.Pages).Select(p=>p.Content));
        }
        Assert.Equal(0,(await h.Api.GetUsageAsync(default)).QuotaRemaining);
    }
}
