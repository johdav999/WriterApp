using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WriterApp.Application.Commands;
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
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private const int MaxTags = 30;
        private const int MaxTagLength = 30;
        private const int MaxTimeRefLength = 120;
        private const int MaxJsonPayloadChars = 8192;
        private readonly ISectionRepository _sections;
        private readonly IUserIdResolver _userIdResolver;
        private readonly AppDbContext _dbContext;
        private readonly ISearchIndexService _searchIndex;
        private readonly IStructureCommandProcessor _structureCommands;
        private readonly IConfiguration _configuration;

        public SectionSceneCardsController(
            ISectionRepository sections,
            IUserIdResolver userIdResolver,
            AppDbContext dbContext,
            ISearchIndexService searchIndex,
            IStructureCommandProcessor structureCommands,
            IConfiguration configuration)
        {
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _structureCommands = structureCommands ?? throw new ArgumentNullException(nameof(structureCommands));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        [HttpGet("sections/{sectionId:guid}/scene-card")]
        public async Task<ActionResult<SectionSceneCardDto>> GetSceneCard(Guid sectionId, CancellationToken ct)
        {
            await EnsureSceneCardSchemaAsync(ct);

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
                    DateTimeOffset.UtcNow,
                    null,
                    null,
                    null,
                    null,
                    Array.Empty<string>(),
                    Array.Empty<SceneCardReferenceDto>()));
            }

            IReadOnlyList<string> tags = DeserializeTags(card.TagsJson);
            IReadOnlyList<SceneCardReferenceDto> references = DeserializeReferences(card.ReferencesJson);
            return Ok(new SectionSceneCardDto(
                card.SectionId,
                card.NarrativePurpose ?? string.Empty,
                card.EmotionalBeat ?? string.Empty,
                card.KeyEvents ?? string.Empty,
                card.OpenQuestions ?? string.Empty,
                card.UpdatedUtc,
                card.PovCharacterId,
                card.PlaceId,
                card.TimelineEventId,
                card.TimeRef,
                tags,
                references));
        }

        [HttpPut("sections/{sectionId:guid}/scene-card")]
        public async Task<ActionResult<SectionSceneCardDto>> UpdateSceneCard(
            Guid sectionId,
            [FromBody] SectionSceneCardUpdateRequest request,
            CancellationToken ct)
        {
            await EnsureSceneCardSchemaAsync(ct);

            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? section = await _sections.GetAsync(sectionId, userId, ct);
            if (section is null)
            {
                return NotFound();
            }

            string? timeRef = Normalize(request.TimeRef);
            if (!string.IsNullOrWhiteSpace(timeRef) && timeRef.Length > MaxTimeRefLength)
            {
                return BadRequest(new { message = $"timeRef max length is {MaxTimeRefLength}." });
            }

            List<string> tags = NormalizeTags(request.Tags);
            if (tags.Count > MaxTags)
            {
                return BadRequest(new { message = $"tags max entries is {MaxTags}." });
            }

            if (tags.Any(tag => tag.Length > MaxTagLength))
            {
                return BadRequest(new { message = $"each tag max length is {MaxTagLength}." });
            }

            List<SceneCardReferenceDto> references = NormalizeReferences(request.References);
            string tagsJson = JsonSerializer.Serialize(tags, JsonOptions);
            string referencesJson = JsonSerializer.Serialize(references, JsonOptions);
            if ((tagsJson.Length + referencesJson.Length) > MaxJsonPayloadChars)
            {
                return BadRequest(new { message = "Combined tags/references payload too large." });
            }

            SectionSceneCardRecord? card = await _dbContext.SectionSceneCards
                .FindAsync(new object?[] { sectionId }, ct);

            bool undoEnabled = IsUndoEnabled();
            UpdateSceneCardCommand.SceneCardState beforeState = card is null
                ? new UpdateSceneCardCommand.SceneCardState()
                : new UpdateSceneCardCommand.SceneCardState
                {
                    NarrativePurpose = card.NarrativePurpose,
                    EmotionalBeat = card.EmotionalBeat,
                    KeyEvents = card.KeyEvents,
                    OpenQuestions = card.OpenQuestions,
                    PovCharacterId = card.PovCharacterId,
                    PlaceId = card.PlaceId,
                    TimelineEventId = card.TimelineEventId,
                    TimeRef = card.TimeRef,
                    TagsJson = card.TagsJson,
                    ReferencesJson = card.ReferencesJson
                };
            UpdateSceneCardCommand.SceneCardState afterState = new()
            {
                NarrativePurpose = request.NarrativePurpose ?? string.Empty,
                EmotionalBeat = request.EmotionalBeat ?? string.Empty,
                KeyEvents = request.KeyEvents ?? string.Empty,
                OpenQuestions = request.OpenQuestions ?? string.Empty,
                PovCharacterId = Normalize(request.PovCharacterId),
                PlaceId = Normalize(request.PlaceId),
                TimelineEventId = Normalize(request.TimelineEventId),
                TimeRef = timeRef,
                TagsJson = tagsJson,
                ReferencesJson = referencesJson
            };
            if (undoEnabled)
            {
                await _structureCommands.ExecuteAsync(
                    new UpdateSceneCardCommand(
                        userId,
                        section.DocumentId,
                        sectionId,
                        SerializeState(beforeState),
                        SerializeState(afterState)),
                    ct);
            }
            else
            {
                if (card is null)
                {
                    card = new SectionSceneCardRecord
                    {
                        SectionId = sectionId
                    };
                    _dbContext.SectionSceneCards.Add(card);
                }

                card.NarrativePurpose = afterState.NarrativePurpose ?? string.Empty;
                card.EmotionalBeat = afterState.EmotionalBeat ?? string.Empty;
                card.KeyEvents = afterState.KeyEvents ?? string.Empty;
                card.OpenQuestions = afterState.OpenQuestions ?? string.Empty;
                card.PovCharacterId = afterState.PovCharacterId;
                card.PlaceId = afterState.PlaceId;
                card.TimelineEventId = afterState.TimelineEventId;
                card.TimeRef = afterState.TimeRef;
                card.TagsJson = afterState.TagsJson;
                card.ReferencesJson = afterState.ReferencesJson;
                card.UpdatedUtc = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
            }

            SectionSceneCardRecord? updatedCard = await _dbContext.SectionSceneCards
                .FindAsync(new object?[] { sectionId }, ct);
            if (updatedCard is null)
            {
                return NotFound();
            }
            await _searchIndex.UpsertSceneCardAsync(section, updatedCard, ct);
            IReadOnlyList<string> updatedTags = DeserializeTags(updatedCard.TagsJson);
            IReadOnlyList<SceneCardReferenceDto> updatedReferences = DeserializeReferences(updatedCard.ReferencesJson);

            return Ok(new SectionSceneCardDto(
                updatedCard.SectionId,
                updatedCard.NarrativePurpose ?? string.Empty,
                updatedCard.EmotionalBeat ?? string.Empty,
                updatedCard.KeyEvents ?? string.Empty,
                updatedCard.OpenQuestions ?? string.Empty,
                updatedCard.UpdatedUtc,
                updatedCard.PovCharacterId,
                updatedCard.PlaceId,
                updatedCard.TimelineEventId,
                updatedCard.TimeRef,
                updatedTags,
                updatedReferences));
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
        {
            if (tags is null || tags.Count == 0)
            {
                return new List<string>();
            }

            return tags
                .Select(tag => tag?.Trim() ?? string.Empty)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<SceneCardReferenceDto> NormalizeReferences(IReadOnlyList<SceneCardReferenceDto>? references)
        {
            if (references is null || references.Count == 0)
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
                return JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions) ?? new List<string>();
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
                return JsonSerializer.Deserialize<List<SceneCardReferenceDto>>(referencesJson, JsonOptions)
                    ?? new List<SceneCardReferenceDto>();
            }
            catch (JsonException)
            {
                return Array.Empty<SceneCardReferenceDto>();
            }
        }

        private bool IsUndoEnabled()
        {
            return _configuration.GetValue<bool?>("Workflow:OutlineUndoEnabled")
                ?? _configuration.GetValue<bool?>("WriterApp:Workflow:OutlineUndoEnabled")
                ?? false;
        }

        private static string SerializeState(UpdateSceneCardCommand.SceneCardState state)
        {
            return JsonSerializer.Serialize(state, JsonOptions);
        }

        private async Task EnsureSceneCardSchemaAsync(CancellationToken ct)
        {
            string provider = _dbContext.Database.ProviderName ?? string.Empty;
            if (!provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await _dbContext.Database.OpenConnectionAsync(ct);
            try
            {
                using var command = _dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA table_info('SectionSceneCards');";
                HashSet<string> existingColumns = new(StringComparer.OrdinalIgnoreCase);

                using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        if (reader.FieldCount > 1 && !reader.IsDBNull(1))
                        {
                            existingColumns.Add(reader.GetString(1));
                        }
                    }
                }

                List<string> alterStatements = new();
                if (!existingColumns.Contains("PovCharacterId"))
                {
                    alterStatements.Add("ALTER TABLE SectionSceneCards ADD COLUMN PovCharacterId TEXT NULL;");
                }

                if (!existingColumns.Contains("PlaceId"))
                {
                    alterStatements.Add("ALTER TABLE SectionSceneCards ADD COLUMN PlaceId TEXT NULL;");
                }

                if (!existingColumns.Contains("TimelineEventId"))
                {
                    alterStatements.Add("ALTER TABLE SectionSceneCards ADD COLUMN TimelineEventId TEXT NULL;");
                }

                if (!existingColumns.Contains("TimeRef"))
                {
                    alterStatements.Add("ALTER TABLE SectionSceneCards ADD COLUMN TimeRef TEXT NULL;");
                }

                if (!existingColumns.Contains("TagsJson"))
                {
                    alterStatements.Add("ALTER TABLE SectionSceneCards ADD COLUMN TagsJson TEXT NULL;");
                }

                if (!existingColumns.Contains("ReferencesJson"))
                {
                    alterStatements.Add("ALTER TABLE SectionSceneCards ADD COLUMN ReferencesJson TEXT NULL;");
                }

                foreach (string sql in alterStatements)
                {
                    using var alter = _dbContext.Database.GetDbConnection().CreateCommand();
                    alter.CommandText = sql;
                    try
                    {
                        await alter.ExecuteNonQueryAsync(ct);
                    }
                    catch (SqliteException ex) when (ex.SqliteErrorCode == 1 && ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ignore concurrent add attempts.
                    }
                }
            }
            finally
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }
    }
}
