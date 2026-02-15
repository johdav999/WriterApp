using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

            await _dbContext.SaveChangesAsync(ct);
            return Ok(new SectionNotesDto(sectionId, notes.NotesText, notes.UpdatedAtUtc));
        }
    }
}
