using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Core;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed class RecommendedWritingExecutorTests
{
    private static AiActionInput Input(string tool) {
        var doc = DocumentFactory.CreateNewDocument(); var section = doc.Chapters[0].Sections[0].SectionId;
        var parameters = RecommendedWriting.Parameters(tool);
        parameters[WritingActions.Parameter] = WritingActions.Serialize(new(1, doc.DocumentId, section, [new(Guid.NewGuid(), [new("0.0", "The visitor waited.")])]));
        parameters["section_text_override"] = "The visitor waited.";
        return new(doc, section, new TextRange(0, 0), "", "", parameters);
    }
    private static string Output(AiRequest request) => request.Inputs.ContainsKey("structured_writing") ? request.Context.OriginalText
        : JsonSerializer.Serialize(new { items = Enumerable.Range(1, RecommendedWriting.ItemCount((string)request.Inputs["recommended_tool"])).Select(i => "A messenger brought a sealed letter " + i + ".").ToArray() });
    private sealed class SyntheticProvider(bool invalid) : IAiProvider {
        public string ProviderId => "recommended-test"; public AiProviderCapabilities Capabilities => new(true, false);
        public Task<AiResult> ExecuteAsync(AiRequest request, CancellationToken ct) => Task.FromResult(new AiResult(request.RequestId, [new(Guid.NewGuid(), AiModality.Text, "application/json", invalid ? "{}" : Output(request), null, null)], new(0, 0, TimeSpan.Zero), new()));
    }
    [Theory][MemberData(nameof(RecommendedWritingTests.Catalog), MemberType = typeof(RecommendedWritingTests))]
    public async Task RecommendedRealExecutorValidatesCatalogAndNeverCreatesFlatReplacementOperations(string tool) {
        var registry = new DefaultAiProviderRegistry([new SyntheticProvider(false)]);
        var router = new DefaultAiRouter(registry, Options.Create(new WriterAiOptions { Enabled = true }), NullLogger<DefaultAiRouter>.Instance);
        var outcome = await new AiActionExecutor(router, new InMemoryArtifactStore(), NullLogger<AiActionExecutor>.Instance).ExecuteAsync(new CustomTransformAction(), Input(tool), default);
        Assert.NotNull(outcome.Proposal); Assert.Empty(outcome.Proposal!.Operations);
    }
    [Theory][InlineData("novel.deepen_character")][InlineData("novel.continue_scene")][InlineData("blog.generate_headlines")][InlineData("other.summarize_clearly")]
    public async Task RecommendedRealExecutorRejectsMalformedOutputWithoutAnApplicableProposal(string tool) {
        var registry = new DefaultAiProviderRegistry([new SyntheticProvider(true)]);
        var router = new DefaultAiRouter(registry, Options.Create(new WriterAiOptions { Enabled = true }), NullLogger<DefaultAiRouter>.Instance);
        var outcome = await new AiActionExecutor(router, new InMemoryArtifactStore(), NullLogger<AiActionExecutor>.Instance).ExecuteAsync(new CustomTransformAction(), Input(tool), default);
        Assert.Null(outcome.Proposal); Assert.Equal("ai.recommendation_rejected", outcome.ErrorCode);
    }
    private sealed class Wire(string output, string status) : HttpMessageHandler, IHttpClientFactory {
        public JsonDocument? Request; public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            Request = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { status, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = output } } } }, usage = new { input_tokens = 1, output_tokens = 1 } }) };
        }
    }
    private static OpenAiProvider Provider(Wire wire) {
        var key = (OpenAiKeyProvider)Activator.CreateInstance(typeof(OpenAiKeyProvider), BindingFlags.NonPublic | BindingFlags.Instance, null, ["synthetic-test-key"], null)!;
        return new(wire, Options.Create(new WriterAiOptions()), key, NullLogger<OpenAiProvider>.Instance);
    }
    [Theory][InlineData("blog.generate_headlines", "completed")][InlineData("other.summarize_clearly", "completed")][InlineData("novel.continue_scene", "completed")][InlineData("blog.generate_headlines", "incomplete")]
    public async Task RecommendedProviderUsesCatalogSystemTemplateAndStrictOutputSpecificSchema(string tool, string status) {
        var request = new CustomTransformAction().BuildRequest(Input(tool)); using var wire = new Wire(Output(request), status);
        if (status == "completed") Assert.Equal(Output(request), Assert.Single((await Provider(wire).ExecuteAsync(request, default)).Artifacts).TextContent);
        else await Assert.ThrowsAsync<AiProviderException>(() => Provider(wire).ExecuteAsync(request, default));
        var payload = wire.Request!.RootElement;
        Assert.Contains(RecommendedWriting.Tool(tool).PromptTemplate.SystemTemplate, payload.GetProperty("input")[0].GetProperty("content")[0].GetProperty("text").GetString());
        Assert.Contains(RecommendedWriting.Tool(tool).PromptTemplate.UserTemplate.Split("Context:")[0].Trim(), payload.GetProperty("input")[1].GetProperty("content")[0].GetProperty("text").GetString());
        var format = payload.GetProperty("text").GetProperty("format"); Assert.True(format.GetProperty("strict").GetBoolean());
        Assert.Equal(RecommendedWriting.ItemCount(tool), format.GetProperty("schema").GetProperty("properties").GetProperty("items").GetProperty("maxItems").GetInt32());
    }
}
