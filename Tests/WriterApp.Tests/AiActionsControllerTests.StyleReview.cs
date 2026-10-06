using System.Reflection;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.AI;
using WriterApp.Controllers;
using WriterApp.Data.Documents;
using WriterApp.Shared;
using WriterApp.Shared.Sync;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class AiActionsControllerTests
{
    [Theory][InlineData("valid")][InlineData("rich")][InlineData("offset")][InlineData("scope")][InlineData("goal")][InlineData("empty")][InlineData("wrong-action")]
    public async Task WebStyleInputRequiresAnExactSavedRangeAndSupportedSelectionContract(string change)
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var document, out var section, out var page);
        string html = change == "rich" ? "<p>First passage.<br>Earlier line.</p><p></p><p>Second passage.</p><p></p>" : "<p>First passage. Second passage.</p>";
        string text = WriterApp.Application.State.PlainTextMapper.ToPlainText(html);
        db.Pages.Single(p => p.Id == page).Content = html; await db.SaveChangesAsync();
        foreach (var sql in DocumentSyncSchemaV1.Install(false)) await db.Database.ExecuteSqlRawAsync(sql);
        var source = await new WebAiSourceService(db, "user-1").Capture(document, section, page, null, default);
        var controller = BuildController(db, new StubAiOrchestrator(true, false));
        var request = new AiActionExecuteRequestDto(document, section, page, text.IndexOf("Second", StringComparison.Ordinal), text.Length, "Second passage.", "Forged surrounding prose.", null,
            new() { ["scope"] = "selection", ["template"] = "Review style", [StyleQualityReview.Parameter] = "polish" }, WebSource: source);
        if (change == "offset") request = request with { SelectionStart = 0, SelectionEnd = 15 };
        if (change == "scope") request.Parameters!["scope"] = "section";
        if (change == "goal") request.Parameters![StyleQualityReview.Parameter] = "unknown";
        if (change == "empty") request = request with { SelectionEnd = request.SelectionStart };
        string action = change == "wrong-action" ? "rewrite.selection" : "custom_transform";
        var method = typeof(AiActionsController).GetMethod("PrepareWebInput", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Task<AiActionExecuteRequestDto> Run() => (Task<AiActionExecuteRequestDto>)method.Invoke(controller, [action, request, section, CancellationToken.None])!;
        if (change is "valid" or "rich") { var prepared = await Run(); Assert.Equal(text, prepared.SurroundingText); Assert.Equal(source.DocumentVersion, prepared.ExpectedDocumentVersion); }
        else await Assert.ThrowsAsync<InvalidDataException>(Run);
        Assert.Equal(html, db.Pages.Single(p => p.Id == page).Content);
    }
}
