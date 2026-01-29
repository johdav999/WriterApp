using System;
using System.Collections.Generic;
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
    [Route("api/documents/{documentId:guid}/outline")]
    [Authorize]
    public sealed class DocumentOutlineController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly IUserIdResolver _userIdResolver;
        private readonly AppDbContext _dbContext;

        public DocumentOutlineController(
            IDocumentRepository documents,
            IUserIdResolver userIdResolver,
            AppDbContext dbContext)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpGet]
        public async Task<ActionResult<DocumentOutlineDto>> GetOutline(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            DocumentOutlineRecord? outline = await _dbContext.DocumentOutlines
                .FindAsync(new object?[] { documentId }, ct);

            if (outline is null)
            {
                string derived = await BuildOutlineFromNodesAsync(documentId, ct);
                return Ok(new DocumentOutlineDto(documentId, derived, DateTimeOffset.UtcNow));
            }

            return Ok(new DocumentOutlineDto(outline.DocumentId, outline.Outline, outline.UpdatedAt));
        }

        [HttpPut]
        public async Task<ActionResult<DocumentOutlineDto>> UpdateOutline(
            Guid documentId,
            [FromBody] DocumentOutlineDto request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            string outlineText = request.Outline ?? string.Empty;
            DocumentOutlineRecord? outline = await _dbContext.DocumentOutlines
                .FindAsync(new object?[] { documentId }, ct);

            if (outline is null)
            {
                outline = new DocumentOutlineRecord
                {
                    DocumentId = documentId,
                    Outline = outlineText,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.DocumentOutlines.Add(outline);
            }
            else
            {
                outline.Outline = outlineText;
                outline.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            return Ok(new DocumentOutlineDto(documentId, outline.Outline, outline.UpdatedAt));
        }

        [HttpGet("nodes")]
        public async Task<ActionResult<IReadOnlyList<DocumentOutlineNodeDto>>> GetOutlineNodes(
            Guid documentId,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            List<DocumentOutlineNodeDto> nodes = await _dbContext.DocumentOutlineNodes
                .AsNoTracking()
                .Where(node => node.DocumentId == documentId)
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.Order)
                .Select(node => new DocumentOutlineNodeDto(
                    node.Id,
                    node.DocumentId,
                    node.ParentId,
                    node.Order,
                    node.Title,
                    node.Notes,
                    node.LinkedSectionId))
                .ToListAsync(ct);

            return Ok(nodes);
        }

        [HttpPut("nodes")]
        public async Task<ActionResult<IReadOnlyList<DocumentOutlineNodeDto>>> UpdateOutlineNodes(
            Guid documentId,
            [FromBody] List<DocumentOutlineNodeDto> nodes,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            nodes ??= new List<DocumentOutlineNodeDto>();
            HashSet<Guid> ids = new(nodes.Select(node => node.Id));
            if (ids.Count != nodes.Count)
            {
                return BadRequest(new { message = "Outline node ids must be unique." });
            }

            HashSet<Guid> parentIds = nodes
                .Where(node => node.ParentId.HasValue)
                .Select(node => node.ParentId!.Value)
                .ToHashSet();
            if (!parentIds.IsSubsetOf(ids))
            {
                return BadRequest(new { message = "Outline parentId must reference a node in the same document." });
            }

            List<Guid> sectionIdList = await _dbContext.Sections
                .Where(section => section.DocumentId == documentId)
                .Select(section => section.Id)
                .ToListAsync(ct);
            HashSet<Guid> sectionIds = new(sectionIdList);

            foreach (DocumentOutlineNodeDto node in nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Title))
                {
                    return BadRequest(new { message = "Outline node title is required." });
                }

                if (node.LinkedSectionId.HasValue && !sectionIds.Contains(node.LinkedSectionId.Value))
                {
                    return BadRequest(new { message = "Linked section must belong to the document." });
                }
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            List<DocumentOutlineNodeRecord> existing = await _dbContext.DocumentOutlineNodes
                .Where(node => node.DocumentId == documentId)
                .ToListAsync(ct);
            if (existing.Count > 0)
            {
                _dbContext.DocumentOutlineNodes.RemoveRange(existing);
                await _dbContext.SaveChangesAsync(ct);
            }

            List<DocumentOutlineNodeRecord> records = nodes
                .Select(node => new DocumentOutlineNodeRecord
                {
                    Id = node.Id == Guid.Empty ? Guid.NewGuid() : node.Id,
                    DocumentId = documentId,
                    ParentId = node.ParentId,
                    Order = node.Order,
                    Title = node.Title.Trim(),
                    Notes = string.IsNullOrWhiteSpace(node.Notes) ? null : node.Notes.Trim(),
                    LinkedSectionId = node.LinkedSectionId
                })
                .ToList();

            _dbContext.DocumentOutlineNodes.AddRange(records);

            string outlineText = BuildOutlineText(records);
            DocumentOutlineRecord? outline = await _dbContext.DocumentOutlines
                .FindAsync(new object?[] { documentId }, ct);
            if (outline is null)
            {
                outline = new DocumentOutlineRecord
                {
                    DocumentId = documentId,
                    Outline = outlineText,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.DocumentOutlines.Add(outline);
            }
            else
            {
                outline.Outline = outlineText;
                outline.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            List<DocumentOutlineNodeDto> result = records
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.Order)
                .Select(node => new DocumentOutlineNodeDto(
                    node.Id,
                    node.DocumentId,
                    node.ParentId,
                    node.Order,
                    node.Title,
                    node.Notes,
                    node.LinkedSectionId))
                .ToList();

            return Ok(result);
        }

        [HttpPost("nodes/{nodeId:guid}/link-section")]
        public async Task<ActionResult<DocumentOutlineNodeDto>> LinkSectionToNode(
            Guid documentId,
            Guid nodeId,
            [FromBody] DocumentOutlineLinkRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            DocumentOutlineNodeRecord? node = await _dbContext.DocumentOutlineNodes
                .FirstOrDefaultAsync(entry => entry.Id == nodeId && entry.DocumentId == documentId, ct);
            if (node is null)
            {
                return NotFound();
            }

            Guid? sectionId = request?.SectionId;
            if (sectionId.HasValue)
            {
                bool sectionExists = await _dbContext.Sections
                    .AnyAsync(section => section.DocumentId == documentId && section.Id == sectionId.Value, ct);
                if (!sectionExists)
                {
                    return BadRequest(new { message = "Linked section must belong to the document." });
                }
            }

            node.LinkedSectionId = sectionId;
            await _dbContext.SaveChangesAsync(ct);

            return Ok(new DocumentOutlineNodeDto(
                node.Id,
                node.DocumentId,
                node.ParentId,
                node.Order,
                node.Title,
                node.Notes,
                node.LinkedSectionId));
        }

        [HttpPost("apply-to-sections")]
        public async Task<ActionResult<OutlineApplyResultDto>> ApplyOutlineToSections(
            Guid documentId,
            [FromBody] OutlineApplyOptionsDto? options,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            OutlineApplyOptionsDto settings = options ?? new OutlineApplyOptionsDto();
            List<DocumentOutlineNodeRecord> allNodes = await _dbContext.DocumentOutlineNodes
                .Where(node => node.DocumentId == documentId)
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.Order)
                .ToListAsync(ct);

            List<DocumentOutlineNodeRecord> applyNodes = GetNodesByDepth(allNodes, settings.MaxDepth ?? 1);
            if (applyNodes.Count == 0)
            {
                return Ok(new OutlineApplyResultDto(Array.Empty<SectionDto>(), Array.Empty<DocumentOutlineNodeDto>()));
            }

            List<SectionRecord> sections = await _dbContext.Sections
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

            Dictionary<Guid, SectionRecord> sectionsById = sections.ToDictionary(section => section.Id);
            Dictionary<string, SectionRecord> sectionsByTitle = sections
                .GroupBy(section => NormalizeTitle(section.Title))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(group => group.Key, group => group.First());

            DateTimeOffset now = DateTimeOffset.UtcNow;
            List<SectionRecord> ordered = new();
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            foreach (DocumentOutlineNodeRecord node in applyNodes)
            {
                SectionRecord? section = null;
                if (node.LinkedSectionId.HasValue && sectionsById.TryGetValue(node.LinkedSectionId.Value, out SectionRecord linked))
                {
                    section = linked;
                }
                else if (settings.MatchByTitle)
                {
                    string normalized = NormalizeTitle(node.Title);
                    if (!string.IsNullOrWhiteSpace(normalized) && sectionsByTitle.TryGetValue(normalized, out SectionRecord byTitle))
                    {
                        section = byTitle;
                    }
                }

                if (section is null && settings.CreateMissingSections)
                {
                    section = new SectionRecord
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        Title = node.Title.Trim(),
                        NarrativePurpose = null,
                        OrderIndex = sections.Count,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.Sections.Add(section);
                    sections.Add(section);
                    sectionsById[section.Id] = section;
                    string normalized = NormalizeTitle(section.Title);
                    if (!string.IsNullOrWhiteSpace(normalized) && !sectionsByTitle.ContainsKey(normalized))
                    {
                        sectionsByTitle[normalized] = section;
                    }

                    PageRecord page = new()
                    {
                        Id = Guid.NewGuid(),
                        DocumentId = documentId,
                        SectionId = section.Id,
                        Title = "Page 1",
                        Content = string.Empty,
                        OrderIndex = 0,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.Pages.Add(page);
                }

                if (section is not null)
                {
                    if (settings.RenameSections && !string.Equals(section.Title, node.Title, StringComparison.Ordinal))
                    {
                        section.Title = node.Title.Trim();
                        section.UpdatedAt = now;
                    }

                    ordered.Add(section);
                    if (settings.LinkNodesToSections && node.LinkedSectionId != section.Id)
                    {
                        node.LinkedSectionId = section.Id;
                    }
                }
            }

            if (settings.ReorderSections)
            {
                HashSet<Guid> included = ordered.Select(section => section.Id).ToHashSet();
                foreach (SectionRecord remaining in sections.Where(section => !included.Contains(section.Id)))
                {
                    ordered.Add(remaining);
                }

                for (int index = 0; index < ordered.Count; index++)
                {
                    if (ordered[index].OrderIndex != index)
                    {
                        ordered[index].OrderIndex = index;
                        ordered[index].UpdatedAt = now;
                    }
                }
            }

            document.UpdatedAt = now;
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            List<SectionDto> sectionDtos = ordered
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

            List<DocumentOutlineNodeDto> nodeDtos = await _dbContext.DocumentOutlineNodes
                .Where(node => node.DocumentId == documentId)
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.Order)
                .Select(node => new DocumentOutlineNodeDto(
                    node.Id,
                    node.DocumentId,
                    node.ParentId,
                    node.Order,
                    node.Title,
                    node.Notes,
                    node.LinkedSectionId))
                .ToListAsync(ct);

            return Ok(new OutlineApplyResultDto(sectionDtos, nodeDtos));
        }

        private async Task<string> BuildOutlineFromNodesAsync(Guid documentId, CancellationToken ct)
        {
            List<DocumentOutlineNodeRecord> nodes = await _dbContext.DocumentOutlineNodes
                .AsNoTracking()
                .Where(node => node.DocumentId == documentId)
                .ToListAsync(ct);
            return BuildOutlineText(nodes);
        }

        private static string BuildOutlineText(IEnumerable<DocumentOutlineNodeRecord> nodes)
        {
            List<DocumentOutlineNodeRecord> roots = nodes
                .Where(node => node.ParentId is null)
                .OrderBy(node => node.Order)
                .ToList();
            Dictionary<Guid, List<DocumentOutlineNodeRecord>> byParent = nodes
                .Where(node => node.ParentId.HasValue)
                .GroupBy(node => node.ParentId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(node => node.Order).ToList());

            List<string> lines = new();
            void Walk(Guid? parentId, int depth)
            {
                List<DocumentOutlineNodeRecord> children;
                if (parentId is null)
                {
                    children = roots;
                }
                else if (!byParent.TryGetValue(parentId.Value, out children))
                {
                    return;
                }

                foreach (DocumentOutlineNodeRecord child in children)
                {
                    string indent = new string(' ', depth * 2);
                    lines.Add($"{indent}- {child.Title}");
                    Walk(child.Id, depth + 1);
                }
            }

            Walk(null, 0);
            return string.Join(Environment.NewLine, lines);
        }

        private static string NormalizeTitle(string? title)
        {
            return string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim().ToLowerInvariant();
        }

        private static List<DocumentOutlineNodeRecord> GetNodesByDepth(
            List<DocumentOutlineNodeRecord> nodes,
            int maxDepth)
        {
            if (maxDepth <= 0)
            {
                return new List<DocumentOutlineNodeRecord>();
            }

            List<DocumentOutlineNodeRecord> roots = nodes
                .Where(node => node.ParentId is null)
                .OrderBy(node => node.Order)
                .ToList();
            Dictionary<Guid, List<DocumentOutlineNodeRecord>> byParent = nodes
                .Where(node => node.ParentId.HasValue)
                .GroupBy(node => node.ParentId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(node => node.Order).ToList());

            List<DocumentOutlineNodeRecord> result = new();
            void Walk(Guid? parentId, int depth)
            {
                if (depth > maxDepth)
                {
                    return;
                }

                List<DocumentOutlineNodeRecord> children;
                if (parentId is null)
                {
                    children = roots;
                }
                else if (!byParent.TryGetValue(parentId.Value, out children))
                {
                    return;
                }

                foreach (DocumentOutlineNodeRecord child in children)
                {
                    result.Add(child);
                    Walk(child.Id, depth + 1);
                }
            }

            Walk(null, 1);
            return result;
        }
    }
}
