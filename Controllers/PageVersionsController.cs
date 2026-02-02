using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.Documents;
using WriterApp.Application.State;
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
        private readonly IPageVersionService _pageVersions;
        private readonly ISearchIndexService _searchIndex;

        public PageVersionsController(
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            IPageVersionService pageVersions,
            ISearchIndexService searchIndex)
        {
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _pageVersions = pageVersions ?? throw new ArgumentNullException(nameof(pageVersions));
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

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            IReadOnlyList<PageVersionRecord> versions = await _pageVersions.ListVersionsAsync(userId, pageId, ct);
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

            PageVersionRecord? version = await _pageVersions.GetVersionAsync(userId, versionId, ct);
            if (version is null)
            {
                return NotFound();
            }

            string content = _pageVersions.DecompressContent(version);
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

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageVersionRecord? version = await _pageVersions.GetVersionAsync(userId, versionId, ct);
            if (version is null || version.PageId != pageId)
            {
                return NotFound();
            }

            await _pageVersions.CreateSnapshotAsync(
                userId,
                page,
                page.Content ?? string.Empty,
                "pre-restore",
                allowDuplicate: true,
                ct);

            string restoredContent = _pageVersions.DecompressContent(version);
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
        public async Task<ActionResult<PageVersionDiffDto>> GetDiff(
            Guid pageId,
            [FromQuery] Guid fromVersionId,
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

            PageRecord? page = await _pages.GetAsync(pageId, userId, ct);
            if (page is null)
            {
                return NotFound();
            }

            PageVersionRecord? version = await _pageVersions.GetVersionAsync(userId, fromVersionId, ct);
            if (version is null || version.PageId != pageId)
            {
                return NotFound();
            }

            string fromContent = _pageVersions.DecompressContent(version);
            string fromText = PlainTextMapper.ToPlainText(fromContent);
            string toText = PlainTextMapper.ToPlainText(page.Content ?? string.Empty);

            return Ok(new PageVersionDiffDto(pageId, fromVersionId, fromText, toText));
        }
    }
}
