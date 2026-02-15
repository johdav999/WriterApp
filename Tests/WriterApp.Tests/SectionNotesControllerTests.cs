using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class SectionNotesControllerTests
    {
        [Fact]
        public async Task PutThenGet_ReturnsPersistedNotes()
        {
            await using AppDbContext dbContext = BuildDbContext();
            (Guid sectionId, string userId) = SeedSection(dbContext);
            SectionNotesController controller = BuildController(dbContext, userId);

            SectionNotesDto request = new(sectionId, "Section notes that should persist.", DateTimeOffset.UtcNow);
            ActionResult<SectionNotesDto> putResult = await controller.UpdateSectionNotes(sectionId, request, CancellationToken.None);
            SectionNotesDto putPayload = Assert.IsType<OkObjectResult>(putResult.Result).Value as SectionNotesDto
                ?? throw new InvalidOperationException("Expected section notes payload.");
            Assert.Equal(request.NotesText, putPayload.NotesText);

            ActionResult<SectionNotesDto> getResult = await controller.GetSectionNotes(sectionId, CancellationToken.None);
            SectionNotesDto getPayload = Assert.IsType<OkObjectResult>(getResult.Result).Value as SectionNotesDto
                ?? throw new InvalidOperationException("Expected section notes payload.");

            Assert.Equal(sectionId, getPayload.SectionId);
            Assert.Equal(request.NotesText, getPayload.NotesText);
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

        private static (Guid SectionId, string UserId) SeedSection(AppDbContext dbContext)
        {
            string userId = "notes-user";
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid projectId = Guid.NewGuid();
            Guid documentId = Guid.NewGuid();
            Guid sectionId = Guid.NewGuid();
            Guid pageId = Guid.NewGuid();

            dbContext.Projects.Add(new ProjectRecord
            {
                Id = projectId,
                OwnerUserId = userId,
                Title = "Notes Project",
                CreatedUtc = now,
                UpdatedUtc = now
            });

            dbContext.Documents.Add(new DocumentRecord
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = userId,
                Title = "Notes Document",
                DocumentKind = DocumentKind.Other,
                CreatedAt = now,
                UpdatedAt = now
            });

            dbContext.Sections.Add(new SectionRecord
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = "Section 1",
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            dbContext.Pages.Add(new PageRecord
            {
                Id = pageId,
                DocumentId = documentId,
                SectionId = sectionId,
                Title = "Page 1",
                Content = string.Empty,
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            });

            dbContext.SaveChanges();
            return (sectionId, userId);
        }

        private static SectionNotesController BuildController(AppDbContext dbContext, string userId)
        {
            SectionNotesController controller = new(
                new SectionRepository(
                    dbContext,
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<SectionRepository>.Instance,
                    new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()),
                new StubUserIdResolver(userId),
                dbContext);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            return controller;
        }

        private sealed class StubUserIdResolver : IUserIdResolver
        {
            private readonly string _userId;

            public StubUserIdResolver(string userId)
            {
                _userId = userId;
            }

            public string ResolveUserId(ClaimsPrincipal user) => _userId;
        }
    }
}
