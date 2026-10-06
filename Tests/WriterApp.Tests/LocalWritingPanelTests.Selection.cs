using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class LocalWritingPanelTests
{
    [Theory]
    [InlineData("rewrite", "Preview rewrite", true, true)]
    [InlineData("expand", "Preview expansion", false, false)]
    [InlineData("tighten", "Preview shortened text", false, false)]
    [InlineData("change_tone", "Preview tone change", true, false)]
    [InlineData("show_dont_tell", "Preview showing revision", false, false)]
    public async Task SelectingAnActionDoesNotGenerateAndPreviewUsesThatAction(string action, string label, bool tone, bool length)
    {
        await using var h = new Harness(); await h.Start();
        await h.Event("SettingsChanged", new WritingSettings("Friendly", "Shorter", false));
        await h.Event("SelectAction", action);
        Assert.Equal(0, h.Api.Calls);
        var html = new HtmlParser().ParseDocument(await h.Html());
        var selected = Assert.Single(html.QuerySelectorAll(".writing-operations button[aria-pressed='true']"));
        Assert.Equal(action == "tighten" ? "Shorten" : action == "show_dont_tell" ? "Show, don't tell" : action == "change_tone" ? "Change tone" : action == "expand" ? "Expand" : "Rewrite", selected.TextContent);
        Assert.Equal(label, Assert.Single(html.QuerySelectorAll(".writing-preview")).TextContent);
        Assert.Equal(tone, html.QuerySelector("select[aria-label='Tone']") is not null);
        Assert.Equal(length, html.QuerySelector("select[aria-label='Length']") is not null);
        Assert.Equal(length, html.QuerySelector(".writing-preserve") is not null);
        if (Environment.GetEnvironmentVariable("WRITERAPP_WRITING_TOOLS_EVIDENCE") is { } evidence)
            await File.WriteAllTextAsync(Path.Combine(evidence, $"action-{action}.html"), await h.Html());
        await h.Event("PreviewAction");
        Assert.Equal(action + ".selection", h.Api.LastKey);
        Assert.Contains("Review writing proposal", await h.Html());
        Assert.Equal(LocalDocumentCodec.Encode(h.Source), LocalDocumentCodec.Encode((await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!));
        await h.Event("SelectAction", action == "expand" ? "tighten" : "expand");
        Assert.Equal(1, h.Api.Calls);
        Assert.DoesNotContain("Review writing proposal", await h.Html());
    }

    [Theory]
    [InlineData("rewrite", WritingScope.Selection)]
    [InlineData("change_tone", WritingScope.Section)]
    [InlineData("tighten", WritingScope.Section)]
    public async Task SavedCombinationLoadsControlsAndUsesTheSameReviewApplyFlow(string action, WritingScope scope)
    {
        await using var h = new Harness(); await h.Start();
        h.Set("_scope", scope); await h.Event("ScopeChanged");
        await h.Event("SelectAction", action);
        await h.Event("SettingsChanged", new WritingSettings("Friendly", "Shorter", false));
        h.Set("_presetName", "Newsletter revision"); await h.Event("SavePreset");
        Assert.Equal(0, h.Api.Calls);
        var saved = Assert.Single(await new LocalAiStore(h.Fixture.Root + "/ai").PresetsAsync(null));
        Assert.Equal(scope, saved.Scope);
        Assert.Equal(action + (scope == WritingScope.Section ? ".section" : ".selection"), saved.BuiltinActionId);
        Assert.Equal(action == "rewrite" ? 3 : action == "change_tone" ? 1 : 0, saved.Parameters!.Count);
        await h.Event("SelectAction", "expand");
        await h.Event("SelectPreset", new ChangeEventArgs { Value = saved.Id.ToString() });
        var html = new HtmlParser().ParseDocument(await h.Html());
        Assert.Equal(scope.ToString(), html.QuerySelector("select[aria-label='Writing target']")!.GetAttribute("value"));
        Assert.Contains("Newsletter revision", html.Body!.TextContent);
        if (action is "rewrite" or "change_tone") Assert.Equal("Friendly", html.QuerySelector("select[aria-label='Tone']")!.GetAttribute("value"));
        if (Environment.GetEnvironmentVariable("WRITERAPP_WRITING_TOOLS_EVIDENCE") is { } evidence)
            await File.WriteAllTextAsync(Path.Combine(evidence, $"saved-{action}.html"), await h.Html());
        await h.Event("PreviewAction");
        var submitted = ReusablePrompts.Parse(h.Api.Last!.Parameters![ReusablePrompts.Parameter]!.ToString()!);
        Assert.Equal(ReusablePrompts.Canonical(LocalAiStore.Definition(saved)), ReusablePrompts.Canonical(submitted));
        await h.Event("SettingsChanged", new WritingSettings("Formal"));
        Assert.DoesNotContain("Review writing proposal", await h.Html());
        await h.Event("PreviewAction");
        Assert.False(h.Api.Last!.Parameters!.ContainsKey(ReusablePrompts.Parameter));
        await h.Event("SelectPreset", new ChangeEventArgs { Value = saved.Id.ToString() });
        await h.Event("PreviewAction");
        await h.Event("Apply");
        var entry = Assert.Single(await h.Fixture.History.HistoryAsync(h.Source.DocumentId), e => e.Status == "Applied");
        Assert.Equal(ReusablePrompts.Canonical(submitted), ReusablePrompts.Canonical(entry.Preset));
        await new LocalAiHistoryActions(h.Fixture.Repository, h.Fixture.History).ChangeAsync(h.Source.DocumentId, entry.Id, false);
        Assert.Equal(h.Source.Sections.SelectMany(s => s.Pages).Select(p => p.Content), (await h.Fixture.Repository.LoadAsync(h.Source.DocumentId))!.Sections.SelectMany(s => s.Pages).Select(p => p.Content));
    }

    [Fact]
    public async Task ImportedPresetsRespectAccountProjectAndRepresentableSettings()
    {
        await using var h = new Harness(); await h.Start();
        var library = h.Services.GetRequiredService<DevicePromptLibrary>();
        var preset = new PromptDefinition("Imported friendly", null, "builtin", "change_tone.selection", null, new() { ["tone"] = "Friendly" });
        await h.Fixture.History.ImportPresetAsync(preset, library.Scope!, Guid.NewGuid(), "v1");
        await h.Fixture.History.SavePresetAsync(preset with { Name = "Free-form tone", Parameters = new() { ["tone"] = "Dramatic" } }, null, null, null);
        await h.Fixture.History.SavePresetAsync(preset with { Name = "Other project", ProjectId = Guid.NewGuid() }, null, null, null);
        await h.Fixture.History.SavePresetAsync(new("Custom template", null, "custom", null, "Revise {context}", new()), null, null, null);
        await h.Event("LoadPresets");
        var html = new HtmlParser().ParseDocument(await h.Html());
        var dropdown = html.QuerySelector("select[aria-label='Saved preset']")!;
        Assert.Contains("Imported friendly", dropdown.TextContent);
        Assert.DoesNotContain("Free-form tone", dropdown.TextContent);
        Assert.DoesNotContain("Other project", dropdown.TextContent);
        Assert.DoesNotContain("Custom template", dropdown.TextContent);
        await h.Event("SelectPreset", new ChangeEventArgs { Value = dropdown.QuerySelectorAll("option")[1].GetAttribute("value") });
        await h.Renderer.Dispatcher.InvokeAsync(() => h.Fixture.Account.SignOutAsync());
        await (Task)typeof(LocalWritingPanel).GetField("_availabilityRefresh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(h.Components.Panel)!;
        Assert.DoesNotContain("Imported friendly", await h.Html());
        await h.Event("PreviewAction"); Assert.Equal(0, h.Api.Calls);
    }
}
