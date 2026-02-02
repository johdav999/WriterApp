using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public sealed class SectionSceneCardsController : ControllerBase
    {
        private readonly ISectionRepository _sections;
        private readonly IUserIdResolver _userIdResolver;
        private readonly AppDbContext _dbContext;
        private readonly ISearchIndexService _searchIndex;

        public SectionSceneCardsController(
            ISectionRepository sections,
            IUserIdResolver userIdResolver,
            AppDbContext dbContext,
            ISearchIndexService searchIndex)
        {
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
        }

        [HttpGet("sections/{sectionId:guid}/scene-card")]
        public async Task<ActionResult<SectionSceneCardDto>> GetSceneCard(Guid sectionId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? section = await _sections.GetAsync(sectionId, userId, ct);
            if (section is null)
            {
                return NotFound();
            }

            SectionSceneCardRecord? card = await _dbContext.SectionSceneCards
                .FindAsync(new object?[] { sectionId }, ct);

            if (card is null)
            {
                return Ok(new SectionSceneCardDto(
                    sectionId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    DateTimeOffset.UtcNow));
            }

            return Ok(new SectionSceneCardDto(
                card.SectionId,
                card.NarrativePurpose ?? string.Empty,
                card.EmotionalBeat ?? string.Empty,
                card.KeyEvents ?? string.Empty,
                card.OpenQuestions ?? string.Empty,
                card.UpdatedUtc));
        }

        [HttpPut("sections/{sectionId:guid}/scene-card")]
        public async Task<ActionResult<SectionSceneCardDto>> UpdateSceneCard(
            Guid sectionId,
            [FromBody] SectionSceneCardUpdateRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? section = await _sections.GetAsync(sectionId, userId, ct);
            if (section is null)
            {
                return NotFound();
            }

            SectionSceneCardRecord? card = await _dbContext.SectionSceneCards
                .FindAsync(new object?[] { sectionId }, ct);

            if (card is null)
            {
                card = new SectionSceneCardRecord
                {
                    SectionId = sectionId
                };
                _dbContext.SectionSceneCards.Add(card);
            }

            card.NarrativePurpose = request.NarrativePurpose ?? string.Empty;
            card.EmotionalBeat = request.EmotionalBeat ?? string.Empty;
            card.KeyEvents = request.KeyEvents ?? string.Empty;
            card.OpenQuestions = request.OpenQuestions ?? string.Empty;
            card.UpdatedUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await _searchIndex.UpsertSceneCardAsync(section, card, ct);

            return Ok(new SectionSceneCardDto(
                card.SectionId,
                card.NarrativePurpose ?? string.Empty,
                card.EmotionalBeat ?? string.Empty,
                card.KeyEvents ?? string.Empty,
                card.OpenQuestions ?? string.Empty,
                card.UpdatedUtc));
        }
    }
}
