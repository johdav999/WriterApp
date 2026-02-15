using System;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public ProjectSceneContentController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
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
            await _dbContext.SaveChangesAsync(ct);

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
    }
}
