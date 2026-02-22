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
    [Route("api/scenes/{sceneNodeId:guid}/annotations")]
    [Authorize]
    public sealed class SceneAnnotationsController : ControllerBase
    {
        private static readonly HashSet<string> AllowedKinds = new(StringComparer.OrdinalIgnoreCase)
        {
            "comment",
            "todo",
            "highlight"
        };

        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "open",
            "resolved",
            "all"
        };

        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;

        public SceneAnnotationsController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SceneAnnotationDto>>> List(
            Guid sceneNodeId,
            [FromQuery] string? status,
            [FromQuery] string? kind,
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

            string statusFilter = string.IsNullOrWhiteSpace(status) ? "open" : status.Trim();
            if (!AllowedStatuses.Contains(statusFilter))
            {
                return BadRequest(new { message = "status must be open, resolved, or all." });
            }

            IQueryable<SceneAnnotationRecord> query = _dbContext.SceneAnnotations
                .AsNoTracking()
                .Where(item => item.SceneNodeId == sceneNodeId);

            if (!string.IsNullOrWhiteSpace(kind))
            {
                string kindFilter = kind.Trim();
                if (!AllowedKinds.Contains(kindFilter))
                {
                    return BadRequest(new { message = "kind must be comment, todo, or highlight." });
                }

                query = query.Where(item => item.Kind == kindFilter);
            }

            if (!string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(item => item.Status == statusFilter);
            }

            List<SceneAnnotationDto> result = await query
                .OrderBy(item => item.CreatedAt)
                .Select(item => new SceneAnnotationDto(
                    item.Id,
                    item.SceneNodeId,
                    item.Kind,
                    item.Status,
                    item.AnchorFrom,
                    item.AnchorTo,
                    item.AnchorText,
                    item.Content,
                    item.AuthorUserId,
                    item.CreatedAt,
                    item.ResolvedAt))
                .ToListAsync(ct);

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<SceneAnnotationDto>> Create(
            Guid sceneNodeId,
            [FromBody] SceneAnnotationCreateRequest request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Kind) || !AllowedKinds.Contains(request.Kind))
            {
                return BadRequest(new { message = "kind must be comment, todo, or highlight." });
            }

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

            string content = request.Content?.Trim() ?? string.Empty;
            if (!string.Equals(request.Kind, "highlight", StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new { message = "content is required for comments and TODOs." });
            }

            (int from, int to) = NormalizeRange(request.AnchorFrom, request.AnchorTo);
            SceneAnnotationRecord record = new()
            {
                Id = Guid.NewGuid(),
                SceneNodeId = sceneNodeId,
                Kind = request.Kind.Trim(),
                Status = "open",
                AnchorFrom = from,
                AnchorTo = to,
                AnchorText = request.AnchorText ?? string.Empty,
                Content = content,
                AuthorUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.SceneAnnotations.Add(record);
            await _dbContext.SaveChangesAsync(ct);

            return Ok(new SceneAnnotationDto(
                record.Id,
                record.SceneNodeId,
                record.Kind,
                record.Status,
                record.AnchorFrom,
                record.AnchorTo,
                record.AnchorText,
                record.Content,
                record.AuthorUserId,
                record.CreatedAt,
                record.ResolvedAt));
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

        private static (int From, int To) NormalizeRange(int from, int to)
        {
            if (from < 0)
            {
                from = 0;
            }

            if (to < 0)
            {
                to = 0;
            }

            if (to < from)
            {
                (from, to) = (to, from);
            }

            return (from, to);
        }
    }
}
