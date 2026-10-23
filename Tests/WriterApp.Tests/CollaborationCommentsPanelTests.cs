using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WriterApp.Shared.Collaboration;
using WriterApp.UI.Shared;
using Xunit;

namespace WriterApp.Tests;

public sealed class CollaborationCommentsPanelTests
{
    private sealed class Components : IComponentActivator
    {
        public CollaborationCommentsPanel Panel = null!;
        public IComponent CreateInstance(Type type) { var component = (IComponent)Activator.CreateInstance(type)!; if (component is CollaborationCommentsPanel panel) Panel = panel; return component; }
    }
    private sealed class Api : HttpMessageHandler
    {
        public readonly Guid Page = Guid.NewGuid(), Generation = Guid.NewGuid();
        public readonly List<CollaborationCreateComment> Requests = [];
        public readonly TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<HttpResponseMessage> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool FailFirst;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/read")) return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CollaborationCommentSnapshot(Page, Generation, 7, [])) };
            Requests.Add((await request.Content!.ReadFromJsonAsync<CollaborationCreateComment>(ct))!);
            if (FailFirst && Requests.Count == 1) throw new HttpRequestException("uncertain response");
            Started.TrySetResult(true); return await Pending.Task; // Ignores cancellation deliberately.
        }
    }
    private static Task Create(Components components) => (Task)typeof(CollaborationCommentsPanel).GetMethod("Create", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(components.Panel, null)!;
    private static void Set(Components components, string field, object value) => typeof(CollaborationCommentsPanel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(components.Panel, value);
    private static object? Get(Components components, string field) => typeof(CollaborationCommentsPanel).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(components.Panel);
    private static ParameterView Parameters(Api api) => ParameterView.FromDictionary(new Dictionary<string, object?>
    {
        [nameof(CollaborationCommentsPanel.Enabled)] = true, [nameof(CollaborationCommentsPanel.CanComment)] = true,
        [nameof(CollaborationCommentsPanel.DocumentId)] = Guid.NewGuid(), [nameof(CollaborationCommentsPanel.PageId)] = api.Page,
        [nameof(CollaborationCommentsPanel.Generation)] = api.Generation, [nameof(CollaborationCommentsPanel.ActorId)] = "editor",
        [nameof(CollaborationCommentsPanel.MembershipRevision)] = 3L, [nameof(CollaborationCommentsPanel.AccountContext)] = "account-one",
        [nameof(CollaborationCommentsPanel.CaptureAnchor)] = (Func<Task<CollaborationAnchor?>>)(() => Task.FromResult<CollaborationAnchor?>(new("anchor-start", "anchor-end")))
    });
    [Fact]
    public async Task LateCommentCompletionCannotAdoptOldAccountOrPageState()
    {
        var components = new Components(); using var api = new Api(); using var client = new HttpClient(api) { BaseAddress = new("https://example.test/") };
        await using var services = new ServiceCollection().AddLogging().AddSingleton(client).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<CollaborationCommentsPanel>(Parameters(api)));
        Set(components, "_body", "Old account comment");
        var pending = renderer.Dispatcher.InvokeAsync(() => Create(components)); await api.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await renderer.Dispatcher.InvokeAsync(() => components.Panel.Invalidate());
        api.Pending.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(new { revision = 1 }) }); await pending;
        Assert.Equal("", Get(components, "_body")); Assert.Null(Get(components, "_pendingCreate")); Assert.Empty((CollaborationCommentThread[])Get(components, "_threads")!);
        Assert.Single(api.Requests); Assert.Equal(3, api.Requests[0].MembershipRevision); Assert.Equal(7, api.Requests[0].ExpectedSequence);
    }
    [Fact]
    public async Task UncertainCommentRetryPreservesOperationAnchorBodyAndRevision()
    {
        var components = new Components(); using var api = new Api { FailFirst = true }; using var client = new HttpClient(api) { BaseAddress = new("https://example.test/") };
        await using var services = new ServiceCollection().AddLogging().AddSingleton(client).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<CollaborationCommentsPanel>(Parameters(api)));
        Set(components, "_body", "Keep this comment"); await renderer.Dispatcher.InvokeAsync(() => Create(components));
        Assert.NotNull(Get(components, "_pendingCreate"));
        var retry = renderer.Dispatcher.InvokeAsync(() => Create(components)); await api.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        api.Pending.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(new { revision = 1 }) }); await retry;
        Assert.Equal(2, api.Requests.Count); Assert.Equal(api.Requests[0], api.Requests[1]); Assert.Equal("Keep this comment", api.Requests[1].Body);
        Assert.Null(Get(components, "_pendingCreate")); Assert.Equal("", Get(components, "_body"));
    }
}
