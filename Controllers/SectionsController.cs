using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Data;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using WriterApp.Application.Diagnostics;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/sections")]
    [Authorize]
    public sealed class SectionsController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ISectionRepository _sections;
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<SectionsController> _logger;
        private readonly IConfiguration _configuration;

        public SectionsController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            AppDbContext dbContext,
            ILogger<SectionsController> logger,
            IConfiguration configuration)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SectionDto>>> ListSections(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            IReadOnlyList<SectionRecord> sections = await _sections.ListByDocumentAsync(documentId, userId, ct);
            List<SectionDto> result = sections
                .Select(section => new SectionDto(
                    section.Id,
                    section.DocumentId,
                    section.Title,
                    section.NarrativePurpose,
                    section.OrderIndex,
                    section.CreatedAt,
                    section.UpdatedAt))
                .ToList();

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<SectionDto>> CreateSection(
            Guid documentId,
            [FromBody] SectionCreateRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            Guid sectionId = request.Id ?? Guid.NewGuid();
            if (request.Id.HasValue)
            {
                SectionRecord? existing = await _sections.GetAsync(sectionId, userId, ct);
                if (existing is not null)
                {
                    if (existing.DocumentId != documentId)
                    {
                        return Conflict(new { message = "Section already exists under a different document." });
                    }

                    return Ok(new SectionDto(
                        existing.Id,
                        existing.DocumentId,
                        existing.Title,
                        existing.NarrativePurpose,
                        existing.OrderIndex,
                        existing.CreatedAt,
                        existing.UpdatedAt));
                }
            }

            string title = string.IsNullOrWhiteSpace(request.Title) ? "Section" : request.Title.Trim();
            DateTimeOffset createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow;
            DateTimeOffset updatedAt = request.UpdatedAt ?? createdAt;

            int orderIndex;
            if (request.OrderIndex.HasValue && request.OrderIndex.Value >= 0)
            {
                orderIndex = request.OrderIndex.Value;
            }
            else
            {
                IReadOnlyList<SectionRecord> existing = await _sections.ListByDocumentAsync(documentId, userId, ct);
                orderIndex = existing.Count;
            }

            SectionRecord section = new()
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = title,
                NarrativePurpose = string.IsNullOrWhiteSpace(request.NarrativePurpose)
                    ? null
                    : request.NarrativePurpose.Trim(),
                OrderIndex = orderIndex,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            await _sections.CreateAsync(section, ct);

            // Ensure every section has at least one page so the editor can render content.
            PageRecord page = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                SectionId = section.Id,
                Title = "Page 1",
                Content = string.Empty,
                OrderIndex = 0,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };
            await _pages.CreateAsync(page, ct);
            await transaction.CommitAsync(ct);

            SectionDto dto = new(
                section.Id,
                section.DocumentId,
                section.Title,
                section.NarrativePurpose,
                section.OrderIndex,
                section.CreatedAt,
                section.UpdatedAt);

            return Ok(dto);
        }

        [HttpPost("reorder")]
        public async Task<ActionResult<IReadOnlyList<SectionDto>>> ReorderSections(
            Guid documentId,
            [FromBody] SectionReorderRequest request,
            CancellationToken ct)
        {
            Stopwatch timer = Stopwatch.StartNew();
            string correlationId = Request.Headers.TryGetValue("X-Reorder-Correlation", out var header)
                ? header.ToString()
                : Guid.NewGuid().ToString("N");
            Response.Headers["X-Reorder-Correlation"] = correlationId;

            if (request.OrderedSectionIds is null || request.OrderedSectionIds.Count == 0)
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Reject missing orderedSectionIds DocId={DocumentId} Corr={CorrelationId}",
                    documentId,
                    correlationId);
                return BadRequest(new { message = "orderedSectionIds is required." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            SectionReorderDiagnostics.LogDebug(
                _logger,
                _configuration,
                "Entry DocId={DocumentId} UserId={UserId} Count={Count} Corr={CorrelationId}",
                documentId,
                userId,
                request.OrderedSectionIds.Count,
                correlationId);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Document not found DocId={DocumentId} Corr={CorrelationId}",
                    documentId,
                    correlationId);
                return NotFound();
            }

            IReadOnlyList<SectionRecord> existing = await _sections.ListByDocumentAsync(documentId, userId, ct);
            if (existing.Count != request.OrderedSectionIds.Count)
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Count mismatch Existing={ExistingCount} Payload={PayloadCount} DocId={DocumentId} Corr={CorrelationId}",
                    existing.Count,
                    request.OrderedSectionIds.Count,
                    documentId,
                    correlationId);
                return BadRequest(new { message = "orderedSectionIds does not match document sections." });
            }

            HashSet<Guid> unique = new(request.OrderedSectionIds);
            if (unique.Count != request.OrderedSectionIds.Count)
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Duplicate ids PayloadCount={PayloadCount} UniqueCount={UniqueCount} DocId={DocumentId} Corr={CorrelationId}",
                    request.OrderedSectionIds.Count,
                    unique.Count,
                    documentId,
                    correlationId);
                return BadRequest(new { message = "orderedSectionIds contains duplicates." });
            }

            HashSet<Guid> existingIds = existing.Select(section => section.Id).ToHashSet();
            if (!existingIds.SetEquals(unique))
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Id mismatch ExistingCount={ExistingCount} PayloadCount={PayloadCount} DocId={DocumentId} Corr={CorrelationId}",
                    existingIds.Count,
                    unique.Count,
                    documentId,
                    correlationId);
                return BadRequest(new { message = "orderedSectionIds must contain all document sections." });
            }

            SectionReorderDiagnostics.LogDebug(
                _logger,
                _configuration,
                "Before order DocId={DocumentId} FirstId={FirstId} LastId={LastId} Corr={CorrelationId}",
                documentId,
                existing.OrderBy(section => section.OrderIndex).Select(section => section.Id).FirstOrDefault(),
                existing.OrderBy(section => section.OrderIndex).Select(section => section.Id).LastOrDefault(),
                correlationId);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            List<SectionRecord> tracked = await _dbContext.Sections
                .Where(section => section.DocumentId == documentId && unique.Contains(section.Id))
                .ToListAsync(ct);

            Dictionary<Guid, int> ordering = new();
            for (int index = 0; index < request.OrderedSectionIds.Count; index++)
            {
                ordering[request.OrderedSectionIds[index]] = index;
            }

            foreach (SectionRecord section in tracked)
            {
                if (ordering.TryGetValue(section.Id, out int order))
                {
                    section.OrderIndex = order;
                    section.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            int saved = await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            List<SectionDto> result = tracked
                .OrderBy(section => section.OrderIndex)
                .Select(section => new SectionDto(
                    section.Id,
                    section.DocumentId,
                    section.Title,
                    section.NarrativePurpose,
                    section.OrderIndex,
                    section.CreatedAt,
                    section.UpdatedAt))
                .ToList();

            SectionReorderDiagnostics.LogDebug(
                _logger,
                _configuration,
                "Saved DocId={DocumentId} Updated={Updated} Returned={Returned} ElapsedMs={ElapsedMs} Corr={CorrelationId}",
                documentId,
                saved,
                result.Count,
                timer.ElapsedMilliseconds,
                correlationId);

            return Ok(result);
        }

        [HttpPut("{sectionId:guid}")]
        public async Task<ActionResult<SectionDto>> UpdateSection(
            Guid documentId,
            Guid sectionId,
            [FromBody] SectionUpdateRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            SectionUpdate update = new(request.Title, request.NarrativePurpose);
            SectionRecord? updated = await _sections.UpdateAsync(sectionId, userId, update, ct);
            if (updated is null)
            {
                return NotFound();
            }

            SectionDto dto = new(
                updated.Id,
                updated.DocumentId,
                updated.Title,
                updated.NarrativePurpose,
                updated.OrderIndex,
                updated.CreatedAt,
                updated.UpdatedAt);

            return Ok(dto);
        }

        [HttpDelete("{sectionId:guid}")]
        public async Task<IActionResult> DeleteSection(Guid documentId, Guid sectionId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            SectionRecord? target = await _sections.GetAsync(sectionId, userId, ct);
            if (target is null || target.DocumentId != documentId)
            {
                return NotFound();
            }

            IReadOnlyList<SectionRecord> existing = await _sections.ListByDocumentAsync(documentId, userId, ct);
            if (existing.Count <= 1)
            {
                return Conflict(new { message = "Document must have at least one section." });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            SectionRecord? removed = await _sections.DeleteAsync(sectionId, userId, ct);
            if (removed is null)
            {
                await transaction.RollbackAsync(ct);
                return NotFound();
            }

            List<SectionRecord> remaining = await _dbContext.Sections
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

            bool reordered = false;
            for (int index = 0; index < remaining.Count; index++)
            {
                if (remaining[index].OrderIndex != index)
                {
                    remaining[index].OrderIndex = index;
                    remaining[index].UpdatedAt = DateTimeOffset.UtcNow;
                    reordered = true;
                }
            }

            if (reordered)
            {
                DocumentRecord? document = await _dbContext.Documents
                    .FirstOrDefaultAsync(item => item.Id == documentId, ct);
                if (document is not null)
                {
                    document.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await _dbContext.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);
            return NoContent();
        }
    }
}
