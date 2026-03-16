using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/projects")]
    [Authorize]
    public sealed class ProjectsController : ControllerBase
    {
        private static readonly ConcurrentDictionary<string, bool> SqliteTableExistsCache = new(StringComparer.Ordinal);
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IProjectWordCountService _wordCounts;
        private readonly IProjectGoalsService _goals;
        private readonly IProjectSceneLinkingService _sceneLinking;
        private readonly IProjectDeletionService _projectDeletion;
        private readonly IEntitlementService _entitlementService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ProjectsController> _logger;

        public ProjectsController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IProjectWordCountService wordCounts,
            IProjectGoalsService goals,
            IProjectSceneLinkingService sceneLinking,
            IProjectDeletionService projectDeletion,
            IEntitlementService entitlementService,
            IConfiguration configuration,
            ILogger<ProjectsController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _wordCounts = wordCounts ?? throw new ArgumentNullException(nameof(wordCounts));
            _goals = goals ?? throw new ArgumentNullException(nameof(goals));
            _sceneLinking = sceneLinking ?? throw new ArgumentNullException(nameof(sceneLinking));
            _projectDeletion = projectDeletion ?? throw new ArgumentNullException(nameof(projectDeletion));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ProjectDto>>> ListProjects(CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectNavigator, "projects.navigator");
            if (gate is not null)
            {
                return gate;
            }

            List<ProjectRecord> projects = await _dbContext.Projects
                .AsNoTracking()
                .Where(project => project.OwnerUserId == userId)
                .ToListAsync(ct);
            projects = projects
                .OrderByDescending(project => project.UpdatedUtc)
                .ToList();

            HashSet<Guid> projectIds = projects.Select(project => project.Id).ToHashSet();
            Dictionary<Guid, int> totals = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => projectIds.Contains(node.ProjectId) && node.ParentId == null)
                .GroupBy(node => node.ProjectId)
                .Select(group => new { group.Key, Total = group.Sum(node => node.WordCountCache) })
                .ToDictionaryAsync(item => item.Key, item => item.Total, ct);

            List<ProjectDto> result = projects
                .Select(project => ToDto(project, totals.TryGetValue(project.Id, out int total) ? total : 0))
                .ToList();

            return Ok(result);
        }

        [HttpGet("list-items")]
        public async Task<ActionResult<IReadOnlyList<ProjectListItemDto>>> ListProjectItems(CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectNavigator, "projects.navigator");
            if (gate is not null)
            {
                return gate;
            }

            List<ProjectRecord> projects = await _dbContext.Projects
                .AsNoTracking()
                .Where(project => project.OwnerUserId == userId)
                .ToListAsync(ct);
            projects = projects
                .OrderByDescending(project => project.UpdatedUtc)
                .ToList();

            if (projects.Count == 0)
            {
                return Ok(Array.Empty<ProjectListItemDto>());
            }

            HashSet<Guid> projectIds = projects.Select(project => project.Id).ToHashSet();
            List<DocumentRecord> documents = await _dbContext.Documents
                .AsNoTracking()
                .Where(document => projectIds.Contains(document.ProjectId) && document.OwnerUserId == userId)
                .ToListAsync(ct);
            Dictionary<Guid, List<DocumentRecord>> docsByProject = documents
                .GroupBy(document => document.ProjectId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(document => document.UpdatedAtUnixSeconds)
                        .ToList());

            Dictionary<Guid, int> totals = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => projectIds.Contains(node.ProjectId) && node.ParentId == null)
                .GroupBy(node => node.ProjectId)
                .Select(group => new { group.Key, Total = group.Sum(node => node.WordCountCache) })
                .ToDictionaryAsync(item => item.Key, item => item.Total, ct);

            Dictionary<Guid, ProjectGoalRecord> goalsByProject = new();
            Dictionary<Guid, List<ProjectProgressDailyRecord>> progressByProject = new();
            bool includeProgress = IsGoalsEnabled();
            if (includeProgress)
            {
                includeProgress = await GoalsTablesExistAsync(ct);
            }

            if (includeProgress)
            {
                List<ProjectGoalRecord> goalRows = await _dbContext.ProjectGoals
                    .AsNoTracking()
                    .Where(item => projectIds.Contains(item.ProjectId))
                    .ToListAsync(ct);
                goalsByProject = goalRows.ToDictionary(item => item.ProjectId, item => item);

                List<ProjectProgressDailyRecord> progressRows = await _dbContext.ProjectProgressDaily
                    .AsNoTracking()
                    .Where(item => projectIds.Contains(item.ProjectId))
                    .ToListAsync(ct);
                progressByProject = progressRows
                    .GroupBy(item => item.ProjectId)
                    .ToDictionary(group => group.Key, group => group.ToList());
            }

            List<ProjectListItemDto> result = new(projects.Count);
            foreach (ProjectRecord project in projects)
            {
                docsByProject.TryGetValue(project.Id, out List<DocumentRecord>? projectDocs);
                projectDocs ??= new List<DocumentRecord>();

                DocumentRecord? primary = projectDocs
                    .Where(item => item.DeletedAtUtc is null && item.DocumentKind == DocumentKind.Manuscript)
                    .OrderByDescending(item => item.UpdatedAtUnixSeconds)
                    .FirstOrDefault()
                    ?? projectDocs
                        .Where(item => item.DeletedAtUtc is null)
                        .OrderByDescending(item => item.UpdatedAtUnixSeconds)
                        .FirstOrDefault();

                DateTimeOffset lastEdited = primary?.UpdatedAt ?? project.UpdatedUtc;
                int totalWords = totals.TryGetValue(project.Id, out int total) ? total : 0;

                int? todayWords = null;
                int? thisWeekWords = null;
                int? streak = null;
                if (includeProgress)
                {
                    ProjectGoalRecord? goal = goalsByProject.TryGetValue(project.Id, out ProjectGoalRecord? goalRecord)
                        ? goalRecord
                        : null;
                    string timezone = string.IsNullOrWhiteSpace(goal?.Timezone) ? "UTC" : goal!.Timezone;
                    TimeZoneInfo timeZone = ResolveTimeZone(timezone);
                    DateOnly today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime);
                    DateOnly weekStart = today.AddDays(-6);
                    string weekStartText = weekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    string todayText = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                    List<ProjectProgressDailyRecord> rows = progressByProject.TryGetValue(project.Id, out List<ProjectProgressDailyRecord>? projectRows)
                        ? projectRows
                        : new List<ProjectProgressDailyRecord>();

                    Dictionary<string, int> lookup = rows
                        .Where(item => string.CompareOrdinal(item.Date, todayText) <= 0)
                        .ToDictionary(item => item.Date, item => item.WordsDelta);

                    todayWords = lookup.TryGetValue(todayText, out int value) ? value : 0;
                    thisWeekWords = rows
                        .Where(item => string.CompareOrdinal(item.Date, weekStartText) >= 0 && string.CompareOrdinal(item.Date, todayText) <= 0)
                        .Sum(item => item.WordsDelta);

                    int dailyTarget = Math.Max(0, goal?.DailyTargetWords ?? 0);
                    if (dailyTarget <= 0)
                    {
                        streak = 0;
                    }
                    else
                    {
                        int days = 0;
                        DateOnly cursor = today;
                        while (true)
                        {
                            string key = cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                            if (!lookup.TryGetValue(key, out int words) || words < dailyTarget)
                            {
                                break;
                            }

                            days++;
                            cursor = cursor.AddDays(-1);
                        }

                        streak = days;
                    }
                }

                result.Add(new ProjectListItemDto(
                    project.Id,
                    project.Title,
                    project.CoverImageUrl,
                    primary?.Id,
                    primary?.Title,
                    lastEdited,
                    totalWords,
                    todayWords,
                    thisWeekWords,
                    streak));
            }

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ProjectDto>> CreateProject(
            [FromBody] ProjectCreateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            ProjectRecord project = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled project" : request.Title.Trim(),
                Subtitle = Normalize(request.Subtitle),
                AuthorName = Normalize(request.AuthorName),
                Language = Normalize(request.Language),
                Genre = Normalize(request.Genre),
                DefaultExportSettingsJson = Normalize(request.DefaultExportSettingsJson),
                CoverImageUrl = Normalize(request.CoverImageUrl),
                CreatedUtc = now,
                UpdatedUtc = now
            };

            _dbContext.Projects.Add(project);
            _dbContext.Documents.Add(new DocumentRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                OwnerUserId = userId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Manuscript" : request.Title.Trim(),
                DocumentKind = DocumentKind.Manuscript,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                UpdatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                IsArchived = false,
                ArchivedAt = null,
                DeletedAtUtc = null,
                LanguageCode = project.Language,
                TranslationGroupId = null
            });
            await _dbContext.SaveChangesAsync(ct);

            ProjectDto dto = ToDto(project, 0);
            return Ok(dto);
        }

        [HttpPatch("{projectId:guid}")]
        public async Task<ActionResult<ProjectDto>> UpdateProject(
            Guid projectId,
            [FromBody] ProjectUpdateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                project.Title = request.Title.Trim();
            }

            project.Subtitle = Normalize(request.Subtitle);
            project.AuthorName = Normalize(request.AuthorName);
            project.Language = Normalize(request.Language);
            project.Genre = Normalize(request.Genre);
            project.DefaultExportSettingsJson = Normalize(request.DefaultExportSettingsJson);
            project.CoverImageUrl = Normalize(request.CoverImageUrl);
            project.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            int total = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.ProjectId == projectId && node.ParentId == null)
                .SumAsync(node => (int?)node.WordCountCache, ct) ?? 0;

            return Ok(ToDto(project, total));
        }

        [HttpPost("{projectId:guid}/cover")]
        public async Task<ActionResult<ProjectDto>> SaveProjectCover(
            Guid projectId,
            [FromBody] ProjectCoverUpdateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            project.CoverImageUrl = Normalize(request.ImageUrl);
            project.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            int total = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.ProjectId == projectId && node.ParentId == null)
                .SumAsync(node => (int?)node.WordCountCache, ct) ?? 0;

            return Ok(ToDto(project, total));
        }

        [HttpDelete("{projectId:guid}")]
        [HttpDelete("/app/api/projects/{projectId:guid}")]
        public async Task<IActionResult> DeleteProject(Guid projectId, CancellationToken ct)
        {
            _logger.LogInformation("Projects delete requested: projectId={ProjectId} path={Path}", projectId, Request.Path.Value);
            if (!IsEnabled())
            {
                _logger.LogWarning("Projects delete rejected: projects workflow disabled. projectId={ProjectId}", projectId);
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ProjectDeletionResult result = await _projectDeletion.DeleteOwnedProjectAsync(projectId, userId, ct);
            if (!result.Deleted || !result.ProjectId.HasValue)
            {
                _logger.LogWarning("Projects delete not found: incomingId={IncomingId} userId={UserId}", projectId, userId);
                return NotFound();
            }

            _logger.LogInformation(
                "Projects delete success: projectId={ProjectId} removedDocuments={DocumentCount} removedSections={SectionCount} removedPages={PageCount}",
                result.ProjectId.Value,
                result.Counts?.Documents ?? 0,
                result.Counts?.Sections ?? 0,
                result.Counts?.Pages ?? 0);
            return NoContent();
        }

        [HttpGet("with-documents")]
        public async Task<ActionResult<IReadOnlyList<ProjectWithDocumentsDto>>> ListProjectsWithDocuments(CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            List<ProjectRecord> projects = await _dbContext.Projects
                .AsNoTracking()
                .Where(project => project.OwnerUserId == userId)
                .ToListAsync(ct);
            projects = projects.OrderByDescending(project => project.UpdatedUtc).ToList();

            HashSet<Guid> projectIds = projects.Select(project => project.Id).ToHashSet();
            List<DocumentRecord> documents = await _dbContext.Documents
                .AsNoTracking()
                .Where(document => projectIds.Contains(document.ProjectId) && document.OwnerUserId == userId)
                .ToListAsync(ct);

            Dictionary<Guid, List<ProjectDocumentDto>> docsByProject = documents
                .GroupBy(document => document.ProjectId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(document => document.UpdatedAtUnixSeconds)
                        .Select(ToProjectDocumentDto)
                        .ToList());

            Dictionary<Guid, int> totals = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => projectIds.Contains(node.ProjectId) && node.ParentId == null)
                .GroupBy(node => node.ProjectId)
                .Select(group => new { group.Key, Total = group.Sum(node => node.WordCountCache) })
                .ToDictionaryAsync(item => item.Key, item => item.Total, ct);

            List<ProjectWithDocumentsDto> result = projects
                .Select(project => new ProjectWithDocumentsDto(
                    ToDto(project, totals.TryGetValue(project.Id, out int total) ? total : 0),
                    docsByProject.TryGetValue(project.Id, out List<ProjectDocumentDto>? docs)
                        ? docs
                        : new List<ProjectDocumentDto>()))
                .ToList();

            return Ok(result);
        }

        [HttpGet("{projectId:guid}/documents")]
        public async Task<ActionResult<IReadOnlyList<ProjectDocumentDto>>> ListProjectDocuments(Guid projectId, CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            bool projectExists = await _dbContext.Projects
                .AsNoTracking()
                .AnyAsync(project => project.Id == projectId && project.OwnerUserId == userId, ct);
            if (!projectExists)
            {
                return NotFound();
            }

            List<DocumentRecord> result = await _dbContext.Documents
                .AsNoTracking()
                .Where(document => document.ProjectId == projectId && document.OwnerUserId == userId)
                .OrderByDescending(document => document.UpdatedAtUnixSeconds)
                .ToListAsync(ct);
            List<ProjectDocumentDto> dto = result.Select(ToProjectDocumentDto).ToList();

            return Ok(dto);
        }

        [HttpPost("{projectId:guid}/documents")]
        public async Task<ActionResult<DocumentCreateResponse>> CreateProjectDocument(
            Guid projectId,
            [FromBody] ProjectDocumentCreateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            DocumentKind kind = ParseDocumentKind(request.Kind);
            if (kind == DocumentKind.Manuscript)
            {
                bool hasManuscript = await _dbContext.Documents
                    .AnyAsync(item => item.ProjectId == projectId && item.DocumentKind == DocumentKind.Manuscript, ct);
                if (hasManuscript)
                {
                    return Conflict(new { message = "Project already has a manuscript document." });
                }
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            DocumentRecord document = new()
            {
                Id = request.Id ?? Guid.NewGuid(),
                ProjectId = projectId,
                OwnerUserId = userId,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title.Trim(),
                DocumentKind = kind,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                UpdatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                IsArchived = false,
                ArchivedAt = null,
                DeletedAtUtc = null,
                LanguageCode = project.Language,
                TranslationGroupId = null
            };

            _dbContext.Documents.Add(document);

            Guid? defaultSectionId = null;
            Guid? defaultPageId = null;
            if (request.CreateDefaultStructure)
            {
                SectionRecord section = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Title = "Draft",
                    NarrativePurpose = null,
                    OrderIndex = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Sections.Add(section);
                defaultSectionId = section.Id;

                PageRecord page = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    SectionId = section.Id,
                    Title = "Page 1",
                    Content = string.Empty,
                    OrderIndex = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Pages.Add(page);
                defaultPageId = page.Id;
            }

            project.UpdatedUtc = now;
            await _dbContext.SaveChangesAsync(ct);

            return Ok(new DocumentCreateResponse(
                new DocumentDetailDto(
                    document.Id,
                    document.Title,
                    document.CreatedAt,
                    document.UpdatedAt,
                    document.LanguageCode,
                    document.TranslationGroupId,
                    document.IsArchived,
                    document.ArchivedAt,
                    ToDeletedAtOffset(document.DeletedAtUtc),
                    document.ProjectId,
                    NormalizeDocumentKind(document.DocumentKind)),
                defaultSectionId,
                defaultPageId));
        }

        [HttpPost("from-document/{documentId:guid}")]
        public async Task<ActionResult<ProjectTreeDto>> CreateFromDocument(Guid documentId, CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ConvertDocumentToProject, "projects.convert");
            if (gate is not null)
            {
                return gate;
            }

            DocumentRecord? document = await _dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == documentId && item.OwnerUserId == userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            if (document.ProjectId != Guid.Empty)
            {
                ProjectRecord? existingProject = await _dbContext.Projects
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == document.ProjectId && item.OwnerUserId == userId, ct);
                if (existingProject is not null)
                {
                    List<ProjectNodeRecord> existingNodes = await _dbContext.ProjectNodes
                        .AsNoTracking()
                        .Where(node => node.ProjectId == existingProject.Id)
                        .OrderBy(node => node.ParentId)
                        .ThenBy(node => node.OrderIndex)
                        .ToListAsync(ct);
                    int existingTotal = existingNodes.Where(node => node.ParentId == null).Sum(node => node.WordCountCache);
                    return Ok(new ProjectTreeDto(ToDto(existingProject, existingTotal), existingNodes.Select(ToDto).ToList()));
                }
            }

            List<SectionRecord> sections = await _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            ProjectRecord project = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = $"{document.Title} Project",
                Subtitle = null,
                AuthorName = null,
                Language = document.LanguageCode,
                Genre = null,
                DefaultExportSettingsJson = null,
                CreatedUtc = now,
                UpdatedUtc = now
            };
            _dbContext.Projects.Add(project);

            List<ProjectNodeRecord> nodes = new();
            for (int i = 0; i < sections.Count; i++)
            {
                SectionRecord section = sections[i];
                ProjectNodeRecord chapter = new()
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ParentId = null,
                    NodeType = ProjectNodeType.Chapter,
                    Title = section.Title,
                    OrderIndex = i,
                    LinkedSectionId = null,
                    MetadataJson = null,
                    WordCountCache = 0,
                    UpdatedUtc = now
                };

                ProjectNodeRecord scene = new()
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ParentId = chapter.Id,
                    NodeType = ProjectNodeType.Scene,
                    Title = section.Title,
                    OrderIndex = 0,
                    LinkedSectionId = section.Id,
                    MetadataJson = null,
                    WordCountCache = 0,
                    UpdatedUtc = now
                };

                nodes.Add(chapter);
                nodes.Add(scene);
            }

            if (nodes.Count > 0)
            {
                _dbContext.ProjectNodes.AddRange(nodes);
            }

            await _dbContext.SaveChangesAsync(ct);
            await _wordCounts.RefreshProjectAsync(project.Id, ct);

            List<ProjectNodeRecord> refreshedNodes = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.ProjectId == project.Id)
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.OrderIndex)
                .ToListAsync(ct);

            int total = refreshedNodes.Where(node => node.ParentId == null).Sum(node => node.WordCountCache);
            return Ok(new ProjectTreeDto(ToDto(project, total), refreshedNodes.Select(ToDto).ToList()));
        }

        [HttpGet("{projectId:guid}/tree")]
        public async Task<ActionResult<ProjectTreeDto>> GetTree(Guid projectId, CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectNavigator, "projects.navigator");
            if (gate is not null)
            {
                return gate;
            }

            ProjectRecord? project = await _dbContext.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            List<ProjectNodeRecord> nodes = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.ProjectId == projectId)
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.OrderIndex)
                .ToListAsync(ct);

            int total = nodes.Where(node => node.ParentId == null).Sum(node => node.WordCountCache);
            return Ok(new ProjectTreeDto(ToDto(project, total), nodes.Select(ToDto).ToList()));
        }

        [HttpPost("{projectId:guid}/nodes")]
        public async Task<ActionResult<ProjectNodeDto>> CreateNode(
            Guid projectId,
            [FromBody] ProjectNodeCreateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectStructureEditing, "projects.structure");
            if (gate is not null)
            {
                return gate;
            }

            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            Guid? parentId = request.ParentId;
            if (parentId.HasValue)
            {
                bool parentExists = await _dbContext.ProjectNodes.AnyAsync(
                    node => node.Id == parentId.Value && node.ProjectId == projectId,
                    ct);
                if (!parentExists)
                {
                    return BadRequest(new { message = "Parent node not found in this project." });
                }
            }

            ProjectNodeType nodeType = ParseNodeType(request.NodeType);
            Guid? linkedSectionId = request.LinkedSectionId;
            if (nodeType != ProjectNodeType.Scene)
            {
                linkedSectionId = null;
            }
            else if (linkedSectionId.HasValue)
            {
                bool owned = await IsOwnedSectionAsync(userId, linkedSectionId.Value, ct);
                if (!owned)
                {
                    return BadRequest(new { message = "Linked section does not belong to the user." });
                }
            }

            List<ProjectNodeRecord> siblings = await _dbContext.ProjectNodes
                .Where(node => node.ProjectId == projectId && node.ParentId == parentId)
                .OrderBy(node => node.OrderIndex)
                .ToListAsync(ct);
            int insertAt = request.OrderIndex.GetValueOrDefault(siblings.Count);
            insertAt = Math.Max(0, Math.Min(insertAt, siblings.Count));

            for (int i = 0; i < siblings.Count; i++)
            {
                if (siblings[i].OrderIndex >= insertAt)
                {
                    siblings[i].OrderIndex += 1;
                    siblings[i].UpdatedUtc = DateTimeOffset.UtcNow;
                }
            }

            ProjectNodeRecord node = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentId = parentId,
                NodeType = nodeType,
                Title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title.Trim(),
                OrderIndex = insertAt,
                LinkedSectionId = linkedSectionId,
                MetadataJson = Normalize(request.MetadataJson),
                WordCountCache = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };

            _dbContext.ProjectNodes.Add(node);
            if (node.NodeType == ProjectNodeType.Scene)
            {
                await _sceneLinking.EnsureSceneLinkedSectionAsync(project, node, userId, ct);
            }
            project.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            await _wordCounts.RefreshProjectAsync(projectId, ct);

            return Ok(ToDto(node));
        }

        [HttpPatch("{projectId:guid}/nodes/{nodeId:guid}")]
        public async Task<ActionResult<ProjectNodeDto>> PatchNode(
            Guid projectId,
            Guid nodeId,
            [FromBody] ProjectNodePatchRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            ProjectNodeRecord? node = await _dbContext.ProjectNodes
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == nodeId, ct);
            if (node is null)
            {
                return NotFound();
            }

            if (!IsTitleOnlyNodeRename(request, node))
            {
                ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectStructureEditing, "projects.structure");
                if (gate is not null)
                {
                    return gate;
                }
            }

            Guid? originalParentId = node.ParentId;
            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                node.Title = request.Title.Trim();
            }

            node.ParentId = request.ParentId;

            if (!string.IsNullOrWhiteSpace(request.NodeType))
            {
                node.NodeType = ParseNodeType(request.NodeType);
            }

            if (node.NodeType != ProjectNodeType.Scene)
            {
                node.LinkedSectionId = null;
            }
            else if (request.LinkedSectionId.HasValue)
            {
                bool owned = await IsOwnedSectionAsync(userId, request.LinkedSectionId.Value, ct);
                if (!owned)
                {
                    return BadRequest(new { message = "Linked section does not belong to the user." });
                }
            }

            if (node.NodeType == ProjectNodeType.Scene && request.LinkedSectionId.HasValue)
            {
                node.LinkedSectionId = request.LinkedSectionId;
            }
            node.MetadataJson = request.MetadataJson;
            node.UpdatedUtc = DateTimeOffset.UtcNow;

            if (originalParentId != node.ParentId)
            {
                List<ProjectNodeRecord> oldSiblings = await _dbContext.ProjectNodes
                    .Where(item => item.ProjectId == projectId && item.ParentId == originalParentId && item.Id != node.Id)
                    .OrderBy(item => item.OrderIndex)
                    .ToListAsync(ct);
                for (int i = 0; i < oldSiblings.Count; i++)
                {
                    oldSiblings[i].OrderIndex = i;
                    oldSiblings[i].UpdatedUtc = DateTimeOffset.UtcNow;
                }

                List<ProjectNodeRecord> newSiblings = await _dbContext.ProjectNodes
                    .Where(item => item.ProjectId == projectId && item.ParentId == node.ParentId && item.Id != node.Id)
                    .OrderBy(item => item.OrderIndex)
                    .ToListAsync(ct);
                node.OrderIndex = newSiblings.Count;
            }

            if (node.NodeType == ProjectNodeType.Scene)
            {
                await _sceneLinking.EnsureSceneLinkedSectionAsync(project, node, userId, ct);
            }

            project.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            await _wordCounts.RefreshProjectAsync(projectId, ct);

            return Ok(ToDto(node));
        }

        [HttpPost("{projectId:guid}/nodes/{nodeId:guid}/duplicate")]
        public async Task<ActionResult<ProjectNodeDuplicateResponse>> DuplicateNode(
            Guid projectId,
            Guid nodeId,
            [FromBody] ProjectNodeDuplicateRequest? request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectStructureEditing, "projects.structure");
            if (gate is not null)
            {
                return gate;
            }

            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            List<ProjectNodeRecord> projectNodes = await _dbContext.ProjectNodes
                .Where(item => item.ProjectId == projectId)
                .OrderBy(item => item.ParentId)
                .ThenBy(item => item.OrderIndex)
                .ThenBy(item => item.Id)
                .ToListAsync(ct);

            ProjectNodeRecord? sourceRoot = projectNodes.FirstOrDefault(item => item.Id == nodeId);
            if (sourceRoot is null)
            {
                return NotFound();
            }

            bool duplicateDeep = request?.Deep ?? sourceRoot.NodeType != ProjectNodeType.Scene;
            Dictionary<Guid, List<ProjectNodeRecord>> childrenByParent = projectNodes
                .Where(item => item.ParentId.HasValue)
                .GroupBy(item => item.ParentId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(item => item.OrderIndex)
                        .ThenBy(item => item.Id)
                        .ToList());

            List<ProjectNodeRecord> sourceSubtree = new();
            void CollectSubtree(ProjectNodeRecord node, bool deep)
            {
                sourceSubtree.Add(node);
                if (!deep)
                {
                    return;
                }

                if (!childrenByParent.TryGetValue(node.Id, out List<ProjectNodeRecord>? children))
                {
                    return;
                }

                foreach (ProjectNodeRecord child in children)
                {
                    CollectSubtree(child, deep: true);
                }
            }

            CollectSubtree(sourceRoot, duplicateDeep);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            Guid? parentId = sourceRoot.ParentId;
            int insertAt = sourceRoot.OrderIndex + 1;

            List<ProjectNodeRecord> siblingsToShift = projectNodes
                .Where(item => item.ParentId == parentId && item.Id != sourceRoot.Id && item.OrderIndex >= insertAt)
                .OrderBy(item => item.OrderIndex)
                .ToList();
            foreach (ProjectNodeRecord sibling in siblingsToShift)
            {
                sibling.OrderIndex += 1;
                sibling.UpdatedUtc = now;
            }

            HashSet<Guid> sourceSectionIds = sourceSubtree
                .Where(item => item.NodeType == ProjectNodeType.Scene && item.LinkedSectionId.HasValue)
                .Select(item => item.LinkedSectionId!.Value)
                .ToHashSet();

            Dictionary<Guid, SectionRecord> sectionsById = sourceSectionIds.Count == 0
                ? new Dictionary<Guid, SectionRecord>()
                : await _dbContext.Sections
                    .AsNoTracking()
                    .Where(item => sourceSectionIds.Contains(item.Id))
                    .ToDictionaryAsync(item => item.Id, ct);

            List<PageRecord> sourcePages = sourceSectionIds.Count == 0
                ? new List<PageRecord>()
                : await _dbContext.Pages
                    .AsNoTracking()
                    .Where(item => sourceSectionIds.Contains(item.SectionId))
                    .OrderBy(item => item.OrderIndex)
                    .ThenBy(item => item.Id)
                    .ToListAsync(ct);
            Dictionary<Guid, List<PageRecord>> pagesBySection = sourcePages
                .GroupBy(item => item.SectionId)
                .ToDictionary(group => group.Key, group => group.ToList());
            HashSet<Guid> sourcePageIds = sourcePages.Select(item => item.Id).ToHashSet();

            Dictionary<Guid, PageNoteRecord> pageNotesByPage = sourcePageIds.Count == 0
                ? new Dictionary<Guid, PageNoteRecord>()
                : await _dbContext.PageNotes
                    .AsNoTracking()
                    .Where(item => sourcePageIds.Contains(item.PageId))
                    .ToDictionaryAsync(item => item.PageId, ct);

            Dictionary<Guid, List<PageAnnotationRecord>> annotationsByPage = sourcePageIds.Count == 0
                ? new Dictionary<Guid, List<PageAnnotationRecord>>()
                : (await _dbContext.PageAnnotations
                    .AsNoTracking()
                    .Where(item => sourcePageIds.Contains(item.PageId))
                    .ToListAsync(ct))
                    .GroupBy(item => item.PageId)
                    .ToDictionary(
                        group => group.Key,
                        group => group
                            .OrderBy(item => item.CreatedAt)
                            .ThenBy(item => item.Id)
                            .ToList());

            Dictionary<Guid, SectionSceneCardRecord> sceneCardsBySection = sourceSectionIds.Count == 0
                ? new Dictionary<Guid, SectionSceneCardRecord>()
                : await _dbContext.SectionSceneCards
                    .AsNoTracking()
                    .Where(item => sourceSectionIds.Contains(item.SectionId))
                    .ToDictionaryAsync(item => item.SectionId, ct);

            Dictionary<Guid, SectionNoteRecord> sectionNotesBySection = sourceSectionIds.Count == 0
                ? new Dictionary<Guid, SectionNoteRecord>()
                : await _dbContext.SectionNotes
                    .AsNoTracking()
                    .Where(item => sourceSectionIds.Contains(item.SectionId))
                    .ToDictionaryAsync(item => item.SectionId, ct);

            Dictionary<Guid, int> nextSectionOrderByDocument = new();
            async Task<int> GetNextSectionOrderAsync(Guid documentId)
            {
                if (nextSectionOrderByDocument.TryGetValue(documentId, out int cached))
                {
                    nextSectionOrderByDocument[documentId] = cached + 1;
                    return cached;
                }

                int max = await _dbContext.Sections
                    .AsNoTracking()
                    .Where(item => item.DocumentId == documentId)
                    .Select(item => (int?)item.OrderIndex)
                    .MaxAsync(ct) ?? -1;
                int next = max + 1;
                nextSectionOrderByDocument[documentId] = next + 1;
                return next;
            }

            async Task<Guid?> DuplicateLinkedSectionAsync(Guid sourceSectionId)
            {
                if (!sectionsById.TryGetValue(sourceSectionId, out SectionRecord? sourceSection))
                {
                    return null;
                }

                int sectionOrder = await GetNextSectionOrderAsync(sourceSection.DocumentId);
                SectionRecord newSection = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = sourceSection.DocumentId,
                    Title = sourceSection.Title,
                    NarrativePurpose = sourceSection.NarrativePurpose,
                    LanguageCode = sourceSection.LanguageCode,
                    TranslationGroupId = sourceSection.TranslationGroupId,
                    OrderIndex = sectionOrder,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Sections.Add(newSection);

                if (sceneCardsBySection.TryGetValue(sourceSectionId, out SectionSceneCardRecord? sourceCard))
                {
                    _dbContext.SectionSceneCards.Add(new SectionSceneCardRecord
                    {
                        SectionId = newSection.Id,
                        NarrativePurpose = sourceCard.NarrativePurpose,
                        EmotionalBeat = sourceCard.EmotionalBeat,
                        KeyEvents = sourceCard.KeyEvents,
                        OpenQuestions = sourceCard.OpenQuestions,
                        PovCharacterId = sourceCard.PovCharacterId,
                        PlaceId = sourceCard.PlaceId,
                        TimelineEventId = sourceCard.TimelineEventId,
                        TimeRef = sourceCard.TimeRef,
                        TagsJson = sourceCard.TagsJson,
                        ReferencesJson = sourceCard.ReferencesJson,
                        UpdatedUtc = now
                    });
                }

                if (sectionNotesBySection.TryGetValue(sourceSectionId, out SectionNoteRecord? sourceNote))
                {
                    _dbContext.SectionNotes.Add(new SectionNoteRecord
                    {
                        SectionId = newSection.Id,
                        NotesText = sourceNote.NotesText,
                        UpdatedAtUtc = now
                    });
                }

                List<PageRecord> pages = pagesBySection.TryGetValue(sourceSectionId, out List<PageRecord>? sourceSectionPages)
                    ? sourceSectionPages
                    : new List<PageRecord>();
                Dictionary<Guid, Guid> pageIdMap = new();
                foreach (PageRecord sourcePage in pages)
                {
                    Guid newPageId = Guid.NewGuid();
                    pageIdMap[sourcePage.Id] = newPageId;

                    _dbContext.Pages.Add(new PageRecord
                    {
                        Id = newPageId,
                        DocumentId = sourcePage.DocumentId,
                        SectionId = newSection.Id,
                        Title = sourcePage.Title,
                        Content = sourcePage.Content,
                        OrderIndex = sourcePage.OrderIndex,
                        CreatedAt = now,
                        UpdatedAt = now
                    });

                    if (pageNotesByPage.TryGetValue(sourcePage.Id, out PageNoteRecord? sourcePageNote))
                    {
                        _dbContext.PageNotes.Add(new PageNoteRecord
                        {
                            PageId = newPageId,
                            Notes = sourcePageNote.Notes,
                            UpdatedAt = now
                        });
                    }
                }

                foreach ((Guid sourcePageId, Guid newPageId) in pageIdMap)
                {
                    if (!annotationsByPage.TryGetValue(sourcePageId, out List<PageAnnotationRecord>? sourceAnnotations))
                    {
                        continue;
                    }

                    foreach (PageAnnotationRecord sourceAnnotation in sourceAnnotations)
                    {
                        _dbContext.PageAnnotations.Add(new PageAnnotationRecord
                        {
                            Id = Guid.NewGuid(),
                            DocumentId = sourceAnnotation.DocumentId,
                            PageId = newPageId,
                            Kind = sourceAnnotation.Kind,
                            Status = sourceAnnotation.Status,
                            AnchorFrom = sourceAnnotation.AnchorFrom,
                            AnchorTo = sourceAnnotation.AnchorTo,
                            AnchorText = sourceAnnotation.AnchorText,
                            Content = sourceAnnotation.Content,
                            AuthorUserId = sourceAnnotation.AuthorUserId,
                            CreatedAt = sourceAnnotation.CreatedAt,
                            ResolvedAt = sourceAnnotation.ResolvedAt
                        });
                    }
                }

                return newSection.Id;
            }

            Dictionary<Guid, Guid> duplicatedIdMap = new();
            List<ProjectNodeRecord> createdNodes = new();
            List<string> existingSiblingTitles = projectNodes
                .Where(item => item.ParentId == parentId)
                .Select(item => item.Title)
                .ToList();

            foreach (ProjectNodeRecord sourceNode in sourceSubtree)
            {
                bool isRoot = sourceNode.Id == sourceRoot.Id;
                Guid? newParentId = isRoot
                    ? parentId
                    : sourceNode.ParentId.HasValue && duplicatedIdMap.TryGetValue(sourceNode.ParentId.Value, out Guid mappedParentId)
                        ? mappedParentId
                        : null;
                string title = isRoot
                    ? BuildDuplicateNodeTitle(sourceNode.Title, existingSiblingTitles)
                    : sourceNode.Title;

                ProjectNodeRecord duplicate = new()
                {
                    Id = Guid.NewGuid(),
                    ProjectId = sourceNode.ProjectId,
                    ParentId = newParentId,
                    NodeType = sourceNode.NodeType,
                    Title = title,
                    OrderIndex = isRoot ? insertAt : sourceNode.OrderIndex,
                    LinkedSectionId = sourceNode.NodeType == ProjectNodeType.Scene ? sourceNode.LinkedSectionId : null,
                    MetadataJson = sourceNode.MetadataJson,
                    WordCountCache = sourceNode.WordCountCache,
                    UpdatedUtc = now
                };

                if (duplicate.NodeType == ProjectNodeType.Scene && sourceNode.LinkedSectionId.HasValue)
                {
                    duplicate.LinkedSectionId = await DuplicateLinkedSectionAsync(sourceNode.LinkedSectionId.Value);
                }

                _dbContext.ProjectNodes.Add(duplicate);
                createdNodes.Add(duplicate);
                duplicatedIdMap[sourceNode.Id] = duplicate.Id;
                if (isRoot)
                {
                    existingSiblingTitles.Add(duplicate.Title);
                }
            }

            await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            // Keep aggregate caches in sync without triggering a full project recalc.
            int duplicatedWordCountDelta = sourceRoot.WordCountCache;
            List<ProjectNodeRecord> aggregateNodesUpdated = new();
            Guid? aggregateNodeId = sourceRoot.ParentId;
            while (aggregateNodeId.HasValue)
            {
                ProjectNodeRecord? aggregateNode = projectNodes.FirstOrDefault(item => item.Id == aggregateNodeId.Value);
                if (aggregateNode is null)
                {
                    break;
                }

                aggregateNode.WordCountCache += duplicatedWordCountDelta;
                aggregateNode.UpdatedUtc = now;
                aggregateNodesUpdated.Add(aggregateNode);
                aggregateNodeId = aggregateNode.ParentId;
            }

            project.UpdatedUtc = now;
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            Guid newRootId = duplicatedIdMap[sourceRoot.Id];
            ProjectNodeRecord? newRoot = createdNodes.FirstOrDefault(item => item.Id == newRootId);
            if (newRoot is null)
            {
                return StatusCode(500, new { message = "Duplicate node failed." });
            }

            return Ok(new ProjectNodeDuplicateResponse(
                newRoot.Id,
                createdNodes
                    .OrderBy(item => item.ParentId)
                    .ThenBy(item => item.OrderIndex)
                    .Select(ToDto)
                    .ToList(),
                siblingsToShift
                    .Concat(aggregateNodesUpdated)
                    .GroupBy(item => item.Id)
                    .Select(group => group.First())
                    .OrderBy(item => item.OrderIndex)
                    .Select(ToDto)
                    .ToList()));
        }

        [HttpPost("{projectId:guid}/nodes/{nodeId:guid}/reorder")]
        public async Task<ActionResult<IReadOnlyList<ProjectNodeDto>>> ReorderChildren(
            Guid projectId,
            Guid nodeId,
            [FromBody] ProjectNodeReorderRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectStructureEditing, "projects.reorder");
            if (gate is not null)
            {
                return gate;
            }

            string correlationId = Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            if (request?.OrderedChildIds is null)
            {
                return CreateReorderProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid reorder payload",
                    "orderedChildIds is required.",
                    "projects.reorder.invalid_payload",
                    correlationId,
                    null,
                    null);
            }

            Guid? parentId = nodeId == Guid.Empty ? null : nodeId;
            if (parentId.HasValue)
            {
                bool parentExists = await _dbContext.ProjectNodes.AnyAsync(
                    item => item.ProjectId == projectId && item.Id == parentId.Value,
                    ct);
                if (!parentExists)
                {
                    return NotFound();
                }
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            List<ProjectNodeRecord> children = await _dbContext.ProjectNodes
                .Where(item => item.ProjectId == projectId && item.ParentId == parentId)
                .OrderBy(item => item.OrderIndex)
                .ToListAsync(ct);

            List<Guid> orderedIds = request.OrderedChildIds.ToList();
            List<Guid> existingIds = children.Select(item => item.Id).ToList();
            List<Guid> duplicateIds = orderedIds
                .GroupBy(id => id)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateIds.Count > 0)
            {
                _logger.LogWarning(
                    "Project node reorder rejected: duplicate ids. ProjectId={ProjectId} ParentId={ParentId} CorrelationId={CorrelationId} DuplicateIds={DuplicateIds}",
                    projectId,
                    parentId,
                    correlationId,
                    string.Join(",", duplicateIds));
                return CreateReorderProblem(
                    StatusCodes.Status409Conflict,
                    "Invalid reorder request",
                    "orderedChildIds contains duplicate ids.",
                    "projects.reorder.duplicate_ids",
                    correlationId,
                    existingIds,
                    orderedIds,
                    duplicateIds);
            }

            if (orderedIds.Count != children.Count)
            {
                _logger.LogWarning(
                    "Project node reorder rejected: child count mismatch. ProjectId={ProjectId} ParentId={ParentId} CorrelationId={CorrelationId} ExistingCount={ExistingCount} OrderedCount={OrderedCount}",
                    projectId,
                    parentId,
                    correlationId,
                    children.Count,
                    orderedIds.Count);
                return CreateReorderProblem(
                    StatusCodes.Status409Conflict,
                    "Invalid reorder request",
                    "orderedChildIds count does not match the current child count for this parent.",
                    "projects.reorder.child_count_mismatch",
                    correlationId,
                    existingIds,
                    orderedIds);
            }

            HashSet<Guid> existing = children.Select(item => item.Id).ToHashSet();
            List<Guid> unknownIds = orderedIds.Where(id => !existing.Contains(id)).ToList();
            List<Guid> missingIds = existingIds.Where(id => !orderedIds.Contains(id)).ToList();
            if (unknownIds.Count > 0 || missingIds.Count > 0)
            {
                _logger.LogWarning(
                    "Project node reorder rejected: ids mismatch. ProjectId={ProjectId} ParentId={ParentId} CorrelationId={CorrelationId} UnknownIds={UnknownIds} MissingIds={MissingIds}",
                    projectId,
                    parentId,
                    correlationId,
                    string.Join(",", unknownIds),
                    string.Join(",", missingIds));
                return CreateReorderProblem(
                    StatusCodes.Status409Conflict,
                    "Invalid reorder request",
                    "orderedChildIds must contain the exact set of current child ids for this parent.",
                    "projects.reorder.child_set_mismatch",
                    correlationId,
                    existingIds,
                    orderedIds,
                    unknownIds: unknownIds,
                    missingIds: missingIds);
            }

            Dictionary<Guid, int> orderLookup = orderedIds
                .Select((id, index) => new { id, index })
                .ToDictionary(item => item.id, item => item.index);

            foreach (ProjectNodeRecord child in children)
            {
                int next = orderLookup[child.Id];
                if (child.OrderIndex != next)
                {
                    child.OrderIndex = next;
                    child.UpdatedUtc = DateTimeOffset.UtcNow;
                }
            }

            project.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            await _wordCounts.RefreshProjectAsync(projectId, ct);
            stopwatch.Stop();

            _logger.LogInformation(
                "Project node reorder applied. ProjectId={ProjectId} ParentId={ParentId} CorrelationId={CorrelationId} ChildCount={ChildCount} DurationMs={DurationMs}",
                projectId,
                parentId,
                correlationId,
                orderedIds.Count,
                stopwatch.ElapsedMilliseconds);

            List<ProjectNodeDto> result = children
                .OrderBy(child => child.OrderIndex)
                .Select(ToDto)
                .ToList();

            return Ok(result);
        }

        [HttpDelete("{projectId:guid}/nodes/{nodeId:guid}")]
        public async Task<IActionResult> DeleteNode(
            Guid projectId,
            Guid nodeId,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectStructureEditing, "projects.structure");
            if (gate is not null)
            {
                return gate;
            }

            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            ProjectNodeRecord? node = await _dbContext.ProjectNodes
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == nodeId, ct);
            if (node is null)
            {
                return NotFound();
            }

            Guid? parentId = node.ParentId;
            List<ProjectNodeRecord> siblings = await _dbContext.ProjectNodes
                .Where(item => item.ProjectId == projectId && item.ParentId == parentId && item.Id != nodeId)
                .OrderBy(item => item.OrderIndex)
                .ToListAsync(ct);

            _dbContext.ProjectNodes.Remove(node);

            for (int i = 0; i < siblings.Count; i++)
            {
                if (siblings[i].OrderIndex != i)
                {
                    siblings[i].OrderIndex = i;
                    siblings[i].UpdatedUtc = DateTimeOffset.UtcNow;
                }
            }

            project.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            await _wordCounts.RefreshProjectAsync(projectId, ct);

            return NoContent();
        }

        [HttpPost("{projectId:guid}/nodes/{nodeId:guid}/open-scene")]
        public async Task<ActionResult<ProjectSceneOpenTargetDto>> OpenScene(
            Guid projectId,
            Guid nodeId,
            CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.OpenSceneInEditor, "projects.open-scene");
            if (gate is not null)
            {
                return gate;
            }

            ProjectRecord? project = await _dbContext.Projects
                .FirstOrDefaultAsync(item => item.Id == projectId && item.OwnerUserId == userId, ct);
            if (project is null)
            {
                return NotFound();
            }

            ProjectNodeRecord? node = await _dbContext.ProjectNodes
                .FirstOrDefaultAsync(item => item.ProjectId == projectId && item.Id == nodeId, ct);
            if (node is null)
            {
                return NotFound();
            }

            if (node.NodeType != ProjectNodeType.Scene)
            {
                return BadRequest(new { message = "Only scene nodes can be opened in the editor." });
            }

            await EnsureSceneContentExistsAsync(project, node, ct);

            await _dbContext.SaveChangesAsync(ct);

            Guid? documentId = null;
            Guid? sectionId = node.LinkedSectionId;
            if (sectionId.HasValue)
            {
                documentId = await _dbContext.Sections
                    .AsNoTracking()
                    .Where(section => section.Id == sectionId.Value)
                    .Select(section => (Guid?)section.DocumentId)
                    .FirstOrDefaultAsync(ct);
            }

            return Ok(new ProjectSceneOpenTargetDto(projectId, nodeId, documentId, sectionId, node.Title));
        }

        private async Task EnsureSceneContentExistsAsync(ProjectRecord project, ProjectNodeRecord sceneNode, CancellationToken ct)
        {
            if (sceneNode.NodeType != ProjectNodeType.Scene)
            {
                return;
            }

            bool exists = await _dbContext.SceneContents
                .AsNoTracking()
                .AnyAsync(item => item.SceneNodeId == sceneNode.Id, ct);
            if (exists)
            {
                return;
            }

            string content = string.Empty;
            if (sceneNode.LinkedSectionId.HasValue)
            {
                Guid sectionId = sceneNode.LinkedSectionId.Value;
                List<string> parts = (await _dbContext.Pages
                    .AsNoTracking()
                    .Where(page => page.SectionId == sectionId)
                    .Select(page => new
                    {
                        page.OrderIndex,
                        page.UpdatedAt,
                        Content = page.Content ?? string.Empty
                    })
                    .ToListAsync(ct))
                    .OrderBy(page => page.OrderIndex)
                    .ThenBy(page => page.UpdatedAt)
                    .Select(page => page.Content)
                    .ToList();

                content = string.Join("\n\n", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
            }

            _dbContext.SceneContents.Add(new SceneContentRecord
            {
                SceneNodeId = sceneNode.Id,
                ContentJson = content,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        [HttpGet("{projectId:guid}/stats")]
        public async Task<ActionResult<ProjectStatsDto>> GetStats(Guid projectId, CancellationToken ct)
        {
            if (!IsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            await _wordCounts.RefreshProjectAsync(projectId, ct);
            ProjectStatsDto? stats = await _wordCounts.GetProjectStatsAsync(userId, projectId, ct);
            if (stats is null)
            {
                return NotFound();
            }

            return Ok(stats);
        }

        [HttpGet("{projectId:guid}/goals")]
        public async Task<ActionResult<ProjectGoalDto>> GetGoals(Guid projectId, CancellationToken ct)
        {
            if (!IsEnabled() || !IsGoalsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.WritingGoals, "projects.goals");
            if (gate is not null)
            {
                return gate;
            }

            ProjectProgressDashboardDto? dashboard = await _goals.GetDashboardAsync(userId, projectId, ct);
            if (dashboard is null)
            {
                return NotFound();
            }

            return Ok(dashboard.Goal);
        }

        [HttpPut("{projectId:guid}/goals")]
        public async Task<ActionResult<ProjectGoalDto>> UpdateGoals(
            Guid projectId,
            [FromBody] ProjectGoalUpdateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled() || !IsGoalsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.WritingGoals, "projects.goals");
            if (gate is not null)
            {
                return gate;
            }

            ProjectGoalDto? goal = await _goals.UpsertGoalAsync(userId, projectId, request, ct);
            if (goal is null)
            {
                return NotFound();
            }

            return Ok(goal);
        }

        [HttpGet("{projectId:guid}/progress")]
        public async Task<ActionResult<ProjectProgressDashboardDto>> GetProgress(Guid projectId, CancellationToken ct)
        {
            if (!IsEnabled() || !IsGoalsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.ProjectProgressDashboard, "projects.progress");
            if (gate is not null)
            {
                return gate;
            }

            ProjectProgressDashboardDto? dashboard = await _goals.GetDashboardAsync(userId, projectId, ct);
            if (dashboard is null)
            {
                return NotFound();
            }

            return Ok(dashboard);
        }

        [HttpPost("{projectId:guid}/milestones")]
        public async Task<ActionResult<ProjectMilestoneDto>> CreateMilestone(
            Guid projectId,
            [FromBody] ProjectMilestoneCreateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled() || !IsGoalsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.Milestones, "projects.milestones");
            if (gate is not null)
            {
                return gate;
            }

            ProjectMilestoneDto? result = await _goals.CreateMilestoneAsync(userId, projectId, request, ct);
            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpPatch("{projectId:guid}/milestones/{milestoneId:guid}")]
        public async Task<ActionResult<ProjectMilestoneDto>> UpdateMilestone(
            Guid projectId,
            Guid milestoneId,
            [FromBody] ProjectMilestoneUpdateRequest request,
            CancellationToken ct)
        {
            if (!IsEnabled() || !IsGoalsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.Milestones, "projects.milestones");
            if (gate is not null)
            {
                return gate;
            }

            ProjectMilestoneDto? result = await _goals.UpdateMilestoneAsync(userId, projectId, milestoneId, request, ct);
            if (result is null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpDelete("{projectId:guid}/milestones/{milestoneId:guid}")]
        public async Task<IActionResult> DeleteMilestone(Guid projectId, Guid milestoneId, CancellationToken ct)
        {
            if (!IsEnabled() || !IsGoalsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.Milestones, "projects.milestones");
            if (gate is not null)
            {
                return gate;
            }

            bool removed = await _goals.DeleteMilestoneAsync(userId, projectId, milestoneId, ct);
            return removed ? NoContent() : NotFound();
        }

        [HttpPost("{projectId:guid}/sessions/start")]
        public async Task<ActionResult<WritingSessionDto>> StartSession(Guid projectId, CancellationToken ct)
        {
            if (!IsEnabled() || !IsGoalsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.WritingSessionTracking, "projects.sessions");
            if (gate is not null)
            {
                return gate;
            }

            WritingSessionDto? session = await _goals.StartSessionAsync(userId, projectId, ct);
            if (session is null)
            {
                return NotFound();
            }

            return Ok(session);
        }

        [HttpPost("{projectId:guid}/sessions/{sessionId:guid}/stop")]
        public async Task<ActionResult<WritingSessionDto>> StopSession(
            Guid projectId,
            Guid sessionId,
            [FromBody] WritingSessionStopRequest? request,
            CancellationToken ct)
        {
            if (!IsEnabled() || !IsGoalsEnabled())
            {
                return NotFound();
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.WritingSessionTracking, "projects.sessions");
            if (gate is not null)
            {
                return gate;
            }

            WritingSessionDto? session = await _goals.StopSessionAsync(userId, projectId, sessionId, request?.Notes, ct);
            if (session is null)
            {
                return NotFound();
            }

            return Ok(session);
        }

        private ObjectResult CreateReorderProblem(
            int statusCode,
            string title,
            string detail,
            string code,
            string correlationId,
            IReadOnlyList<Guid>? currentChildIds,
            IReadOnlyList<Guid>? orderedChildIds,
            IReadOnlyList<Guid>? duplicateIds = null,
            IReadOnlyList<Guid>? unknownIds = null,
            IReadOnlyList<Guid>? missingIds = null)
        {
            ProblemDetails problem = new()
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            problem.Extensions["correlationId"] = correlationId;
            if (currentChildIds is not null)
            {
                problem.Extensions["currentChildIds"] = currentChildIds;
            }
            if (orderedChildIds is not null)
            {
                problem.Extensions["orderedChildIds"] = orderedChildIds;
            }
            if (duplicateIds is not null && duplicateIds.Count > 0)
            {
                problem.Extensions["duplicateIds"] = duplicateIds;
            }
            if (unknownIds is not null && unknownIds.Count > 0)
            {
                problem.Extensions["unknownIds"] = unknownIds;
            }
            if (missingIds is not null && missingIds.Count > 0)
            {
                problem.Extensions["missingIds"] = missingIds;
            }

            return new ObjectResult(problem)
            {
                StatusCode = statusCode
            };
        }

        private bool IsEnabled()
        {
            return _configuration.GetValue<bool?>("Workflow:ProjectsEnabled")
                ?? _configuration.GetValue<bool?>("WriterApp:Workflow:ProjectsEnabled")
                ?? false;
        }

        private bool IsGoalsEnabled()
        {
            return _configuration.GetValue<bool?>("Workflow:GoalsEnabled")
                ?? _configuration.GetValue<bool?>("WriterApp:Workflow:GoalsEnabled")
                ?? false;
        }

        private async Task<ActionResult?> EnsureFeatureAllowedAsync(string userId, FeatureKey feature, string featureCode)
        {
            UserEntitlements entitlements = await _entitlementService.GetEntitlementsAsync(userId);
            PlanTier userTier = _entitlementService.GetUserTier(entitlements);
            if (FeatureRegistry.IsFeatureAllowed(feature, userTier))
            {
                return null;
            }

            PlanTier requiredTier = FeatureRegistry.FeatureMinimumTier[feature];
            _logger.LogInformation(
                "FeatureAccessDenied FeatureKey={FeatureKey} UserTier={UserTier} RequiredTier={RequiredTier}",
                feature,
                userTier,
                requiredTier);

            ProblemDetails problem = EntitlementDeniedApiError.ForFeature(
                featureCode,
                $"Available in {requiredTier} plan.");
            problem.Extensions["code"] = "entitlement_denied";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

            ObjectResult result = new(problem)
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
            result.ContentTypes.Add("application/problem+json");
            return result;
        }

        private static bool IsTitleOnlyNodeRename(ProjectNodePatchRequest request, ProjectNodeRecord node)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return false;
            }

            string nextTitle = request.Title.Trim();
            if (string.Equals(node.Title, nextTitle, StringComparison.Ordinal))
            {
                return false;
            }

            bool sameParent = request.ParentId == node.ParentId;
            bool sameLinkedSection = request.LinkedSectionId == node.LinkedSectionId;
            bool sameMetadata = string.Equals(request.MetadataJson, node.MetadataJson, StringComparison.Ordinal);
            bool sameNodeType = string.IsNullOrWhiteSpace(request.NodeType)
                || string.Equals(request.NodeType.Trim(), node.NodeType.ToString(), StringComparison.OrdinalIgnoreCase);

            return sameParent && sameLinkedSection && sameMetadata && sameNodeType;
        }

        private async Task<bool> GoalsTablesExistAsync(CancellationToken ct)
        {
            bool hasGoalsTable = await DbTableExistsAsync("ProjectGoals", ct);
            bool hasProgressDailyTable = await DbTableExistsAsync("ProjectProgressDaily", ct);
            if (hasGoalsTable && hasProgressDailyTable)
            {
                return true;
            }

            _logger.LogWarning(
                "Goals feature is enabled, but required tables are missing. ProjectGoalsExists={ProjectGoalsExists}, ProjectProgressDailyExists={ProjectProgressDailyExists}.",
                hasGoalsTable,
                hasProgressDailyTable);
            return false;
        }

        private async Task<bool> DbTableExistsAsync(string tableName, CancellationToken ct)
        {
            if (!_dbContext.Database.IsSqlite())
            {
                return true;
            }

            if (SqliteTableExistsCache.TryGetValue(tableName, out bool cachedExists))
            {
                return cachedExists;
            }

            DbConnection connection = _dbContext.Database.GetDbConnection();
            bool openedHere = false;
            try
            {
                if (connection.State != ConnectionState.Open)
                {
                    await connection.OpenAsync(ct);
                    openedHere = true;
                }

                await using DbCommand command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $tableName LIMIT 1;";
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = "$tableName";
                parameter.Value = tableName;
                command.Parameters.Add(parameter);

                object? result = await command.ExecuteScalarAsync(ct);
                bool exists = result is not null;
                SqliteTableExistsCache[tableName] = exists;
                return exists;
            }
            finally
            {
                if (openedHere)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task<bool> IsOwnedSectionAsync(string userId, Guid sectionId, CancellationToken ct)
        {
            return await _dbContext.Sections
                .Where(section => section.Id == sectionId)
                .Join(
                    _dbContext.Documents,
                    section => section.DocumentId,
                    document => document.Id,
                    (section, document) => new { section, document })
                .AnyAsync(row => row.document.OwnerUserId == userId, ct);
        }

        private static ProjectNodeType ParseNodeType(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return ProjectNodeType.Scene;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "part" => ProjectNodeType.Part,
                "chapter" => ProjectNodeType.Chapter,
                "frontmatteritem" => ProjectNodeType.FrontMatterItem,
                "front_matter_item" => ProjectNodeType.FrontMatterItem,
                "front-matter-item" => ProjectNodeType.FrontMatterItem,
                _ => ProjectNodeType.Scene
            };
        }

        private static string NormalizeNodeType(ProjectNodeType nodeType)
        {
            return nodeType switch
            {
                ProjectNodeType.Part => "part",
                ProjectNodeType.Chapter => "chapter",
                ProjectNodeType.FrontMatterItem => "frontMatterItem",
                _ => "scene"
            };
        }

        private static string BuildDuplicateNodeTitle(string? title, IEnumerable<string> existingTitles)
        {
            string baseTitle = string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim();
            HashSet<string> normalized = new(
                existingTitles
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim()),
                StringComparer.OrdinalIgnoreCase);

            string candidate = $"{baseTitle} (Copy)";
            int counter = 2;
            while (normalized.Contains(candidate))
            {
                candidate = $"{baseTitle} (Copy {counter})";
                counter++;
            }

            return candidate;
        }

        private static ProjectDto ToDto(ProjectRecord project, int totalWords)
        {
            return new ProjectDto(
                project.Id,
                project.Title,
                project.Subtitle,
                project.AuthorName,
                project.Language,
                project.Genre,
                project.CoverImageUrl,
                project.CreatedUtc,
                project.UpdatedUtc,
                totalWords);
        }

        private static ProjectNodeDto ToDto(ProjectNodeRecord node)
        {
            return new ProjectNodeDto(
                node.Id,
                node.ProjectId,
                node.ParentId,
                NormalizeNodeType(node.NodeType),
                node.Title,
                node.OrderIndex,
                node.LinkedSectionId,
                node.MetadataJson,
                node.WordCountCache,
                node.UpdatedUtc);
        }

        private static ProjectDocumentDto ToProjectDocumentDto(DocumentRecord document)
        {
            return new ProjectDocumentDto(
                document.Id,
                document.ProjectId,
                document.Title,
                NormalizeDocumentKind(document.DocumentKind),
                document.CreatedAt,
                document.UpdatedAt,
                document.IsArchived,
                document.ArchivedAt,
                ToDeletedAtOffset(document.DeletedAtUtc));
        }

        private static DateTimeOffset? ToDeletedAtOffset(DateTime? deletedAtUtc)
        {
            if (!deletedAtUtc.HasValue)
            {
                return null;
            }

            DateTime normalized = deletedAtUtc.Value.Kind switch
            {
                DateTimeKind.Utc => deletedAtUtc.Value,
                DateTimeKind.Local => deletedAtUtc.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(deletedAtUtc.Value, DateTimeKind.Utc)
            };

            return new DateTimeOffset(normalized);
        }

        private static DocumentKind ParseDocumentKind(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DocumentKind.Manuscript;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "manuscript" => DocumentKind.Manuscript,
                "synopsis" => DocumentKind.Synopsis,
                "notes" => DocumentKind.Notes,
                "outline" => DocumentKind.Outline,
                "other" => DocumentKind.Other,
                _ => DocumentKind.Other
            };
        }

        private static string NormalizeDocumentKind(DocumentKind kind)
        {
            return kind switch
            {
                DocumentKind.Manuscript => "manuscript",
                DocumentKind.Synopsis => "synopsis",
                DocumentKind.Notes => "notes",
                DocumentKind.Outline => "outline",
                _ => "other"
            };
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static TimeZoneInfo ResolveTimeZone(string timezone)
        {
            if (string.IsNullOrWhiteSpace(timezone))
            {
                return TimeZoneInfo.Utc;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezone);
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Utc;
            }
            catch (InvalidTimeZoneException)
            {
                return TimeZoneInfo.Utc;
            }
        }
    }
}
