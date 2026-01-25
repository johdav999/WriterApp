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

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/sections")]
    [Authorize]
    public sealed class SectionsController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ISectionRepository _sections;
        private readonly IUserIdResolver _userIdResolver;

        public SectionsController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IUserIdResolver userIdResolver)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
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

            await _sections.CreateAsync(section, ct);

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
    }
}
