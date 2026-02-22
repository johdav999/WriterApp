using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public SearchController(ISearchIndexService searchIndex, IUserIdResolver userIdResolver)
        {
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SearchResultDto>>> Search(
            [FromQuery] string? q,
            [FromQuery] bool includeMeta = true,
            [FromQuery] int limit = 50,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Ok(Array.Empty<SearchResultDto>());
            }

            string userId = _userIdResolver.ResolveUserId(User);
            IReadOnlyList<SearchResultDto> results = await _searchIndex.SearchAsync(userId, q, includeMeta, limit, ct);
            return Ok(results);
        }
    }
}
