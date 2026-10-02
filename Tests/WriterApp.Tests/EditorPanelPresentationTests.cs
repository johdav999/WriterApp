using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using AngleSharp.Html.Parser;
using WriterApp.Device.Shared.Services;
using WriterApp.Device.Shared.Storage;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class EditorPanelPresentationTests
{
    [Fact]
    public async Task SharedAiHistoryDisplaysProposalAsInertTextAndSeparatesUndo()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<AiHistoryPanel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Entries"] = new AiHistoryItem[] { new(Guid.NewGuid(), "Translation", "Selection", "Reviewed", DateTimeOffset.UtcNow, "Before", "<script>alert(1)</script>") }
            }));
            return output.ToHtmlString();
        });
        Assert.Contains("separately from editor undo", html);
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("Recover original", html);
    }
    [Fact]
    public async Task HistoryShowsCorrectActionsAndExplainsUnavailableUndo()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var entries = new[] {
            new AiHistoryItem(Guid.NewGuid(), "rewrite.selection", "Writing · Opening", "Applied", DateTimeOffset.UtcNow, "Before", "After", CanUndo: true),
            new AiHistoryItem(Guid.NewGuid(), "synopsis.story_coach", "SynopsisField:logline", "Undone", DateTimeOffset.UtcNow, "Before", "After", CanRedo: true),
            new AiHistoryItem(Guid.NewGuid(), "scene.suggest", "SceneCard:", "Reviewed", DateTimeOffset.UtcNow, "null", "{\"summary\":\"A return to the harbor.\"}"),
            new AiHistoryItem(Guid.NewGuid(), "translate.selection", "Writing", "Applied", DateTimeOffset.UtcNow, "Before", "After", UnavailableReason: "This writing has changed since the AI action.") };
        string html = await renderer.Dispatcher.InvokeAsync(async () => {
            var output = await renderer.RenderComponentAsync<AiHistoryPanel>(ParameterView.FromDictionary(new Dictionary<string, object?> {
                ["Entries"] = entries, ["Undo"] = EventCallback.Factory.Create<Guid>(this, _ => { }),
                ["Redo"] = EventCallback.Factory.Create<Guid>(this, _ => { }), ["Recover"] = EventCallback.Factory.Create<Guid>(this, _ => { }) }));
            return output.ToHtmlString();
        });
        var dom = new HtmlParser().ParseDocument(html);
        var cards = dom.QuerySelectorAll(".history-card"); Assert.Equal(4, cards.Length);
        Assert.Contains("Undo change", cards[0].TextContent); Assert.Null(cards[0].QuerySelector(".history-item-actions button")!.GetAttribute("disabled"));
        Assert.Contains("Redo change", cards[1].TextContent);
        Assert.Null(cards[2].QuerySelector(".history-item-actions button"));
        Assert.Contains("Reviewed only", cards[2].TextContent); Assert.DoesNotContain("scene.suggest", cards[2].TextContent);
        Assert.Contains("A return to the harbor.", cards[2].TextContent); Assert.DoesNotContain("{", cards[2].TextContent);
        Assert.True(cards[3].QuerySelector(".history-item-actions button")!.HasAttribute("disabled"));
        Assert.Contains("This writing has changed", cards[3].TextContent);
        Assert.Equal(4, dom.QuerySelectorAll("button").Count(b => b.TextContent == "Recover a copy"));
    }
    [Theory]
    [InlineData("ArrowRight", 1, 2, 0)]
    [InlineData("ArrowLeft", 0, 2, 1)]
    [InlineData("Home", 1, 2, 0)]
    [InlineData("End", 0, 2, 1)]
    [InlineData("Tab", 0, 2, -1)]
    [InlineData("Escape", 0, 2, -1)]
    [InlineData("ArrowRight", 0, 0, -1)]
    public void KeyboardTraversalWrapsWithoutTrappingTab(string key, int current, int count, int expected) =>
        Assert.Equal(expected, EditorTabNavigation.TargetIndex(key, current, count));

    [Fact]
    public async Task OnlyAvailableTabsRenderWithSelectedStateAndPanelRelationship()
    {
        using var services = new ServiceCollection().AddLogging().AddSingleton<IJSRuntime, NoJs>().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<EditorTabList<string>>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Tabs"] = new EditorTabDescriptor<string>[] { new("writing", "Writing", "writing"), new("story", "Story", "story", false), new("navigator", "Navigator", "navigator") },
                ["Selected"] = "navigator", ["PanelId"] = "test-panel"
            }));
            return output.ToHtmlString();
        });
        Assert.DoesNotContain("Story", html);
        Assert.Contains("role=\"tablist\"", html);
        Assert.Contains("aria-controls=\"test-panel\"", html);
        Assert.Contains("tabindex=\"0\" aria-selected=\"true\"", html);
        Assert.Contains("tabindex=\"-1\" aria-selected=\"false\"", html);
    }

    [Fact]
    public async Task DevicePanelSelectionNotifiesAndDocumentResetReturnsToWriting()
    {
        var view = new DeviceWorkspaceView();
        int notifications = 0;
        view.Changed = () => { notifications++; return Task.CompletedTask; };
        await view.SelectPanelAsync(DeviceEditorPanel.Navigator);
        Assert.Equal(DeviceEditorPanel.Navigator, view.Panel);
        await view.ToggleContextAsync();
        Assert.Equal(DeviceEditorPanel.Navigator, view.Panel);
        view.Reset();
        Assert.Equal(DeviceEditorPanel.Writing, view.Panel);
        Assert.False(view.ContextCollapsed);
        Assert.Equal(3, notifications);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => view.SelectPanelAsync((DeviceEditorPanel)99));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("<p>Räksmörgås 日本語 café.</p>", 3)]
    [InlineData("<h6>Hello</h6><p>world &amp; 42</p>", 3)]
    [InlineData("<p>don't re-write</p>", 3)]
    [InlineData("<p><strong>two</strong> words<br>here</p>", 3)]
    [InlineData("<p>123 😀 !!!</p>", 1)]
    [InlineData("<p>cafe&#769; café</p>", 2)]
    public void HtmlWordCountKeepsClientUnicodeRule(string html, int expected)
    {
        Assert.Equal(expected, EditorWordMetrics.CountHtml(html));
        Assert.Equal(expected, DeviceWordMetrics.Count(new LocalPage { PageId = Guid.NewGuid(), Title = "Fixture", OrderIndex = 0, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, Content = html, ContentFormat = LocalContentFormat.Html }));
        Assert.Equal(EditorWordMetrics.ToPlainText(html), WriterApp.Client.State.PlainTextMapper.ToPlainText(html));
    }

    [Fact]
    public void LegacyTextAndJsonUseSameTokenRuleWithoutCountingJsonPropertyNames()
    {
        Assert.Equal(3, DeviceWordMetrics.Count(new LocalPage { PageId = Guid.NewGuid(), Title = "Fixture", OrderIndex = 0, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, Content = "Räksmörgås 日本語 café", ContentFormat = LocalContentFormat.LegacyText }));
        Assert.Equal(2, DeviceWordMetrics.Count(new LocalPage { PageId = Guid.NewGuid(), Title = "Fixture", OrderIndex = 0, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, Content = "{\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"Hello\"}]},{\"type\":\"paragraph\",\"content\":[{\"type\":\"text\",\"text\":\"world\"}]}]}", ContentFormat = LocalContentFormat.LegacyJson }));
        Assert.Null(DeviceWordMetrics.Count(new LocalPage { PageId = Guid.NewGuid(), Title = "Fixture", OrderIndex = 0, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, Content = "{invalid", ContentFormat = LocalContentFormat.LegacyJson }));
    }

    [Fact]
    public void ProjectWordTotalIncludesMixedPageFormatsAndDoesNotHideUnknownCounts()
    {
        LocalPage Page(string content, LocalContentFormat format) => new() { PageId = Guid.NewGuid(), Title = "Fixture", OrderIndex = 0,
            CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow, Content = content, ContentFormat = format };
        var pages = new[] { Page("<p>Two words</p>", LocalContentFormat.Html), Page("日本語 café", LocalContentFormat.LegacyText),
            Page("{\"type\":\"text\",\"text\":\"One\"}", LocalContentFormat.LegacyJson) };
        Assert.Equal(5, DeviceWordMetrics.CountPages(pages));
        Assert.Equal(0, DeviceWordMetrics.CountPages([]));
        Assert.Null(DeviceWordMetrics.CountPages(pages.Append(Page("{invalid", LocalContentFormat.LegacyJson))));
    }

    [Fact]
    public async Task SharedProjectCoachEscapesProjectContentAndKeepsHostAction()
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<WorkspaceCoachCard>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["ContextTitle"] = "Project coach", ["Observations"] = new[] { "<script>alert(1)</script>" }, ["Why"] = "<b>Reason</b>",
                ["PrimaryAction"] = (RenderFragment)(builder => { builder.OpenElement(0, "button"); builder.AddContent(1, "Create first scene"); builder.CloseElement(); })
            }));
            return output.ToHtmlString();
        });
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;b&gt;Reason&lt;/b&gt;", html);
        Assert.Contains("<button>Create first scene</button>", html);
    }

    private sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException("Static render must not invoke JS.");
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => InvokeAsync<TValue>(identifier, args);
    }
}
