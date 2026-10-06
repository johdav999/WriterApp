using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Commands;
using WriterApp.Controllers;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class StyleQualityProviderTests
{
    private sealed class Wire(string output, string status = "completed") : HttpMessageHandler, IHttpClientFactory
    {
        public JsonDocument? Request;
        public int Calls;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Request = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new {
                status, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = output } } } },
                usage = new { input_tokens = 100, output_tokens = 1600 }
            }) };
        }
    }

    private static AiRequest Request(string source) => new(Guid.NewGuid(), "custom_transform", [AiModality.Text],
        new(Guid.NewGuid(), Guid.NewGuid(), new TextRange(0, source.Length), source, "Test", null, null, "en", source, 0, source.Length, null, null, null),
        new() { ["instruction"] = StyleQualityReview.Instruction("polish"), ["style_quality_review"] = true }, new(), new());

    private static OpenAiProvider Provider(Wire wire)
    {
        var key = (OpenAiKeyProvider)Activator.CreateInstance(typeof(OpenAiKeyProvider), BindingFlags.NonPublic | BindingFlags.Instance,
            null, ["synthetic-test-key"], null)!;
        return new(wire, Options.Create(new WriterAiOptions()), key, NullLogger<OpenAiProvider>.Instance);
    }

    [Theory]
    [InlineData(1774)][InlineData(60000)]
    public async Task ExplainedReviewUsesRequiredFieldsAndBoundedCapacityForFindings(int length)
    {
        string source = "Åsa walked slowly." + new string(' ', length - 18);
        using var wire = new Wire("""{"edits":[{"original":"Åsa walked slowly.","replacement":"Åsa walked.","criterion":"word_choice","kind":"preference","reason":"Remove an adverb.","tradeoff":"Less emphasis on pace."}]}""");
        var result = await Provider(wire).ExecuteAsync(Request(source), default);
        Assert.Single(StyleQualityReview.Parse(Assert.Single(result.Artifacts).TextContent!, source).Edits);
        var payload = wire.Request!.RootElement;
        var format = payload.GetProperty("text").GetProperty("format");
        Assert.Equal("json_schema", format.GetProperty("type").GetString());
        Assert.True(format.GetProperty("strict").GetBoolean());
        var schema = format.GetProperty("schema");
        Assert.False(schema.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal("edits", schema.GetProperty("required")[0].GetString());
        var edits = schema.GetProperty("properties").GetProperty("edits");
        Assert.Equal(24, edits.GetProperty("maxItems").GetInt32());
        var item = edits.GetProperty("items");
        Assert.False(item.GetProperty("additionalProperties").GetBoolean());
        Assert.Equal(new[] { "original", "replacement", "criterion", "kind", "reason", "tradeoff" },
            item.GetProperty("required").EnumerateArray().Select(x => x.GetString()));
        Assert.Equal(StyleQualityReview.CriteriaKeys, item.GetProperty("properties").GetProperty("criterion").GetProperty("enum").EnumerateArray().Select(x => x.GetString()));
        Assert.InRange(payload.GetProperty("max_output_tokens").GetInt32(), 8192, 32768);
        Assert.Equal(1600, result.Usage.OutputTokens);
        Assert.Equal(1, wire.Calls);
    }

    [Fact]
    public async Task EmptyCompletedReportIsValid()
    {
        using var wire = new Wire("""{"edits":[]}""");
        var result = await Provider(wire).ExecuteAsync(Request("Åsa waited."), default);
        Assert.Empty(StyleQualityReview.Parse(Assert.Single(result.Artifacts).TextContent!, "Åsa waited.").Edits);
    }

    [Theory]
    [InlineData("incomplete")][InlineData("failed")]
    public async Task UnfinishedResponseCannotBecomeAReviewEvenWhenJsonParses(string status)
    {
        using var wire = new Wire("""{"edits":[]}""", status);
        var error = await Assert.ThrowsAsync<AiProviderException>(() => Provider(wire).ExecuteAsync(Request("Åsa waited."), default));
        Assert.Equal("OpenAI returned an incomplete style review.", error.Message);
        Assert.Equal(1, wire.Calls);
        var mapped = ((int Status, string Code, string Detail))typeof(AiActionsController)
            .GetMethod("MapProviderException", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [error])!;
        Assert.Equal(502, mapped.Status);
        Assert.Equal("ai.style_review_incomplete", mapped.Code);
    }

    [Fact]
    public void RejectedProviderFindingsAreAResultFailure()
    {
        int status = (int)typeof(AiActionsController).GetMethod("MapBlockedErrorStatusCode", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, ["ai.style_review_rejected"])!;
        Assert.Equal(502, status);
    }
}
