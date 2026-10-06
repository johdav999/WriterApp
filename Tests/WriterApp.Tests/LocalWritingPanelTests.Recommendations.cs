using System.Reflection;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;
namespace WriterApp.Tests;
public sealed partial class LocalWritingPanelTests
{
    [Theory][MemberData(nameof(RecommendedWritingTests.Catalog), MemberType = typeof(RecommendedWritingTests))]
    public async Task RecommendedDesktopPanelExecutesCatalogWithExplicitOutputSpecificApprovalAndRecovery(string tool) {
        await using var h = new Harness(); await h.Start(); h.Set("_recommendedId", tool);
        h.Set("_intent", tool.Split('.')[0] switch { "novel" => "Novel", "short_story" => "ShortStory", "non_fiction" => "NonFiction", "blog" => "Blog", _ => "Other" });
        await h.Event("GenerateRecommended"); string review = await h.Html();
        Assert.True(!review.Contains("role=\"alert\""), review);
        Assert.Equal(tool, RecommendedWriting.From(h.Api.Last!.Parameters)!.ToolId); Assert.False(h.Api.Last.Parameters!.ContainsKey(ReusablePrompts.Parameter));
        Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        if (RecommendedWriting.CopyOnly(tool)) {
            Assert.Contains("Review recommended writing result", review); Assert.DoesNotContain("Approve &amp; apply", review);
            string text = RecommendedWriting.Output(tool) == RecommendedOutput.Headlines ? "Headline 3" : h.Api.Paragraph;
            await h.Event("CopyRecommended", text); Assert.Equal(text, h.Interop.Copied); Assert.Empty(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));
            Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
            await h.Event("Dismiss"); Assert.DoesNotContain("Review recommended writing result", await h.Html());
        } else {
            Assert.Contains("Review writing proposal", review); await h.Event("Apply"); Assert.DoesNotContain("role=\"alert\"", await h.Html());
            var entry = Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId), e => e.Status == "Applied"); Assert.Equal(tool, entry.RecommendationId);
            var saved = (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
            if (RecommendedWriting.Output(tool) == RecommendedOutput.AppendParagraph) {
                Assert.Equal(h.Source.Sections[0].Pages[0], saved.Sections[0].Pages[0]); Assert.StartsWith(h.Source.Sections[0].Pages[^1].Content, saved.Sections[0].Pages[^1].Content);
                Assert.Contains(h.Api.Paragraph, saved.Sections[0].Pages[^1].Content);
            }
            var history = new LocalAiHistoryActions(h.Fixture.Repository, h.Fixture.History);
            await history.ChangeAsync(h.Source.DocumentId, entry.Id, false);
            Assert.Equal(h.Source.Sections.SelectMany(s => s.Pages).Select(p => p.Content), (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s => s.Pages).Select(p => p.Content));
            await history.ChangeAsync(h.Source.DocumentId, entry.Id, true);
            Assert.Equal(saved.Sections.SelectMany(s => s.Pages).Select(p => p.Content), (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s => s.Pages).Select(p => p.Content));
        }
        string? evidence = Environment.GetEnvironmentVariable("WRITERAPP_PARITY_P07_UI_EVIDENCE");
        if (evidence is not null && tool is "novel.deepen_character" or "blog.generate_headlines" or "other.summarize_clearly" or "novel.continue_scene") { Directory.CreateDirectory(evidence); await File.WriteAllTextAsync(Path.Combine(evidence, "desktop-" + tool + ".html"), review); }
    }
    [Theory][InlineData("malformed")][InlineData("stale")][InlineData("account")][InlineData("capability")]
    public async Task RecommendedDesktopRefusesInvalidOrChangedCopyResultsWithoutWriting(string failure) {
        await using var h = new Harness(); await h.Start(); h.Set("_recommendedId", "blog.generate_headlines");
        if (failure == "malformed") h.Api.Invalid = "{\"items\":[\"one\"]}";
        if (failure == "capability") h.Api.Recommendations = false;
        await h.Event("GenerateRecommended");
        if (failure == "stale") h.Source = await h.Fixture.Repository.SaveAsync(h.Source with { Title = "Later" });
        if (failure == "account") await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignOutAsync());
        await h.Event("CopyRecommended", "Headline 1"); Assert.Null(h.Interop.Copied); Assert.Empty(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));
        Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        Assert.Equal(failure == "capability" ? 0 : 1, h.Api.Calls);
    }
    [Fact]
    public async Task RecommendedDesktopPreparedSaveCanResumeAndRetainedToolIdentityCannotChange() {
        await using var h = new Harness(); await h.Start(); h.Set("_recommendedId", "novel.deepen_character"); await h.Event("GenerateRecommended");
        var entry = Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId));
        await Assert.ThrowsAsync<InvalidDataException>(() => h.Fixture.History.SaveHistoryAsync(entry with { RecommendationId = "novel.raise_stakes" }));
        var pending = entry with { Status = "Applying" }; await h.Fixture.History.SaveHistoryAsync(pending);
        await LocalWritingActions.ResumeAsync(h.Fixture.Repository, h.Fixture.History, pending, default);
        await LocalWritingActions.ResumeAsync(h.Fixture.Repository, h.Fixture.History, pending, default);
        Assert.Equal("Applied", Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId)).Status);
        Assert.Equal(entry.After!.Sections.SelectMany(s => s.Pages).Select(p => p.Content), (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s => s.Pages).Select(p => p.Content));
    }
}
