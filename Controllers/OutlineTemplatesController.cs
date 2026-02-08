using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public sealed class OutlineTemplatesController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IStructureCommandProcessor _structureCommands;
        private readonly IConfiguration _configuration;

        public OutlineTemplatesController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IStructureCommandProcessor structureCommands,
            IConfiguration configuration)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _structureCommands = structureCommands ?? throw new ArgumentNullException(nameof(structureCommands));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpPost("outline-templates")]
        public async Task<ActionResult<OutlineTemplateDto>> CreateTemplate(
            [FromBody] OutlineTemplateCreateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Template name is required." });
            }

            if (request.Nodes is null || request.Nodes.Count == 0)
            {
                return BadRequest(new { message = "Template requires at least one node." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            OutlineTemplateRecord template = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = request.Name.Trim(),
                TemplateJson = JsonSerializer.Serialize(request.Nodes, JsonOptions),
                CreatedUtc = now,
                UpdatedUtc = now
            };

            _dbContext.OutlineTemplates.Add(template);
            await _dbContext.SaveChangesAsync(ct);

            return Ok(ToDto(template));
        }

        [HttpGet("outline-templates")]
        public async Task<ActionResult<IReadOnlyList<OutlineTemplateDto>>> ListTemplates(CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            List<OutlineTemplateDto> templates = await _dbContext.OutlineTemplates
                .AsNoTracking()
                .Where(template => template.OwnerUserId == userId)
                .OrderBy(template => template.Name)
                .Select(template => new OutlineTemplateDto(
                    template.Id,
                    template.Name,
                    template.CreatedUtc,
                    template.UpdatedUtc))
                .ToListAsync(ct);

            return Ok(templates);
        }

        [HttpDelete("outline-templates/{id:guid}")]
        public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            OutlineTemplateRecord? template = await _dbContext.OutlineTemplates
                .FirstOrDefaultAsync(item => item.Id == id && item.OwnerUserId == userId, ct);
            if (template is null)
            {
                return NotFound();
            }

            _dbContext.OutlineTemplates.Remove(template);
            await _dbContext.SaveChangesAsync(ct);
            return NoContent();
        }

        [HttpPost("documents/{documentId:guid}/outline/apply-template/{templateId:guid}")]
        public async Task<ActionResult<IReadOnlyList<DocumentOutlineNodeDto>>> ApplyTemplate(
            Guid documentId,
            Guid templateId,
            [FromBody] OutlineTemplateApplyOptionsDto? options,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _dbContext.Documents
                .FirstOrDefaultAsync(item => item.Id == documentId && item.OwnerUserId == userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            OutlineTemplateRecord? template = await _dbContext.OutlineTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == templateId && item.OwnerUserId == userId, ct);
            if (template is null)
            {
                return NotFound();
            }

            List<OutlineTemplateNodeDto>? nodes =
                JsonSerializer.Deserialize<List<OutlineTemplateNodeDto>>(template.TemplateJson, JsonOptions);
            if (nodes is null || nodes.Count == 0)
            {
                return BadRequest(new { message = "Template has no nodes." });
            }

            OutlineTemplateApplyOptionsDto settings = options ?? new OutlineTemplateApplyOptionsDto(null, false, "none");
            Guid? parentRoot = settings.ParentNodeId;
            if (parentRoot.HasValue)
            {
                bool parentExists = await _dbContext.DocumentOutlineNodes
                    .AnyAsync(node => node.DocumentId == documentId && node.Id == parentRoot.Value, ct);
                if (!parentExists)
                {
                    return BadRequest(new { message = "parentNodeId does not exist in this document." });
                }
            }

            List<SectionRecord> sections = await _dbContext.Sections
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

            Dictionary<string, SectionRecord> sectionByTitle = sections
                .GroupBy(section => NormalizeTitle(section.Title))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(group => group.Key, group => group.First());

            string strategy = (settings.LinkStrategy ?? "none").Trim().ToLowerInvariant();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            Dictionary<Guid, Guid> idMap = new();
            List<DocumentOutlineNodeRecord> newNodes = new();
            List<SectionRecord> newSections = new();
            List<PageRecord> newPages = new();

            int baseOrder = await _dbContext.DocumentOutlineNodes
                .Where(node => node.DocumentId == documentId && node.ParentId == parentRoot)
                .Select(node => (int?)node.Order)
                .MaxAsync(ct) ?? -1;

            List<OutlineTemplateNodeDto> ordered = nodes
                .OrderBy(node => node.ParentSourceId.HasValue ? 1 : 0)
                .ThenBy(node => node.Order)
                .ToList();

            foreach (OutlineTemplateNodeDto source in ordered)
            {
                Guid newId = Guid.NewGuid();
                idMap[source.SourceId] = newId;

                Guid? parentId = source.ParentSourceId.HasValue
                    ? idMap[source.ParentSourceId.Value]
                    : parentRoot;
                int order = source.ParentSourceId.HasValue ? source.Order : baseOrder + 1 + source.Order;

                Guid? linkedSectionId = null;
                if (string.Equals(source.NodeType, "scene", StringComparison.OrdinalIgnoreCase))
                {
                    linkedSectionId = ResolveSectionLink(
                        source,
                        strategy,
                        settings.CreateLinkedSections,
                        documentId,
                        sections,
                        sectionByTitle,
                        now,
                        newSections,
                        newPages);
                }

                newNodes.Add(new DocumentOutlineNodeRecord
                {
                    Id = newId,
                    DocumentId = documentId,
                    ParentId = parentId,
                    Order = Math.Max(0, order),
                    Title = source.Title,
                    Notes = source.Notes,
                    MetadataJson = source.MetadataJson,
                    LinkedSectionId = linkedSectionId
                });
            }

            if (IsUndoEnabled())
            {
                await _structureCommands.ExecuteAsync(
                    new ApplyOutlineTemplateCommand(userId, documentId, newNodes, newSections, newPages),
                    ct);
            }
            else
            {
                if (newSections.Count > 0)
                {
                    _dbContext.Sections.AddRange(newSections);
                }

                if (newPages.Count > 0)
                {
                    _dbContext.Pages.AddRange(newPages);
                }

                _dbContext.DocumentOutlineNodes.AddRange(newNodes);
                await _dbContext.SaveChangesAsync(ct);
            }

            List<DocumentOutlineNodeDto> result = await _dbContext.DocumentOutlineNodes
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
                    node.LinkedSectionId,
                    node.MetadataJson))
                .ToListAsync(ct);

            return Ok(result);
        }

        private Guid? ResolveSectionLink(
            OutlineTemplateNodeDto source,
            string strategy,
            bool createLinkedSections,
            Guid documentId,
            List<SectionRecord> sections,
            Dictionary<string, SectionRecord> sectionByTitle,
            DateTimeOffset now,
            List<SectionRecord> newSections,
            List<PageRecord> newPages)
        {
            if (strategy == "by-title")
            {
                string key = NormalizeTitle(source.Title);
                if (!string.IsNullOrWhiteSpace(key) && sectionByTitle.TryGetValue(key, out SectionRecord? byTitle))
                {
                    return byTitle.Id;
                }
            }

            if (strategy == "create" || createLinkedSections)
            {
                int nextOrder = sections.Count == 0 ? 0 : sections.Max(section => section.OrderIndex) + 1;
                SectionRecord section = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    Title = string.IsNullOrWhiteSpace(source.Title) ? "Untitled scene" : source.Title.Trim(),
                    NarrativePurpose = null,
                    OrderIndex = nextOrder,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                sections.Add(section);
                newSections.Add(section);
                string normalized = NormalizeTitle(section.Title);
                if (!string.IsNullOrWhiteSpace(normalized) && !sectionByTitle.ContainsKey(normalized))
                {
                    sectionByTitle[normalized] = section;
                }

                newPages.Add(new PageRecord
                {
                    Id = Guid.NewGuid(),
                    DocumentId = documentId,
                    SectionId = section.Id,
                    Title = "Page 1",
                    Content = string.Empty,
                    OrderIndex = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                return section.Id;
            }

            return null;
        }

        private static string NormalizeTitle(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }

        private bool IsUndoEnabled()
        {
            return _configuration.GetValue<bool?>("Workflow:OutlineUndoEnabled")
                ?? _configuration.GetValue<bool?>("WriterApp:Workflow:OutlineUndoEnabled")
                ?? false;
        }

        private bool IsEnabled()
        {
            return _configuration.GetValue<bool?>("Workflow:OutlineTemplatesEnabled")
                ?? _configuration.GetValue<bool?>("WriterApp:Workflow:OutlineTemplatesEnabled")
                ?? false;
        }

        private static OutlineTemplateDto ToDto(OutlineTemplateRecord template)
        {
            return new OutlineTemplateDto(
                template.Id,
                template.Name,
                template.CreatedUtc,
                template.UpdatedUtc);
        }
    }
}
