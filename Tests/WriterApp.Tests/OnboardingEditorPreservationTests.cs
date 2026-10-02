using System.Net;
using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Client.Pages;
using Xunit;

namespace WriterApp.Tests;

public sealed class OnboardingEditorPreservationTests
{
    [Theory]
    [InlineData("<p><strong>Räksmörgås 日本語</strong> — saved writing.</p>")]
    [InlineData("<p><img src=\"/saved-cover.png\"></p>")]
    [InlineData("<p></p>")]
    [InlineData("")]
    public async Task InitialRouteLoadDoesNotOverwriteContentBeforeEditorMounts(string content)
    {
        using var handler = new RecordingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var component = new DocumentEditor { Http = http };
        // Exercise the real lifecycle helper with persisted data and no mounted editor.
        Set(component, "_showOnboardingWalkthrough", true);
        var page = new PageDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Scene", content,
            0, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        Set(component, "_activePage", page);
        typeof(DocumentEditor).GetProperty("Logger")?.SetValue(component, NullLogger<DocumentEditor>.Instance);

        var initialize = typeof(DocumentEditor).GetMethod("EnsureOnboardingStarterTextAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)initialize.Invoke(component, null)!;

        Assert.Equal(0, handler.Writes);
        Assert.Equal(page, typeof(DocumentEditor).GetField("_activePage", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(component));
    }

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public int Writes { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method != HttpMethod.Get) Writes++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
