using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class ConsistencyPassageUiTests
{
    [Theory][InlineData("located")][InlineData("ambiguous")][InlineData("missing")]
    public async Task ConsistencyPagePresentationShowsCheckedLocationOrActionableErrorAndInertEvidence(string state)
    {
        string? error = state switch { "ambiguous" => "The primary passage is ambiguous across the checked pages. Use a unique passage and run the check again.",
            "missing" => "The primary passage is missing from the checked pages. Run the consistency check again.", _ => null };
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<ConsistencyPrimaryPassageView>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(ConsistencyPrimaryPassageView.Location)] = error is null ? "Page 2 · Åsa 日本語 🧭" : "Primary passage unavailable",
                [nameof(ConsistencyPrimaryPassageView.Quote)] = "The clock stood beside the clock.\n<script>alert('inert')</script>", [nameof(ConsistencyPrimaryPassageView.Error)] = error }))).ToHtmlString());
        Assert.Contains("Passage in your writing", html); Assert.DoesNotContain("<script>", html); Assert.DoesNotContain("@if", html);
        if (error is null) { Assert.Contains("Page 2", html); Assert.DoesNotContain("role=\"status\"", html); }
        else { Assert.Contains("role=\"status\"", html); Assert.Contains(error, html); }
        var evidence = Environment.GetEnvironmentVariable("WRITERAPP_PARITY_P05_EVIDENCE");
        if (!string.IsNullOrWhiteSpace(evidence)) {
            Directory.CreateDirectory(evidence); await File.WriteAllTextAsync(Path.Combine(evidence, state + ".html"), html);
        }
    }
}
