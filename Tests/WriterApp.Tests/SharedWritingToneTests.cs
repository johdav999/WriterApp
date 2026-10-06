using System.Collections;
using System.Reflection;
using System.Text.Encodings.Web;
using AngleSharp.Html.Parser;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Client.Pages;
using WriterApp.Shared;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class SharedWritingToneTests
{
    internal static object[] ClientPresets() => ((IEnumerable)typeof(DocumentEditor).GetField("_aiActionPresets", BindingFlags.NonPublic | BindingFlags.Instance)!
        .GetValue(new DocumentEditor())!).Cast<object>().ToArray();
    internal static T Property<T>(object value, string name) => (T)value.GetType().GetProperty(name)!.GetValue(value)!;
    [Fact]
    public void AdvertisedDescriptorsPreserveStableValuesLabelsAndLegacyPresetSettings()
    {
        Assert.Equal(new[] { "Neutral", "Formal", "Casual", "Friendly", "Technical", "Executive" }, WritingActions.Tones);
        Assert.Equal(WritingActions.Tones, WritingActions.ToneDescriptors.Select(t => t.Value));
        Assert.Equal(WritingActions.Tones.Length, WritingActions.Tones.Distinct().Count());
        var presets = ClientPresets();
        foreach (var tone in WritingActions.ToneDescriptors)
        {
            WritingActions.Validate(new WritingCommand("rewrite.selection", WritingScope.Selection, new(tone.Value)));
            WritingActions.Validate(new WritingCommand("change_tone.section", WritingScope.Section, new(tone.Value)));
            var preset = Assert.Single(presets, p => Property<string>(p, "Label") == tone.ClientPresetLabel);
            Assert.Equal("rewrite.selection", Property<string>(preset, "ActionKey"));
            Assert.True(Property<bool>(preset, "RequiresSelection"));
            Assert.Equal(tone.ClientPresetLabel, Property<string>(preset, "Instruction"));
            var parameters = Property<Dictionary<string, object?>>(preset, "Parameters");
            Assert.Equal(tone.Value, parameters["tone"]); Assert.Equal("Same", parameters["length"]); Assert.Equal(true, parameters["preserve_terms"]);
        }
        Assert.Contains(presets, p => Property<string>(p, "Label") == "Shorten (Neutral)" && (string)Property<Dictionary<string, object?>>(p, "Parameters")["length"]! == "Shorter");
        Assert.Contains(presets, p => Property<string>(p, "Label") == "Fix grammar (Neutral)");
        Assert.Throws<InvalidDataException>(() => WritingActions.Validate(new WritingCommand("rewrite.selection", WritingScope.Selection, new("Unknown advertised tone"))));
    }
    [Theory][InlineData("Executive")][InlineData("Formal")][InlineData("dramatic")][InlineData("cinematic")]
    public async Task SavedPresetToneRemainsLosslessAcrossLocalReloadAndTypedExecution(string tone)
    {
        using var f = new PresetTransferFixture();
        var definition = new PromptDefinition("Saved tone", "Style", "builtin", "rewrite.selection", null,
            new() { ["tone"] = tone, ["length"] = "Same", ["preserve_terms"] = true }, WritingScope.Selection);
        await f.Store.SavePresetAsync(definition, null, null, null);
        var saved = Assert.Single(await new WriterApp.Device.Shared.Storage.LocalAiStore(f.Root + "/ai").PresetsAsync(null));
        var loaded = WriterApp.Device.Shared.Storage.LocalAiStore.Definition(saved);
        ReusablePrompts.ValidateForRun(loaded);
        Assert.Equal(ReusablePrompts.Canonical(definition), ReusablePrompts.Canonical(loaded));
        Assert.Equal(tone, ReusablePrompts.ExecutionParameters(loaded)["tone"]);
    }
    [Fact]
    public void ExecutiveKeepsTheExistingProviderPromptMeaningWithoutConfigurationChanges()
    {
        var context = new WriterApp.AI.Abstractions.AiRequestContext(Guid.NewGuid(), Guid.NewGuid(), new WriterApp.Application.Commands.TextRange(0, 11),
            "Maya waited", "Synthetic", null, null, "en", "Maya waited", 0, 11, null, null, null);
        string instruction = WritingActions.ToneDescriptors.Single(t => t.Value == "Executive").SelectionRewriteInstruction!;
        string prompt = (string)typeof(OpenAiProvider).GetMethod("BuildUserPrompt", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, ["Maya waited", instruction, "Executive", "Same", true, context])!;
        Assert.Contains("Tone: Executive.", prompt); Assert.Contains("Instruction: Rewrite (Executive)", prompt);
        Assert.Contains("Length: Same.", prompt); Assert.Contains("Preserve terms: yes.", prompt);
    }
    [Theory][InlineData("rewrite", false)][InlineData("change_tone", false)][InlineData("rewrite", true)]
    public async Task CompiledWritingMenuAdvertisesEveryDescriptorWithExecutiveSelected(string action, bool disabled)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<WritingOptions>(ParameterView.FromDictionary(new Dictionary<string, object?> {
            [nameof(WritingOptions.Value)] = new WritingSettings("Executive"), [nameof(WritingOptions.Action)] = action, [nameof(WritingOptions.Disabled)] = disabled
        })));
        string html = await renderer.Dispatcher.InvokeAsync(root.ToHtmlString);
        var doc = new HtmlParser().ParseDocument(html); var select = doc.QuerySelector("select[aria-label=Tone]")!;
        Assert.Equal(WritingActions.Tones, select.QuerySelectorAll("option").Select(o => o.GetAttribute("value")));
        Assert.Equal("Executive", Assert.Single(select.QuerySelectorAll("option[selected]")).TextContent);
        Assert.Equal(disabled, doc.QuerySelector("fieldset")!.HasAttribute("disabled"));
        if (Environment.GetEnvironmentVariable("WRITERAPP_P13_EVIDENCE") is { } path)
        {
            await File.WriteAllTextAsync(Path.Combine(path, action + (disabled ? "-busy" : "-idle") + ".html"), html);
            // Catalog evidence uses actual client preset labels/settings; it does not simulate the authenticated shell.
            var catalog = "<section aria-label='Client tone catalog'>" + string.Join("", ClientPresets().Where(p => WritingActions.ToneDescriptors.Any(t => t.ClientPresetLabel == Property<string>(p, "Label"))).Select(p =>
                "<button type='button' data-tone='" + HtmlEncoder.Default.Encode((string)Property<Dictionary<string, object?>>(p, "Parameters")["tone"]!) + "'>" + HtmlEncoder.Default.Encode(Property<string>(p, "Label")) + "</button>")) + "</section>";
            await File.WriteAllTextAsync(Path.Combine(path, "client-tone-catalog.html"), catalog);
        }
    }
}
