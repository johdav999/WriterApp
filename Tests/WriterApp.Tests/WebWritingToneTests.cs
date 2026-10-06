using System.Collections;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Client.Pages;
using WriterApp.Client.Services;
using WriterApp.Device.Shared.Services;
using WriterApp.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed partial class DesktopAiWebPersistenceTests
{
    [Theory][InlineData("Neutral")][InlineData("Formal")][InlineData("Casual")][InlineData("Friendly")][InlineData("Technical")][InlineData("Executive")]
    public async Task AdvertisedClientToneRunsThroughCheckedHandlerAndExecutiveMatchesDesktopRequest(string tone)
    {
        await using var f = new TranslationFixture(); await f.Start();
        using var provider = new CheckedProvider(f); using var editor = CheckedEditor(f, provider); using var transport = new CancellationTransport(provider);
        var page = (await f.Http.GetFromJsonAsync<PageDto[]>($"api/sections/{f.Sections[0]}/pages"))![0];
        await ConfigureCancellationEditor(f, editor, transport, page);
        await using var coordinator = new EditorSaveCoordinator(new RecoveryDraftService(new CancellationEditorJs(page.Content)), NullLogger<EditorSaveCoordinator>.Instance);
        CancellationPage(editor, page, coordinator);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new TranslationRenderer(services, services.GetRequiredService<ILoggerFactory>()); await renderer.Attach(editor);
        var descriptor = WritingActions.ToneDescriptors.Single(t => t.Value == tone);
        var action = ((IEnumerable)TranslationField(editor, "_aiActionPresets")!).Cast<object>().Single(p => SharedWritingToneTests.Property<string>(p, "Label") == descriptor.ClientPresetLabel);
        var method = typeof(DocumentEditor).GetMethods(TranslationPrivate).Single(m => m.Name == "OnAiActionSelected" && m.GetParameters().Length == 2);
        await renderer.Dispatcher.InvokeAsync(() => (Task)method.Invoke(editor, [action, false])!);
        Assert.Equal(1, provider.Calls); Assert.NotNull(TranslationField(editor, "_pendingAiProposal"));
        Assert.Equal(tone, JsonSerializer.SerializeToElement(provider.Request!.Parameters!["tone"]).GetString());
        Assert.Equal("Same", JsonSerializer.SerializeToElement(provider.Request.Parameters["length"]).GetString());
        Assert.True(JsonSerializer.SerializeToElement(provider.Request.Parameters["preserve_terms"]).GetBoolean());
        await AssertCancellationWritingUnchanged(f);
        if (tone == "Executive")
        {
            using var local = new TranslationTestFixture(); var source = await local.Create();
            var desktop = LocalWritingTests.Prepare(source, WritingScope.Selection, "rewrite.selection", new("Executive"));
            foreach (var key in new[] { "tone", "length", "preserve_terms", "instruction" })
                Assert.Equal(JsonSerializer.Serialize(provider.Request.Parameters[key]), JsonSerializer.Serialize(desktop.Request.Request.Parameters![key]));
        }
    }
}
