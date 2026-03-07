using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Data.Documents;
using WriterApp.Data;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public sealed class PagesController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ISectionRepository _sections;
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ISearchIndexService _searchIndex;
        private readonly IVersionHistoryService _versionHistory;
        private readonly IProjectWordCountService _projectWordCounts;
        private readonly IProjectGoalsService _projectGoals;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<PagesController> _logger;

        public PagesController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            ISearchIndexService searchIndex,
            IVersionHistoryService versionHistory,
            IProjectWordCountService projectWordCounts,
            IProjectGoalsService projectGoals,
            AppDbContext dbContext,
            ILogger<PagesController> logger)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _versionHistory = versionHistory ?? throw new ArgumentNullException(nameof(versionHistory));
            _projectWordCounts = projectWordCounts ?? throw new ArgumentNullException(nameof(projectWordCounts));
            _projectGoals = projectGoals ?? throw new ArgumentNullException(nameof(projectGoals));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("sections/{sectionId:guid}/pages")]
        public async Task<ActionResult<IReadOnlyList<PageDto>>> ListPages(Guid sectionId, CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string traceId = Request.Headers["X-Trace-Id"].FirstOrDefault()
                ?? HttpContext.TraceIdentifier;
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _sections.ExistsAsync(sectionId, userId, ct))
            {
                return NotFound();
            }

            _logger.LogDebug("GET_PAGE_BEGIN TraceId={TraceId} SectionId={SectionId}", traceId, sectionId);

            IReadOnlyList<PageRecord> pages = await _pages.ListBySectionAsync(sectionId, userId, ct);
            List<PageDto> result = pages
                .Select(page => new PageDto(
                    page.Id,
                    page.DocumentId,
                    page.SectionId,
                    page.Title,
                    page.Content,
                    page.OrderIndex,
                    page.CreatedAt,
                    page.UpdatedAt))
                .ToList();

            foreach (PageRecord page in pages)
            {
                ContentFingerprint fp = BuildFingerprint(page.Content);
                _logger.LogDebug(
                    "GET_PAGE_RETURN TraceId={TraceId} PageId={PageId} JsonLength={JsonLength} TextLength={TextLength} Hash={Hash}",
                    traceId,
                    page.Id,
                    fp.JsonLength,
                    fp.TextLength,
                    fp.Hash);
            }

            return Ok(result);
        }

        [HttpPost("sections/{sectionId:guid}/pages")]
        public async Task<ActionResult<PageDto>> CreatePage(
            Guid sectionId,
            [FromBody] PageCreateRequest request,
            CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? section = await _sections.GetAsync(sectionId, userId, ct);
            if (section is null)
            {
                return NotFound();
            }

            Guid pageId = request.Id ?? Guid.NewGuid();
            if (request.Id.HasValue)
            {
                PageRecord? existing = await _pages.GetAsync(pageId, userId, ct);
                if (existing is not null)
                {
                    if (existing.SectionId != sectionId)
                    {
                        return Conflict(new { message = "Page already exists under a different section." });
                    }

                    return Ok(new PageDto(
                        existing.Id,
                        existing.DocumentId,
                        existing.SectionId,
                        existing.Title,
                        existing.Content,
                        existing.OrderIndex,
                        existing.CreatedAt,
                        existing.UpdatedAt));
                }
            }

            string title = string.IsNullOrWhiteSpace(request.Title) ? "Page" : request.Title.Trim();
            string content = request.Content ?? string.Empty;
            DateTimeOffset createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow;
            DateTimeOffset updatedAt = request.UpdatedAt ?? createdAt;

            int orderIndex;
            if (request.OrderIndex.HasValue && request.OrderIndex.Value >= 0)
            {
                orderIndex = request.OrderIndex.Value;
            }
            else
            {
                IReadOnlyList<PageRecord> existing = await _pages.ListBySectionAsync(sectionId, userId, ct);
                orderIndex = existing.Count;
            }

            PageRecord page = new()
            {
                Id = pageId,
                DocumentId = section.DocumentId,
                SectionId = section.Id,
                Title = title,
                Content = content,
                OrderIndex = orderIndex,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

            await _pages.CreateAsync(page, ct);
            await _searchIndex.UpsertPageAsync(page, ct);
            await _versionHistory.CreateCheckpointIfDueAsync(
                userId,
                page,
                page.Content ?? string.Empty,
                TimeSpan.FromSeconds(60),
                ct);
            await _projectWordCounts.RefreshForSectionAsync(sectionId, ct);
            await _projectGoals.TrackPageDeltaAsync(
                null,
                page,
                $"page:create:{page.Id}:{page.CreatedAt.UtcTicks}",
                ct);
            await MirrorSectionPagesToSceneContentAsync(page.SectionId, ct);

            PageDto dto = new(
                page.Id,
                page.DocumentId,
                page.SectionId,
                page.Title,
                page.Content ?? string.Empty,
                page.OrderIndex,
                page.CreatedAt,
                page.UpdatedAt);

            return Ok(dto);
        }

        [HttpPut("pages/{pageId:guid}")]
        public async Task<ActionResult<PageDto>> UpdatePage(
            Guid pageId,
            [FromBody] PageUpdateRequest request,
            CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string traceId = Request.Headers["X-Trace-Id"].FirstOrDefault()
                ?? HttpContext.TraceIdentifier;
            string userId = _userIdResolver.ResolveUserId(User);
            PageUpdate update = new(
                string.IsNullOrWhiteSpace(request.Title) ? null : request.Title.Trim(),
                request.Content);

            PageRecord? before = await _pages.GetAsync(pageId, userId, ct);
            if (before is null)
            {
                return NotFound();
            }

            ContentFingerprint incomingFp = BuildFingerprint(request.Content ?? string.Empty);
            ContentFingerprint beforeFp = BuildFingerprint(before.Content ?? string.Empty);
            _logger.LogDebug(
                "PUT_BEGIN TraceId={TraceId} PageId={PageId} JsonLength={JsonLength} TextLength={TextLength} Hash={Hash}",
                traceId,
                pageId,
                incomingFp.JsonLength,
                incomingFp.TextLength,
                incomingFp.Hash);
            _logger.LogDebug(
                "DB_BEFORE TraceId={TraceId} PageId={PageId} JsonLength={JsonLength} TextLength={TextLength} Hash={Hash}",
                traceId,
                pageId,
                beforeFp.JsonLength,
                beforeFp.TextLength,
                beforeFp.Hash);

            PageRecord? page = await _pages.UpdateAsync(pageId, userId, update, ct);
            if (page is null)
            {
                return NotFound();
            }

            bool contentChanged = !string.Equals(before.Content ?? string.Empty, page.Content ?? string.Empty, StringComparison.Ordinal);
            bool titleChanged = !string.Equals(before.Title ?? string.Empty, page.Title ?? string.Empty, StringComparison.Ordinal);
            if (!contentChanged && !titleChanged)
            {
                PageDto unchangedDto = new(
                    page.Id,
                    page.DocumentId,
                    page.SectionId,
                    page.Title,
                    page.Content ?? string.Empty,
                    page.OrderIndex,
                    page.CreatedAt,
                    page.UpdatedAt);

                return Ok(unchangedDto);
            }

            await _searchIndex.UpsertPageAsync(page, ct);
            await _versionHistory.CreateCheckpointIfDueAsync(
                userId,
                page,
                page.Content ?? string.Empty,
                TimeSpan.FromSeconds(60),
                ct);
            await _projectWordCounts.RefreshForSectionAsync(page.SectionId, ct);
            await _projectGoals.TrackPageDeltaAsync(
                before,
                page,
                $"page:update:{page.Id}:{page.UpdatedAt.UtcTicks}",
                ct);
            await MirrorSectionPagesToSceneContentAsync(page.SectionId, ct);

            ContentFingerprint afterFp = BuildFingerprint(page.Content ?? string.Empty);
            _logger.LogDebug(
                "DB_AFTER TraceId={TraceId} PageId={PageId} JsonLength={JsonLength} TextLength={TextLength} Hash={Hash}",
                traceId,
                pageId,
                afterFp.JsonLength,
                afterFp.TextLength,
                afterFp.Hash);

            PageDto dto = new(
                page.Id,
                page.DocumentId,
                page.SectionId,
                page.Title,
                page.Content ?? string.Empty,
                page.OrderIndex,
                page.CreatedAt,
                page.UpdatedAt);

            return Ok(dto);
        }

        private readonly record struct ContentFingerprint(int JsonLength, int TextLength, string Hash);

        private static ContentFingerprint BuildFingerprint(string content)
        {
            string value = content ?? string.Empty;
            int jsonLength = value.Length;
            int textLength = StripHtml(value).Length;
            string hash = ComputeShortHash(value);
            return new ContentFingerprint(jsonLength, textLength, hash);
        }

        private static string StripHtml(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return string.Empty;
            }

            return System.Text.RegularExpressions.Regex.Replace(input, "<.*?>", string.Empty);
        }

        private static string ComputeShortHash(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "0";
            }

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(value);
            byte[] hash = System.Security.Cryptography.SHA256.HashData(bytes);
            return Convert.ToHexString(hash.AsSpan(0, 4));
        }

        [HttpDelete("pages/{pageId:guid}")]
        public async Task<IActionResult> DeletePage(Guid pageId, CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string userId = _userIdResolver.ResolveUserId(User);
            PageRecord? existing = await _pages.GetAsync(pageId, userId, ct);
            bool removed = await _pages.DeleteAsync(pageId, userId, ct);
            if (removed)
            {
                await _searchIndex.DeleteByEntityAsync(SearchEntityTypes.Page, pageId, ct);
                await _searchIndex.DeleteByEntityAsync(SearchEntityTypes.Note, pageId, ct);
                if (existing is not null)
                {
                    await _projectWordCounts.RefreshForSectionAsync(existing.SectionId, ct);
                    await _projectGoals.TrackPageDeltaAsync(
                        existing,
                        null,
                        $"page:delete:{existing.Id}:{existing.UpdatedAt.UtcTicks}",
                        ct);
                    await MirrorSectionPagesToSceneContentAsync(existing.SectionId, ct);
                }
            }
            return removed ? NoContent() : NotFound();
        }

        [HttpPost("pages/{pageId:guid}/move")]
        public async Task<ActionResult<PageDto>> MovePage(
            Guid pageId,
            [FromBody] PageMoveRequest request,
            CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? targetSection = await _sections.GetAsync(request.TargetSectionId, userId, ct);
            if (targetSection is null)
            {
                return NotFound();
            }

            PageRecord? existing = await _pages.GetAsync(pageId, userId, ct);
            if (existing is null)
            {
                return NotFound();
            }

            if (existing.DocumentId != targetSection.DocumentId)
            {
                return BadRequest(new { message = "Target section must belong to the same document." });
            }

            Guid sourceSectionId = existing.SectionId;
            PageRecord? moved = await _pages.MoveAsync(pageId, userId, request.TargetSectionId, request.TargetOrderIndex, ct);
            if (moved is null)
            {
                return NotFound();
            }

            await _searchIndex.UpsertPageAsync(moved, ct);
            await _projectWordCounts.RefreshForSectionAsync(sourceSectionId, ct);
            if (sourceSectionId != moved.SectionId)
            {
                await _projectWordCounts.RefreshForSectionAsync(moved.SectionId, ct);
            }
            await MirrorSectionPagesToSceneContentAsync(sourceSectionId, ct);
            if (sourceSectionId != moved.SectionId)
            {
                await MirrorSectionPagesToSceneContentAsync(moved.SectionId, ct);
            }

            PageDto dto = new(
                moved.Id,
                moved.DocumentId,
                moved.SectionId,
                moved.Title,
                moved.Content,
                moved.OrderIndex,
                moved.CreatedAt,
                moved.UpdatedAt);

            return Ok(dto);
        }

        [HttpGet("pages/{pageId:guid}/notes")]
        public async Task<ActionResult<PageNotesDto>> GetPageNotes(Guid pageId, CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string userId = _userIdResolver.ResolveUserId(User);
            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageNoteRecord? notes = await _dbContext.PageNotes
                .FindAsync(new object?[] { pageId }, ct);

            if (notes is null)
            {
                return Ok(new PageNotesDto(pageId, string.Empty, DateTimeOffset.UtcNow));
            }

            return Ok(new PageNotesDto(notes.PageId, notes.Notes, notes.UpdatedAt));
        }

        [HttpPut("pages/{pageId:guid}/notes")]
        public async Task<ActionResult<PageNotesDto>> UpdatePageNotes(
            Guid pageId,
            [FromBody] PageNotesDto request,
            CancellationToken ct)
        {
            AddLegacyApiHeaders();
            string userId = _userIdResolver.ResolveUserId(User);
            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            string notesText = request.Notes ?? string.Empty;
            PageNoteRecord? notes = await _dbContext.PageNotes
                .FindAsync(new object?[] { pageId }, ct);

            if (notes is null)
            {
                notes = new PageNoteRecord
                {
                    PageId = pageId,
                    Notes = notesText,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.PageNotes.Add(notes);
            }
            else
            {
                notes.Notes = notesText;
                notes.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            await _searchIndex.UpsertPageNotesAsync(page, notes, ct);
            return Ok(new PageNotesDto(pageId, notes.Notes, notes.UpdatedAt));
        }

        private void AddLegacyApiHeaders()
        {
            Response.Headers["Deprecation"] = "true";
            Response.Headers["Link"] = "</api/projects/{projectId}/scenes/{sceneNodeId}/content>; rel=\"successor-version\"";
        }

        private async Task MirrorSectionPagesToSceneContentAsync(Guid sectionId, CancellationToken ct)
        {
            Guid[] sceneNodeIds = await _dbContext.ProjectNodes
                .Where(node => node.NodeType == ProjectNodeType.Scene && node.LinkedSectionId == sectionId)
                .Select(node => node.Id)
                .ToArrayAsync(ct);
            if (sceneNodeIds.Length == 0)
            {
                return;
            }

            SectionRecord? section = await _dbContext.Sections
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == sectionId, ct);

            string combinedContent = string.Join(
                "\n\n",
                (await _dbContext.Pages
                    .AsNoTracking()
                    .Where(page => page.SectionId == sectionId)
                    .OrderBy(page => page.OrderIndex)
                    .ThenBy(page => page.Id)
                    .Select(page => page.Content)
                    .ToListAsync(ct))
                    .Select(content => content ?? string.Empty));

            Dictionary<Guid, SceneContentRecord> existing = await _dbContext.SceneContents
                .Where(item => sceneNodeIds.Contains(item.SceneNodeId))
                .ToDictionaryAsync(item => item.SceneNodeId, ct);

            DateTimeOffset now = DateTimeOffset.UtcNow;
            foreach (Guid sceneNodeId in sceneNodeIds)
            {
                if (!existing.TryGetValue(sceneNodeId, out SceneContentRecord? sceneContent))
                {
                    sceneContent = new SceneContentRecord
                    {
                        SceneNodeId = sceneNodeId
                    };
                    _dbContext.SceneContents.Add(sceneContent);
                }

                sceneContent.ContentJson = combinedContent;
                sceneContent.LanguageCode = section?.LanguageCode;
                sceneContent.UpdatedAtUtc = now;
            }

            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
