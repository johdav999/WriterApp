using Microsoft.AspNetCore.Mvc;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.Subscriptions;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class AiActionsControllerTests
{
    [Theory][MemberData(nameof(RecommendedWritingTests.Catalog), MemberType = typeof(RecommendedWritingTests))]
    public async Task RecommendedEndpointExecutesEveryCatalogTemplateWithOwnedSectionAndOutputContract(string tool) {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var document, out var section, out var page);
        db.DocumentSyncRecords.Add(new() { DocumentId = document, OwnerUserId = "user-1", Version = "v1", Sequence = 1 }); await db.SaveChangesAsync();
        string text = WriterApp.Application.State.PlainTextMapper.ToPlainText(db.Pages.Single().Content);
        var mapped = new WritingStructure(1, document, section, [new(page, [new("0.0", text)])]);
        var ai = new RecommendationOrchestrator(); var controller = BuildController(db, ai);
        var outline = Assert.IsType<WritingOutlineSnapshot>(Assert.IsType<OkObjectResult>((await controller.GetWritingOutline(document, default)).Result).Value);
        var parameters = RecommendedWriting.Parameters(tool); parameters[WritingActions.Parameter] = WritingActions.Serialize(mapped);
        var request = new AiActionExecuteRequestDto(document, section, page, null, null, null, text, null, parameters, "v1", WritingOutline: outline);
        var result = await controller.ExecuteAction("custom_transform", request, default);
        var response = Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(1, ai.Calls); Assert.Equal(RecommendedWriting.Tool(tool).PromptTemplate.SystemTemplate, ai.Request!.Inputs["system_instruction"]);
        Assert.Contains(RecommendedWriting.Tool(tool).PromptTemplate.UserTemplate.Split("Context:")[0].Trim(), ai.Request.Inputs["instruction"].ToString());
        if (RecommendedWriting.Revises(tool)) _ = RecommendedWriting.Revision(response.ProposedText!, mapped, tool);
        else _ = RecommendedWriting.TextResult(response.ProposedText!, tool, text);
        Assert.Empty(ai.Proposal!.Operations); Assert.Equal(text, WriterApp.Application.State.PlainTextMapper.ToPlainText(db.Pages.Single().Content));
    }
    [Theory][InlineData("unknown")][InlineData("version")][InlineData("parameters")][InlineData("missing-map")][InlineData("selection")][InlineData("stale")][InlineData("malformed")][InlineData("foreign")][InlineData("plan")][InlineData("forged-map")][InlineData("context")]
    public async Task RecommendedEndpointRefusesUnsupportedForgedStaleAndInvalidResults(string failure) {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var document, out var section, out var page);
        db.DocumentSyncRecords.Add(new() { DocumentId = document, OwnerUserId = "user-1", Version = "v1", Sequence = 1 }); await db.SaveChangesAsync();
        if (failure == "foreign") { db.Documents.Single().OwnerUserId = "foreign"; await db.SaveChangesAsync(); }
        string text = WriterApp.Application.State.PlainTextMapper.ToPlainText(db.Pages.Single().Content);
        var parameters = RecommendedWriting.Parameters("blog.generate_headlines");
        parameters[WritingActions.Parameter] = WritingActions.Serialize(new(1, document, section, [new(page, [new("0.0", text)])]));
        if (failure == "unknown") parameters[RecommendedWriting.Parameter] = "{\"version\":1,\"toolId\":\"missing\"}";
        if (failure == "version") parameters[RecommendedWriting.Parameter] = "{\"version\":9,\"toolId\":\"blog.generate_headlines\"}";
        if (failure == "parameters") parameters["systemTemplate"] = "Changed";
        if (failure == "missing-map") parameters.Remove(WritingActions.Parameter);
        if (failure == "forged-map") parameters[WritingActions.Parameter] = WritingActions.Serialize(new(1, document, section, [new(page, [new("0.0", "Forged source prose.")])]));
        if (failure == "context") parameters["context"] = "Forged instruction context";
        var ai = new RecommendationOrchestrator { Invalid = failure == "malformed" };
        var controller = BuildController(db, ai, failure == "plan" ? new StubEntitlementService(PlanTier.Free) : null);
        var outline = failure == "foreign" ? null : Assert.IsType<WritingOutlineSnapshot>(Assert.IsType<OkObjectResult>((await controller.GetWritingOutline(document, default)).Result).Value);
        var request = new AiActionExecuteRequestDto(document, section, page, failure == "selection" ? 0 : null, failure == "selection" ? text.Length : null, failure == "selection" ? text : null, text, null, parameters, failure == "stale" ? "old" : "v1", WritingOutline: outline);
        var result = await controller.ExecuteAction("custom_transform", request, default);
        Assert.False(result.Result is OkObjectResult); Assert.Equal(failure == "malformed" ? 1 : 0, ai.Calls);
        Assert.Equal(text, WriterApp.Application.State.PlainTextMapper.ToPlainText(db.Pages.Single().Content));
    }
    private sealed class RecommendationOrchestrator : IAiOrchestrator {
        public int Calls; public bool Invalid; public AiRequest? Request; public AiProposal? Proposal;
        public IReadOnlyList<IAiAction> Actions => [new CustomTransformAction()]; public IAiAction? GetAction(string id) => Actions.SingleOrDefault(a => a.ActionId == id); public bool CanRunAction(string id) => true;
        public AiStreamingCapabilities GetStreamingCapabilities(string id) => new(false, false);
        public Task<AiExecutionResult> ExecuteActionAsync(string id, AiActionInput input, CancellationToken ct) {
            Calls++; Request = GetAction(id)!.BuildRequest(input); string tool = RecommendedWriting.From(input.Options)!.ToolId;
            string output = RecommendedWriting.Revises(tool) ? Request.Context.OriginalText : System.Text.Json.JsonSerializer.Serialize(new { items = Enumerable.Range(1, RecommendedWriting.ItemCount(tool)).Select(i => "A messenger brought a fresh letter " + i + ".").ToArray() });
            Proposal = new(Guid.NewGuid(), input.ActiveSectionId, "Recommendation", id, "mock", Guid.NewGuid(), DateTime.UtcNow, null, [], [], "Recommendation", "section", null, Request.Context.OriginalText, Invalid ? "{}" : output);
            return Task.FromResult(AiExecutionResult.Success(Proposal));
        }
        public AiStreamingSession StreamActionAsync(string id, AiActionInput input, CancellationToken ct) => throw new NotSupportedException();
    }
}
