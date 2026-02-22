using System;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/projects/{projectId:guid}/scenes/{sceneNodeId:guid}/content")]
    [Authorize]
    public sealed class ProjectSceneContentController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IProjectWordCountService _projectWordCountService;
        private readonly ILogger<ProjectSceneContentController> _logger;

        public ProjectSceneContentController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IProjectWordCountService projectWordCountService,
            ILogger<ProjectSceneContentController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _projectWordCountService = projectWordCountService ?? throw new ArgumentNullException(nameof(projectWordCountService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<SceneContentDto>> Get(
            Guid projectId,
            Guid sceneNodeId,
            CancellationToken ct)
        {
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            ProjectNodeRecord? scene = await GetOwnedSceneAsync(projectId, sceneNodeId, userId, ct);
            if (scene is null)
            {
                return NotFound();
            }

            SceneContentRecord? content = await _dbContext.SceneContents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId, ct);
            if (content is null)
            {
                return Ok(new SceneContentDto(sceneNodeId, projectId, string.Empty, null, DateTimeOffset.UtcNow));
            }

            return Ok(new SceneContentDto(
                sceneNodeId,
                projectId,
                content.ContentJson ?? string.Empty,
                content.LanguageCode,
                content.UpdatedAtUtc));
        }

        [HttpPut]
        public async Task<ActionResult<SceneContentDto>> Put(
            Guid projectId,
            Guid sceneNodeId,
            [FromBody] SceneContentUpdateRequest request,
            CancellationToken ct)
        {
            string correlationId = Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            ProjectNodeRecord? scene = await GetOwnedSceneAsync(projectId, sceneNodeId, userId, ct);
            if (scene is null)
            {
                return NotFound();
            }

            SceneContentRecord? content = await _dbContext.SceneContents
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId, ct);
            if (content is null)
            {
                content = new SceneContentRecord
                {
                    SceneNodeId = sceneNodeId
                };
                _dbContext.SceneContents.Add(content);
            }

            content.ContentJson = request.ContentJson ?? string.Empty;
            content.LanguageCode = string.IsNullOrWhiteSpace(request.LanguageCode) ? null : request.LanguageCode.Trim();
            content.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await SyncLinkedSectionPagesAsync(scene, content.ContentJson, content.UpdatedAtUtc, ct);
            await _dbContext.SaveChangesAsync(ct);
            await _projectWordCountService.RefreshProjectAsync(projectId, ct);

            _logger.LogInformation(
                "Scene content saved. TraceId={TraceId} CorrelationId={CorrelationId} ProjectId={ProjectId} SceneNodeId={SceneNodeId} LinkedSectionId={LinkedSectionId} ContentLength={ContentLength}",
                HttpContext.TraceIdentifier,
                correlationId,
                projectId,
                sceneNodeId,
                scene.LinkedSectionId,
                content.ContentJson.Length);

            return Ok(new SceneContentDto(
                sceneNodeId,
                projectId,
                content.ContentJson,
                content.LanguageCode,
                content.UpdatedAtUtc));
        }

        private async Task<ProjectNodeRecord?> GetOwnedSceneAsync(
            Guid projectId,
            Guid sceneNodeId,
            string userId,
            CancellationToken ct)
        {
            return await _dbContext.ProjectNodes
                .Join(
                    _dbContext.Projects,
                    node => node.ProjectId,
                    project => project.Id,
                    (node, project) => new { node, project })
                .Where(pair =>
                    pair.project.OwnerUserId == userId
                    && pair.node.ProjectId == projectId
                    && pair.node.Id == sceneNodeId
                    && pair.node.NodeType == ProjectNodeType.Scene)
                .Select(pair => pair.node)
                .FirstOrDefaultAsync(ct);
        }

        private async Task SyncLinkedSectionPagesAsync(
            ProjectNodeRecord scene,
            string contentHtml,
            DateTimeOffset updatedAtUtc,
            CancellationToken ct)
        {
            if (!scene.LinkedSectionId.HasValue)
            {
                return;
            }

            Guid sectionId = scene.LinkedSectionId.Value;
            SectionRecord? section = await _dbContext.Sections
                .FirstOrDefaultAsync(item => item.Id == sectionId, ct);
            if (section is null)
            {
                return;
            }

            List<PageRecord> pages = await _dbContext.Pages
                .Where(page => page.SectionId == sectionId)
                .OrderBy(page => page.OrderIndex)
                .ThenBy(page => page.Id)
                .ToListAsync(ct);

            if (pages.Count == 0)
            {
                Guid documentId = section.DocumentId;
                int nextOrderIndex = await _dbContext.Pages
                    .Where(page => page.SectionId == sectionId)
                    .Select(page => (int?)page.OrderIndex)
                    .MaxAsync(ct) ?? -1;

                pages.Add(new PageRecord
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    SectionId = sectionId,
                    Title = "Page 1",
                    Content = string.Empty,
                    OrderIndex = nextOrderIndex + 1,
                    CreatedAt = updatedAtUtc,
                    UpdatedAt = updatedAtUtc
                });
                _dbContext.Pages.Add(pages[0]);
            }

            pages[0].Content = contentHtml ?? string.Empty;
            pages[0].UpdatedAt = updatedAtUtc;
            for (int i = 1; i < pages.Count; i++)
            {
                pages[i].Content = string.Empty;
                pages[i].UpdatedAt = updatedAtUtc;
            }
        }
    }
}
