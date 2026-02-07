using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Security;
using WriterApp.Controllers;
using WriterApp.Data;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class PromptPresetsControllerTests
    {
        [Fact]
        public async Task Crud_Works_ForOwnerScopedPresets()
        {
            using AppDbContext dbContext = BuildDbContext();
            AiPresetsController controller = BuildController(dbContext);

            UpsertPromptPresetRequest createRequest = new(
                Guid.NewGuid(),
                "Tighten dramatic",
                "Style",
                "builtin",
                "tighten.section",
                null,
                new Dictionary<string, object?> { ["tone"] = "dramatic" });

            ActionResult<PromptPresetDto> createResult = await controller.Create(createRequest, CancellationToken.None);
            PromptPresetDto created = Assert.IsType<OkObjectResult>(createResult.Result).Value as PromptPresetDto
                ?? throw new InvalidOperationException("Expected preset payload.");
            Assert.Equal("Tighten dramatic", created.Name);

            ActionResult<IReadOnlyList<PromptPresetDto>> listResult = await controller.List(createRequest.ProjectId, CancellationToken.None);
            IReadOnlyList<PromptPresetDto> listed = Assert.IsType<OkObjectResult>(listResult.Result).Value as IReadOnlyList<PromptPresetDto>
                ?? throw new InvalidOperationException("Expected preset list.");
            Assert.Single(listed);

            UpsertPromptPresetRequest updateRequest = createRequest with { Name = "Tighten intense" };
            ActionResult<PromptPresetDto> updateResult = await controller.Update(created.Id, updateRequest, CancellationToken.None);
            PromptPresetDto updated = Assert.IsType<OkObjectResult>(updateResult.Result).Value as PromptPresetDto
                ?? throw new InvalidOperationException("Expected updated preset.");
            Assert.Equal("Tighten intense", updated.Name);

            IActionResult deleteResult = await controller.Delete(created.Id, CancellationToken.None);
            Assert.IsType<NoContentResult>(deleteResult);

            ActionResult<IReadOnlyList<PromptPresetDto>> emptyListResult = await controller.List(createRequest.ProjectId, CancellationToken.None);
            IReadOnlyList<PromptPresetDto> empty = Assert.IsType<OkObjectResult>(emptyListResult.Result).Value as IReadOnlyList<PromptPresetDto>
                ?? throw new InvalidOperationException("Expected preset list.");
            Assert.Empty(empty);
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

        private static AiPresetsController BuildController(AppDbContext dbContext)
        {
            AiPresetsController controller = new(dbContext, new StubUserIdResolver());
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
            public string ResolveUserId(ClaimsPrincipal user) => "user-1";
        }
    }
}
