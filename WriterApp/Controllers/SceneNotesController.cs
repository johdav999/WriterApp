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
    [Route("api/scenes/{sceneNodeId:guid}/notes")]
    [Authorize]
    public sealed class SceneNotesController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ILogger<SceneNotesController> _logger;

        public SceneNotesController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            ILogger<SceneNotesController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<SceneNotesDto>> Get(Guid sceneNodeId, CancellationToken ct)
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

            if (!await IsOwnedSceneAsync(sceneNodeId, userId, ct))
            {
                return NotFound();
            }

            SceneNoteRecord? note = await _dbContext.SceneNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId, ct);
            if (note is null)
            {
                _logger.LogInformation(
                    "Scene notes get empty. TraceId={TraceId}, CorrelationId={CorrelationId}, SceneNodeId={SceneNodeId}, UserId={UserId}",
                    HttpContext.TraceIdentifier,
                    correlationId,
                    sceneNodeId,
                    userId);
                return Ok(new SceneNotesDto(sceneNodeId, string.Empty, DateTimeOffset.UtcNow));
            }

            _logger.LogInformation(
                "Scene notes loaded. TraceId={TraceId}, CorrelationId={CorrelationId}, SceneNodeId={SceneNodeId}, UserId={UserId}, Length={Length}",
                HttpContext.TraceIdentifier,
                correlationId,
                sceneNodeId,
                userId,
                note.NotesText?.Length ?? 0);
            return Ok(new SceneNotesDto(sceneNodeId, note.NotesText, note.UpdatedAtUtc));
        }

        [HttpPut]
        public async Task<ActionResult<SceneNotesDto>> Put(
            Guid sceneNodeId,
            [FromBody] SceneNotesUpdateRequest request,
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

            if (!await IsOwnedSceneAsync(sceneNodeId, userId, ct))
            {
                return NotFound();
            }

            SceneNoteRecord? note = await _dbContext.SceneNotes
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId, ct);
            if (note is null)
            {
                note = new SceneNoteRecord
                {
                    SceneNodeId = sceneNodeId
                };
                _dbContext.SceneNotes.Add(note);
            }

            note.NotesText = request.NotesText ?? string.Empty;
            note.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Scene notes saved. TraceId={TraceId}, CorrelationId={CorrelationId}, SceneNodeId={SceneNodeId}, UserId={UserId}, Length={Length}",
                HttpContext.TraceIdentifier,
                correlationId,
                sceneNodeId,
                userId,
                note.NotesText.Length);

            return Ok(new SceneNotesDto(sceneNodeId, note.NotesText, note.UpdatedAtUtc));
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
