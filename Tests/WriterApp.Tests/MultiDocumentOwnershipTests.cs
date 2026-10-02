using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Client.Services;
using WriterApp.Client.State;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests;

public sealed class MultiDocumentOwnershipTests
{
    [Fact]
    public async Task TreeRequestsAdoptLegacyNodesThenIsolateSelectedManuscriptAndRejectNotes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var project = new ProjectRecord { Id = Guid.NewGuid(), OwnerUserId = "owner", Title = "Novel" };
        var primary = new DocumentRecord { Id = Guid.NewGuid(), Project = project, ProjectId = project.Id, OwnerUserId = "owner", Title = "Main" };
        var alternative = new DocumentRecord { Id = Guid.NewGuid(), Project = project, ProjectId = project.Id, OwnerUserId = "owner", Title = "Alternative" };
        var notes = new DocumentRecord { Id = Guid.NewGuid(), Project = project, ProjectId = project.Id, OwnerUserId = "owner", Title = "Research", DocumentKind = DocumentKind.Notes };
        project.PrimaryDocumentId = primary.Id;
        db.Documents.AddRange(primary, alternative, notes);
        var legacy = new ProjectNodeRecord { Id = Guid.NewGuid(), ProjectId = project.Id, NodeType = ProjectNodeType.Chapter, Title = "Main chapter" };
        var other = new ProjectNodeRecord { Id = Guid.NewGuid(), ProjectId = project.Id, DocumentId = alternative.Id, NodeType = ProjectNodeType.Chapter, Title = "Alternative chapter" };
        db.ProjectNodes.AddRange(legacy, other);
        await db.SaveChangesAsync();
        var filter = new ManuscriptScopeFilter(db, new Owner());
        async Task<ActionExecutingContext> Run(Guid? selected, Guid? node = null)
        {
            var http = new DefaultHttpContext();
            http.Request.Path = $"/api/projects/{project.Id}/nodes";
            if (selected is not null) http.Request.QueryString = new QueryString($"?documentId={selected}");
            var descriptor = new ActionDescriptor { RouteValues = new Dictionary<string, string?> { ["action"] = "GetTree" } };
            var context = new ActionContext(http, new RouteData(), descriptor);
            var args = new Dictionary<string, object?> { ["projectId"] = project.Id };
            if (node is not null) args["nodeId"] = node.Value;
            var executing = new ActionExecutingContext(context, [], args, new object());
            await filter.OnActionExecutionAsync(executing, async () =>
            {
                var visible = await db.ProjectNodes.ToListAsync();
                Assert.Single(visible);
                Assert.Equal(selected ?? (node is not null ? alternative.Id : primary.Id), visible[0].DocumentId);
                return new ActionExecutedContext(context, [], new object());
            });
            Assert.Null(db.ManuscriptScopeId);
            return executing;
        }
        Assert.Null((await Run(null)).Result);
        Assert.Equal(primary.Id, legacy.DocumentId);
        Assert.Null((await Run(alternative.Id)).Result);
        Assert.Null((await Run(null, other.Id)).Result); // Cold scene links derive their owner.
        Assert.IsType<NotFoundObjectResult>((await Run(notes.Id)).Result);
        Assert.IsType<NotFoundObjectResult>((await Run(Guid.NewGuid())).Result);
    }

    [Fact]
    public async Task ClientCapturesManuscriptInRequestsAndCachesTreesIndependently()
    {
        var projectId = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var selection = new ManuscriptSelectionState();
        var capture = new Capture();
        using var client = new HttpClient(new ManuscriptRequestHandler(selection) { InnerHandler = capture }) { BaseAddress = new Uri("https://writer.test/") };
        selection.Select(projectId, first);
        await client.GetAsync($"api/projects/{projectId}/tree");
        Assert.Contains($"documentId={first:D}", capture.Last!.Query);
        selection.Select(projectId, second);
        await client.GetAsync($"api/projects/{projectId}/nodes?documentId={first:D}");
        Assert.Contains($"documentId={first:D}", capture.Last!.Query);
        await client.GetAsync($"api/projects/{projectId}/nodes/{Guid.NewGuid()}/open-scene");
        Assert.Equal("", capture.Last!.Query);
        var cache = new ProjectStructureCacheService(NullLogger<ProjectStructureCacheService>.Instance, selection);
        ProjectTreeDto Tree(Guid documentId, string title) => new(new(projectId, "Novel", null, null, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, first, documentId),
            [new(Guid.NewGuid(), projectId, null, "chapter", title, 0, null, null, 0, DateTimeOffset.UtcNow)], documentId);
        cache.SetProjectTree(Tree(first, "Main")); cache.SetProjectTree(Tree(second, "Alternative"));
        Assert.True(cache.TryGetProjectTree(projectId, out var tree)); Assert.Equal("Alternative", tree.Nodes[0].Title);
        selection.Select(projectId, first);
        Assert.True(cache.TryGetProjectTree(projectId, out tree)); Assert.Equal("Main", tree.Nodes[0].Title);
        cache.InvalidateProjectTree(projectId, "primary changed");
        Assert.False(cache.TryGetProjectTree(projectId, out _));
        selection.Select(projectId, second); Assert.False(cache.TryGetProjectTree(projectId, out _));
    }

    private sealed class Owner : IUserIdResolver { public string ResolveUserId(ClaimsPrincipal user) => "owner"; }
    private sealed class Capture : HttpMessageHandler
    {
        public Uri? Last { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Last = request.RequestUri; return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)); }
    }
}
