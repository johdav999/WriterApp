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
            ActionResult? disabled = RejectIfDisabled();
            if (disabled is not null)
            {
                return disabled;
            }

            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Template name is required." });
            }

            if (request.Nodes is null || request.Nodes.Count == 0)
            {
                return BadRequest(new { message = "Template requires at least one node." });
            }

            if (!TryValidateTemplateNodes(request.Nodes, out string validationError))
            {
                return BadRequest(new { message = validationError });
            }

            List<OutlineTemplateNodeDto> normalizedNodes = NormalizeTemplateNodes(request.Nodes);
            string userId = _userIdResolver.ResolveUserId(User);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            OutlineTemplateRecord template = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = request.Name.Trim(),
                TemplateJson = JsonSerializer.Serialize(normalizedNodes, JsonOptions),
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
            ActionResult? disabled = RejectIfDisabled();
            if (disabled is not null)
            {
                return disabled;
            }

            string userId = _userIdResolver.ResolveUserId(User);
            List<OutlineTemplateRecord> templates = await _dbContext.OutlineTemplates
                .AsNoTracking()
                .Where(template => template.OwnerUserId == userId)
                .OrderBy(template => template.Name)
                .ToListAsync(ct);

            return Ok(templates.Select(ToDto).ToList());
        }

        [HttpDelete("outline-templates/{id:guid}")]
        public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
        {
            ActionResult? disabled = RejectIfDisabled();
            if (disabled is not null)
            {
                return disabled;
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
            ActionResult? disabled = RejectIfDisabled();
            if (disabled is not null)
            {
                return disabled;
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

            if (!TryValidateTemplateNodes(nodes, out string validationError))
            {
                return BadRequest(new { message = validationError });
            }

            OutlineTemplateApplyOptionsDto settings = options ?? new OutlineTemplateApplyOptionsDto(null, false, "none");
            if (!TryNormalizeLinkStrategy(settings.LinkStrategy, out string strategy))
            {
                return BadRequest(new { message = "linkStrategy must be one of: none, by-title, create." });
            }

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

            DateTimeOffset now = DateTimeOffset.UtcNow;
            Dictionary<Guid, Guid> idMap = new();
            List<DocumentOutlineNodeRecord> newNodes = new();
            List<SectionRecord> newSections = new();
            List<PageRecord> newPages = new();

            int baseOrder = await _dbContext.DocumentOutlineNodes
                .Where(node => node.DocumentId == documentId && node.ParentId == parentRoot)
                .Select(node => (int?)node.Order)
                .MaxAsync(ct) ?? -1;

            List<OutlineTemplateNodeDto> ordered = BuildApplyOrder(nodes);

            foreach (OutlineTemplateNodeDto source in ordered)
            {
                Guid newId = Guid.NewGuid();
                idMap[source.SourceId] = newId;

                Guid? parentId = source.ParentSourceId.HasValue
                    ? idMap[source.ParentSourceId.Value]
                    : parentRoot;
                int order = source.ParentSourceId.HasValue ? source.Order : baseOrder + 1 + source.Order;

                Guid? linkedSectionId = null;
                if (IsSceneNode(source.NodeType))
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

        private ActionResult? RejectIfDisabled()
        {
            if (IsEnabled())
            {
                return null;
            }

            return NotFound(new { message = "Outline templates are disabled." });
        }

        private static bool TryValidateTemplateNodes(
            IReadOnlyList<OutlineTemplateNodeDto> nodes,
            out string message)
        {
            if (nodes.Count == 0)
            {
                message = "Template requires at least one node.";
                return false;
            }

            Dictionary<Guid, Guid?> parentById = new();
            HashSet<Guid> ids = new();
            foreach (OutlineTemplateNodeDto node in nodes)
            {
                if (node.SourceId == Guid.Empty)
                {
                    message = "Template node sourceId is required.";
                    return false;
                }

                if (!ids.Add(node.SourceId))
                {
                    message = "Template node sourceId values must be unique.";
                    return false;
                }

                if (node.ParentSourceId == node.SourceId)
                {
                    message = "Template node cannot reference itself as parent.";
                    return false;
                }

                string nodeType = NormalizeNodeType(node.NodeType);
                if (!IsSupportedNodeType(nodeType))
                {
                    message = "Template nodeType must be one of: part, chapter, scene.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(node.Title))
                {
                    message = "Template node title is required.";
                    return false;
                }

                if (node.Order < 0)
                {
                    message = "Template node order must be zero or positive.";
                    return false;
                }

                parentById[node.SourceId] = node.ParentSourceId;
            }

            foreach (Guid? parentId in parentById.Values)
            {
                if (parentId.HasValue && !ids.Contains(parentId.Value))
                {
                    message = "Template parentSourceId must reference another node in the template.";
                    return false;
                }
            }

            if (parentById.Values.All(parent => parent.HasValue))
            {
                message = "Template requires at least one root node.";
                return false;
            }

            foreach (Guid nodeId in ids)
            {
                HashSet<Guid> seen = new();
                Guid? current = nodeId;
                while (current.HasValue)
                {
                    if (!seen.Add(current.Value))
                    {
                        message = "Template hierarchy contains a cycle.";
                        return false;
                    }

                    current = parentById[current.Value];
                }
            }

            message = string.Empty;
            return true;
        }

        private static List<OutlineTemplateNodeDto> NormalizeTemplateNodes(IReadOnlyList<OutlineTemplateNodeDto> nodes)
        {
            return nodes
                .Select(node => node with
                {
                    NodeType = NormalizeNodeType(node.NodeType),
                    Title = string.IsNullOrWhiteSpace(node.Title) ? "Untitled" : node.Title.Trim(),
                    Notes = string.IsNullOrWhiteSpace(node.Notes) ? null : node.Notes.Trim(),
                    MetadataJson = string.IsNullOrWhiteSpace(node.MetadataJson) ? null : node.MetadataJson.Trim()
                })
                .ToList();
        }

        private static List<OutlineTemplateNodeDto> BuildApplyOrder(IReadOnlyList<OutlineTemplateNodeDto> nodes)
        {
            List<OutlineTemplateNodeDto> ordered = new(nodes.Count);
            List<OutlineTemplateNodeDto> rootNodes = nodes
                .Where(node => !node.ParentSourceId.HasValue)
                .OrderBy(node => node.Order)
                .ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Dictionary<Guid, List<OutlineTemplateNodeDto>> byParent = nodes
                .Where(node => node.ParentSourceId.HasValue)
                .GroupBy(node => node.ParentSourceId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(node => node.Order)
                        .ThenBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                        .ToList());

            void Visit(Guid parentId)
            {
                if (!byParent.TryGetValue(parentId, out List<OutlineTemplateNodeDto>? children))
                {
                    return;
                }

                foreach (OutlineTemplateNodeDto child in children)
                {
                    ordered.Add(child);
                    Visit(child.SourceId);
                }
            }

            foreach (OutlineTemplateNodeDto root in rootNodes)
            {
                ordered.Add(root);
                Visit(root.SourceId);
            }

            return ordered;
        }

        private static bool TryNormalizeLinkStrategy(string? strategy, out string normalized)
        {
            normalized = string.IsNullOrWhiteSpace(strategy)
                ? "none"
                : strategy.Trim().ToLowerInvariant();

            return normalized is "none" or "by-title" or "create";
        }

        private static string NormalizeNodeType(string? nodeType)
        {
            string normalized = string.IsNullOrWhiteSpace(nodeType)
                ? "scene"
                : nodeType.Trim().ToLowerInvariant();

            return normalized;
        }

        private static bool IsSupportedNodeType(string nodeType)
        {
            return nodeType is "part" or "chapter" or "scene";
        }

        private static bool IsSceneNode(string? nodeType)
        {
            return string.Equals(nodeType, "scene", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsUndoEnabled()
        {
            return _configuration.GetValue<bool?>("Workflow:OutlineUndoEnabled")
                ?? _configuration.GetValue<bool?>("WriterApp:Workflow:OutlineUndoEnabled")
                ?? false;
        }

        private bool IsEnabled()
        {
            return _configuration.GetValue<bool?>("WriterApp:Workflow:OutlineTemplatesEnabled")
                ?? false;
        }

        private static OutlineTemplateDto ToDto(OutlineTemplateRecord template)
        {
            int nodeCount = 0;
            if (!string.IsNullOrWhiteSpace(template.TemplateJson))
            {
                try
                {
                    List<OutlineTemplateNodeDto>? nodes = JsonSerializer.Deserialize<List<OutlineTemplateNodeDto>>(
                        template.TemplateJson,
                        JsonOptions);
                    nodeCount = nodes?.Count ?? 0;
                }
                catch
                {
                    nodeCount = 0;
                }
            }

            return new OutlineTemplateDto(
                template.Id,
                template.Name,
                template.CreatedUtc,
                template.UpdatedUtc,
                nodeCount);
        }
    }
}
