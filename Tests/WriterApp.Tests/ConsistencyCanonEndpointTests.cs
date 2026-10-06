using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.Continuity;
using WriterApp.Controllers;
using WriterApp.Data.Documents;
using WriterApp.Shared.Canon;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class AiActionsControllerTests
{
    [Theory]
    [InlineData("current", 1, true)]
    [InlineData("token", 0, false)]
    [InlineData("source", 0, false)]
    [InlineData("payload", 0, false)]
    [InlineData("omitted-payload", 0, false)]
    [InlineData("late-canon", 1, false)]
    [InlineData("late-source", 1, false)]
    [InlineData("foreign-owner", 0, false)]
    public async Task ConsistencyCanonUsesOwnedExactSnapshotAndRejectsLateChanges(string scenario, int calls, bool succeeds)
    {
        await using var db = BuildDbContext(); SeedDocumentGraph(db, out var id, out var section, out var page);
        db.DocumentSyncRecords.Add(new DocumentSyncRecord { DocumentId = id, OwnerUserId = "user-1", Version = "v1", Sequence = 1 });
        await db.SaveChangesAsync();
        var orchestrator = new CanonOrchestrator(); var controller = BuildController(db, orchestrator);
        var method = typeof(AiActionsController).GetMethod("BuildAiDocumentAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var document = await (Task<WriterApp.Domain.Documents.Document>)method.Invoke(controller,
            [db.Documents.Single(), db.Sections.ToArray(), "user-1", null, CancellationToken.None])!;
        var store = new EfCoreBibleStore(db);
        string json = "{\"schemaVersion\":\"1.0\",\"characters\":[{\"name\":\"Anna\"}]}";
        var snapshot = await store.UpsertSnapshotAsync(id, BibleType.Character, json,
            scenario == "source" ? "stale" : BibleRefreshService.SourceHash(document), BibleJson.EmptyCursor(), BibleJson.EmptyStats(), default);
        string token = DocumentBiblesController.SnapshotToken(snapshot);
        if (scenario == "foreign-owner") { db.Documents.Single().OwnerUserId = "other"; await db.SaveChangesAsync(); }
        orchestrator.Before = async () => {
            if (scenario == "late-canon") await store.UpsertSnapshotAsync(id, BibleType.Character, json.Replace("Anna", "Else"), snapshot.LastRefreshSourceHash, snapshot.Cursor, snapshot.Stats, default);
            if (scenario == "late-source") { db.DocumentSyncRecords.Single().Version = "v2"; await db.SaveChangesAsync(); }
        };
        var request = new AiActionExecuteRequestDto(id, section, page, null, null, null, "Exact\neditor\nsource", "Saved planning", new() {
            ["character_bible_json"] = scenario == "payload" ? json.Replace("Anna", "Injected") : json,
            ["place_bible_json"] = scenario == "omitted-payload" ? json : "{}", ["timeline_bible_json"] = "{}"
        }, "v1", new Dictionary<CanonKind, string> { [CanonKind.Character] = scenario == "token" ? "old" : token });
        var result = await controller.ExecuteAction("continuity.check_section", request, default);
        Assert.Equal(calls, orchestrator.Calls);
        if (succeeds) {
            var response = Assert.IsType<AiActionExecuteResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(token, response.SourceCanonVersions![CanonKind.Character]);
            Assert.Equal("Exact\neditor\nsource", orchestrator.Request!.Inputs["section_text"]);
            Assert.Equal(json, orchestrator.Request.Inputs["character_bible_json"]);
            Assert.Equal("Saved planning", orchestrator.Request.Inputs["story_context"]);
        } else Assert.True(result.Result is ConflictObjectResult or NotFoundResult);
        Assert.Contains("Maya checked", db.Pages.Single().Content);
    }
    private sealed class CanonOrchestrator : IAiOrchestrator
    {
        private readonly StubAiOrchestrator _inner = new(true, false);
        public int Calls; public Func<Task>? Before; public AiRequest? Request;
        public IReadOnlyList<IAiAction> Actions => [new ContinuityCheckAction()];
        public IAiAction? GetAction(string id) => Actions.SingleOrDefault(a => a.ActionId == id);
        public bool CanRunAction(string id) => true;
        public AiStreamingCapabilities GetStreamingCapabilities(string id) => new(false, false);
        public async Task<AiExecutionResult> ExecuteActionAsync(string id, AiActionInput input, CancellationToken ct) {
            Calls++; Request = new ContinuityCheckAction().BuildRequest(input); if (Before is not null) await Before();
            var result = await _inner.ExecuteActionAsync(id, input, ct);
            return result with { Proposal = result.Proposal! with { ProposedText = "{\"schemaVersion\":\"1.0\",\"issues\":[]}" } };
        }
        public AiStreamingSession StreamActionAsync(string id, AiActionInput input, CancellationToken ct) => throw new NotSupportedException();
    }
}
