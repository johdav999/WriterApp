using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/search")]
    [Authorize]
    public sealed class SearchController : ControllerBase
    {
        private readonly ISearchIndexService _searchIndex;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ILogger<SearchController> _logger;
        private readonly IHostEnvironment _hostEnvironment;

        public SearchController(
            ISearchIndexService searchIndex,
            IUserIdResolver userIdResolver,
            ILogger<SearchController> logger,
            IHostEnvironment hostEnvironment)
        {
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> Search(
            [FromQuery] string? q,
            [FromQuery] Guid? projectId,
            [FromQuery] bool includeMeta = true,
            [FromQuery] int limit = 50,
            CancellationToken ct = default)
        {
            string correlationIdHeader = Request.Headers["X-Correlation-ID"].ToString();
            string correlationId = string.IsNullOrWhiteSpace(correlationIdHeader)
                ? HttpContext.TraceIdentifier
                : correlationIdHeader;

            if (!projectId.HasValue || projectId.Value == Guid.Empty)
            {
                return BadRequest("projectId is required.");
            }

            if (string.IsNullOrWhiteSpace(q))
            {
                return Ok(Array.Empty<SearchResultDto>());
            }

            string userId = _userIdResolver.ResolveUserId(User);
            IReadOnlyList<SearchResultDto> results;
            try
            {
                results = await _searchIndex.SearchAsync(userId, projectId.Value, q, includeMeta, limit, correlationId, ct);
            }
            catch (OperationCanceledException)
            {
                return Ok(Array.Empty<SearchResultDto>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Search query failed. CorrelationId={CorrelationId}", correlationId);
                return Problem(
                    title: "Search failed.",
                    detail: $"CorrelationId={correlationId}",
                    statusCode: StatusCodes.Status500InternalServerError);
            }

            if (_hostEnvironment.IsDevelopment())
            {
                int contentCount = 0;
                int metaCount = 0;
                foreach (SearchResultDto result in results)
                {
                    if (string.Equals(result.MatchKind, "content", StringComparison.OrdinalIgnoreCase))
                    {
                        contentCount++;
                    }
                    else
                    {
                        metaCount++;
                    }
                }

                _logger.LogInformation(
                    "Search request: queryString={QueryString} q='{Query}' projectId={ProjectId} includeMeta={IncludeMeta} limit={Limit} results={ResultCount} content={ContentCount} meta={MetaCount}",
                    Request.QueryString.Value,
                    q,
                    projectId.Value,
                    includeMeta,
                    limit,
                    results.Count,
                    contentCount,
                    metaCount);
            }

            return Ok(results);
        }
    }
}
