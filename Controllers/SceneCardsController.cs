using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/scenes/{sceneNodeId:guid}/scene-card")]
    [Authorize]
    public sealed class SceneCardsController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;

        public SceneCardsController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet]
        public async Task<ActionResult<SceneCardDto>> Get(Guid sceneNodeId, CancellationToken ct)
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

            if (!await IsOwnedSceneAsync(sceneNodeId, userId, ct))
            {
                return NotFound();
            }

            SceneCardRecord? card = await _dbContext.SceneCards
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId, ct);
            if (card is null)
            {
                return Ok(new SceneCardDto(
                    sceneNodeId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    DateTimeOffset.UtcNow,
                    null,
                    null,
                    null,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<SceneCardReferenceDto>()));
            }

            return Ok(ToDto(card));
        }

        [HttpPut]
        public async Task<ActionResult<SceneCardDto>> Put(
            Guid sceneNodeId,
            [FromBody] SceneCardUpdateRequest request,
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

            if (!await IsOwnedSceneAsync(sceneNodeId, userId, ct))
            {
                return NotFound();
            }

            SceneCardRecord? card = await _dbContext.SceneCards
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId, ct);
            if (card is null)
            {
                card = new SceneCardRecord
                {
                    SceneNodeId = sceneNodeId
                };
                _dbContext.SceneCards.Add(card);
            }

            card.NarrativePurpose = request.NarrativePurpose ?? string.Empty;
            card.EmotionalBeat = request.EmotionalBeat ?? string.Empty;
            card.KeyEvents = request.KeyEvents ?? string.Empty;
            card.OpenQuestions = request.OpenQuestions ?? string.Empty;
            card.PovCharacterId = Normalize(request.PovCharacterId);
            card.PlaceId = Normalize(request.PlaceId);
            card.TimelineEventId = Normalize(request.TimelineEventId);
            card.TimeRef = Normalize(request.TimeRef);
            card.TagsJson = JsonSerializer.Serialize(NormalizeTags(request.Tags), JsonOptions);
            card.ReferencesJson = JsonSerializer.Serialize(NormalizeReferences(request.References), JsonOptions);
            card.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            return Ok(ToDto(card));
        }

        private static SceneCardDto ToDto(SceneCardRecord card)
        {
            return new SceneCardDto(
                card.SceneNodeId,
                card.NarrativePurpose ?? string.Empty,
                card.EmotionalBeat ?? string.Empty,
                card.KeyEvents ?? string.Empty,
                card.OpenQuestions ?? string.Empty,
                card.UpdatedAtUtc,
                card.PovCharacterId,
                card.PlaceId,
                card.TimelineEventId,
                card.TimeRef,
                DeserializeTags(card.TagsJson),
                DeserializeReferences(card.ReferencesJson));
        }

        private async Task<bool> IsOwnedSceneAsync(Guid sceneNodeId, string userId, CancellationToken ct)
        {
            return await _dbContext.ProjectNodes
                .Join(
                    _dbContext.Projects,
                    node => node.ProjectId,
                    project => project.Id,
                    (node, project) => new { node, project })
                .AnyAsync(pair =>
                    pair.project.OwnerUserId == userId
                    && pair.node.Id == sceneNodeId
                    && pair.node.NodeType == ProjectNodeType.Scene,
                    ct);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
        {
            if (tags is null)
            {
                return new List<string>();
            }

            return tags
                .Select(item => item?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<SceneCardReferenceDto> NormalizeReferences(IReadOnlyList<SceneCardReferenceDto>? references)
        {
            if (references is null)
            {
                return new List<SceneCardReferenceDto>();
            }

            List<SceneCardReferenceDto> normalized = new();
            foreach (SceneCardReferenceDto reference in references)
            {
                string kind = reference.Kind?.Trim() ?? string.Empty;
                string targetId = reference.TargetId?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(targetId))
                {
                    continue;
                }

                normalized.Add(new SceneCardReferenceDto(kind, targetId, Normalize(reference.Note)));
            }

            return normalized;
        }

        private static IReadOnlyList<string> DeserializeTags(string? tagsJson)
        {
            if (string.IsNullOrWhiteSpace(tagsJson))
            {
                return Array.Empty<string>();
            }

            try
            {
                List<string>? parsed = JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions);
                return parsed ?? new List<string>();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }

        private static IReadOnlyList<SceneCardReferenceDto> DeserializeReferences(string? referencesJson)
        {
            if (string.IsNullOrWhiteSpace(referencesJson))
            {
                return Array.Empty<SceneCardReferenceDto>();
            }

            try
            {
                List<SceneCardReferenceDto>? parsed =
                    JsonSerializer.Deserialize<List<SceneCardReferenceDto>>(referencesJson, JsonOptions);
                return parsed ?? new List<SceneCardReferenceDto>();
            }
            catch (JsonException)
            {
                return Array.Empty<SceneCardReferenceDto>();
            }
        }
    }
}
