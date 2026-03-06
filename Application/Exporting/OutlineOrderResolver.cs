using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;

namespace WriterApp.Application.Exporting
{
    public interface IOutlineOrderResolver
    {
        Task<OutlineSectionOrderResult> ResolveForManuscriptAsync(
            Guid projectId,
            Guid manuscriptDocumentId,
            CancellationToken ct);
    }

    public sealed record OutlineSectionOrderResult(
        IReadOnlyList<Guid> OrderedSectionIds,
        IReadOnlyDictionary<Guid, string> TitleBySectionId);

    public sealed class OutlineOrderResolver : IOutlineOrderResolver
    {
        private readonly AppDbContext _dbContext;

        public OutlineOrderResolver(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<OutlineSectionOrderResult> ResolveForManuscriptAsync(
            Guid projectId,
            Guid manuscriptDocumentId,
            CancellationToken ct)
        {
            if (projectId == Guid.Empty || manuscriptDocumentId == Guid.Empty)
            {
                return new OutlineSectionOrderResult(Array.Empty<Guid>(), new Dictionary<Guid, string>());
            }

            List<ProjectNodeOrderRow> nodes = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.ProjectId == projectId)
                .Select(node => new ProjectNodeOrderRow(
                    node.Id,
                    node.ParentId,
                    node.OrderIndex,
                    node.LinkedSectionId,
                    node.Title))
                .ToListAsync(ct);

            if (nodes.Count == 0)
            {
                return new OutlineSectionOrderResult(Array.Empty<Guid>(), new Dictionary<Guid, string>());
            }

            List<Guid> linkedSectionIds = nodes
                .Where(node => node.LinkedSectionId.HasValue)
                .Select(node => node.LinkedSectionId!.Value)
                .Distinct()
                .ToList();

            if (linkedSectionIds.Count == 0)
            {
                return new OutlineSectionOrderResult(Array.Empty<Guid>(), new Dictionary<Guid, string>());
            }

            List<Guid> validSectionIdList = await _dbContext.Sections
                .AsNoTracking()
                .Where(section =>
                    section.DocumentId == manuscriptDocumentId
                    && linkedSectionIds.Contains(section.Id))
                .Select(section => section.Id)
                .ToListAsync(ct);
            HashSet<Guid> validSectionIds = new(validSectionIdList);

            if (validSectionIds.Count == 0)
            {
                return new OutlineSectionOrderResult(Array.Empty<Guid>(), new Dictionary<Guid, string>());
            }

            Dictionary<Guid, List<ProjectNodeOrderRow>> childrenByParent = nodes
                .Where(node => node.ParentId.HasValue)
                .GroupBy(node => node.ParentId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(item => item.OrderIndex)
                        .ThenBy(item => item.Id)
                        .ToList());

            HashSet<Guid> nodeIds = nodes.Select(node => node.Id).ToHashSet();
            List<ProjectNodeOrderRow> roots = nodes
                .Where(node => !node.ParentId.HasValue || !nodeIds.Contains(node.ParentId.Value))
                .OrderBy(node => node.OrderIndex)
                .ThenBy(node => node.Id)
                .ToList();

            List<Guid> orderedSectionIds = new();
            Dictionary<Guid, string> titlesBySectionId = new();
            HashSet<Guid> seenSectionIds = new();

            foreach (ProjectNodeOrderRow root in roots)
            {
                CollectDepthFirst(root);
            }

            return new OutlineSectionOrderResult(orderedSectionIds, titlesBySectionId);

            void CollectDepthFirst(ProjectNodeOrderRow node)
            {
                if (node.LinkedSectionId.HasValue)
                {
                    Guid sectionId = node.LinkedSectionId.Value;
                    if (validSectionIds.Contains(sectionId) && seenSectionIds.Add(sectionId))
                    {
                        orderedSectionIds.Add(sectionId);
                        if (!string.IsNullOrWhiteSpace(node.Title))
                        {
                            titlesBySectionId[sectionId] = node.Title.Trim();
                        }
                    }
                }

                if (!childrenByParent.TryGetValue(node.Id, out List<ProjectNodeOrderRow>? children))
                {
                    return;
                }

                foreach (ProjectNodeOrderRow child in children)
                {
                    CollectDepthFirst(child);
                }
            }
        }

        private sealed record ProjectNodeOrderRow(
            Guid Id,
            Guid? ParentId,
            int OrderIndex,
            Guid? LinkedSectionId,
            string Title);
    }
}
