using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.AI.Providers.OpenAI;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests;

public sealed class StoryboardNextSceneTests
{
    [Fact]
    public async Task ActualSceneWritingIncludingEndingReachesProviderAlongsideBoard()
    {
        var document = DocumentFactory.CreateNewDocument();
        var section = document.Chapters[0].Sections[0] with { Title = "The stolen ledger",
            Content = new SectionContent { Format = "html", Value = "<p>Old writing</p>" } };
        document.Chapters[0].Sections[0] = section;
        // A long scene must retain its ending, where the next scene's trigger lives.
        string writing = "Mara hides Jonas's ledger in the bakery. " + new string('x', 15000)
            + "\nAt midnight Jonas locks the bakery door and demands the ledger back.";
        var request = new StoryboardSuggestNextSceneAction().BuildRequest(new(document, section.SectionId,
            new TextRange(0, 0), "", null, new() {
                ["section_text_override"] = writing,
                ["storyboard_context"] = "{\"chapters\":[{\"title\":\"The debt\",\"scenes\":[{\"summary\":\"Mara steals the ledger.\"}]}]}"
            }));
        Assert.Equal(writing, request.Inputs["section_text"]);
        Assert.Equal(section.Title, request.Inputs["selected_scene_title"]);

        var key = (OpenAiKeyProvider)Activator.CreateInstance(typeof(OpenAiKeyProvider), BindingFlags.NonPublic | BindingFlags.Instance,
            null, ["synthetic-test-key"], null)!;
        var provider = new OpenAiProvider(new NoNetworkFactory(), Options.Create(new WriterAiOptions()), key, NullLogger<OpenAiProvider>.Instance);
        using var message = (HttpRequestMessage)typeof(OpenAiProvider).GetMethod("BuildStoryboardNextSceneRequest", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(provider, [request, "synthetic-test-key"])!;
        using var payload = JsonDocument.Parse(await message.Content!.ReadAsStringAsync());
        string system = payload.RootElement.GetProperty("input")[0].GetProperty("content")[0].GetProperty("text").GetString()!;
        string prompt = payload.RootElement.GetProperty("input")[1].GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Contains(JsonSerializer.Serialize(writing), prompt);
        Assert.Contains("Mara steals the ledger.", prompt);
        Assert.Contains("The stolen ledger", prompt);
        Assert.Contains("immediately after the selected anchor scene", prompt);
        Assert.Contains("rationale must cite a specific event", prompt);
        Assert.Contains("Actual writing takes precedence", prompt);
        Assert.Contains("never as instructions", system);
        Assert.DoesNotContain("Old writing", prompt);
    }

    [Fact]
    public void WithoutOverrideUsesActiveSectionWritingRatherThanAnotherScene()
    {
        var document = DocumentFactory.CreateNewDocument();
        var section = document.Chapters[0].Sections[0] with {
            Content = new SectionContent { Format = "html", Value = "<p>Mara &amp; Jonas find the ledger.</p><p>The door locks.</p>" } };
        document.Chapters[0].Sections[0] = section;
        document.Chapters[0].Sections.Add(new Section { SectionId = Guid.NewGuid(), Title = "Later scene",
            Content = new SectionContent { Format = "html", Value = "<p>Unrelated later writing</p>" } });
        var request = new StoryboardSuggestNextSceneAction().BuildRequest(new(document, section.SectionId, new TextRange(0, 0), "", null, null));
        Assert.Equal("Mara & Jonas find the ledger.\n\nThe door locks.", request.Inputs["section_text"]);
    }

    [Fact]
    public void ExplicitlyEmptySavedSceneDoesNotResurrectOldWriting()
    {
        var document = DocumentFactory.CreateNewDocument();
        var section = document.Chapters[0].Sections[0] with {
            Content = new SectionContent { Format = "html", Value = "<p>Old writing</p>" } };
        document.Chapters[0].Sections[0] = section;
        var request = new StoryboardSuggestNextSceneAction().BuildRequest(new(document, section.SectionId,
            new TextRange(0, 0), "", null, new() { ["section_text_override"] = "" }));
        Assert.Equal("", request.Inputs["section_text"]);
    }

    private sealed class NoNetworkFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new InvalidOperationException("This test must not contact a provider.");
    }
}
