using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data.Documents;
using WriterApp.Data;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public sealed class PagesController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ISectionRepository _sections;
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;
        private readonly AppDbContext _dbContext;

        public PagesController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            AppDbContext dbContext)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpGet("sections/{sectionId:guid}/pages")]
        public async Task<ActionResult<IReadOnlyList<PageDto>>> ListPages(Guid sectionId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _sections.ExistsAsync(sectionId, userId, ct))
            {
                return NotFound();
            }

            IReadOnlyList<PageRecord> pages = await _pages.ListBySectionAsync(sectionId, userId, ct);
            List<PageDto> result = pages
                .Select(page => new PageDto(
                    page.Id,
                    page.DocumentId,
                    page.SectionId,
                    page.Title,
                    page.Content,
                    page.OrderIndex,
                    page.CreatedAt,
                    page.UpdatedAt))
                .ToList();

            return Ok(result);
        }

        [HttpPost("sections/{sectionId:guid}/pages")]
        public async Task<ActionResult<PageDto>> CreatePage(
            Guid sectionId,
            [FromBody] PageCreateRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? section = await _sections.GetAsync(sectionId, userId, ct);
            if (section is null)
            {
                return NotFound();
            }

            Guid pageId = request.Id ?? Guid.NewGuid();
            if (request.Id.HasValue)
            {
                PageRecord? existing = await _pages.GetAsync(pageId, userId, ct);
                if (existing is not null)
                {
                    if (existing.SectionId != sectionId)
                    {
                        return Conflict(new { message = "Page already exists under a different section." });
                    }

                    return Ok(new PageDto(
                        existing.Id,
                        existing.DocumentId,
                        existing.SectionId,
                        existing.Title,
                        existing.Content,
                        existing.OrderIndex,
                        existing.CreatedAt,
                        existing.UpdatedAt));
                }
            }

            string title = string.IsNullOrWhiteSpace(request.Title) ? "Page" : request.Title.Trim();
            string content = request.Content ?? string.Empty;
            DateTimeOffset createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow;
            DateTimeOffset updatedAt = request.UpdatedAt ?? createdAt;

            int orderIndex;
            if (request.OrderIndex.HasValue && request.OrderIndex.Value >= 0)
            {
                orderIndex = request.OrderIndex.Value;
            }
            else
            {
                IReadOnlyList<PageRecord> existing = await _pages.ListBySectionAsync(sectionId, userId, ct);
                orderIndex = existing.Count;
            }

            PageRecord page = new()
            {
                Id = pageId,
                DocumentId = section.DocumentId,
                SectionId = section.Id,
                Title = title,
                Content = content,
                OrderIndex = orderIndex,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

            await _pages.CreateAsync(page, ct);

            PageDto dto = new(
                page.Id,
                page.DocumentId,
                page.SectionId,
                page.Title,
                page.Content,
                page.OrderIndex,
                page.CreatedAt,
                page.UpdatedAt);

            return Ok(dto);
        }

        [HttpPut("pages/{pageId:guid}")]
        public async Task<ActionResult<PageDto>> UpdatePage(
            Guid pageId,
            [FromBody] PageUpdateRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            PageUpdate update = new(
                string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
                request.Content);

            PageRecord? page = await _pages.UpdateAsync(pageId, userId, update, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageDto dto = new(
                page.Id,
                page.DocumentId,
                page.SectionId,
                page.Title,
                page.Content,
                page.OrderIndex,
                page.CreatedAt,
                page.UpdatedAt);

            return Ok(dto);
        }

        [HttpDelete("pages/{pageId:guid}")]
        public async Task<IActionResult> DeletePage(Guid pageId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            bool removed = await _pages.DeleteAsync(pageId, userId, ct);
            return removed ? NoContent() : NotFound();
        }

        [HttpPost("pages/{pageId:guid}/move")]
        public async Task<ActionResult<PageDto>> MovePage(
            Guid pageId,
            [FromBody] PageMoveRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? targetSection = await _sections.GetAsync(request.TargetSectionId, userId, ct);
            if (targetSection is null)
            {
                return NotFound();
            }

            PageRecord? existing = await _pages.GetAsync(pageId, userId, ct);
            if (existing is null)
            {
                return NotFound();
            }

            if (existing.DocumentId != targetSection.DocumentId)
            {
                return BadRequest(new { message = "Target section must belong to the same document." });
            }

            PageRecord? moved = await _pages.MoveAsync(pageId, userId, request.TargetSectionId, request.TargetOrderIndex, ct);
            if (moved is null)
            {
                return NotFound();
            }

            PageDto dto = new(
                moved.Id,
                moved.DocumentId,
                moved.SectionId,
                moved.Title,
                moved.Content,
                moved.OrderIndex,
                moved.CreatedAt,
                moved.UpdatedAt);

            return Ok(dto);
        }

        [HttpGet("pages/{pageId:guid}/notes")]
        public async Task<ActionResult<PageNotesDto>> GetPageNotes(Guid pageId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageNoteRecord? notes = await _dbContext.PageNotes
                .FindAsync(new object?[] { pageId }, ct);

            if (notes is null)
            {
                return Ok(new PageNotesDto(pageId, string.Empty, DateTimeOffset.UtcNow));
            }

            return Ok(new PageNotesDto(notes.PageId, notes.Notes, notes.UpdatedAt));
        }

        [HttpPut("pages/{pageId:guid}/notes")]
        public async Task<ActionResult<PageNotesDto>> UpdatePageNotes(
            Guid pageId,
            [FromBody] PageNotesDto request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            string notesText = request.Notes ?? string.Empty;
            PageNoteRecord? notes = await _dbContext.PageNotes
                .FindAsync(new object?[] { pageId }, ct);

            if (notes is null)
            {
                notes = new PageNoteRecord
                {
                    PageId = pageId,
                    Notes = notesText,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.PageNotes.Add(notes);
            }
            else
            {
                notes.Notes = notesText;
                notes.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            return Ok(new PageNotesDto(pageId, notes.Notes, notes.UpdatedAt));
        }
    }
}
