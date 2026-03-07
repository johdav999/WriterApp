using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Commands;
using WriterApp.Application.Continuity;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Domain.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class DocumentBiblesControllerTests
    {
        [Fact]
        public async Task Refresh_ReturnsProblemDetails_WhenCharacterRefreshPayloadIsInvalid()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out Guid sectionId);

            DocumentBiblesController controller = BuildController(db, """
            {
              "schemaVersion":"1.0",
              "characters":[{"name":"Anna","facts":[{"fact":"Broken "quote""}]}]
            }
            """);

            ActionResult<BibleSnapshotDto> result = await controller.Refresh(
                documentId,
                "character",
                new RefreshBibleRequest(false, sectionId),
                CancellationToken.None);

            ObjectResult objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);
            ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("bible_refresh_invalid_payload", problem.Extensions["code"]?.ToString());
        }

        private static DocumentBiblesController BuildController(AppDbContext db, string payload)
        {
            BibleRefreshService refreshService = new(
                new StubAiOrchestrator(payload),
                new StubEntitlementService(),
                new InMemoryBibleStore(),
                new BiblePatchApplier(),
                NullLogger<BibleRefreshService>.Instance);

            DocumentBiblesController controller = new(
                new DocumentRepository(db, NullLogger<DocumentRepository>.Instance),
                new SectionRepository(db, NullLogger<SectionRepository>.Instance, new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
                new PageRepository(db),
                new StubUserIdResolver(),
                new InMemoryBibleStore(),
                refreshService);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
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

        private static void SeedDocumentGraph(AppDbContext db, out Guid documentId, out Guid sectionId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();
            documentId = Guid.NewGuid();
            sectionId = Guid.NewGuid();

            db.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = "user-1",
                Title = "Project",
                CreatedUtc = now,
                UpdatedUtc = now
            });

            db.Documents.Add(new DocumentRecord
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = "user-1",
                Title = "Doc",
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Sections.Add(new SectionRecord
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Scene",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.Pages.Add(new PageRecord
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                SectionId = sectionId,
                Title = "Page 1",
                Content = "<p>Anna pressed the brass key into her palm.</p>",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.SaveChanges();
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubEntitlementService : IEntitlementService
        {
            public Task<UserEntitlements> GetEntitlementsAsync(string userId)
            {
                Dictionary<string, string> entitlements = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["ai.enabled"] = "true"
                };

                return Task.FromResult(new UserEntitlements(userId, "professional", "professional", entitlements));
            }

            public Task<bool> HasAsync(string userId, string entitlementKey)
                => Task.FromResult(string.Equals(entitlementKey, "ai.enabled", StringComparison.OrdinalIgnoreCase));

            public Task<int?> GetIntAsync(string userId, string entitlementKey) => Task.FromResult<int?>(null);

            public void InvalidateForUser(string userId)
            {
            }
        }

        private sealed class InMemoryBibleStore : IBibleStore
        {
            public Task<BibleSnapshotState?> GetSnapshotAsync(Guid documentId, BibleType bibleType, CancellationToken ct)
                => Task.FromResult<BibleSnapshotState?>(null);

            public Task<BibleSnapshotState> UpsertSnapshotAsync(
                Guid documentId,
                BibleType bibleType,
                string contentJson,
                string sourceHash,
                BibleRefreshCursor cursor,
                BibleRefreshStats stats,
                CancellationToken ct)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                return Task.FromResult(new BibleSnapshotState(
                    Guid.NewGuid(),
                    documentId,
                    bibleType,
                    1,
                    contentJson,
                    now,
                    now,
                    now,
                    sourceHash,
                    stats,
                    cursor));
            }
        }

        private sealed class StubAiOrchestrator : IAiOrchestrator
        {
            private readonly string _payload;

            public StubAiOrchestrator(string payload)
            {
                _payload = payload;
            }

            public IReadOnlyList<IAiAction> Actions => Array.Empty<IAiAction>();

            public IAiAction? GetAction(string actionId) => null;

            public bool CanRunAction(string actionId) => true;

            public AiStreamingCapabilities GetStreamingCapabilities(string actionId) => new(true, false);

            public Task<AiExecutionResult> ExecuteActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                AiProposal proposal = new(
                    Guid.NewGuid(),
                    input.ActiveSectionId,
                    "Refresh character bible",
                    actionId,
                    "test",
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    null,
                    new List<ProposedOperation>(),
                    new List<Guid>(),
                    "Refresh character bible",
                    "Document",
                    "refresh",
                    null,
                    _payload);

                return Task.FromResult(AiExecutionResult.Success(proposal));
            }

            public AiStreamingSession StreamActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                throw new NotSupportedException();
            }
        }
    }
}
