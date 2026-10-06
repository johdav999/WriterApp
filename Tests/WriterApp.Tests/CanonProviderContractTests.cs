using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using Xunit;

namespace WriterApp.Tests;

public sealed class CanonProviderContractTests
{
    [Theory]
    [InlineData("place", false, "BuildPlaceBibleRequest")]
    [InlineData("timeline", false, "BuildTimelineBibleRequest")]
    [InlineData("place", true, "BuildBibleRefreshRequest")]
    [InlineData("timeline", true, "BuildBibleRefreshRequest")]
    public async Task RepairReachesProviderForExtractionAndIncrementalRefresh(string kind, bool incremental, string method)
    {
        IAiAction action = (kind, incremental) switch {
            ("place", false) => new ExtractPlaceBibleAction(), ("place", true) => new RefreshPlaceBibleAction(),
            ("timeline", false) => new ExtractTimelineBibleAction(), _ => new RefreshTimelineBibleAction()
        };
        var document = DocumentFactory.CreateNewDocument();
        var request = action.BuildRequest(new AiActionInput(document, document.Chapters[0].Sections[0].SectionId,
            new TextRange(0, 0), "", "", new() {
                ["repair_invalid_json"] = true, ["invalid_json_payload"] = "prior-invalid-output",
                ["invalid_json_failure_reason"] = "entry needs a name", ["existing_bible_json"] = "{}", ["delta_sections_json"] = "[]"
            }));
        var key = (OpenAiKeyProvider)Activator.CreateInstance(typeof(OpenAiKeyProvider), BindingFlags.NonPublic | BindingFlags.Instance,
            null, ["synthetic-test-key"], null)!;
        var provider = new OpenAiProvider(new NoNetworkFactory(), Options.Create(new WriterAiOptions()), key, NullLogger<OpenAiProvider>.Instance);
        using var message = (HttpRequestMessage)typeof(OpenAiProvider).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(provider, [request, "synthetic-test-key"])!;
        using var payload = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
        string prompt = payload.RootElement.GetProperty("input")[1].GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Contains("prior-invalid-output", prompt);
        Assert.Contains("entry needs a name", prompt);
        Assert.Contains(request.Inputs["output_contract"].ToString()!, prompt);
    }

    private sealed class NoNetworkFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("This test must not contact a provider.");
    }
}
