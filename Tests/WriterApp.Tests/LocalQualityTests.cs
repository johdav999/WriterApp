using System.Text.Json;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Usage;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed class LocalQualityTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WriterApp.QualityTests", Guid.NewGuid().ToString("N"));
    private FileLocalDocumentStore Store => new(_root);
    private const string Plain = "First scene. Elin carried carried her suitcase. The door was opened by Anna. Later that day, she left.";
    private async Task<LocalDocument> Book(string text = Plain)
    {
        var store = Store;
        var doc = await store.CreateProjectAsync("Quality book");
        doc = await store.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with {
            Content = "<p>" + text + "</p>", ContentFormat = LocalContentFormat.Html
        }).ToArray() }).ToArray() });
        return await store.ApplySyncAsync(doc with { ServerDocumentId = Guid.NewGuid(), ServerVersion = "v1", SyncState = LocalSyncState.Synced },
            doc.LocalRevision, default, projects: true);
    }
    private static LocalQualityAnalysis Analyze(LocalDocument doc, string text = Plain, int from = 0, int? to = null, bool selection = false) =>
        LocalQualityChecks.Analyze(doc, doc.Sections[0].Pages[0].PageId,
            new(doc.Sections[0].Pages[0].Content, text, text[from..(to ?? text.Length)], from, to ?? text.Length, from + 1, (to ?? text.Length) + 1, 0), selection);
    private static LocalQualityFix Repeated(LocalQualityAnalysis a) => LocalQualityChecks.Target(a,
        a.Issues.First(i => QualityIssueCapabilities.IsRepeatedWordIssue(i)).IssueKey);
    private static LocalQualityPreview Preview(LocalQualityFix fix) => new(fix, fix.LocalReplacement!,
        fix.Analysis.Html.Replace("carried carried", "carried", StringComparison.Ordinal), null, DateTimeOffset.UtcNow);

    [Theory]
    [InlineData(Plain)]
    [InlineData("  🧭 Åsa was invited.\n  Anna Anna waited. Earlier that day, she left.")]
    [InlineData("The extraordinarily complicated incomprehensible description continued across the narrative while every reader tried to follow the relationships between the characters and the events that happened during the very long and exhausting journey.")]
    public async Task OfflineResultsMatchSharedReferenceEngineAndEveryAnchorMatchesExactSource(string text)
    {
        var doc = await Book(text); var a = Analyze(doc, text);
        var context = new QualityCheckContext(text, QualityTextAnalyzer.GetTokens(text), QualityTextAnalyzer.GetSentences(text), QualityTextAnalyzer.GetParagraphs(text), []);
        var expected = new QualityCheckEngine(QualityRuleCatalog.Create()).Evaluate(context, 200);
        Assert.Equal(expected.Select(i => (i.RuleId, i.Severity, i.StartOffset, i.EndOffset, i.AnchorText)),
            a.Issues.Select(i => (i.RuleId, i.Severity, i.StartOffset, i.EndOffset, i.AnchorText)));
        Assert.All(a.Issues.Where(i => i.AnchorText is not null), i => Assert.Equal(i.AnchorText, text[i.StartOffset..i.EndOffset]));
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Store.GetAsync(doc.DocumentId))!));
    }
    [Fact]
    public async Task EmptyPageHasNoIssuesButEmptySelectionIsActionableError()
    {
        var doc = await Book("");
        Assert.Empty(Analyze(doc, "").Issues);
        Assert.Throws<InvalidOperationException>(() => Analyze(doc, "", selection: true));
    }
    [Fact]
    public async Task SelectionOffsetsArePageRelativeAndTargetNeverExceedsCheckedRange()
    {
        var doc = await Book(); int from = Plain.IndexOf("carried", StringComparison.Ordinal), to = Plain.IndexOf(" suitcase", StringComparison.Ordinal);
        var a = Analyze(doc, from: from, to: to, selection: true);
        var fix = Repeated(a);
        Assert.Equal("carried carried", fix.Before); Assert.Equal("carried", fix.LocalReplacement);
        Assert.Equal(from, fix.From); Assert.True(fix.To <= to);
        Assert.All(a.Issues.Where(i => i.AnchorText is not null), i => Assert.Equal(i.AnchorText, Plain[i.StartOffset..i.EndOffset]));
        var invalid = new AiEditorSnapshot(a.Html, Plain, "Other selection", from, to, 1, 4, 0);
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Analyze(doc, a.PageId, invalid, true));
    }
    [Fact]
    public async Task RepeatedCleanupIsLocalAndOtherActionableRulesPrepareExistingRevisionCheckedRewrite()
    {
        var doc = await Book(); var a = Analyze(doc); var repeated = Repeated(a);
        Assert.Equal("carried carried", repeated.Before);
        Assert.Equal("carried", repeated.LocalReplacement);
        var passive = LocalQualityChecks.Target(a, a.Issues.Single(i => QualityIssueCapabilities.IsPassiveVoiceIssue(i)).IssueKey);
        Assert.Null(passive.LocalReplacement);
        Assert.Equal("The door was opened by Anna.", passive.Before);
        var prepared = LocalQualityChecks.Rewrite(doc, passive);
        Assert.Equal("rewrite.selection", prepared.Key); Assert.Equal("v1", prepared.Request.ExpectedDocumentVersion);
        Assert.Equal(doc.ServerDocumentId, prepared.Request.DocumentId); Assert.Equal(passive.Before, prepared.Request.OriginalText);
        Assert.Equal(Plain, prepared.Request.SurroundingText);
        Assert.Equal(passive.From, prepared.Request.SelectionStart); Assert.Equal(passive.To, prepared.Request.SelectionEnd);
        var timeline = a.Issues.Single(i => i.RuleId == "consistency.timeline_hint");
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Target(a, timeline.IssueKey));
        Assert.Equal("Later that day", LocalQualityChecks.Passage(a, timeline.IssueKey).ExpectedText);
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Rewrite(doc with { SyncState = LocalSyncState.Conflict }, passive));
    }
    [Fact]
    public async Task SeparatedRepetitionsRequireGeneratedProseAndLongSentenceUsesSameRequestPath()
    {
        const string text = "Elin carried her suitcase and carried her coat.";
        var doc = await Book(text); Assert.Null(Repeated(Analyze(doc, text)).LocalReplacement);
        string longText = string.Join(" ", Enumerable.Range(0, 35).Select(i => "word" + i)) + ".";
        doc = await Book(longText); var a = Analyze(doc, longText);
        var fix = LocalQualityChecks.Target(a, a.Issues.Single(i => QualityIssueCapabilities.IsSentenceLengthIssue(i)).IssueKey);
        Assert.Equal(longText, fix.Before); Assert.Null(fix.LocalReplacement);
        Assert.Equal("rewrite.selection", LocalQualityChecks.Rewrite(doc, fix).Key);
    }
    [Theory]
    [InlineData("Elin carried a bag. She carried a coat.", false)]
    [InlineData("Keep this outside. Elin carried a bag. She carried a coat. Leave this alone.", true)]
    public async Task RepeatedTargetIncludesBothOccurrencesAcrossSentencesWithinCheckedScope(string text, bool selection)
    {
        var doc = await Book(text);
        int from = selection ? text.IndexOf("Elin", StringComparison.Ordinal) : 0;
        int to = selection ? text.IndexOf(" Leave", StringComparison.Ordinal) : text.Length;
        var a = Analyze(doc, text, from, to, selection);
        var issue = Assert.Single(a.Issues, i => i.AnchorText == "carried");
        var fix = LocalQualityChecks.Target(a, issue.IssueKey);
        Assert.Equal("Elin carried a bag. She carried a coat.", fix.Before);
        Assert.InRange(fix.From, a.From, a.To);
        Assert.InRange(fix.To, a.From, a.To);
        Assert.Equal("Elin carried a bag. She held a coat.", LocalQualityChecks.Validate(fix, "Elin carried a bag. She held a coat."));
    }

    [Theory]
    [InlineData("consistency.proper_names")]
    [InlineData("readability.paragraph_length")]
    public async Task SharedDirectNameAndParagraphFixesStayLocalAndBoundToTheirExactPassage(string rule)
    {
        string text = rule == "consistency.proper_names" ? "Anna arrived. A stranger saw ANNA."
            : "  " + string.Join(" ", Enumerable.Repeat("Sara opened the door and looked down the hallway where the lights flickered in the cold draft.", 14)) + "  ";
        var doc = await Book(text); var a = Analyze(doc, text); var issue = a.Issues.Single(i => i.RuleId == rule);
        Assert.True(QualityIssueCapabilities.CanReview(issue));
        var fix = LocalQualityChecks.Target(a, issue.IssueKey);
        Assert.NotNull(fix.LocalReplacement); Assert.Equal(text[issue.StartOffset..issue.EndOffset], fix.Before);
        if (rule == "consistency.proper_names") Assert.Equal("Anna", fix.LocalReplacement);
        else Assert.Contains("\n\n", fix.LocalReplacement!);
        var invalid = issue with { Fix = issue.Fix! with { From = issue.StartOffset - 1 } };
        Assert.Throws<InvalidDataException>(() => LocalQualityChecks.Target(a with { Issues = [invalid] }, invalid.IssueKey));
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Store.GetAsync(doc.DocumentId))!));
    }
    [Theory]
    [InlineData("")]
    [InlineData("Rewrite the sentence into active voice.")]
    [InlineData("{\"analysis\":\"Remove the repeated word\"}")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("carried carried")]
    [InlineData("Elin.")]
    [InlineData("carried\nExplanation: I removed the repeated word.")]
    [InlineData("```text\ncarried\n```")]
    public async Task InvalidOrInstructionLeakingRepeatedFixesAreInert(string candidate)
    {
        var doc = await Book(); var fix = Repeated(Analyze(doc));
        Assert.Throws<InvalidDataException>(() => LocalQualityChecks.Validate(fix, candidate));
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Store.GetAsync(doc.DocumentId))!));
    }
    [Fact]
    public async Task TargetIdentityExpiredMalformedAndChangedPageBlockApplyButMetadataDoesNot()
    {
        var doc = await Book(); var a = Analyze(doc); var preview = Preview(Repeated(a));
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Apply(doc with { DocumentId = Guid.NewGuid() }, preview));
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Apply(doc with { DeletedAtUtc = DateTimeOffset.UtcNow }, preview));
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Apply(doc with { SyncState = LocalSyncState.Conflict }, preview));
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Apply(doc, preview with { CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) }));
        Assert.Throws<InvalidDataException>(() => LocalQualityChecks.Apply(doc, preview with { AfterHtml = "<iframe src='https://invalid/'></iframe>" }));
        var changed = doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Select(p => p with { Content = p.Content + "<p>Later writing</p>" }).ToArray() }).ToArray() };
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Apply(changed, preview));
        var metadata = doc with { Title = "New title", ServerVersion = "v2", LocalRevision = doc.LocalRevision + 1 };
        Assert.Equal("New title", LocalQualityChecks.Apply(metadata, preview).Title);
        var issue = a.Issues.First(i => i.AnchorText is not null);
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Passage(a with { Issues = [issue with { AnchorText = "wrong source" }] }, issue.IssueKey));
    }
    [Fact]
    public async Task ApplicationPreservesOtherPagesAndInterruptedApprovalRetainsRecoveryEvidence()
    {
        var doc = await Book(); var first = doc.Sections[0].Pages[0];
        doc = await Store.SaveAsync(doc with { Sections = doc.Sections.Select(s => s with { Pages = s.Pages.Append(first with {
            PageId = Guid.NewGuid(), OrderIndex = 1, Title = "Other page", Content = "<p>Keep <strong>other writing</strong>.</p>"
        }).ToArray() }).ToArray() });
        var preview = Preview(Repeated(Analyze(doc))); var next = LocalQualityChecks.Apply(doc, preview);
        Assert.Equal(doc.Sections[0].Pages[1], next.Sections[0].Pages[1]);
        var history = new LocalAiStore(Path.Combine(_root, "ai"));
        var entry = new LocalAiHistory(1, Guid.NewGuid(), doc.DocumentId, "quality.apply_issue", "Manuscript revision",
            doc.LocalRevision, doc.ServerVersion, DateTimeOffset.UtcNow, "Applying", doc, preview.Proposed,
            PageId: first.PageId, AfterHtml: preview.AfterHtml, OriginalText: preview.Fix.Before);
        await history.SaveHistoryAsync(entry);
        var saved = await new LocalDocumentRepository(Store).SaveAsync(next);
        // Simulate interruption after the document commit, before history completion.
        history = new(Path.Combine(_root, "ai")); var interrupted = Assert.Single(await history.HistoryAsync(doc.DocumentId));
        Assert.Equal("Applying", interrupted.Status); Assert.NotNull(LocalAiHistoryActions.UnavailableReason(saved, interrupted, false));
        var recovered = await new LocalDocumentRepository(Store).RecoverSnapshotAsync(interrupted.Before);
        Assert.NotEqual(doc.DocumentId, recovered.DocumentId); Assert.Null(recovered.ServerDocumentId);
        Assert.Equal(doc.Sections[0].Pages[0].Content, recovered.Sections[0].Pages[0].Content);
        Assert.Equal(saved.Sections[0].Pages[0].Content, (await Store.GetAsync(doc.DocumentId))!.Sections[0].Pages[0].Content);
    }
    [Fact]
    public async Task ApprovedLocalFixSavesDurableHistoryUndoRedoAcrossRestartAndRejectsStaleSecondFix()
    {
        var doc = await Book(); var a = Analyze(doc); var preview = Preview(Repeated(a));
        var repository = new LocalDocumentRepository(Store); var history = new LocalAiStore(Path.Combine(_root, "ai"));
        var service = new DeviceAiService(new Api(), new(new Identity()), new()); // Signed out local fix.
        var after = await new LocalQualityActions(repository, history, service).ApplyAsync(doc.DocumentId, preview, default);
        Assert.Equal(JsonSerializer.Serialize(doc.Project), JsonSerializer.Serialize(after.Project));
        Assert.Contains("carried her suitcase", after.Sections[0].Pages[0].Content);
        var entry = Assert.Single(await history.HistoryAsync(doc.DocumentId));
        Assert.Equal("Applied", entry.Status); Assert.Equal("quality.apply_issue", entry.Action);
        Assert.Equal(doc.Sections[0].Pages[0].Content, entry.Before.Sections[0].Pages[0].Content);
        repository = new(new FileLocalDocumentStore(_root)); history = new(Path.Combine(_root, "ai"));
        var undo = new LocalAiHistoryActions(repository, history);
        await undo.ChangeAsync(doc.DocumentId, entry.Id, false);
        Assert.Equal(doc.Sections[0].Pages[0].Content, (await repository.LoadAsync(doc.DocumentId))!.Sections[0].Pages[0].Content);
        await undo.ChangeAsync(doc.DocumentId, entry.Id, true);
        Assert.Equal(after.Sections[0].Pages[0].Content, (await repository.LoadAsync(doc.DocumentId))!.Sections[0].Pages[0].Content);
        Assert.Throws<InvalidOperationException>(() => LocalQualityChecks.Apply(after, preview));
        Assert.DoesNotContain(Analyze(after, Plain.Replace("carried carried", "carried")).Issues, i => QualityIssueCapabilities.IsRepeatedWordIssue(i));
    }
    [Theory]
    [InlineData("account")]
    [InlineData("revision")]
    [InlineData("cancel")]
    public async Task RejectedGeneratedApplyDoesNotSaveWritingOrHistory(string rejection)
    {
        var doc = await Book(); var fix = Repeated(Analyze(doc)); var account = new DeviceAccountService(new Identity()); await account.SignInAsync();
        var service = new DeviceAiService(new Api(), account, new());
        var proposal = await service.ProposeAsync(LocalQualityChecks.Rewrite(doc, fix), default);
        var preview = Preview(fix) with { Provider = proposal };
        using var ct = new CancellationTokenSource();
        if (rejection == "account") await account.SignOutAsync();
        if (rejection == "cancel") ct.Cancel();
        if (rejection == "revision") doc = await Store.ApplySyncAsync(doc with { ServerVersion = "v2" }, doc.LocalRevision, default, projects: true);
        var history = new LocalAiStore(Path.Combine(_root, "ai"));
        await Assert.ThrowsAnyAsync<Exception>(() => new LocalQualityActions(new(Store), history, service).ApplyAsync(doc.DocumentId, preview, ct.Token));
        Assert.Equal(LocalDocumentCodec.Encode(doc), LocalDocumentCodec.Encode((await Store.GetAsync(doc.DocumentId))!));
        Assert.Empty(await history.HistoryAsync(doc.DocumentId));
    }
    private sealed class Identity : IDeviceIdentityClient
    {
        public bool IsConfigured => true;
        public Task<DeviceAccessToken?> AcquireAsync(bool interactive, CancellationToken ct) => Task.FromResult<DeviceAccessToken?>(new("synthetic", DateTimeOffset.UtcNow.AddHours(1), "Writer", "account-1"));
        public Task SignOutAsync() => Task.CompletedTask;
    }
    private sealed class Api : IDeviceAiApi
    {
        public Task<AiUsageStatusDto> GetUsageAsync(CancellationToken ct) => Task.FromResult(new AiUsageStatusDto { AiEnabled = true, UiEnabled = true, QuotaRemaining = 10, SupportsDocumentVersionChecks = true });
        public Task<AiActionExecuteResponseDto> ExecuteAsync(string key, AiActionExecuteRequestDto request, CancellationToken ct) => Task.FromResult(new AiActionExecuteResponseDto(Guid.NewGuid(), request.OriginalText!, "Elin carried her suitcase.", "", DateTimeOffset.UtcNow, key, SourceDocumentVersion: request.ExpectedDocumentVersion));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
