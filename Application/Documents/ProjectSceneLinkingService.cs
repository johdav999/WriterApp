using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public interface IProjectSceneLinkingService
    {
        Task<DocumentRecord?> GetOrCreateManuscriptDocumentAsync(Guid projectId, string ownerUserId, CancellationToken ct);

        Task<SceneLinkResult?> EnsureSceneLinkedSectionAsync(Guid projectId, Guid sceneNodeId, string ownerUserId, CancellationToken ct);

        Task<SceneLinkResult?> EnsureSceneLinkedSectionAsync(ProjectRecord project, ProjectNodeRecord sceneNode, string ownerUserId, CancellationToken ct);

        Task<IReadOnlyList<ManuscriptSceneSectionItem>> GetManuscriptSceneSectionsAsync(Guid projectId, string ownerUserId, CancellationToken ct);
    }

    public sealed record SceneLinkResult(Guid DocumentId, Guid SectionId);

    public sealed record ManuscriptSceneSectionItem(
        ProjectNodeRecord SceneNode,
        SectionRecord Section,
        Guid DocumentId,
        string ContentHtml);

    public sealed class ProjectSceneLinkingService : IProjectSceneLinkingService
    {
        private readonly AppDbContext _dbContext;

        public ProjectSceneLinkingService(AppDbContext dbContext)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public async Task<DocumentRecord?> GetOrCreateManuscriptDocumentAsync(Guid projectId, string ownerUserId, CancellationToken ct)
        {
            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == ownerUserId, ct);
            if (project is null)
            {
                return null;
            }

            return await GetOrCreateManuscriptDocumentInternalAsync(project, ownerUserId, ct);
        }

        public async Task<SceneLinkResult?> EnsureSceneLinkedSectionAsync(Guid projectId, Guid sceneNodeId, string ownerUserId, CancellationToken ct)
        {
            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == ownerUserId, ct);
            if (project is null)
            {
                return null;
            }

            ProjectNodeRecord? sceneNode = await _dbContext.ProjectNodes
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == sceneNodeId, ct);
            if (sceneNode is null || sceneNode.NodeType != ProjectNodeType.Scene)
            {
                return null;
            }

            SceneLinkResult? result = await EnsureSceneLinkedSectionAsync(project, sceneNode, ownerUserId, ct);
            if (result is null)
            {
                return null;
            }

            await _dbContext.SaveChangesAsync(ct);
            return result;
        }

        public async Task<SceneLinkResult?> EnsureSceneLinkedSectionAsync(ProjectRecord project, ProjectNodeRecord sceneNode, string ownerUserId, CancellationToken ct)
        {
            if (project.OwnerUserId != ownerUserId || sceneNode.ProjectId != project.Id || sceneNode.NodeType != ProjectNodeType.Scene)
            {
                return null;
            }

            DocumentRecord manuscript = await GetOrCreateManuscriptDocumentInternalAsync(project, ownerUserId, ct);
            SectionRecord? linkedSection = null;

            if (sceneNode.LinkedSectionId.HasValue)
            {
                linkedSection = await _dbContext.Sections
                    .FirstOrDefaultAsync(item => item.Id == sceneNode.LinkedSectionId.Value, ct);
                if (linkedSection is not null && linkedSection.DocumentId != manuscript.Id)
                {
                    linkedSection = null;
                }
            }

            if (linkedSection is null)
            {
                DateTimeOffset now = DateTimeOffset.UtcNow;
                int nextOrder = await _dbContext.Sections
                    .Where(item => item.DocumentId == manuscript.Id)
                    .Select(item => (int?)item.OrderIndex)
                    .MaxAsync(ct) ?? -1;
                nextOrder += 1;

                linkedSection = new SectionRecord
                {
                    Id = Guid.NewGuid(),
                    DocumentId = manuscript.Id,
                    Title = string.IsNullOrWhiteSpace(sceneNode.Title) ? "New scene" : sceneNode.Title.Trim(),
                    NarrativePurpose = null,
                    LanguageCode = manuscript.LanguageCode,
                    TranslationGroupId = manuscript.TranslationGroupId,
                    OrderIndex = nextOrder,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Sections.Add(linkedSection);

                PageRecord page = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = manuscript.Id,
                    SectionId = linkedSection.Id,
                    Title = "Page 1",
                    Content = string.Empty,
                    OrderIndex = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Pages.Add(page);

                sceneNode.LinkedSectionId = linkedSection.Id;
                sceneNode.UpdatedUtc = now;
                project.UpdatedUtc = now;
                manuscript.UpdatedAt = now;
            }

            return new SceneLinkResult(manuscript.Id, linkedSection.Id);
        }

        public async Task<IReadOnlyList<ManuscriptSceneSectionItem>> GetManuscriptSceneSectionsAsync(
            Guid projectId,
            string ownerUserId,
            CancellationToken ct)
        {
            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == ownerUserId, ct);
            if (project is null)
            {
                return Array.Empty<ManuscriptSceneSectionItem>();
            }

            List<ProjectNodeRecord> nodes = await _dbContext.ProjectNodes
                .Where(item => item.ProjectId == projectId)
                .OrderBy(item => item.OrderIndex)
                .ThenBy(item => item.Id)
                .ToListAsync(ct);
            if (nodes.Count == 0)
            {
                return Array.Empty<ManuscriptSceneSectionItem>();
            }

            List<ProjectNodeRecord> roots = nodes
                .Where(item => !item.ParentId.HasValue)
                .OrderBy(item => item.OrderIndex)
                .ThenBy(item => item.Id)
                .ToList();

            Dictionary<Guid, List<ProjectNodeRecord>> byParent = nodes
                .Where(item => item.ParentId.HasValue)
                .GroupBy(item => item.ParentId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(item => item.OrderIndex)
                        .ThenBy(item => item.Id)
                        .ToList());

            List<(ProjectNodeRecord Scene, SceneLinkResult Link)> orderedScenes = new();
            foreach (ProjectNodeRecord root in roots)
            {
                await CollectSceneNodesDepthFirstAsync(root, project, ownerUserId, byParent, orderedScenes, ct);
            }

            if (orderedScenes.Count == 0)
            {
                return Array.Empty<ManuscriptSceneSectionItem>();
            }

            await _dbContext.SaveChangesAsync(ct);

            Guid documentId = orderedScenes[0].Link.DocumentId;
            List<Guid> sectionIds = orderedScenes
                .Select(item => item.Link.SectionId)
                .Distinct()
                .ToList();

            List<SectionRecord> sectionRecords = await _dbContext.Sections
                .AsNoTracking()
                .Where(item => item.DocumentId == documentId && sectionIds.Contains(item.Id))
                .ToListAsync(ct);
            Dictionary<Guid, SectionRecord> sectionsById = sectionRecords.ToDictionary(item => item.Id);

            List<PageRecord> pages = await _dbContext.Pages
                .AsNoTracking()
                .Where(item => item.DocumentId == documentId && sectionIds.Contains(item.SectionId))
                .OrderBy(item => item.SectionId)
                .ThenBy(item => item.OrderIndex)
                .ToListAsync(ct);
            Dictionary<Guid, string> contentBySectionId = pages
                .GroupBy(item => item.SectionId)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join("\n", group.Select(page => page.Content ?? string.Empty)));

            List<ManuscriptSceneSectionItem> result = new();
            foreach ((ProjectNodeRecord scene, SceneLinkResult link) in orderedScenes)
            {
                if (!sectionsById.TryGetValue(link.SectionId, out SectionRecord? section))
                {
                    continue;
                }

                string content = contentBySectionId.TryGetValue(section.Id, out string? value)
                    ? value
                    : string.Empty;
                result.Add(new ManuscriptSceneSectionItem(scene, section, documentId, content));
            }

            return result;
        }

        private async Task<DocumentRecord> GetOrCreateManuscriptDocumentInternalAsync(ProjectRecord project, string ownerUserId, CancellationToken ct)
        {
            DocumentRecord? manuscript = await _dbContext.Documents
                .Where(item => item.ProjectId == project.Id && item.OwnerUserId == ownerUserId && item.DocumentKind == DocumentKind.Manuscript)
                .OrderByDescending(item => item.UpdatedAtUnixSeconds)
                .FirstOrDefaultAsync(ct);
            if (manuscript is not null)
            {
                return manuscript;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            manuscript = new DocumentRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                OwnerUserId = ownerUserId,
                Title = string.IsNullOrWhiteSpace(project.Title) ? "Manuscript" : $"{project.Title.Trim()} Manuscript",
                DocumentKind = DocumentKind.Manuscript,
                LanguageCode = project.Language,
                TranslationGroupId = null,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                UpdatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                IsArchived = false,
                ArchivedAt = null,
                DeletedAt = null
            };
            _dbContext.Documents.Add(manuscript);
            project.UpdatedUtc = now;
            return manuscript;
        }

        private async Task CollectSceneNodesDepthFirstAsync(
            ProjectNodeRecord node,
            ProjectRecord project,
            string ownerUserId,
            IReadOnlyDictionary<Guid, List<ProjectNodeRecord>> byParent,
            ICollection<(ProjectNodeRecord Scene, SceneLinkResult Link)> destination,
            CancellationToken ct)
        {
            if (node.NodeType == ProjectNodeType.Scene)
            {
                SceneLinkResult? link = await EnsureSceneLinkedSectionAsync(project, node, ownerUserId, ct);
                if (link is not null)
                {
                    destination.Add((node, link));
                }
            }

            if (!byParent.TryGetValue(node.Id, out List<ProjectNodeRecord>? children))
            {
                return;
            }

            foreach (ProjectNodeRecord child in children)
            {
                await CollectSceneNodesDepthFirstAsync(child, project, ownerUserId, byParent, destination, ct);
            }
        }
    }
}
