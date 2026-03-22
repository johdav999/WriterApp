using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/admin/projects/integrity")]
    [Authorize(Policy = "AdminOnly")]
    public sealed class AdminProjectNodeIntegrityController : ControllerBase
    {
        private readonly AppDbContext _dbContext;

        public AdminProjectNodeIntegrityController(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpGet]
        public async Task<ActionResult<ProjectNodeIntegrityAdminReportDto>> Get(CancellationToken ct)
        {
            Dictionary<Guid, string> projectTitlesById = await _dbContext.Projects
                .AsNoTracking()
                .ToDictionaryAsync(
                    project => project.Id,
                    project => string.IsNullOrWhiteSpace(project.Title) ? "Untitled project" : project.Title,
                    ct);

            List<ProjectNodeRecord> nodes = await _dbContext.ProjectNodes
                .AsNoTracking()
                .ToListAsync(ct);
            Dictionary<Guid, ProjectNodeRecord> nodesById = nodes.ToDictionary(node => node.Id);

            IReadOnlyList<ProjectNodeIntegrityIssue> issues = ProjectNodeHierarchyValidator.Evaluate(nodes);
            ProjectNodeIntegritySummaryDto summary = BuildSummary(nodes, nodesById, issues);
            List<ProjectNodeIntegrityIssueRowDto> rows = issues
                .Select(issue =>
                {
                    Guid projectId = nodesById.TryGetValue(issue.NodeId, out ProjectNodeRecord? node)
                        ? node.ProjectId
                        : Guid.Empty;

                    return new ProjectNodeIntegrityIssueRowDto(
                        projectId,
                        projectTitlesById.TryGetValue(projectId, out string? projectTitle)
                            ? projectTitle
                            : "Unknown project",
                        issue.NodeId,
                        issue.Title,
                        issue.NodeType,
                        issue.ParentId,
                        issue.Code,
                        issue.Message);
                })
                .OrderBy(row => row.ProjectTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.NodeType, StringComparer.OrdinalIgnoreCase)
                .ThenBy(row => row.NodeTitle, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return Ok(new ProjectNodeIntegrityAdminReportDto(
                DateTimeOffset.UtcNow,
                projectTitlesById.Count,
                nodes.Count,
                summary,
                rows));
        }

        private static ProjectNodeIntegritySummaryDto BuildSummary(
            IReadOnlyList<ProjectNodeRecord> nodes,
            IReadOnlyDictionary<Guid, ProjectNodeRecord> nodesById,
            IReadOnlyList<ProjectNodeIntegrityIssue> issues)
        {
            int invalidParentChildCombinations = issues.Count(issue => string.Equals(issue.Code, "invalid_parent_type", StringComparison.Ordinal));
            int scenesNotUnderChapters = nodes.Count(node =>
            {
                if (node.NodeType != ProjectNodeType.Scene)
                {
                    return false;
                }

                ProjectNodeRecord? parent = node.ParentId.HasValue && nodesById.TryGetValue(node.ParentId.Value, out ProjectNodeRecord? resolvedParent)
                    ? resolvedParent
                    : null;
                return parent is null || parent.NodeType != ProjectNodeType.Chapter;
            });
            int partsWithNonRootParent = nodes.Count(node => node.NodeType == ProjectNodeType.Part && node.ParentId.HasValue);
            int crossProjectParentLinks = issues.Count(issue => string.Equals(issue.Code, "cross_project_parent", StringComparison.Ordinal));
            int selfParentRows = nodes.Count(node => node.ParentId == node.Id);
            int cycles = issues.Count(issue => string.Equals(issue.Code, "cycle", StringComparison.Ordinal));
            int validRootChapters = nodes.Count(node => node.NodeType == ProjectNodeType.Chapter && !node.ParentId.HasValue);

            return new ProjectNodeIntegritySummaryDto(
                invalidParentChildCombinations,
                scenesNotUnderChapters,
                partsWithNonRootParent,
                crossProjectParentLinks,
                selfParentRows,
                cycles,
                validRootChapters);
        }
    }
}
