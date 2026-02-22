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
    [Route("api/pages/{pageId:guid}/annotations")]
    [Authorize]
    public sealed class PageAnnotationsController : ControllerBase
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
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;

        public PageAnnotationsController(
            AppDbContext dbContext,
            IPageRepository pages,
            IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<PageAnnotationDto>>> List(
            Guid pageId,
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

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            string statusFilter = string.IsNullOrWhiteSpace(status) ? "open" : status.Trim();
            if (!AllowedStatuses.Contains(statusFilter))
            {
                return BadRequest(new { message = "status must be open, resolved, or all." });
            }

            IQueryable<PageAnnotationRecord> query = _dbContext.PageAnnotations
                .AsNoTracking()
                .Where(annotation => annotation.PageId == pageId);

            if (!string.IsNullOrWhiteSpace(kind))
            {
                string kindFilter = kind.Trim();
                if (!AllowedKinds.Contains(kindFilter))
                {
                    return BadRequest(new { message = "kind must be comment, todo, or highlight." });
                }

                query = query.Where(annotation => annotation.Kind == kindFilter);
            }

            if (!string.Equals(statusFilter, "all", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(annotation => annotation.Status == statusFilter);
            }

            List<PageAnnotationRecord> records = await query.ToListAsync(ct);
            List<PageAnnotationDto> result = records
                .OrderBy(record => record.CreatedAt)
                .Select(record => new PageAnnotationDto(
                    record.Id,
                    record.DocumentId,
                    record.PageId,
                    record.Kind,
                    record.Status,
                    record.AnchorFrom,
                    record.AnchorTo,
                    record.AnchorText,
                    record.Content,
                    record.AuthorUserId,
                    record.CreatedAt,
                    record.ResolvedAt))
                .ToList();

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<PageAnnotationDto>> Create(
            Guid pageId,
            [FromBody] PageAnnotationCreateRequest request,
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

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
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

            PageAnnotationRecord record = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = page.DocumentId,
                PageId = pageId,
                Kind = request.Kind.Trim(),
                Status = "open",
                AnchorFrom = from,
                AnchorTo = to,
                AnchorText = request.AnchorText ?? string.Empty,
                Content = content,
                AuthorUserId = userId,
                CreatedAt = DateTimeOffset.UtcNow,
                ResolvedAt = null
            };

            _dbContext.PageAnnotations.Add(record);
            await _dbContext.SaveChangesAsync(ct);

            return Ok(MapToDto(record));
        }

        [HttpPut("{annotationId:guid}")]
        public async Task<ActionResult<PageAnnotationDto>> Update(
            Guid pageId,
            Guid annotationId,
            [FromBody] PageAnnotationUpdateRequest request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
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

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageAnnotationRecord? record = await _dbContext.PageAnnotations
                .FirstOrDefaultAsync(annotation => annotation.Id == annotationId && annotation.PageId == pageId, ct);
            if (record is null)
            {
                return NotFound();
            }

            if (string.Equals(record.Kind, "highlight", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Highlights do not support content edits." });
            }

            record.Content = request.Content?.Trim() ?? string.Empty;
            await _dbContext.SaveChangesAsync(ct);
            return Ok(MapToDto(record));
        }

        [HttpPut("anchors")]
        public async Task<IActionResult> UpdateAnchors(
            Guid pageId,
            [FromBody] IReadOnlyList<PageAnnotationAnchorUpdateRequest> updates,
            CancellationToken ct)
        {
            if (updates is null)
            {
                return BadRequest(new { message = "Request body is required." });
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

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            HashSet<Guid> ids = updates.Select(update => update.Id).ToHashSet();
            if (ids.Count == 0)
            {
                return NoContent();
            }

            List<PageAnnotationRecord> records = await _dbContext.PageAnnotations
                .Where(annotation => annotation.PageId == pageId && ids.Contains(annotation.Id))
                .ToListAsync(ct);

            Dictionary<Guid, PageAnnotationAnchorUpdateRequest> byId = updates
                .GroupBy(update => update.Id)
                .ToDictionary(group => group.Key, group => group.Last());

            foreach (PageAnnotationRecord record in records)
            {
                if (!byId.TryGetValue(record.Id, out PageAnnotationAnchorUpdateRequest? update))
                {
                    continue;
                }

                (int from, int to) = NormalizeRange(update.AnchorFrom, update.AnchorTo);
                record.AnchorFrom = from;
                record.AnchorTo = to;
                record.AnchorText = update.AnchorText ?? string.Empty;
            }

            await _dbContext.SaveChangesAsync(ct);
            return NoContent();
        }

        [HttpPost("{annotationId:guid}/resolve")]
        public async Task<IActionResult> Resolve(Guid pageId, Guid annotationId, CancellationToken ct)
        {
            return await SetResolvedAsync(pageId, annotationId, true, ct);
        }

        [HttpPost("{annotationId:guid}/reopen")]
        public async Task<IActionResult> Reopen(Guid pageId, Guid annotationId, CancellationToken ct)
        {
            return await SetResolvedAsync(pageId, annotationId, false, ct);
        }

        private async Task<IActionResult> SetResolvedAsync(
            Guid pageId,
            Guid annotationId,
            bool resolved,
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

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageAnnotationRecord? record = await _dbContext.PageAnnotations
                .FirstOrDefaultAsync(annotation => annotation.Id == annotationId && annotation.PageId == pageId, ct);
            if (record is null)
            {
                return NotFound();
            }

            record.Status = resolved ? "resolved" : "open";
            record.ResolvedAt = resolved ? DateTimeOffset.UtcNow : null;
            await _dbContext.SaveChangesAsync(ct);
            return NoContent();
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

        private static PageAnnotationDto MapToDto(PageAnnotationRecord record)
        {
            return new PageAnnotationDto(
                record.Id,
                record.DocumentId,
                record.PageId,
                record.Kind,
                record.Status,
                record.AnchorFrom,
                record.AnchorTo,
                record.AnchorText,
                record.Content,
                record.AuthorUserId,
                record.CreatedAt,
                record.ResolvedAt);
        }
    }
}
