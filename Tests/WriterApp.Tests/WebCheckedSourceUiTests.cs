using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;
public sealed class WebCheckedSourceUiTests
{
    [Theory][InlineData("expand.section")][InlineData("scene.suggest")]
    public async Task CheckedWebReviewShowsInertReadableOriginalAndProposedData(string key)
    {
        using var services=new ServiceCollection().AddLogging().BuildServiceProvider();await using var renderer=new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        string original=key.StartsWith("scene")?"{\"summary\":\"Authored 日本語\"}":"Authored 日本語 🧭";
        string proposed=key.StartsWith("scene")?"{\"summary\":\"Reviewed <script>alert(1)</script>\",\"explanation\":\"Readable reasoning\"}":"Reviewed <script>alert(1)</script>";
        string html=await renderer.Dispatcher.InvokeAsync(async ()=> (await renderer.RenderComponentAsync<AiResultPreview>(ParameterView.FromDictionary(new Dictionary<string,object?>{{"ActionKey",key},{"Original",original},{"Proposed",proposed}}))).ToHtmlString());
        Assert.Contains("Original",html);Assert.Contains("Proposed",html);Assert.Contains("Reviewed &lt;script&gt;",html);Assert.DoesNotContain("<script>",html);
        var root=Environment.GetEnvironmentVariable("WRITERAPP_P19_EVIDENCE");if(root is not null){Directory.CreateDirectory(Path.Combine(root,"p19"));await File.WriteAllTextAsync(Path.Combine(root,"p19",key+"-checked-review.html"),html);}
    }
}
