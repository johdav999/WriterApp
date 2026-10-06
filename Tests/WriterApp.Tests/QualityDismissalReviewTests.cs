using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Shared.Quality;
using WriterApp.UI.Shared.Components;
using Xunit;

namespace WriterApp.Tests;

public sealed class QualityDismissalReviewTests
{
    [Theory]
    [InlineData("synced")][InlineData("pending")][InlineData("failed")][InlineData("unmapped")][InlineData("conflict")]
    [InlineData("inactive")][InlineData("busy")][InlineData("restore-pending")]
    public async Task CompiledReviewShowsExactSourceStatusEncodedEvidenceAndReachableRestore(string state)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string status = state switch { "synced" => "Synced", "pending" => "Pending; delivery pending", "failed" => "Failed; delivery pending",
            "unmapped" => "Unmapped; delivery pending", "conflict" => "Conflict; delivery pending", _ => state };
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<QualityDismissalReview>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(QualityDismissalReview.Items)] = new QualityDismissedFinding[] { new("id", "Repeated words", "Review this exact occurrence.", "<script>inert</script> 🧭 " + new string('x', 180), status, state != "inactive", state != "restore-pending") },
            [nameof(QualityDismissalReview.Message)] = "Synthetic persisted state: " + state,
            [nameof(QualityDismissalReview.Busy)] = state == "busy"
        })));
        string html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        var parsed = new HtmlParser().ParseDocument(html);
        Assert.Empty(parsed.QuerySelectorAll("script")); Assert.Contains("<script>inert</script>", parsed.Body!.TextContent);
        Assert.Contains(status, parsed.Body.TextContent);
        if (state == "restore-pending") { Assert.Empty(parsed.QuerySelectorAll("button")); Assert.Contains("Restored locally", parsed.Body.TextContent); }
        else { var button = Assert.Single(parsed.QuerySelectorAll("button")); Assert.Equal(state == "busy", button.HasAttribute("disabled")); Assert.Equal("Restore finding", button.TextContent); }
        if (state == "inactive") Assert.Contains("does not hide current findings", parsed.Body.TextContent);
        if (Environment.GetEnvironmentVariable("WRITERAPP_P10_EVIDENCE") is { } path)
            await File.WriteAllTextAsync(Path.Combine(path, "review-" + state + ".html"), html);
    }
}
