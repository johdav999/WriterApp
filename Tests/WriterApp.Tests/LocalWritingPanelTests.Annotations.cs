using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalWritingPanelTests
{
    private static async Task AddWritingAnnotations(Harness h)
    {
        var scene = h.Source.Project!.Nodes.Single(n => n.SectionId == h.Source.Sections[0].SectionId && n.NodeType == "scene");
        var source = LocalPlanning.AddAnnotation(h.Source, scene.NodeId, "comment", "Check the character", "Åsa 日本語 0");
        source = LocalPlanning.AddAnnotation(source, scene.NodeId, "highlight", "Keep this detail", "日本語 0");
        source = LocalPlanning.AddAnnotation(source, scene.NodeId, "todo", "Check the scene ending", "");
        source = await h.Fixture.Repository.SaveAsync(source);
        h.Source = await h.Fixture.Store.ApplySyncAsync(source with { SyncState = LocalSyncState.Synced }, source.LocalRevision, default, projects: true);
        h.Selection = h.Selection with { Document = h.Source, Editor = h.Selection.Editor with { SelectedText = "", From = 1, To = 1, SelectionStart = 0, SelectionEnd = 0 } };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomSectionPromptWithAnnotationsReviewsAppliesAndRestoresAnchors(bool replaceAnchor)
    {
        await using var h = new Harness(); await h.Start(); await AddWritingAnnotations(h);
        h.Api.Before = _ => {
            var source = WriterApp.Shared.WritingActions.Parse(h.Api.Last!.Parameters![WriterApp.Shared.WritingActions.Parameter]!.ToString()!);
            h.Api.Invalid = WriterApp.Shared.WritingActions.Serialize(source with { Pages = source.Pages.Select((p, i) => i == 0
                ? p with { Runs = p.Runs.Select((r, j) => j == 0 ? r with { Text = (replaceAnchor ? r.Text.Replace("Åsa", "Elin") : r.Text) + " Something moved in the woods." } : r).ToArray() }
                : p).ToArray() });
            return Task.CompletedTask;
        };
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Components.Panel.RunCustomAsync("Add movement in the dark woods."));
        Assert.Contains("Review writing proposal", await h.Html());
        Assert.Contains("Comments, tasks and highlights are retained", await h.Html());
        Assert.Equal(1, h.Api.Calls);
        Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        await h.Event("Apply");
        var saved = (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
        var scene = saved.Project!.Nodes.Single(n => n.SectionId == h.Source.Sections[0].SectionId && n.NodeType == "scene");
        var original = h.Source.Project!.Nodes.Single(n => n.NodeId == scene.NodeId).Annotations;
        Assert.Equal(original.Select(a => a.LocalId), scene.Annotations.Select(a => a.LocalId));
        Assert.Equal(original.Select(a => a.Value.Content), scene.Annotations.Select(a => a.Value.Content));
        Assert.Equal(replaceAnchor, scene.Annotations.Single(a => a.Value.Kind == "comment").Value.AnchorDetached);
        Assert.False(scene.Annotations.Single(a => a.Value.Kind == "highlight").Value.AnchorDetached);
        Assert.False(scene.Annotations.Single(a => a.Value.Kind == "todo").Value.AnchorDetached);
        Assert.Equal(replaceAnchor ? 1 : 2, LocalAnnotationMarkup.ForPage(saved, saved.Sections[0].Pages[0].PageId).Count);
        Assert.Equal(DeviceAiRequests.PlainText(h.Source.Sections[0].Pages[1]), DeviceAiRequests.PlainText(saved.Sections[0].Pages[1]));
        Assert.Equal(h.Source.Sections[1].Pages[0].Content, saved.Sections[1].Pages[0].Content);
        var entry = Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId), e => e.Status == "Applied");
        var history = new LocalAiHistoryActions(h.Fixture.Repository, new(h.Fixture.Root + "/ai"));
        await history.ChangeAsync(h.Source.DocumentId, entry.Id, false);
        var undone = (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
        Assert.Equal(2, LocalAnnotationMarkup.ForPage(undone, undone.Sections[0].Pages[0].PageId).Count);
        Assert.False(undone.Project!.Nodes.Single(n => n.NodeId == scene.NodeId).Annotations.Single(a => a.Value.Kind == "comment").Value.AnchorDetached);
        await history.ChangeAsync(h.Source.DocumentId, entry.Id, true);
        var redone = (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
        Assert.Equal(LocalTranslation.Signature(saved), LocalTranslation.Signature(redone));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ApprovedAnnotatedRevisionRecoveryIsIdempotentAfterContentAndAnchorSave(bool committed)
    {
        await using var h = new Harness(); await h.Start(); await AddWritingAnnotations(h);
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Components.Panel.RunCustomAsync("Change the character name."));
        var entry = Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId)) with { Status = "Applying" };
        await h.Fixture.History.SaveHistoryAsync(entry);
        if (committed) await h.Fixture.Repository.SaveAsync(entry.After!);
        await LocalWritingActions.ResumeAsync(h.Fixture.Repository, h.Fixture.History, entry, default);
        var saved = (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
        Assert.True(saved.Project!.Nodes.Single(n => n.SectionId == h.Source.Sections[0].SectionId && n.NodeType == "scene").Annotations.Single(a => a.Value.Kind == "comment").Value.AnchorDetached);
        await LocalWritingActions.ResumeAsync(h.Fixture.Repository, h.Fixture.History, entry, default);
        Assert.Equal(saved.LocalRevision, (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.LocalRevision);
        Assert.Equal("Applied", Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId)).Status);
        var later = await h.Fixture.Repository.SaveAsync(saved with { Title = "Later edits" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => LocalWritingActions.ResumeAsync(h.Fixture.Repository, h.Fixture.History, entry, default));
        Assert.Equal(LocalTranslation.Signature(later), LocalTranslation.Signature((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
    }

    [Fact]
    public async Task ContinuationWithAnnotationsAppendsWithoutDetachingUnchangedQuotes()
    {
        await using var h = new Harness(); await h.Start(); await AddWritingAnnotations(h);
        h.Set("_scope", WriterApp.Shared.WritingScope.Continuation);
        await h.Event("Generate", "propose.next-paragraph");
        Assert.Contains("Review writing proposal", await h.Html());
        await h.Event("Apply");
        var saved = (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!;
        Assert.Equal(h.Source.Sections[0].Pages[0].Content, saved.Sections[0].Pages[0].Content);
        Assert.StartsWith(h.Source.Sections[0].Pages[1].Content, saved.Sections[0].Pages[1].Content);
        Assert.Equal(2, LocalAnnotationMarkup.ForPage(saved, saved.Sections[0].Pages[0].PageId).Count);
        Assert.Equal(h.Source.Project!.Nodes.SelectMany(n => n.Annotations), saved.Project!.Nodes.SelectMany(n => n.Annotations));
    }
}
