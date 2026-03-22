using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public interface IOnboardingBootstrapService
    {
        Task<OnboardingBootstrapResult> CreateStarterWorkspaceForOnboardingAsync(string ownerUserId, string intent, CancellationToken ct);
    }

    public sealed record OnboardingBootstrapResult(Guid ProjectId, string ProjectTitle, Guid FirstSceneNodeId);

    public sealed class OnboardingBootstrapException : InvalidOperationException
    {
        public OnboardingBootstrapException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }

    public sealed class OnboardingBootstrapService : IOnboardingBootstrapService
    {
        private const string DemoSceneText = """
The café was quiet that afternoon, the kind of quiet that settles softly between the clink of cups and the low murmur of strangers. Outside, the street moved slowly through a pale autumn light.

He had chosen the table by the window without thinking much about it. It was simply where he always sat when he came here—close enough to watch the world passing by, far enough away from everyone else.

He noticed her only after she had already been sitting there for several minutes.

She was across the room, near the bookshelf, a cup of coffee resting untouched in front of her. She was reading something on her phone, though from time to time her eyes lifted, drifting around the room as if searching for something she couldn’t quite name.

At one of those moments their eyes met.
""";

        private readonly AppDbContext _dbContext;
        private readonly IProjectSceneLinkingService _sceneLinking;
        private readonly IProjectWordCountService _wordCounts;
        private readonly ILogger<OnboardingBootstrapService> _logger;

        public OnboardingBootstrapService(
            AppDbContext dbContext,
            IProjectSceneLinkingService sceneLinking,
            IProjectWordCountService wordCounts,
            ILogger<OnboardingBootstrapService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _sceneLinking = sceneLinking ?? throw new ArgumentNullException(nameof(sceneLinking));
            _wordCounts = wordCounts ?? throw new ArgumentNullException(nameof(wordCounts));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<OnboardingBootstrapResult> CreateStarterWorkspaceForOnboardingAsync(string ownerUserId, string intent, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                throw new ArgumentException("ownerUserId is required.", nameof(ownerUserId));
            }

            string normalizedIntent = NormalizeIntent(intent);
            string projectTitle = GetDefaultProjectName(normalizedIntent);
            Microsoft.EntityFrameworkCore.Storage.IExecutionStrategy strategy = _dbContext.Database.CreateExecutionStrategy();
            OnboardingBootstrapResult? result = null;

            await strategy.ExecuteAsync(async () =>
            {
                await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct);

                ProjectRecord project;
                try
                {
                    (project, _) = await GetOrCreateBootstrapProjectAsync(ownerUserId, projectTitle, ct);
                }
                catch (OnboardingBootstrapException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new OnboardingBootstrapException("project_create_failed", $"Failed to create the onboarding project. {ex.Message}");
                }

                try
                {
                    DocumentRecord? manuscript = await _sceneLinking.GetOrCreateManuscriptDocumentAsync(project, ownerUserId, ct);
                    if (manuscript is null)
                    {
                        throw new OnboardingBootstrapException("starter_structure_failed", "Failed to create or load the starter manuscript.");
                    }

                    _logger.LogInformation(
                        "Onboarding bootstrap manuscript phase completed. UserId={UserId} ProjectId={ProjectId} DocumentId={DocumentId} ManuscriptState={ManuscriptState}",
                        ownerUserId,
                        project.Id,
                        manuscript.Id,
                        _dbContext.Entry(manuscript).State == EntityState.Added ? "created" : "reused");

                    ProjectNodeRecord sceneNode = await EnsureStarterStructureAsync(project, ownerUserId, normalizedIntent, ct);
                    SceneLinkResult? link = await _sceneLinking.EnsureSceneLinkedSectionAsync(project, sceneNode, ownerUserId, ct);
                    if (link is null)
                    {
                        throw new OnboardingBootstrapException("starter_structure_failed", "Failed to link the starter scene.");
                    }

                    await EnsureStarterSceneDemoContentAsync(sceneNode, link, ct);

                    _logger.LogInformation(
                        "Onboarding bootstrap first scene workspace completed. UserId={UserId} ProjectId={ProjectId} SceneNodeId={SceneNodeId} DocumentId={DocumentId} SectionId={SectionId} SceneState={SceneState} SectionState={SectionState} PageState={PageState}",
                        ownerUserId,
                        project.Id,
                        sceneNode.Id,
                        link.DocumentId,
                        link.SectionId,
                        _dbContext.Entry(sceneNode).State == EntityState.Added ? "created" : "reused",
                        link.SectionCreated ? "created" : "reused",
                        link.PageCreated ? "created" : "reused");

                    await _dbContext.SaveChangesAsync(ct);
                    await _wordCounts.RefreshProjectAsync(project.Id, ct);
                    await transaction.CommitAsync(ct);

                    result = new OnboardingBootstrapResult(project.Id, project.Title, sceneNode.Id);
                }
                catch (OnboardingBootstrapException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new OnboardingBootstrapException("starter_structure_failed", $"Failed to create starter structure. {ex.Message}");
                }
            });

            _logger.LogInformation(
                "Onboarding bootstrap completed. UserId={UserId} ProjectId={ProjectId} FirstSceneNodeId={FirstSceneNodeId}",
                ownerUserId,
                result!.ProjectId,
                result.FirstSceneNodeId);

            return result;
        }

        private async Task EnsureStarterSceneDemoContentAsync(
            ProjectNodeRecord sceneNode,
            SceneLinkResult link,
            CancellationToken ct)
        {
            sceneNode.MetadataJson = OnboardingDemoSceneMetadata.Merge(sceneNode.MetadataJson);

            PageRecord? page = await _dbContext.Pages
                .FirstOrDefaultAsync(
                    item => item.DocumentId == link.DocumentId
                        && item.SectionId == link.SectionId
                        && item.OrderIndex == 0,
                    ct);
            if (page is null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(page.Content))
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            string demoHtml = ToParagraphHtml(DemoSceneText);
            page.Content = demoHtml;
            page.UpdatedAt = now;

            SceneContentRecord? sceneContent = await _dbContext.SceneContents
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNode.Id, ct);
            if (sceneContent is null)
            {
                sceneContent = new SceneContentRecord
                {
                    SceneNodeId = sceneNode.Id
                };
                _dbContext.SceneContents.Add(sceneContent);
            }

            if (string.IsNullOrWhiteSpace(sceneContent.ContentJson))
            {
                sceneContent.ContentJson = demoHtml;
                sceneContent.UpdatedAtUtc = now;
            }

            _logger.LogInformation(
                "Onboarding bootstrap seeded demo scene content. SceneNodeId={SceneNodeId} SectionId={SectionId}",
                sceneNode.Id,
                link.SectionId);
        }

        private static string ToParagraphHtml(string text)
        {
            string[] paragraphs = text
                .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return string.Join(
                string.Empty,
                paragraphs.Select(paragraph => $"<p>{System.Net.WebUtility.HtmlEncode(paragraph)}</p>"));
        }

        private async Task<(ProjectRecord Project, bool Created)> GetOrCreateBootstrapProjectAsync(string ownerUserId, string projectTitle, CancellationToken ct)
        {
            List<ProjectCandidate> candidates = await _dbContext.Projects
                .Where(item => item.OwnerUserId == ownerUserId)
                .OrderByDescending(item => item.UpdatedUtc)
                .Select(item => new ProjectCandidate(
                    item,
                    _dbContext.ProjectNodes.Any(node => node.ProjectId == item.Id),
                    _dbContext.ProjectNodes.Any(node => node.ProjectId == item.Id && node.NodeType == ProjectNodeType.Scene)))
                .ToListAsync(ct);

            ProjectRecord? selected =
                candidates.FirstOrDefault(item => item.HasSceneNodes && string.Equals(item.Project.Title, projectTitle, StringComparison.OrdinalIgnoreCase))?.Project
                ?? candidates.FirstOrDefault(item => string.Equals(item.Project.Title, projectTitle, StringComparison.OrdinalIgnoreCase))?.Project
                ?? candidates.FirstOrDefault(item => !item.HasAnyNodes)?.Project;

            if (selected is not null)
            {
                _logger.LogInformation(
                    "Onboarding bootstrap reused project. UserId={UserId} ProjectId={ProjectId} Title={Title}",
                    ownerUserId,
                    selected.Id,
                    selected.Title);
                return (selected, false);
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            ProjectRecord project = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = ownerUserId,
                Title = projectTitle,
                CreatedUtc = now,
                UpdatedUtc = now
            };

            _dbContext.Projects.Add(project);

            _logger.LogInformation(
                "Onboarding bootstrap created project. UserId={UserId} ProjectId={ProjectId} Title={Title}",
                ownerUserId,
                project.Id,
                project.Title);

            return (project, true);
        }

        private async Task<ProjectNodeRecord> EnsureStarterStructureAsync(ProjectRecord project, string ownerUserId, string intent, CancellationToken ct)
        {
            List<ProjectNodeRecord> nodes = await _dbContext.ProjectNodes
                .Where(item => item.ProjectId == project.Id)
                .OrderBy(item => item.ParentId)
                .ThenBy(item => item.OrderIndex)
                .ToListAsync(ct);

            ProjectNodeRecord scene = intent switch
            {
                "Novel" => await EnsureNovelStructureAsync(project, nodes, ct),
                "Blog" => await EnsureBlogStructureAsync(project, nodes, ct),
                "NonFiction" => await EnsureNonFictionStructureAsync(project, nodes, ct),
                "ShortStory" => await EnsureShortStoryStructureAsync(project, nodes, ct),
                _ => await EnsureOtherStructureAsync(project, nodes, ct)
            };

            if (scene.NodeType != ProjectNodeType.Scene)
            {
                throw new OnboardingBootstrapException("starter_structure_failed", "Starter structure did not produce a scene.");
            }

            project.UpdatedUtc = DateTimeOffset.UtcNow;
            _logger.LogInformation(
                "Onboarding bootstrap ensured starter structure. UserId={UserId} ProjectId={ProjectId} SceneNodeId={SceneNodeId} Intent={Intent}",
                ownerUserId,
                project.Id,
                scene.Id,
                intent);

            return scene;
        }

        private Task<ProjectNodeRecord> EnsureNovelStructureAsync(ProjectRecord project, List<ProjectNodeRecord> nodes, CancellationToken ct)
        {
            ProjectNodeRecord act = EnsureNode(project, nodes, null, ProjectNodeType.Part, "Act I");
            ProjectNodeRecord chapter = EnsureNode(project, nodes, act.Id, ProjectNodeType.Chapter, "Chapter 1");
            ProjectNodeRecord scene = EnsureNode(project, nodes, chapter.Id, ProjectNodeType.Scene, "Scene 1");
            return Task.FromResult(scene);
        }

        private Task<ProjectNodeRecord> EnsureBlogStructureAsync(ProjectRecord project, List<ProjectNodeRecord> nodes, CancellationToken ct)
        {
            ProjectNodeRecord draft = EnsureNode(project, nodes, null, ProjectNodeType.Chapter, "Draft");
            _ = EnsureNode(project, nodes, null, ProjectNodeType.Chapter, "Headline Ideas");
            ProjectNodeRecord scene = EnsureNode(project, nodes, draft.Id, ProjectNodeType.Scene, "Scene 1");
            return Task.FromResult(scene);
        }

        private Task<ProjectNodeRecord> EnsureNonFictionStructureAsync(ProjectRecord project, List<ProjectNodeRecord> nodes, CancellationToken ct)
        {
            ProjectNodeRecord chapter = EnsureNode(project, nodes, null, ProjectNodeType.Chapter, "Chapter 1");
            _ = EnsureNode(project, nodes, null, ProjectNodeType.Chapter, "Research");
            ProjectNodeRecord scene = EnsureNode(project, nodes, chapter.Id, ProjectNodeType.Scene, "Scene 1");
            return Task.FromResult(scene);
        }

        private Task<ProjectNodeRecord> EnsureShortStoryStructureAsync(ProjectRecord project, List<ProjectNodeRecord> nodes, CancellationToken ct)
        {
            ProjectNodeRecord draft = EnsureNode(project, nodes, null, ProjectNodeType.Chapter, "Draft");
            _ = EnsureNode(project, nodes, null, ProjectNodeType.Chapter, "Ending Notes");
            ProjectNodeRecord scene = EnsureNode(project, nodes, draft.Id, ProjectNodeType.Scene, "Scene 1");
            return Task.FromResult(scene);
        }

        private Task<ProjectNodeRecord> EnsureOtherStructureAsync(ProjectRecord project, List<ProjectNodeRecord> nodes, CancellationToken ct)
        {
            ProjectNodeRecord draft = EnsureNode(project, nodes, null, ProjectNodeType.Chapter, "Draft");
            ProjectNodeRecord scene = EnsureNode(project, nodes, draft.Id, ProjectNodeType.Scene, "Scene 1");
            return Task.FromResult(scene);
        }

        private ProjectNodeRecord EnsureNode(
            ProjectRecord project,
            ICollection<ProjectNodeRecord> knownNodes,
            Guid? parentId,
            ProjectNodeType nodeType,
            string title)
        {
            ProjectNodeRecord? parent = parentId.HasValue
                ? knownNodes.FirstOrDefault(item => item.ProjectId == project.Id && item.Id == parentId.Value)
                : null;
            if (parentId.HasValue && parent is null)
            {
                throw new OnboardingBootstrapException("starter_structure_failed", "Starter structure referenced a missing parent node.");
            }

            if (!ProjectNodeHierarchyValidator.IsPlacementAllowed(nodeType, parent?.NodeType))
            {
                throw new OnboardingBootstrapException(
                    "starter_structure_failed",
                    $"Starter structure attempted an invalid node placement for {ProjectNodeHierarchyValidator.NormalizeNodeType(nodeType)}.");
            }

            ProjectNodeRecord? existing = knownNodes.FirstOrDefault(item =>
                item.ProjectId == project.Id
                && item.ParentId == parentId
                && item.NodeType == nodeType
                && string.Equals(item.Title, title, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return existing;
            }

            int nextOrder = knownNodes.Count(item => item.ProjectId == project.Id && item.ParentId == parentId);
            ProjectNodeRecord node = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                ParentId = parentId,
                NodeType = nodeType,
                Title = title,
                OrderIndex = nextOrder,
                WordCountCache = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };

            _dbContext.ProjectNodes.Add(node);
            knownNodes.Add(node);
            return node;
        }

        private static string NormalizeIntent(string raw)
        {
            string normalized = (raw ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "novel" => "Novel",
                "short story" => "ShortStory",
                "shortstory" => "ShortStory",
                "non-fiction" => "NonFiction",
                "non fiction" => "NonFiction",
                "nonfiction" => "NonFiction",
                "blog" => "Blog",
                _ => "Other"
            };
        }

        private static string GetDefaultProjectName(string intent)
        {
            return intent switch
            {
                "Novel" => "My Novel",
                "ShortStory" => "My Short Story",
                "NonFiction" => "My Non-fiction Draft",
                "Blog" => "My Blog Post",
                _ => "My First Project"
            };
        }

        private sealed record ProjectCandidate(ProjectRecord Project, bool HasAnyNodes, bool HasSceneNodes);
    }
}
