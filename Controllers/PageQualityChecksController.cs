using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/pages/{pageId:guid}/quality-checks")]
    [Authorize]
    public sealed class PageQualityChecksController : ControllerBase
    {
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IQualityCheckService _qualityChecks;

        public PageQualityChecksController(
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            IQualityCheckService qualityChecks)
        {
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _qualityChecks = qualityChecks ?? throw new ArgumentNullException(nameof(qualityChecks));
        }

        [HttpGet("issues")]
        public async Task<ActionResult<IReadOnlyList<PageQualityIssueDto>>> ListIssues(
            Guid pageId,
            [FromQuery] bool includeDismissed,
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

            IReadOnlyList<PageQualityIssueDto> issues = await _qualityChecks.ListIssuesAsync(
                userId,
                pageId,
                includeDismissed,
                ct);
            return Ok(issues.ToList());
        }

        [HttpPost("run")]
        public async Task<ActionResult<QualityCheckRunResultDto>> Run(
            Guid pageId,
            [FromBody] QualityCheckRunRequest request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
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

            var db=HttpContext.RequestServices.GetService(typeof(WriterApp.Data.AppDbContext)) as WriterApp.Data.AppDbContext;
            try {
                if(request.WebSource is { } source) {
                    if(db is null || source.DocumentId!=page.DocumentId || source.PageId!=page.Id || source.SectionId!=page.SectionId || request.Text?.Length>100_000 || page.Content.Length>100_000 || request.Scope is not ("page" or "selection"))
                        return BadRequest(new { message="Invalid checked quality target." });
                    await new WriterApp.Application.AI.WebAiSourceService(db,userId).Require(source,ct);
                    if(request.Scope=="selection" && (string.IsNullOrWhiteSpace(request.Text) || !WriterApp.Application.State.PlainTextMapper.ToPlainText(page.Content).Contains(request.Text,StringComparison.Ordinal)))
                        return BadRequest(new { message="Quality selection differs from saved writing. Save and select the current text again." });
                }
                QualityCheckRunResultDto result = await _qualityChecks.RunChecksAsync(userId, page, request, ct);
                ct.ThrowIfCancellationRequested();
                if(request.WebSource is { } completed) await new WriterApp.Application.AI.WebAiSourceService(db!,userId).Require(completed,ct);
                return Ok(result with { WebSource=request.WebSource });
            } catch(Exception e) when(e is DocumentSyncException or InvalidDataException or InvalidOperationException) { return Conflict(new { message=e.Message }); }
        }

        [HttpPost("issues/{issueKey}/dismiss")]
        public async Task<IActionResult> Dismiss(
            Guid pageId,
            string issueKey,
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

            await _qualityChecks.DismissIssueAsync(userId, pageId, issueKey, ct);
            return NoContent();
        }

        [HttpDelete("issues/{issueKey}/dismiss")]
        public async Task<IActionResult> Reopen(
            Guid pageId,
            string issueKey,
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

            await _qualityChecks.ReopenIssueAsync(userId, pageId, issueKey, ct);
            return NoContent();
        }
    }
}
