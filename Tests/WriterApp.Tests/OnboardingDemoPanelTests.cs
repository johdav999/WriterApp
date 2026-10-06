using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class OnboardingDemoPanelTests
{
    [Theory][InlineData("signed-out")][InlineData("available")][InlineData("used")][InlineData("completed")][InlineData("loading")]
    public async Task ActualSharedDemoPanelKeepsExplicitChoicesAndReportsServerState(string state) {
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var status=new OnboardingDemoStatus(1,new string('a',64),state=="completed",state=="completed"?10:2,
            new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid()),state=="available",state=="used"?"demo-already-used":"onboarding-complete",
            OnboardingAiDemoRequest.ActionKey,"section",state=="used",null,DateTimeOffset.UtcNow.AddDays(1),true);
        var root=await renderer.Dispatcher.InvokeAsync(()=>renderer.RenderComponentAsync<OnboardingDemoPanel>(ParameterView.FromDictionary(new Dictionary<string,object?> {
            [nameof(OnboardingDemoPanel.SignedIn)]=state!="signed-out",[nameof(OnboardingDemoPanel.Busy)]=state=="loading",
            [nameof(OnboardingDemoPanel.Status)]=status,[nameof(OnboardingDemoPanel.WorkspaceUrl)]="/documents/demo/tools"
        })));
        string html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);var parsed=new HtmlParser().ParseDocument(html);
        if(state=="signed-out"){Assert.Empty(parsed.QuerySelectorAll("button"));Assert.Contains("local guide offline",parsed.Body!.TextContent);}
        if(state=="available")Assert.Contains("One server-authorized",parsed.Body!.TextContent);
        if(state=="used")Assert.Contains("Request used",parsed.Body!.TextContent);
        if(state=="completed")Assert.DoesNotContain("Complete online onboarding explicitly",parsed.Body!.TextContent);
        if(state=="loading")Assert.All(parsed.QuerySelectorAll("button"),button=>Assert.True(button.HasAttribute("disabled")));
        if(Environment.GetEnvironmentVariable("WRITERAPP_P15_EVIDENCE") is { } evidence) {
            var folder=Path.Combine(evidence,"p15");Directory.CreateDirectory(folder);await File.WriteAllTextAsync(Path.Combine(folder,"demo-"+state+".html"),html);
        }
    }
}
