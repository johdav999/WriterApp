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

public sealed class CollaborationSharingPanelTests
{
    private sealed class Components : IComponentActivator
    {
        public CollaborationSharingPanel Panel = null!;
        public IComponent CreateInstance(Type type) { var component = (IComponent)Activator.CreateInstance(type)!; if (component is CollaborationSharingPanel panel) Panel = panel; return component; }
    }
    private sealed class Api : HttpMessageHandler
    {
        public readonly TaskCompletionSource<bool> Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<HttpResponseMessage> Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<CollaborationInviteRequest> Requests = [];
        public bool FailFirst;
        private sealed record EmailInvite(Guid OperationId,string Email,CollaborationRole Role);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path.EndsWith("/invitations/by-email"))
            {
                var payload = await request.Content!.ReadFromJsonAsync<EmailInvite>(ct);
                Requests.Add(new(payload!.OperationId,payload.Role,RecipientEmail:payload.Email));
                if (FailFirst && Requests.Count == 1) throw new HttpRequestException("uncertain response");
                Started.TrySetResult(true); return await Pending.Task; // Deliberately ignore cancellation to exercise fencing.
            }
            object result = path.EndsWith("/availability") ? new { enabled = true }
                : path == "/api/auth/me" ? new { isAuthenticated = true, userId = "owner" }
                : (path.EndsWith("/shared") || path.EndsWith("/owned")) ? new { complete = true, documents = Array.Empty<CollaborationSharedDocument>() }
                : path.EndsWith("/members") ? new[] { new CollaborationMember("owner", CollaborationRole.Owner, 0, false) }
                : path.EndsWith("/supporting") ? Array.Empty<CollaborationSupportingChoice>()
                : Array.Empty<CollaborationInvitation>();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
        }
    }
    private static Task Invite(Components components) => (Task)typeof(CollaborationSharingPanel).GetMethod("Invite", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(components.Panel, null)!;
    private static void Set(Components components, string key, object value) => typeof(CollaborationSharingPanel).GetField(key, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(components.Panel, value);
    [Fact]
    public async Task AccountInvalidationCannotAdoptLateInvitationSecretOrMemberState()
    {
        var components = new Components(); using var api = new Api(); using var client = new HttpClient(api) { BaseAddress = new("https://example.test/") };
        await using var services = new ServiceCollection().AddLogging().AddSingleton(client).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<CollaborationSharingPanel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(CollaborationSharingPanel.DocumentId)] = Guid.NewGuid(), [nameof(CollaborationSharingPanel.AccountContext)] = "account-one" })));
        Set(components, "_recipient", "recipient@example.test");
        var pending = renderer.Dispatcher.InvokeAsync(() => Invite(components)); await api.Started.Task;
        await renderer.Dispatcher.InvokeAsync(() => components.Panel.Invalidate());
        var id = Guid.NewGuid(); api.Pending.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(new CollaborationCreatedInvitation(
            new(id, Guid.NewGuid(), CollaborationRole.Editor, null, "recipient@example.test", long.MaxValue, 1, "pending"), "private-one-time-secret", false)) });
        await pending;
        Assert.Null(typeof(CollaborationSharingPanel).GetField("_created", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(components.Panel));
        Assert.Null(typeof(CollaborationSharingPanel).GetField("_pendingInvite", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(components.Panel));
    }
    [Fact]
    public async Task UncertainInviteResponseRetriesExactOperationInsteadOfCreatingAnotherInvitation()
    {
        var components = new Components(); using var api = new Api { FailFirst = true }; using var client = new HttpClient(api) { BaseAddress = new("https://example.test/") };
        await using var services = new ServiceCollection().AddLogging().AddSingleton(client).AddSingleton<IComponentActivator>(components).BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<CollaborationSharingPanel>(ParameterView.FromDictionary(new Dictionary<string, object?>
            { [nameof(CollaborationSharingPanel.DocumentId)] = Guid.NewGuid(), [nameof(CollaborationSharingPanel.AccountContext)] = "account-one" })));
        Set(components, "_recipient", "recipient@example.test");
        await renderer.Dispatcher.InvokeAsync(() => Invite(components));
        var pending = renderer.Dispatcher.InvokeAsync(() => Invite(components)); await api.Started.Task;
        var first = api.Requests[0];
        api.Pending.SetResult(new(HttpStatusCode.OK) { Content = JsonContent.Create(new CollaborationCreatedInvitation(
            new(first.OperationId, Guid.NewGuid(), CollaborationRole.Editor, null, first.RecipientEmail, long.MaxValue, 1, "pending"), null, true)) });
        await pending; Assert.Equal(2, api.Requests.Count); Assert.Equal(api.Requests[0], api.Requests[1]);
    }
}
