using System;
using System.Linq;
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
    [Route("api/sections/{sectionId:guid}/notes")]
    [Authorize]
    public sealed class SectionNotesController : ControllerBase
    {
        private readonly ISectionRepository _sections;
        private readonly IUserIdResolver _userIdResolver;
        private readonly AppDbContext _dbContext;

        public SectionNotesController(
            ISectionRepository sections,
            IUserIdResolver userIdResolver,
            AppDbContext dbContext)
        {
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpGet]
        public async Task<ActionResult<SectionNotesDto>> GetSectionNotes(Guid sectionId, CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? section = await _sections.GetAsync(sectionId, userId, ct);
            if (section is null)
            {
                return NotFound();
            }

            SectionNoteRecord? notes = await _dbContext.SectionNotes
                .FindAsync(new object?[] { sectionId }, ct);

            if (notes is null)
            {
                SceneNoteRecord? sceneNote = await FindAnySceneNoteBySectionAsync(sectionId, ct);
                if (sceneNote is not null)
                {
                    return Ok(new SectionNotesDto(sectionId, sceneNote.NotesText, sceneNote.UpdatedAtUtc));
                }

                return Ok(new SectionNotesDto(sectionId, string.Empty, DateTimeOffset.UtcNow));
            }

            return Ok(new SectionNotesDto(notes.SectionId, notes.NotesText, notes.UpdatedAtUtc));
        }

        [HttpPut]
        public async Task<ActionResult<SectionNotesDto>> UpdateSectionNotes(
            Guid sectionId,
            [FromBody] SectionNotesDto request,
            CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? section = await _sections.GetAsync(sectionId, userId, ct);
            if (section is null)
            {
                return NotFound();
            }

            string notesText = request.NotesText ?? string.Empty;
            SectionNoteRecord? notes = await _dbContext.SectionNotes
                .FindAsync(new object?[] { sectionId }, ct);

            if (notes is null)
            {
                notes = new SectionNoteRecord
                {
                    SectionId = sectionId,
                    NotesText = notesText,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                };
                _dbContext.SectionNotes.Add(notes);
            }
            else
            {
                notes.NotesText = notesText;
                notes.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            await MirrorSectionNotesToScenesAsync(sectionId, notes.NotesText, notes.UpdatedAtUtc, ct);
            await _dbContext.SaveChangesAsync(ct);
            return Ok(new SectionNotesDto(sectionId, notes.NotesText, notes.UpdatedAtUtc));
        }

        private void AddLegacyApiHeaders()
        {
            Response.Headers["Deprecation"] = "true";
            Response.Headers["Link"] = "</api/scenes/{sceneNodeId}/notes>; rel=\"successor-version\"";
        }

        private async Task<SceneNoteRecord?> FindAnySceneNoteBySectionAsync(Guid sectionId, CancellationToken ct)
        {
            Guid? sceneNodeId = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.NodeType == ProjectNodeType.Scene && node.LinkedSectionId == sectionId)
                .Select(node => (Guid?)node.Id)
                .FirstOrDefaultAsync(ct);
            if (!sceneNodeId.HasValue)
            {
                return null;
            }

            return await _dbContext.SceneNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId.Value, ct);
        }

        private async Task MirrorSectionNotesToScenesAsync(
            Guid sectionId,
            string notesText,
            DateTimeOffset updatedAt,
            CancellationToken ct)
        {
            Guid[] sceneNodeIds = await _dbContext.ProjectNodes
                .Where(node => node.NodeType == ProjectNodeType.Scene && node.LinkedSectionId == sectionId)
                .Select(node => node.Id)
                .ToArrayAsync(ct);
            if (sceneNodeIds.Length == 0)
            {
                return;
            }

            var existing = await _dbContext.SceneNotes
                .Where(item => sceneNodeIds.Contains(item.SceneNodeId))
                .ToDictionaryAsync(item => item.SceneNodeId, ct);

            foreach (Guid sceneNodeId in sceneNodeIds)
            {
                if (existing.TryGetValue(sceneNodeId, out SceneNoteRecord? sceneNote))
                {
                    sceneNote.NotesText = notesText ?? string.Empty;
                    sceneNote.UpdatedAtUtc = updatedAt;
                    continue;
                }

                _dbContext.SceneNotes.Add(new SceneNoteRecord
                {
                    SceneNodeId = sceneNodeId,
                    NotesText = notesText ?? string.Empty,
                    UpdatedAtUtc = updatedAt
                });
            }
        }
    }
}
