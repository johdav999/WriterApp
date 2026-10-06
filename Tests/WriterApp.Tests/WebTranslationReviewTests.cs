using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class WebTranslationReviewTests
{
    [Fact]
    public async Task SharedReviewDisplaysEveryPageAndBlankPageAsInertReadableOriginalAndProposedText() {
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        var root=await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<TranslationProposalPanel>(
            ParameterView.FromDictionary(new Dictionary<string,object?> {
                [nameof(TranslationProposalPanel.Pages)]=new TranslationPageReview[] {
                    new("Section 1 / Page 1","Åsa 日本語 🧭","EN Åsa 日本語 🧭"),new("Section 1 / Page 2","",""),
                    new("Section 2 / Page 1","Keep <strong>voice</strong>","<script>inert provider text</script>") }
            })));
        string html=await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);var parsed=new HtmlParser().ParseDocument(html);
        Assert.Equal(3,parsed.QuerySelectorAll("h3").Length);Assert.Equal(6,parsed.QuerySelectorAll(".translation-proposal-cell").Length);
        Assert.Equal(2,parsed.QuerySelectorAll(".translation-proposal-text").Count(e => e.TextContent=="(Blank page)"));
        Assert.Empty(parsed.QuerySelectorAll("script,strong"));Assert.Contains("<script>inert provider text</script>",parsed.Body!.TextContent);
        var evidence=Environment.GetEnvironmentVariable("WRITERAPP_P14_EVIDENCE");
        if(evidence is not null) {
            var folder=Path.Combine(evidence,"p14");Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(Path.Combine(folder,"web-translation-review.html"),html);
        }
    }
}
