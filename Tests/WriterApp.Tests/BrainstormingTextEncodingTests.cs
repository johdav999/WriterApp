using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Shared.Brainstorming;
using Xunit;

namespace WriterApp.Tests;

public sealed class BrainstormingTextEncodingTests
{
    private const string CorruptProse = "The drill stalled abruptly.\n\n\u001cWhat's going on?\u001d Luis muttered, kneeling by the control panel.\n\n\u001cI don't believe it,\u001d whispered Anna. \u001cThis is... artificial.\u001d\n\n\u001cWhat on Earth could this be, miles under the ice?\u001d Luis asked.\n\nAnna shook her head. \u001cWhatever it is, this changes everything. We need to document and study it carefully.\u001d";
    private const string CorrectProse = "The drill stalled abruptly.\n\n\u201cWhat's going on?\u201d Luis muttered, kneeling by the control panel.\n\n\u201cI don't believe it,\u201d whispered Anna. \u201cThis is... artificial.\u201d\n\n\u201cWhat on Earth could this be, miles under the ice?\u201d Luis asked.\n\nAnna shook her head. \u201cWhatever it is, this changes everything. We need to document and study it carefully.\u201d";

    [Fact]
    public async Task ProviderQuoteControlsAreRepairedBeforePersistenceWhileRawInspectionIsRetained()
    {
        await using var f = new BrainstormingConversationTests.Fixture();
        await f.Start();
        f.Provider.Wire = JsonSerializer.Serialize(new BrainstormingAnswer(
            "An editable \u001cdraft\u001d is ready.", 1, BrainstormingIntent.Draft,
            new(CorruptProse, "Keep \u001cdialogue\u001d", ["Anna says \u001cartificial\u001d"]), [], []),
            BrainstormingConversationContract.ResponseJson);
        var original = f.Provider.Wire;
        Assert.Contains("\\u001C", original);
        var events = await f.Run();
        var output = events[^1].Output!;
        Assert.Equal(BrainstormingGenerationState.Completed, events[^1].State);
        Assert.Equal(CorrectProse, output.Draft!.Text);
        Assert.Equal("An editable \u201cdraft\u201d is ready.", output.Answer);
        Assert.Equal("Keep \u201cdialogue\u201d", output.Draft.Constraints);
        Assert.Equal("Anna says \u201cartificial\u201d", Assert.Single(output.Draft.IntroducedFacts));
        var session = (await new BrainstormingSessions(f.Db, "author", f.Data.Backend)
            .ReadAsync(f.Data.Document, f.Send.Scope.SessionId, default))!;
        var draft = Assert.Single(session.Drafts);
        Assert.Equal(CorrectProse, draft.PlainText);
        Assert.Equal(CorrectProse, BrainstormingDraftContent.PlainText(BrainstormingDraftContent.Html(draft.PlainText)));
        Assert.Equal(output.Answer, Assert.Single(session.Turns, t => t.Role == BrainstormingRole.Assistant).Text);
        Assert.Equal(original, (await f.Service().InspectAsync(f.Send.Scope, f.Send.OperationId, default))!.InspectionText);
        Assert.Empty(session.Applications);
    }

    [Fact]
    public async Task OrdinaryUnicodeAndWhitespaceSurviveAndPreviewUsesTheSameQuoteRepair()
    {
        await using var f = new BrainstormingConversationTests.Fixture();
        await f.Start();
        const string prose = "\u201cÅsa — 日本語 🧭,\u201d Anna said.\r\n\tIt's −12°C.\n\n\u2018Wait…\u2019";
        var wire = JsonSerializer.Serialize(new BrainstormingAnswer(prose, 1, BrainstormingIntent.Draft,
            new(prose, "", []), [], []), BrainstormingConversationContract.ResponseJson);
        Assert.Equal(prose, BrainstormingConversationContract.ReadAnswer(wire, f.Send, f.Context).Draft!.Text);
        Assert.Equal(prose, BrainstormingConversationContract.PreviewAnswer(wire));
        Assert.Equal("\u201cReady\u201d", BrainstormingConversationContract.PreviewAnswer("{\"answer\":\"\\u001cReady\\u001d\","));
    }

    [Theory]
    [InlineData('\u0000')]
    [InlineData('\u0001')]
    [InlineData('\u000b')]
    [InlineData('\u001b')]
    [InlineData('\u007f')]
    [InlineData('\u0085')]
    public async Task OtherControlCodesFailValidationAndRemainInspectionOnly(char control)
    {
        await using var f = new BrainstormingConversationTests.Fixture();
        await f.Start();
        f.Provider.Wire = JsonSerializer.Serialize(new BrainstormingAnswer("Draft ready.", 1, BrainstormingIntent.Draft,
            new("Anna" + control + "waited.", "", []), [], []), BrainstormingConversationContract.ResponseJson);
        var events = await f.Run();
        Assert.Equal(BrainstormingGenerationState.Failed, events[^1].State);
        Assert.Null(events[^1].Output);
        Assert.Equal(f.Provider.Wire, (await f.Service().InspectAsync(f.Send.Scope, f.Send.OperationId, default))!.InspectionText);
        var session = (await new BrainstormingSessions(f.Db, "author", f.Data.Backend)
            .ReadAsync(f.Data.Document, f.Send.Scope.SessionId, default))!;
        Assert.Empty(session.Drafts);
        Assert.Null(BrainstormingConversationContract.PreviewAnswer(JsonSerializer.Serialize(new { answer = "Ready" + control })));
    }
}
