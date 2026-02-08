using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.State;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed class ProjectWordCountService : IProjectWordCountService
    {
        private static readonly Regex WordRegex = new(@"\b[\p{L}\p{N}']+\b", RegexOptions.Compiled);
        private readonly AppDbContext _dbContext;

        public ProjectWordCountService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task RefreshProjectAsync(Guid projectId, CancellationToken ct)
        {
            List<ProjectNodeRecord> nodes = await _dbContext.ProjectNodes
                .Where(node => node.ProjectId == projectId)
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.OrderIndex)
                .ToListAsync(ct);

            if (nodes.Count == 0)
            {
                return;
            }

            HashSet<Guid> linkedSectionIds = nodes
                .Where(node => node.LinkedSectionId.HasValue)
                .Select(node => node.LinkedSectionId!.Value)
                .ToHashSet();

            Dictionary<Guid, int> sectionCounts = await LoadSectionWordCountsAsync(linkedSectionIds, ct);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            foreach (ProjectNodeRecord node in nodes)
            {
                if (node.NodeType == ProjectNodeType.Scene)
                {
                    int next = 0;
                    if (node.LinkedSectionId.HasValue)
                    {
                        sectionCounts.TryGetValue(node.LinkedSectionId.Value, out next);
                    }

                    if (node.WordCountCache != next)
                    {
                        node.WordCountCache = next;
                        node.UpdatedUtc = now;
                    }
                }
            }

            RecomputeAggregateNodes(nodes, now);
            await _dbContext.SaveChangesAsync(ct);
        }

        public async Task RefreshForSectionAsync(Guid sectionId, CancellationToken ct)
        {
            List<ProjectNodeRecord> affectedScenes = await _dbContext.ProjectNodes
                .Where(node => node.LinkedSectionId == sectionId)
                .ToListAsync(ct);

            if (affectedScenes.Count == 0)
            {
                return;
            }

            foreach (Guid projectId in affectedScenes.Select(node => node.ProjectId).Distinct())
            {
                await RefreshProjectAsync(projectId, ct);
            }
        }

        public async Task<ProjectStatsDto?> GetProjectStatsAsync(string ownerUserId, Guid projectId, CancellationToken ct)
        {
            ProjectRecord? project = await _dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == ownerUserId, ct);
            if (project is null)
            {
                return null;
            }

            List<ProjectNodeRecord> nodes = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.ProjectId == projectId)
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.OrderIndex)
                .ToListAsync(ct);

            int total = nodes
                .Where(node => node.ParentId is null)
                .Sum(node => node.WordCountCache);

            return new ProjectStatsDto(
                projectId,
                total,
                nodes.Select(node => new ProjectNodeStatDto(node.Id, node.WordCountCache)).ToList());
        }

        private async Task<Dictionary<Guid, int>> LoadSectionWordCountsAsync(HashSet<Guid> sectionIds, CancellationToken ct)
        {
            if (sectionIds.Count == 0)
            {
                return new Dictionary<Guid, int>();
            }

            List<PageRecord> pages = await _dbContext.Pages
                .AsNoTracking()
                .Where(page => sectionIds.Contains(page.SectionId))
                .OrderBy(page => page.SectionId)
                .ThenBy(page => page.OrderIndex)
                .ToListAsync(ct);

            Dictionary<Guid, int> counts = new();
            foreach (IGrouping<Guid, PageRecord> group in pages.GroupBy(page => page.SectionId))
            {
                int total = 0;
                foreach (PageRecord page in group)
                {
                    total += CountWords(page.Content);
                }

                counts[group.Key] = total;
            }

            return counts;
        }

        private static void RecomputeAggregateNodes(IReadOnlyList<ProjectNodeRecord> nodes, DateTimeOffset now)
        {
            Dictionary<Guid, List<ProjectNodeRecord>> byParent = nodes
                .GroupBy(node => node.ParentId ?? Guid.Empty)
                .ToDictionary(group => group.Key, group => group.OrderBy(node => node.OrderIndex).ToList());

            int SumNode(ProjectNodeRecord node)
            {
                if (node.NodeType == ProjectNodeType.Scene)
                {
                    return node.WordCountCache;
                }

                if (!byParent.TryGetValue(node.Id, out List<ProjectNodeRecord>? children) || children.Count == 0)
                {
                    if (node.WordCountCache != 0)
                    {
                        node.WordCountCache = 0;
                        node.UpdatedUtc = now;
                    }

                    return 0;
                }

                int sum = 0;
                foreach (ProjectNodeRecord child in children)
                {
                    sum += SumNode(child);
                }

                if (node.WordCountCache != sum)
                {
                    node.WordCountCache = sum;
                    node.UpdatedUtc = now;
                }

                return sum;
            }

            if (byParent.TryGetValue(Guid.Empty, out List<ProjectNodeRecord>? roots))
            {
                foreach (ProjectNodeRecord root in roots)
                {
                    SumNode(root);
                }
            }
        }

        private static int CountWords(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return 0;
            }

            string decoded = PlainTextMapper.ToPlainText(html);
            return WordRegex.Matches(decoded).Count;
        }
    }
}
