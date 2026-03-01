using System;
using System.Collections.Generic;
using System.Linq;
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
    [Route("api/scenes/{sceneNodeId:guid}/quality-checks")]
    [Authorize]
    public sealed class SceneQualityChecksController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;

        public SceneQualityChecksController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet("issues")]
        public async Task<ActionResult<IReadOnlyList<SceneQualityIssueDto>>> ListIssues(
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

            if (!await IsOwnedSceneAsync(sceneNodeId, userId, ct))
            {
                return NotFound();
            }

            List<SceneQualityIssueDto> issues = await _dbContext.SceneQualityIssues
                .AsNoTracking()
                .Where(item => item.SceneNodeId == sceneNodeId)
                .OrderBy(item => item.CreatedAt)
                .ThenBy(item => item.Id)
                .Select(item => new SceneQualityIssueDto(
                    item.IssueKey,
                    item.SceneNodeId,
                    item.RuleId,
                    item.Kind,
                    item.Severity,
                    item.Message,
                    item.Suggestion,
                    item.AnchorText,
                    item.StartOffset,
                    item.EndOffset,
                    item.CreatedAt))
                .ToListAsync(ct);

            return Ok(issues);
        }

        private async Task<bool> IsOwnedSceneAsync(Guid sceneNodeId, string userId, CancellationToken ct)
        {
            return await _dbContext.ProjectNodes
                .Join(
                    _dbContext.Projects,
                    node => node.ProjectId,
                    project => project.Id,
                    (node, project) => new { node, project })
                .AnyAsync(pair =>
                    pair.project.OwnerUserId == userId
                    && pair.node.Id == sceneNodeId
                    && pair.node.NodeType == ProjectNodeType.Scene,
                    ct);
        }
    }
}
