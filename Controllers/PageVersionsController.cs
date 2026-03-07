using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public sealed class PageVersionsController : ControllerBase
    {
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IVersionHistoryService _versionHistory;
        private readonly IVersionHistoryPolicyService _versionHistoryPolicy;
        private readonly IPageVersionDiffService _pageVersionDiffs;
        private readonly ISearchIndexService _searchIndex;

        public PageVersionsController(
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            IVersionHistoryService versionHistory,
            IVersionHistoryPolicyService versionHistoryPolicy,
            IPageVersionDiffService pageVersionDiffs,
            ISearchIndexService searchIndex)
        {
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _versionHistory = versionHistory ?? throw new ArgumentNullException(nameof(versionHistory));
            _versionHistoryPolicy = versionHistoryPolicy ?? throw new ArgumentNullException(nameof(versionHistoryPolicy));
            _pageVersionDiffs = pageVersionDiffs ?? throw new ArgumentNullException(nameof(pageVersionDiffs));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
        }

        [HttpGet("pages/{pageId:guid}/versions")]
        public async Task<ActionResult<IReadOnlyList<PageVersionListItemDto>>> ListVersions(
            Guid pageId,
            CancellationToken ct)
        {
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            VersionHistoryPolicy policy = await _versionHistoryPolicy.GetPolicyAsync(userId);
            if (!policy.Enabled)
            {
                return Ok(Array.Empty<PageVersionListItemDto>());
            }

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            IReadOnlyList<PageVersionRecord> versions = await _versionHistory.ListVersionsAsync(userId, pageId, ct);
            List<PageVersionListItemDto> result = versions
                .Select(version => new PageVersionListItemDto(
                    version.Id,
                    version.PageId,
                    version.CreatedAt,
                    version.Reason,
                    version.WordCount,
                    version.SizeBytes))
                .ToList();

            return Ok(result);
        }

        [HttpGet("page-versions/{versionId:guid}")]
        public async Task<ActionResult<PageVersionDetailDto>> GetVersion(
            Guid versionId,
            CancellationToken ct)
        {
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            VersionHistoryPolicy policy = await _versionHistoryPolicy.GetPolicyAsync(userId);
            if (!policy.Enabled)
            {
                return NotFound();
            }

            PageVersionRecord? version = await _versionHistory.GetVersionAsync(userId, versionId, ct);
            if (version is null)
            {
                return NotFound();
            }

            string content = _versionHistory.DecompressContent(version);
            PageVersionDetailDto dto = new(
                version.Id,
                version.PageId,
                version.DocumentId,
                version.CreatedAt,
                version.Reason,
                content,
                version.WordCount,
                version.SizeBytes);

            return Ok(dto);
        }

        [HttpPost("pages/{pageId:guid}/versions/{versionId:guid}/restore")]
        public async Task<ActionResult<PageDto>> RestoreVersion(
            Guid pageId,
            Guid versionId,
            CancellationToken ct)
        {
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            VersionHistoryPolicy policy = await _versionHistoryPolicy.GetPolicyAsync(userId);
            if (!policy.Enabled || !policy.CanRestoreVersions)
            {
                return StatusCode(403, new { message = "Restore version is not available on the current plan." });
            }

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageVersionRecord? version = await _versionHistory.GetVersionAsync(userId, versionId, ct);
            if (version is null || version.PageId != pageId)
            {
                return NotFound();
            }

            await _versionHistory.CreateCheckpointAsync(
                userId,
                page,
                page.Content ?? string.Empty,
                "pre-restore",
                allowDuplicate: true,
                ct);

            string restoredContent = _versionHistory.DecompressContent(version);
            PageRecord? updated = await _pages.UpdateAsync(pageId, userId, new PageUpdate(null, restoredContent), ct);
            if (updated is null)
            {
                return NotFound();
            }

            await _searchIndex.UpsertPageAsync(updated, ct);

            PageDto dto = new(
                updated.Id,
                updated.DocumentId,
                updated.SectionId,
                updated.Title,
                updated.Content,
                updated.OrderIndex,
                updated.CreatedAt,
                updated.UpdatedAt);

            return Ok(dto);
        }

        [HttpGet("pages/{pageId:guid}/versions/diff")]
        public async Task<ActionResult<PageVersionDiffResultDto>> GetDiff(
            Guid pageId,
            [FromQuery] Guid fromVersionId,
            [FromQuery] Guid? toVersionId,
            [FromQuery] string? granularity,
            [FromQuery] int? maxLines,
            CancellationToken ct)
        {
            if (fromVersionId == Guid.Empty)
            {
                return BadRequest(new { message = "fromVersionId is required." });
            }

            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            VersionHistoryPolicy policy = await _versionHistoryPolicy.GetPolicyAsync(userId);
            if (!policy.Enabled || !policy.CanCompareVersions)
            {
                return StatusCode(403, new { message = "Version compare is not available on the current plan." });
            }

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageVersionRecord? version = await _versionHistory.GetVersionAsync(userId, fromVersionId, ct);
            if (version is null || version.PageId != pageId)
            {
                return NotFound();
            }

            string fromContent = _versionHistory.DecompressContent(version);

            string toContent;
            Guid? resolvedToVersionId = null;
            bool compareToCurrent = true;
            if (toVersionId.HasValue && toVersionId.Value != Guid.Empty)
            {
                PageVersionRecord? toVersion = await _versionHistory.GetVersionAsync(userId, toVersionId.Value, ct);
                if (toVersion is null || toVersion.PageId != pageId)
                {
                    return NotFound();
                }

                toContent = _versionHistory.DecompressContent(toVersion);
                resolvedToVersionId = toVersion.Id;
                compareToCurrent = false;
            }
            else
            {
                toContent = page.Content ?? string.Empty;
            }

            PageVersionDiffResultDto result = _pageVersionDiffs.BuildDiff(
                pageId,
                fromVersionId,
                resolvedToVersionId,
                compareToCurrent,
                fromContent,
                toContent,
                granularity ?? "word",
                maxLines ?? 800);

            return Ok(result);
        }
    }
}
