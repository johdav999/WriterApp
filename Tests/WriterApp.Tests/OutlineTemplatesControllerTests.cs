using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class OutlineTemplatesControllerTests
    {
        [Fact]
        public async Task DisabledFlag_ReturnsNotFoundWithMessage_ForListAndApply()
        {
            using AppDbContext dbContext = BuildDbContext();
            OutlineTemplatesController controller = BuildController(
                dbContext,
                BuildConfig(("WriterApp:Workflow:OutlineTemplatesEnabled", "false")));

            ActionResult<IReadOnlyList<OutlineTemplateDto>> listResult = await controller.ListTemplates(CancellationToken.None);
            NotFoundObjectResult listNotFound = Assert.IsType<NotFoundObjectResult>(listResult.Result);
            string listPayload = JsonSerializer.Serialize(listNotFound.Value);
            Assert.Contains("Outline templates are disabled.", listPayload, StringComparison.Ordinal);

            ActionResult<IReadOnlyList<DocumentOutlineNodeDto>> applyResult = await controller.ApplyTemplate(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new OutlineTemplateApplyOptionsDto(null, false, "none"),
                CancellationToken.None);
            NotFoundObjectResult applyNotFound = Assert.IsType<NotFoundObjectResult>(applyResult.Result);
            string applyPayload = JsonSerializer.Serialize(applyNotFound.Value);
            Assert.Contains("Outline templates are disabled.", applyPayload, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ApplyTemplate_CreatesOutlineNodes_AndLinkedSectionWhenRequested()
        {
            using AppDbContext dbContext = BuildDbContext();
            Guid documentId = Guid.NewGuid();
            SeedDocument(dbContext, documentId);

            OutlineTemplatesController controller = BuildController(
                dbContext,
                BuildConfig(
                    ("WriterApp:Workflow:OutlineTemplatesEnabled", "true"),
                    ("Workflow:OutlineUndoEnabled", "false")));

            Guid rootId = Guid.NewGuid();
            Guid chapterId = Guid.NewGuid();
            Guid sceneId = Guid.NewGuid();
            OutlineTemplateCreateRequest request = new(
                "Starter",
                new List<OutlineTemplateNodeDto>
                {
                    new(rootId, null, "part", "Part I", 0, null, null, null),
                    new(chapterId, rootId, "chapter", "Chapter 1", 0, null, null, null),
                    new(sceneId, chapterId, "scene", "Scene 1", 0, null, null, null)
                });

            ActionResult<OutlineTemplateDto> createResult = await controller.CreateTemplate(request, CancellationToken.None);
            OutlineTemplateDto created = Assert.IsType<OkObjectResult>(createResult.Result).Value as OutlineTemplateDto
                ?? throw new InvalidOperationException("Expected template payload.");
            Assert.Equal(3, created.NodeCount);

            ActionResult<IReadOnlyList<DocumentOutlineNodeDto>> applyResult = await controller.ApplyTemplate(
                documentId,
                created.Id,
                new OutlineTemplateApplyOptionsDto(null, true, "create"),
                CancellationToken.None);

            IReadOnlyList<DocumentOutlineNodeDto> applied = Assert.IsType<OkObjectResult>(applyResult.Result).Value as IReadOnlyList<DocumentOutlineNodeDto>
                ?? throw new InvalidOperationException("Expected outline payload.");

            Assert.Equal(3, applied.Count);
            Assert.Equal(3, await dbContext.DocumentOutlineNodes.CountAsync());
            Assert.Equal(1, await dbContext.Sections.CountAsync());
            Assert.Equal(1, await dbContext.Pages.CountAsync());
        }

        private static AppDbContext BuildDbContext()
        {
            DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite("Filename=:memory:")
                .Options;

            AppDbContext context = new(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();
            return context;
        }

        private static IConfiguration BuildConfig(params (string Key, string Value)[] pairs)
        {
            Dictionary<string, string?> values = new();
            foreach ((string key, string value) in pairs)
            {
                values[key] = value;
            }

            return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        }

        private static OutlineTemplatesController BuildController(AppDbContext dbContext, IConfiguration configuration)
        {
            OutlineTemplatesController controller = new(
                dbContext,
                new StubUserIdResolver(),
                new StubStructureCommandProcessor(),
                configuration);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        private static void SeedDocument(AppDbContext dbContext, Guid documentId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();

            dbContext.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Template Project",
                CreatedUtc = now,
                UpdatedUtc = now
            });

            dbContext.Documents.Add(new DocumentRecord
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Template Doc",
                DocumentKind = DocumentKind.Other,
                CreatedAt = now,
                UpdatedAt = now
            });
            dbContext.SaveChanges();
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubStructureCommandProcessor : IStructureCommandProcessor
        {
            public Task ExecuteAsync(IStructureUndoCommand command, CancellationToken ct) => Task.CompletedTask;

            public Task<bool> UndoAsync(string userId, Guid documentId, CancellationToken ct) => Task.FromResult(false);

            public Task<bool> RedoAsync(string userId, Guid documentId, CancellationToken ct) => Task.FromResult(false);
        }
    }
}
