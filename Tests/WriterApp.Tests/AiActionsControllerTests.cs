using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AiActionsControllerTests
    {
        [Fact]
        public async Task ExecuteAction_SceneSuggest_ReturnsOk_WhenRequestIsValid()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out Guid sectionId, out Guid pageId);
            SeedSectionSceneCard(db, sectionId);

            AiActionsController controller = BuildController(db, new StubAiOrchestrator(success: true, providerFailure: false));
            AiActionExecuteRequestDto request = new(
                documentId,
                sectionId,
                pageId,
                null,
                null,
                "{}",
                "Scene text for suggest",
                null,
                new Dictionary<string, object?> { ["instruction"] = "Suggest scene card" });

            ActionResult<AiActionExecuteResponseDto> result = await controller.ExecuteAction(
                SceneSuggestAction.ActionIdValue,
                request,
                CancellationToken.None);

            OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
            AiActionExecuteResponseDto payload = Assert.IsType<AiActionExecuteResponseDto>(ok.Value);
            Assert.NotEqual(Guid.Empty, payload.ProposalId);
            Assert.NotNull(payload.ProposedSceneCard);
            Assert.Equal("Klara", payload.ProposedSceneCard!.PovCharacterId);
            Assert.Equal("Old town cafe", payload.ProposedSceneCard.PlaceId);
            Assert.Equal("Day 3 / two weeks later", payload.ProposedSceneCard.TimeRef);
            Assert.NotNull(payload.ProposedSceneCard.Tags);
        }

        [Fact]
        public async Task ExecuteAction_SceneSuggest_ReturnsNotFound_WhenSectionDoesNotExist()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out _, out Guid pageId);

            AiActionsController controller = BuildController(db, new StubAiOrchestrator(success: true, providerFailure: false));
            AiActionExecuteRequestDto request = new(
                documentId,
                Guid.NewGuid(),
                pageId,
                null,
                null,
                "{}",
                "Scene text",
                null,
                new Dictionary<string, object?>());

            ActionResult<AiActionExecuteResponseDto> result = await controller.ExecuteAction(
                SceneSuggestAction.ActionIdValue,
                request,
                CancellationToken.None);

            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task ExecuteAction_SceneSuggest_ReturnsBadRequest_WhenDocumentMissing()
        {
            await using AppDbContext db = BuildDbContext();
            AiActionsController controller = BuildController(db, new StubAiOrchestrator(success: true, providerFailure: false));
            AiActionExecuteRequestDto request = new(
                null,
                Guid.NewGuid(),
                null,
                null,
                null,
                "{}",
                "Scene text",
                null,
                new Dictionary<string, object?>());

            ActionResult<AiActionExecuteResponseDto> result = await controller.ExecuteAction(
                SceneSuggestAction.ActionIdValue,
                request,
                CancellationToken.None);

            BadRequestObjectResult badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
            string message = badRequest.Value?.ToString() ?? string.Empty;
            Assert.Contains("documentId is required", message);
        }

        [Fact]
        public async Task ExecuteAction_SceneSuggest_Returns503Problem_WhenProviderFails()
        {
            await using AppDbContext db = BuildDbContext();
            SeedDocumentGraph(db, out Guid documentId, out Guid sectionId, out Guid pageId);
            SeedSectionSceneCard(db, sectionId);

            AiActionsController controller = BuildController(db, new StubAiOrchestrator(success: false, providerFailure: true));
            AiActionExecuteRequestDto request = new(
                documentId,
                sectionId,
                pageId,
                null,
                null,
                "{}",
                "Scene text",
                null,
                new Dictionary<string, object?>());

            ActionResult<AiActionExecuteResponseDto> result = await controller.ExecuteAction(
                SceneSuggestAction.ActionIdValue,
                request,
                CancellationToken.None);

            ObjectResult objectResult = Assert.IsType<ObjectResult>(result.Result);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
            ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("ai.misconfigured", problem.Extensions["code"]?.ToString());
        }

        private static AiActionsController BuildController(AppDbContext db, IAiOrchestrator orchestrator)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();
            AiActionsController controller = new(
                orchestrator,
                new DocumentRepository(db, NullLogger<DocumentRepository>.Instance),
                new SectionRepository(db, NullLogger<SectionRepository>.Instance, config),
                new PageRepository(db),
                db,
                new StubUserIdResolver(),
                new InMemoryAiActionHistoryStore(),
                new StubPageVersionService(),
                NullLogger<AiActionsController>.Instance);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };
            controller.ControllerContext.HttpContext.Request.Headers["X-Correlation-ID"] = "test-correlation";
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

        private static void SeedDocumentGraph(AppDbContext db, out Guid documentId, out Guid sectionId, out Guid pageId)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();
            documentId = Guid.NewGuid();
            sectionId = Guid.NewGuid();
            pageId = Guid.NewGuid();

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
                Id = pageId,
                DocumentId = documentId,
                SectionId = sectionId,
                Title = "Page 1",
                Content = "<p>Maya checked her phone at 08:05 and sighed.</p>",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            db.SaveChanges();
        }

        private static void SeedSectionSceneCard(AppDbContext db, Guid sectionId)
        {
            db.SectionSceneCards.Add(new SectionSceneCardRecord
            {
                SectionId = sectionId,
                NarrativePurpose = "Introduce the start of the story",
                EmotionalBeat = "Tension",
                KeyEvents = "Maya arrives late",
                OpenQuestions = "Will she make it on time?",
                PovCharacterId = "Maya",
                PlaceId = "Office",
                TimelineEventId = "evt-1",
                TimeRef = "Morning",
                TagsJson = "[\"lateness\"]",
                ReferencesJson = "[]",
                UpdatedUtc = DateTimeOffset.UtcNow
            });
            db.SaveChanges();
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }

        private sealed class StubPageVersionService : IPageVersionService
        {
            public Task<PageVersionRecord?> CreateSnapshotAsync(string userId, PageRecord page, string content, string reason, bool allowDuplicate, CancellationToken ct)
                => Task.FromResult<PageVersionRecord?>(null);

            public Task<PageVersionRecord?> CreateAutosnapshotIfDueAsync(string userId, PageRecord page, string content, TimeSpan minAge, CancellationToken ct)
                => Task.FromResult<PageVersionRecord?>(null);

            public Task<IReadOnlyList<PageVersionRecord>> ListVersionsAsync(string userId, Guid pageId, CancellationToken ct)
                => Task.FromResult<IReadOnlyList<PageVersionRecord>>(Array.Empty<PageVersionRecord>());

            public Task<PageVersionRecord?> GetVersionAsync(string userId, Guid versionId, CancellationToken ct)
                => Task.FromResult<PageVersionRecord?>(null);

            public string DecompressContent(PageVersionRecord version) => string.Empty;

            public Task CleanupAsync(string userId, Guid pageId, CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class StubAiOrchestrator : IAiOrchestrator
        {
            private readonly IAiAction _action = new SceneSuggestAction();
            private readonly bool _success;
            private readonly bool _providerFailure;

            public StubAiOrchestrator(bool success, bool providerFailure)
            {
                _success = success;
                _providerFailure = providerFailure;
            }

            public IReadOnlyList<IAiAction> Actions => new[] { _action };

            public IAiAction? GetAction(string actionId)
                => string.Equals(actionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal) ? _action : null;

            public bool CanRunAction(string actionId) => true;

            public AiStreamingCapabilities GetStreamingCapabilities(string actionId)
                => new(false, false);

            public Task<AiExecutionResult> ExecuteActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                if (_providerFailure)
                {
                    throw new AiProviderException("openai", "API key is not configured.");
                }

                if (!_success)
                {
                    return Task.FromResult(AiExecutionResult.Blocked("ai.blocked", "Blocked."));
                }

                AiProposal proposal = new(
                    Guid.NewGuid(),
                    input.ActiveSectionId,
                    "Scene card suggestion",
                    actionId,
                    "mock",
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    null,
                    new List<ProposedOperation>(),
                    new List<Guid>(),
                    "Suggested scene card",
                    "section",
                    null,
                    input.SelectedText,
                    "{\"narrativePurpose\":\"Introduce the beginning\",\"emotionalBeat\":\"Anxious urgency\",\"keyEvents\":\"Maya races to the meeting\",\"openQuestions\":\"Will she be accepted despite lateness?\",\"povCharacterId\":\"Klara\",\"placeId\":\"Old town cafe\",\"timelineEventId\":\"evt-42\",\"timeRef\":\"Day 3 / two weeks later\",\"tags\":[\"reveal\",\"tension\"],\"references\":[],\"explanation\":\"Generated from section text.\"}");
                return Task.FromResult(AiExecutionResult.Success(proposal));
            }

            public AiStreamingSession StreamActionAsync(string actionId, AiActionInput input, CancellationToken ct)
            {
                throw new NotSupportedException();
            }
        }
    }
}
