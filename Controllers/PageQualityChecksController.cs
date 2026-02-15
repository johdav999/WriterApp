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

            QualityCheckRunResultDto result = await _qualityChecks.RunChecksAsync(userId, page, request, ct);
            return Ok(result);
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
