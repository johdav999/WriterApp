using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
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
        private readonly ISearchIndexService _searchIndex;
        private readonly IPageVersionService _pageVersions;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<PagesController> _logger;

        public PagesController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            ISearchIndexService searchIndex,
            IPageVersionService pageVersions,
            AppDbContext dbContext,
            ILogger<PagesController> logger)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _pageVersions = pageVersions ?? throw new ArgumentNullException(nameof(pageVersions));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("sections/{sectionId:guid}/pages")]
        public async Task<ActionResult<IReadOnlyList<PageDto>>> ListPages(Guid sectionId, CancellationToken ct)
        {
            string traceId = Request.Headers["X-Trace-Id"].FirstOrDefault()
                ?? HttpContext.TraceIdentifier;
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _sections.ExistsAsync(sectionId, userId, ct))
            {
                return NotFound();
            }

            _logger.LogDebug("GET_PAGE_BEGIN TraceId={TraceId} SectionId={SectionId}", traceId, sectionId);

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

            foreach (PageRecord page in pages)
            {
                ContentFingerprint fp = BuildFingerprint(page.Content);
                _logger.LogDebug(
                    "GET_PAGE_RETURN TraceId={TraceId} PageId={PageId} JsonLength={JsonLength} TextLength={TextLength} Hash={Hash}",
                    traceId,
                    page.Id,
                    fp.JsonLength,
                    fp.TextLength,
                    fp.Hash);
            }

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
            await _searchIndex.UpsertPageAsync(page, ct);
            await _pageVersions.CreateAutosnapshotIfDueAsync(
                userId,
                page,
                page.Content ?? string.Empty,
                TimeSpan.FromSeconds(30),
                ct);

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
            string traceId = Request.Headers["X-Trace-Id"].FirstOrDefault()
                ?? HttpContext.TraceIdentifier;
            string userId = _userIdResolver.ResolveUserId(User);
            PageUpdate update = new(
                string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
                request.Content);

            PageRecord? before = await _pages.GetAsync(pageId, userId, ct);
            if (before is null)
            {
                return NotFound();
            }

            ContentFingerprint incomingFp = BuildFingerprint(request.Content ?? string.Empty);
            ContentFingerprint beforeFp = BuildFingerprint(before.Content ?? string.Empty);
            _logger.LogDebug(
                "PUT_BEGIN TraceId={TraceId} PageId={PageId} JsonLength={JsonLength} TextLength={TextLength} Hash={Hash}",
                traceId,
                pageId,
                incomingFp.JsonLength,
                incomingFp.TextLength,
                incomingFp.Hash);
            _logger.LogDebug(
                "DB_BEFORE TraceId={TraceId} PageId={PageId} JsonLength={JsonLength} TextLength={TextLength} Hash={Hash}",
                traceId,
                pageId,
                beforeFp.JsonLength,
                beforeFp.TextLength,
                beforeFp.Hash);

            PageRecord? page = await _pages.UpdateAsync(pageId, userId, update, ct);
            if (page is null)
            {
                return NotFound();
            }

            await _searchIndex.UpsertPageAsync(page, ct);
            await _pageVersions.CreateAutosnapshotIfDueAsync(
                userId,
                page,
                page.Content ?? string.Empty,
                TimeSpan.FromSeconds(30),
                ct);

            ContentFingerprint afterFp = BuildFingerprint(page.Content ?? string.Empty);
            _logger.LogDebug(
                "DB_AFTER TraceId={TraceId} PageId={PageId} JsonLength={JsonLength} TextLength={TextLength} Hash={Hash}",
                traceId,
                pageId,
                afterFp.JsonLength,
                afterFp.TextLength,
                afterFp.Hash);

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

        private readonly record struct ContentFingerprint(int JsonLength, int TextLength, string Hash);

        private static ContentFingerprint BuildFingerprint(string content)
        {
            string value = content ?? string.Empty;
            int jsonLength = value.Length;
            int textLength = StripHtml(value).Length;
            string hash = ComputeShortHash(value);
            return new ContentFingerprint(jsonLength, textLength, hash);
        }

        private static string StripHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
        }

        private static string ComputeShortHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "0";
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
            byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexString(hash.AsSpan(0, 4));
        }

        [HttpDelete("pages/{pageId:guid}")]
        public async Task<IActionResult> DeletePage(Guid pageId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            bool removed = await _pages.DeleteAsync(pageId, userId, ct);
            if (removed)
            {
                await _searchIndex.DeleteByEntityAsync(SearchEntityTypes.Page, pageId, ct);
                await _searchIndex.DeleteByEntityAsync(SearchEntityTypes.Note, pageId, ct);
            }
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

            await _searchIndex.UpsertPageAsync(moved, ct);

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
            await _searchIndex.UpsertPageNotesAsync(page, notes, ct);
            return Ok(new PageNotesDto(pageId, notes.Notes, notes.UpdatedAt));
        }
    }
}
