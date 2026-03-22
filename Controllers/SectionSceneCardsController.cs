using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
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
        private const int MaxNarrativeIntentLength = 1000;
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Idea",
            "Draft",
            "Revised",
            "Final"
        };
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
            AddLegacyApiHeaders();
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
                SceneCardRecord? sceneCard = await FindAnySceneCardBySectionAsync(sectionId, ct);
                if (sceneCard is not null)
                {
                    (string? sceneNarrativeRole, string? sceneNarrativeIntent) = ResolveNarrativeFields(
                        sceneCard.NarrativeRole,
                        sceneCard.NarrativeIntent,
                        sceneCard.NarrativePurpose);
                    return Ok(new SectionSceneCardDto(
                        sectionId,
                        SceneNarrativeRoleCatalog.ToLegacyPurpose(sceneNarrativeRole, sceneNarrativeIntent) ?? string.Empty,
                        sceneCard.EmotionalBeat ?? string.Empty,
                        sceneCard.KeyEvents ?? string.Empty,
                        sceneCard.OpenQuestions ?? string.Empty,
                        sceneCard.UpdatedAtUtc,
                        sceneCard.PovCharacterId,
                        sceneCard.PlaceId,
                        sceneCard.TimelineEventId,
                        sceneCard.TimeRef,
                        DeserializeTags(sceneCard.TagsJson),
                        DeserializeReferences(sceneCard.ReferencesJson),
                        NormalizeSceneField(sceneCard.Summary),
                        NormalizeStatus(sceneCard.Status),
                        DeserializeTags(sceneCard.SubplotTagsJson),
                        sceneNarrativeRole,
                        NormalizeSceneField(sceneNarrativeIntent)));
                }

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
                    Array.Empty<SceneCardReferenceDto>(),
                    null,
                    "Draft",
                    Array.Empty<string>(),
                    null,
                    null));
            }

            IReadOnlyList<string> tags = DeserializeTags(card.TagsJson);
            IReadOnlyList<string> subplotTags = DeserializeTags(card.SubplotTagsJson);
            IReadOnlyList<SceneCardReferenceDto> references = DeserializeReferences(card.ReferencesJson);
            (string? cardNarrativeRole, string? cardNarrativeIntent) = ResolveNarrativeFields(
                card.NarrativeRole,
                card.NarrativeIntent,
                card.NarrativePurpose);
            return Ok(new SectionSceneCardDto(
                card.SectionId,
                SceneNarrativeRoleCatalog.ToLegacyPurpose(cardNarrativeRole, cardNarrativeIntent) ?? string.Empty,
                NormalizeSceneField(card.EmotionalBeat) ?? string.Empty,
                NormalizeSceneField(card.KeyEvents) ?? string.Empty,
                NormalizeSceneField(card.OpenQuestions) ?? string.Empty,
                card.UpdatedUtc,
                NormalizeSceneField(card.PovCharacterId),
                NormalizeSceneField(card.PlaceId),
                NormalizeSceneField(card.TimelineEventId),
                NormalizeSceneField(card.TimeRef),
                tags,
                references,
                NormalizeSceneField(card.Summary),
                NormalizeStatus(card.Status),
                subplotTags,
                cardNarrativeRole,
                NormalizeSceneField(cardNarrativeIntent)));
        }

        [HttpPut("sections/{sectionId:guid}/scene-card")]
        public async Task<ActionResult<SectionSceneCardDto>> UpdateSceneCard(
            Guid sectionId,
            [FromBody] SectionSceneCardUpdateRequest request,
            CancellationToken ct)
        {
            AddLegacyApiHeaders();
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

            List<string> subplotTags = NormalizeTags(request.SubplotTags);
            if (subplotTags.Count > MaxTags)
            {
                return BadRequest(new { message = $"subplotTags max entries is {MaxTags}." });
            }

            if (subplotTags.Any(tag => tag.Length > MaxTagLength))
            {
                return BadRequest(new { message = $"each subplot tag max length is {MaxTagLength}." });
            }

            List<SceneCardReferenceDto> references = NormalizeReferences(request.References);
            string tagsJson = JsonSerializer.Serialize(tags, JsonOptions);
            string subplotTagsJson = JsonSerializer.Serialize(subplotTags, JsonOptions);
            string referencesJson = JsonSerializer.Serialize(references, JsonOptions);
            if ((tagsJson.Length + subplotTagsJson.Length + referencesJson.Length) > MaxJsonPayloadChars)
            {
                return BadRequest(new { message = "Combined tags/subplotTags/references payload too large." });
            }

            SectionSceneCardRecord? card = await _dbContext.SectionSceneCards
                .FindAsync(new object?[] { sectionId }, ct);

            bool undoEnabled = IsUndoEnabled();
            UpdateSceneCardCommand.SceneCardState beforeState = card is null
                ? new UpdateSceneCardCommand.SceneCardState()
                : new UpdateSceneCardCommand.SceneCardState
                {
                    NarrativePurpose = card.NarrativePurpose,
                    NarrativeRole = card.NarrativeRole,
                    NarrativeIntent = card.NarrativeIntent,
                    EmotionalBeat = card.EmotionalBeat,
                    KeyEvents = card.KeyEvents,
                    OpenQuestions = card.OpenQuestions,
                    Summary = card.Summary,
                    Status = NormalizeStatus(card.Status),
                    PovCharacterId = card.PovCharacterId,
                    PlaceId = card.PlaceId,
                    TimelineEventId = card.TimelineEventId,
                    TimeRef = card.TimeRef,
                    TagsJson = card.TagsJson,
                    SubplotTagsJson = card.SubplotTagsJson,
                    ReferencesJson = card.ReferencesJson
                };
            (string? narrativeRole, string? narrativeIntent) = ResolveNarrativeFields(
                request.NarrativeRole,
                request.NarrativeIntent,
                request.NarrativePurpose);
            if (!string.IsNullOrWhiteSpace(narrativeIntent) && narrativeIntent.Length > MaxNarrativeIntentLength)
            {
                return BadRequest(new { message = $"narrativeIntent max length is {MaxNarrativeIntentLength}." });
            }
            UpdateSceneCardCommand.SceneCardState afterState = new()
            {
                NarrativePurpose = SceneNarrativeRoleCatalog.ToLegacyPurpose(narrativeRole, narrativeIntent) ?? string.Empty,
                NarrativeRole = narrativeRole,
                NarrativeIntent = NormalizeSceneField(narrativeIntent),
                EmotionalBeat = NormalizeSceneField(request.EmotionalBeat) ?? string.Empty,
                KeyEvents = NormalizeSceneField(request.KeyEvents) ?? string.Empty,
                OpenQuestions = NormalizeSceneField(request.OpenQuestions) ?? string.Empty,
                Summary = NormalizeSceneField(request.Summary),
                Status = NormalizeStatus(request.Status),
                PovCharacterId = NormalizeSceneField(request.PovCharacterId),
                PlaceId = NormalizeSceneField(request.PlaceId),
                TimelineEventId = NormalizeSceneField(request.TimelineEventId),
                TimeRef = NormalizeSceneField(timeRef),
                TagsJson = tagsJson,
                SubplotTagsJson = subplotTagsJson,
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

                card.NarrativeRole = afterState.NarrativeRole;
                card.NarrativeIntent = afterState.NarrativeIntent;
                card.NarrativePurpose = SceneNarrativeRoleCatalog.ToLegacyPurpose(afterState.NarrativeRole, afterState.NarrativeIntent) ?? string.Empty;
                card.EmotionalBeat = afterState.EmotionalBeat ?? string.Empty;
                card.KeyEvents = afterState.KeyEvents ?? string.Empty;
                card.OpenQuestions = afterState.OpenQuestions ?? string.Empty;
                card.Summary = afterState.Summary;
                card.Status = NormalizeStatus(afterState.Status);
                card.PovCharacterId = afterState.PovCharacterId;
                card.PlaceId = afterState.PlaceId;
                card.TimelineEventId = afterState.TimelineEventId;
                card.TimeRef = afterState.TimeRef;
                card.TagsJson = afterState.TagsJson;
                card.SubplotTagsJson = afterState.SubplotTagsJson;
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
            await MirrorSectionSceneCardToScenesAsync(sectionId, updatedCard, ct);
            await _searchIndex.UpsertSceneCardAsync(section, updatedCard, ct);
            IReadOnlyList<string> updatedTags = DeserializeTags(updatedCard.TagsJson);
            IReadOnlyList<string> updatedSubplotTags = DeserializeTags(updatedCard.SubplotTagsJson);
            IReadOnlyList<SceneCardReferenceDto> updatedReferences = DeserializeReferences(updatedCard.ReferencesJson);
            (string? updatedNarrativeRole, string? updatedNarrativeIntent) = ResolveNarrativeFields(
                updatedCard.NarrativeRole,
                updatedCard.NarrativeIntent,
                updatedCard.NarrativePurpose);

            return Ok(new SectionSceneCardDto(
                updatedCard.SectionId,
                SceneNarrativeRoleCatalog.ToLegacyPurpose(updatedNarrativeRole, updatedNarrativeIntent) ?? string.Empty,
                updatedCard.EmotionalBeat ?? string.Empty,
                updatedCard.KeyEvents ?? string.Empty,
                updatedCard.OpenQuestions ?? string.Empty,
                updatedCard.UpdatedUtc,
                updatedCard.PovCharacterId,
                updatedCard.PlaceId,
                updatedCard.TimelineEventId,
                updatedCard.TimeRef,
                updatedTags,
                updatedReferences,
                NormalizeSceneField(updatedCard.Summary),
                NormalizeStatus(updatedCard.Status),
                updatedSubplotTags,
                updatedNarrativeRole,
                NormalizeSceneField(updatedNarrativeIntent)));
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? NormalizeSceneField(string? value)
        {
            return SceneCardAiTextNormalizer.NormalizeAiText(Normalize(value));
        }

        private static (string? NarrativeRole, string? NarrativeIntent) ResolveNarrativeFields(
            string? narrativeRole,
            string? narrativeIntent,
            string? legacyNarrativePurpose)
        {
            string? normalizedRole = Normalize(narrativeRole);
            if (!string.IsNullOrWhiteSpace(normalizedRole)
                && !SceneNarrativeRoleCatalog.TryNormalize(normalizedRole, out normalizedRole))
            {
                normalizedRole = null;
            }

            string? normalizedIntent = NormalizeSceneField(narrativeIntent);
            if (!string.IsNullOrWhiteSpace(normalizedIntent) && normalizedIntent.Length > MaxNarrativeIntentLength)
            {
                normalizedIntent = normalizedIntent[..MaxNarrativeIntentLength].Trim();
            }
            if (normalizedRole is null && normalizedIntent is null)
            {
                if (SceneNarrativeRoleCatalog.TryNormalize(legacyNarrativePurpose, out string? legacyRole))
                {
                    normalizedRole = legacyRole;
                }
                else
                {
                    normalizedIntent = NormalizeSceneField(legacyNarrativePurpose);
                }
            }

            return (normalizedRole, normalizedIntent);
        }

        private static string NormalizeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return "Draft";
            }

            string trimmed = status.Trim();
            return AllowedStatuses.Contains(trimmed) ? trimmed : "Draft";
        }

        private static List<string> NormalizeTags(IReadOnlyList<string>? tags)
        {
            if (tags is null || tags.Count == 0)
            {
                return new List<string>();
            }

            return tags
                .Select(tag => NormalizeSceneField(tag) ?? string.Empty)
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

                normalized.Add(new SceneCardReferenceDto(kind, targetId, NormalizeSceneField(reference.Note)));
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
                List<string> parsed = JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOptions) ?? new List<string>();
                return SceneCardAiTextNormalizer.NormalizeAiTextList(parsed);
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

        private void AddLegacyApiHeaders()
        {
            Response.Headers["Deprecation"] = "true";
            Response.Headers["Link"] = "</api/scenes/{sceneNodeId}/scene-card>; rel=\"successor-version\"";
        }

        private static string SerializeState(UpdateSceneCardCommand.SceneCardState state)
        {
            return JsonSerializer.Serialize(state, JsonOptions);
        }

        private async Task<SceneCardRecord?> FindAnySceneCardBySectionAsync(Guid sectionId, CancellationToken ct)
        {
            Guid? sceneNodeId = await _dbContext.ProjectNodes
                .AsNoTracking()
                .Where(node => node.NodeType == ProjectNodeType.Scene && node.LinkedSectionId == sectionId)
                .Select(node => (Guid?)node.Id)
                .FirstOrDefaultAsync(ct);
            if (!sceneNodeId.HasValue)
            {
                return null;
            }

            return await _dbContext.SceneCards
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.SceneNodeId == sceneNodeId.Value, ct);
        }

        private async Task MirrorSectionSceneCardToScenesAsync(
            Guid sectionId,
            SectionSceneCardRecord updatedCard,
            CancellationToken ct)
        {
            Guid[] sceneNodeIds = await _dbContext.ProjectNodes
                .Where(node => node.NodeType == ProjectNodeType.Scene && node.LinkedSectionId == sectionId)
                .Select(node => node.Id)
                .ToArrayAsync(ct);
            if (sceneNodeIds.Length == 0)
            {
                return;
            }

            Dictionary<Guid, SceneCardRecord> existingByScene = await _dbContext.SceneCards
                .Where(item => sceneNodeIds.Contains(item.SceneNodeId))
                .ToDictionaryAsync(item => item.SceneNodeId, ct);

            foreach (Guid sceneNodeId in sceneNodeIds)
            {
                if (!existingByScene.TryGetValue(sceneNodeId, out SceneCardRecord? sceneCard))
                {
                    sceneCard = new SceneCardRecord
                    {
                        SceneNodeId = sceneNodeId
                    };
                    _dbContext.SceneCards.Add(sceneCard);
                }

                sceneCard.NarrativeRole = updatedCard.NarrativeRole;
                sceneCard.NarrativeIntent = updatedCard.NarrativeIntent;
                sceneCard.NarrativePurpose = SceneNarrativeRoleCatalog.ToLegacyPurpose(updatedCard.NarrativeRole, updatedCard.NarrativeIntent);
                sceneCard.EmotionalBeat = updatedCard.EmotionalBeat;
                sceneCard.KeyEvents = updatedCard.KeyEvents;
                sceneCard.OpenQuestions = updatedCard.OpenQuestions;
                sceneCard.Summary = updatedCard.Summary;
                sceneCard.Status = NormalizeStatus(updatedCard.Status);
                sceneCard.PovCharacterId = updatedCard.PovCharacterId;
                sceneCard.PlaceId = updatedCard.PlaceId;
                sceneCard.TimelineEventId = updatedCard.TimelineEventId;
                sceneCard.TimeRef = updatedCard.TimeRef;
                sceneCard.TagsJson = updatedCard.TagsJson;
                sceneCard.SubplotTagsJson = updatedCard.SubplotTagsJson;
                sceneCard.ReferencesJson = updatedCard.ReferencesJson;
                sceneCard.UpdatedAtUtc = updatedCard.UpdatedUtc;
            }
        }

        private async Task EnsureSceneCardSchemaAsync(CancellationToken ct)
        {
            string provider = _dbContext.Database.ProviderName ?? string.Empty;
            if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureSceneCardSchemaSqliteAsync(ct);
            }
            else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureSceneCardSchemaSqlServerAsync(ct);
            }
        }

        private async Task EnsureSceneCardSchemaSqliteAsync(CancellationToken ct)
        {
            await _dbContext.Database.OpenConnectionAsync(ct);
            try
            {
                using (var create = _dbContext.Database.GetDbConnection().CreateCommand())
                {
                    create.CommandText =
                        """
                        CREATE TABLE IF NOT EXISTS SectionSceneCards (
                            SectionId TEXT NOT NULL PRIMARY KEY,
                            NarrativePurpose TEXT NULL,
                            NarrativeRole TEXT NULL,
                            NarrativeIntent TEXT NULL,
                            EmotionalBeat TEXT NULL,
                            KeyEvents TEXT NULL,
                            OpenQuestions TEXT NULL,
                            UpdatedUtc TEXT NOT NULL,
                            FOREIGN KEY (SectionId) REFERENCES Sections (Id) ON DELETE CASCADE
                        );
                        """;
                    await create.ExecuteNonQueryAsync(ct);
                }

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

                List<string> alterStatements = BuildMissingSectionSceneCardColumnStatements(
                    existingColumns,
                    textType: "TEXT NULL");

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

        private async Task EnsureSceneCardSchemaSqlServerAsync(CancellationToken ct)
        {
            await _dbContext.Database.OpenConnectionAsync(ct);
            try
            {
                using (var create = _dbContext.Database.GetDbConnection().CreateCommand())
                {
                    create.CommandText =
                        """
                        IF OBJECT_ID(N'dbo.SectionSceneCards', N'U') IS NULL
                        BEGIN
                            CREATE TABLE [dbo].[SectionSceneCards] (
                                [SectionId] uniqueidentifier NOT NULL PRIMARY KEY,
                                [NarrativePurpose] nvarchar(max) NULL,
                                [NarrativeRole] nvarchar(max) NULL,
                                [NarrativeIntent] nvarchar(max) NULL,
                                [EmotionalBeat] nvarchar(max) NULL,
                                [KeyEvents] nvarchar(max) NULL,
                                [OpenQuestions] nvarchar(max) NULL,
                                [UpdatedUtc] datetimeoffset NOT NULL
                            );
                        END
                        """;
                    await create.ExecuteNonQueryAsync(ct);
                }

                using var command = _dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText =
                    """
                    SELECT [COLUMN_NAME]
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE [TABLE_SCHEMA] = 'dbo' AND [TABLE_NAME] = 'SectionSceneCards';
                    """;
                HashSet<string> existingColumns = new(StringComparer.OrdinalIgnoreCase);

                using (var reader = await command.ExecuteReaderAsync(ct))
                {
                    while (await reader.ReadAsync(ct))
                    {
                        if (reader.FieldCount > 0 && !reader.IsDBNull(0))
                        {
                            existingColumns.Add(reader.GetString(0));
                        }
                    }
                }

                List<string> alterStatements = BuildMissingSectionSceneCardColumnStatements(
                    existingColumns,
                    textType: "NVARCHAR(MAX) NULL");

                foreach (string sql in alterStatements)
                {
                    using var alter = _dbContext.Database.GetDbConnection().CreateCommand();
                    alter.CommandText = sql;
                    try
                    {
                        await alter.ExecuteNonQueryAsync(ct);
                    }
                    catch (SqlException ex) when (ex.Number == 2705)
                    {
                    }
                }
            }
            finally
            {
                await _dbContext.Database.CloseConnectionAsync();
            }
        }

        private static List<string> BuildMissingSectionSceneCardColumnStatements(
            IReadOnlySet<string> existingColumns,
            string textType)
        {
            List<string> alterStatements = new();
            if (!existingColumns.Contains("NarrativeRole"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD NarrativeRole {textType};");
            }

            if (!existingColumns.Contains("NarrativeIntent"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD NarrativeIntent {textType};");
            }

            if (!existingColumns.Contains("PovCharacterId"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD PovCharacterId {textType};");
            }

            if (!existingColumns.Contains("Summary"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD Summary {textType};");
            }

            if (!existingColumns.Contains("Status"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD Status {textType};");
            }

            if (!existingColumns.Contains("PlaceId"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD PlaceId {textType};");
            }

            if (!existingColumns.Contains("TimelineEventId"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD TimelineEventId {textType};");
            }

            if (!existingColumns.Contains("TimeRef"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD TimeRef {textType};");
            }

            if (!existingColumns.Contains("TagsJson"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD TagsJson {textType};");
            }

            if (!existingColumns.Contains("SubplotTagsJson"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD SubplotTagsJson {textType};");
            }

            if (!existingColumns.Contains("ReferencesJson"))
            {
                alterStatements.Add($"ALTER TABLE SectionSceneCards ADD ReferencesJson {textType};");
            }

            return alterStatements;
        }
    }
}
