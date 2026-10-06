using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Commands;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class StructuredWritingProviderTests
{
    private sealed class Wire(string output, string status = "completed") : HttpMessageHandler, IHttpClientFactory
    {
        public JsonDocument? Request;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri);
            Request = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                status, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = output } } } },
                usage = new { input_tokens = 100, output_tokens = 200 }
            }) };
        }
    }
    private static WritingStructure Source() => new(1, Guid.NewGuid(), Guid.NewGuid(), [
        new(Guid.NewGuid(), [new("0.0", "Elin watched the trees."), new("0.1", " "), new("1.0", "The track was quiet.")]),
        new(Guid.NewGuid(), []),
        new(Guid.NewGuid(), [new("0.0", " Åsa waited. ")])
    ]);
    private static AiRequest Request(WritingStructure source, bool structured = true) => new(Guid.NewGuid(), "custom_transform", [AiModality.Text],
        new(source.DocumentId, source.SectionId, new TextRange(0, 1), WritingActions.Serialize(source), "Test", null, null, "en", null, null, null, null, null, null),
        new() { ["instruction"] = "Add something moving in the dark forest.", ["structured_writing"] = structured }, new(), new());
    private static OpenAiProvider Provider(Wire wire)
    {
        var key = (OpenAiKeyProvider)Activator.CreateInstance(typeof(OpenAiKeyProvider), BindingFlags.NonPublic | BindingFlags.Instance,
            null, ["synthetic-test-key"], null)!;
        return new(wire, Options.Create(new WriterAiOptions()), key, NullLogger<OpenAiProvider>.Instance);
    }
    [Fact]
    public async Task RequiredRunSchemaRestoresEveryPageAndRunIncludingWhitespaceAndEmptyPages()
    {
        // Wire order differs deliberately: identity must come from the captured source, not the model.
        using var wire = new Wire("""{"runs":{"p2_r0":" Åsa listened. ","p0_r2":"Something moved beyond the track.","p0_r1":" ","p0_r0":"Elin watched the dark trees."}}""");
        var source = Source();
        var result = await Provider(wire).ExecuteAsync(Request(source), default);
        var revision = WritingActions.Result(Assert.Single(result.Artifacts).TextContent!, source);
        Assert.Equal(source.Pages.Select(p => p.Id), revision.Pages.Select(p => p.Id));
        Assert.Empty(revision.Pages[1].Runs);
        Assert.Equal(source.Pages[0].Runs.Select(r => r.Id), revision.Pages[0].Runs.Select(r => r.Id));
        Assert.Equal("Something moved beyond the track.", revision.Pages[0].Runs[2].Text);
        Assert.Equal(" ", revision.Pages[0].Runs[1].Text);
        Assert.Equal(" Åsa listened. ", revision.Pages[2].Runs[0].Text);
        var request = wire.Request!.RootElement;
        var format = request.GetProperty("text").GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        var schema = format.GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        var runs = schema.GetProperty("properties").GetProperty("runs");
        Assert.False(runs.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "p0_r0", "p0_r1", "p0_r2", "p2_r0" }, runs.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.True(request.GetProperty("max_output_tokens").GetInt32() > 800);
        var user = request.GetProperty("input")[1].GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Contains("Åsa", user); // The model receives Unicode, not a nested JSON escape sequence.
        using var wireSource = JsonDocument.Parse(user[(user.LastIndexOf(":\n", StringComparison.Ordinal) + 2)..]);
        Assert.Equal("p2_r0", wireSource.RootElement.GetProperty("pages")[2].GetProperty("runs")[0].GetProperty("key").GetString());
        Assert.Equal(" Åsa waited. ", wireSource.RootElement.GetProperty("pages")[2].GetProperty("runs")[0].GetProperty("text").GetString());
        Assert.Equal(200, result.Usage.OutputTokens);
    }
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"runs\":{\"p0_r0\":\"Elin waited.\"}}")]
    [InlineData("{\"runs\":{\"p0_r0\":\"Elin waited.\",\"p0_r1\":\" \",\"p0_r2\":\"The track was quiet.\",\"wrong\":\" Åsa waited. \"}}")]
    [InlineData("{\"runs\":{\"p0_r0\":\"Elin waited.\",\"p0_r0\":\"Elin waited.\",\"p0_r1\":\" \",\"p0_r2\":\"The track was quiet.\",\"p2_r0\":\" Åsa waited. \"}}")]
    [InlineData("{\"runs\":{\"p0_r0\":\"Elin waited.\",\"p0_r1\":\" \",\"p0_r2\":\"The track was quiet.\",\"p2_r0\":\"Åsa waited.\"}}")]
    [InlineData("{\"runs\":{\"p0_r0\":\"Here is the revised text:\",\"p0_r1\":\" \",\"p0_r2\":\"The track was quiet.\",\"p2_r0\":\" Åsa waited. \"}}")]
    [InlineData("{\"runs\":{\"p0_r0\":\"Elin waited.\",\"p0_r1\":\" \",\"p0_r2\":\"The track was quiet.\",\"p2_r0\":\" \\u0000C5sa waited. \"}}")]
    [InlineData("{\"runs\":{\"p0_r0\":\"Elin waited.\",\"p0_r1\":\" \",\"p0_r2\":\"The track was quiet.\",\"p2_r0\":\" Åsa waited. \"},\"extra\":true}")]
    public async Task InvalidResponsesNeverBecomeWritingArtifacts(string output)
    {
        using var wire = new Wire(output);
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Provider(wire).ExecuteAsync(Request(Source()), default));
        Assert.Equal("OpenAI returned an invalid structured writing revision.", error.Message);
    }
    [Fact]
    public async Task IncompleteResponseCannotBecomeARevisionEvenWithParseableText()
    {
        using var wire = new Wire("""{"runs":{"p0_r0":"Elin waited.","p0_r1":" ","p0_r2":"The track was quiet.","p2_r0":" Åsa waited. "}}""", "incomplete");
        await Assert.ThrowsAsync<AiProviderException>(() => Provider(wire).ExecuteAsync(Request(Source()), default));
    }
    [Fact]
    public async Task StreamingDeliversOnlyCompleteRestoredEditorStructure()
    {
        using var wire = new Wire("""{"runs":{"p0_r0":"Elin watched the dark trees.","p0_r1":" ","p0_r2":"Something moved beyond the track.","p2_r0":" Åsa waited. "}}""");
        var source = Source();
        var events = new List<AiStreamEvent>();
        await foreach (var item in Provider(wire).StreamAsync(Request(source) with { ActionId = "expand.section" }, default)) events.Add(item);
        Assert.IsType<AiStreamEvent.Started>(events[0]);
        var delta = Assert.IsType<AiStreamEvent.TextDelta>(events[1]);
        _ = WritingActions.Result(delta.Delta, source);
        Assert.IsType<AiStreamEvent.Completed>(events[2]);
        Assert.Equal(3, events.Count);
        Assert.False(wire.Request!.RootElement.TryGetProperty("stream", out _));
    }
    [Fact]
    public async Task SelectionRequestsKeepPlainTextOutputAndConfiguredBudget()
    {
        using var wire = new Wire("Elin watched something move.");
        var result = await Provider(wire).ExecuteAsync(Request(Source(), false), default);
        Assert.Equal("Elin watched something move.", Assert.Single(result.Artifacts).TextContent);
        Assert.False(wire.Request!.RootElement.TryGetProperty("text", out _));
        Assert.Equal(800, wire.Request.RootElement.GetProperty("max_output_tokens").GetInt32());
    }
}
