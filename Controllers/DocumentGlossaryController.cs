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
    [Route("api/documents/{documentId:guid}/glossary")]
    [Authorize]
    public sealed class DocumentGlossaryController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IDocumentRepository _documents;
        private readonly IUserIdResolver _userIdResolver;

        public DocumentGlossaryController(
            AppDbContext dbContext,
            IDocumentRepository documents,
            IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<GlossaryEntryDto>>> List(
            Guid documentId,
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

            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            List<GlossaryEntryDto> entries = await _dbContext.DocumentGlossaryEntries
                .AsNoTracking()
                .Where(entry => entry.DocumentId == documentId)
                .OrderBy(entry => entry.Term)
                .Select(entry => new GlossaryEntryDto(
                    entry.Id,
                    entry.DocumentId,
                    entry.Term,
                    entry.Notes,
                    entry.UpdatedAt))
                .ToListAsync(ct);

            return Ok(entries);
        }

        [HttpPost]
        public async Task<ActionResult<GlossaryEntryDto>> Create(
            Guid documentId,
            [FromBody] GlossaryEntryCreateRequest request,
            CancellationToken ct)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Term))
            {
                return BadRequest(new { message = "Term is required." });
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

            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            string term = request.Term.Trim();
            string normalized = term.ToLowerInvariant();
            DateTimeOffset now = DateTimeOffset.UtcNow;

            DocumentGlossaryEntryRecord entry = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Term = term,
                NormalizedTerm = normalized,
                Notes = request.Notes?.Trim(),
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.DocumentGlossaryEntries.Add(entry);
            await _dbContext.SaveChangesAsync(ct);

            GlossaryEntryDto dto = new(entry.Id, entry.DocumentId, entry.Term, entry.Notes, entry.UpdatedAt);
            return Ok(dto);
        }

        [HttpDelete("{entryId:guid}")]
        public async Task<IActionResult> Delete(
            Guid documentId,
            Guid entryId,
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

            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            DocumentGlossaryEntryRecord? entry = await _dbContext.DocumentGlossaryEntries
                .FirstOrDefaultAsync(item => item.Id == entryId && item.DocumentId == documentId, ct);
            if (entry is null)
            {
                return NotFound();
            }

            _dbContext.DocumentGlossaryEntries.Remove(entry);
            await _dbContext.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
