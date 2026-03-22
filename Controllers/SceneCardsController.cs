using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
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
        private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Idea",
            "Draft",
            "Revised",
            "Final"
        };
        private const int MaxNarrativeIntentLength = 1000;
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
            await EnsureSceneCardSchemaAsync(ct);

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
                    Array.Empty<SceneCardReferenceDto>(),
                    null,
                    "Draft",
                    Array.Empty<string>(),
                    null,
                    null));
            }

            return Ok(ToDto(card));
        }

        [HttpPut]
        public async Task<ActionResult<SceneCardDto>> Put(
            Guid sceneNodeId,
            [FromBody] SceneCardUpdateRequest request,
            CancellationToken ct)
        {
            await EnsureSceneCardSchemaAsync(ct);

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

            (string? narrativeRole, string? narrativeIntent) = ResolveNarrativeFields(
                request.NarrativeRole,
                request.NarrativeIntent,
                request.NarrativePurpose);
            if (!string.IsNullOrWhiteSpace(narrativeIntent) && narrativeIntent.Length > MaxNarrativeIntentLength)
            {
                return BadRequest(new { message = $"narrativeIntent max length is {MaxNarrativeIntentLength}." });
            }
            card.NarrativeRole = narrativeRole;
            card.NarrativeIntent = NormalizeSceneField(narrativeIntent);
            card.NarrativePurpose = SceneNarrativeRoleCatalog.ToLegacyPurpose(narrativeRole, narrativeIntent) ?? string.Empty;
            card.EmotionalBeat = NormalizeSceneField(request.EmotionalBeat) ?? string.Empty;
            card.KeyEvents = NormalizeSceneField(request.KeyEvents) ?? string.Empty;
            card.OpenQuestions = NormalizeSceneField(request.OpenQuestions) ?? string.Empty;
            card.Summary = NormalizeSceneField(request.Summary);
            card.Status = NormalizeStatus(request.Status);
            card.PovCharacterId = NormalizeSceneField(request.PovCharacterId);
            card.PlaceId = NormalizeSceneField(request.PlaceId);
            card.TimelineEventId = NormalizeSceneField(request.TimelineEventId);
            card.TimeRef = NormalizeSceneField(request.TimeRef);
            card.TagsJson = JsonSerializer.Serialize(NormalizeTags(request.Tags), JsonOptions);
            card.SubplotTagsJson = JsonSerializer.Serialize(NormalizeTags(request.SubplotTags), JsonOptions);
            card.ReferencesJson = JsonSerializer.Serialize(NormalizeReferences(request.References), JsonOptions);
            card.UpdatedAtUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            return Ok(ToDto(card));
        }

        private static SceneCardDto ToDto(SceneCardRecord card)
        {
            (string? narrativeRole, string? narrativeIntent) = ResolveNarrativeFields(
                card.NarrativeRole,
                card.NarrativeIntent,
                card.NarrativePurpose);
            return new SceneCardDto(
                card.SceneNodeId,
                SceneNarrativeRoleCatalog.ToLegacyPurpose(narrativeRole, narrativeIntent) ?? string.Empty,
                NormalizeSceneField(card.EmotionalBeat) ?? string.Empty,
                NormalizeSceneField(card.KeyEvents) ?? string.Empty,
                NormalizeSceneField(card.OpenQuestions) ?? string.Empty,
                card.UpdatedAtUtc,
                NormalizeSceneField(card.PovCharacterId),
                NormalizeSceneField(card.PlaceId),
                NormalizeSceneField(card.TimelineEventId),
                NormalizeSceneField(card.TimeRef),
                DeserializeTags(card.TagsJson),
                DeserializeReferences(card.ReferencesJson),
                NormalizeSceneField(card.Summary),
                NormalizeStatus(card.Status),
                DeserializeTags(card.SubplotTagsJson),
                narrativeRole,
                NormalizeSceneField(narrativeIntent));
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
            if (tags is null)
            {
                return new List<string>();
            }

            return tags
                .Select(item => NormalizeSceneField(item) ?? string.Empty)
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
                List<SceneCardReferenceDto>? parsed =
                    JsonSerializer.Deserialize<List<SceneCardReferenceDto>>(referencesJson, JsonOptions);
                return parsed ?? new List<SceneCardReferenceDto>();
            }
            catch (JsonException)
            {
                return Array.Empty<SceneCardReferenceDto>();
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
                        CREATE TABLE IF NOT EXISTS SceneCards (
                            SceneNodeId TEXT NOT NULL PRIMARY KEY,
                            NarrativePurpose TEXT NULL,
                            NarrativeRole TEXT NULL,
                            NarrativeIntent TEXT NULL,
                            EmotionalBeat TEXT NULL,
                            KeyEvents TEXT NULL,
                            OpenQuestions TEXT NULL,
                            UpdatedAtUtc TEXT NOT NULL,
                            FOREIGN KEY (SceneNodeId) REFERENCES ProjectNodes (Id) ON DELETE CASCADE
                        );
                        """;
                    await create.ExecuteNonQueryAsync(ct);
                }

                using var command = _dbContext.Database.GetDbConnection().CreateCommand();
                command.CommandText = "PRAGMA table_info('SceneCards');";
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

                List<string> alterStatements = BuildMissingSceneCardColumnStatements(
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
                        IF OBJECT_ID(N'dbo.SceneCards', N'U') IS NULL
                        BEGIN
                            CREATE TABLE [dbo].[SceneCards] (
                                [SceneNodeId] uniqueidentifier NOT NULL PRIMARY KEY,
                                [NarrativePurpose] nvarchar(max) NULL,
                                [NarrativeRole] nvarchar(max) NULL,
                                [NarrativeIntent] nvarchar(max) NULL,
                                [EmotionalBeat] nvarchar(max) NULL,
                                [KeyEvents] nvarchar(max) NULL,
                                [OpenQuestions] nvarchar(max) NULL,
                                [UpdatedAtUtc] datetimeoffset NOT NULL
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
                    WHERE [TABLE_SCHEMA] = 'dbo' AND [TABLE_NAME] = 'SceneCards';
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

                List<string> alterStatements = BuildMissingSceneCardColumnStatements(
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

        private static List<string> BuildMissingSceneCardColumnStatements(
            IReadOnlySet<string> existingColumns,
            string textType)
        {
            List<string> alterStatements = new();
            if (!existingColumns.Contains("NarrativeRole"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD NarrativeRole {textType};");
            }

            if (!existingColumns.Contains("NarrativeIntent"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD NarrativeIntent {textType};");
            }

            if (!existingColumns.Contains("Summary"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD Summary {textType};");
            }

            if (!existingColumns.Contains("Status"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD Status {textType};");
            }

            if (!existingColumns.Contains("PovCharacterId"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD PovCharacterId {textType};");
            }

            if (!existingColumns.Contains("PlaceId"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD PlaceId {textType};");
            }

            if (!existingColumns.Contains("TimelineEventId"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD TimelineEventId {textType};");
            }

            if (!existingColumns.Contains("TimeRef"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD TimeRef {textType};");
            }

            if (!existingColumns.Contains("TagsJson"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD TagsJson {textType};");
            }

            if (!existingColumns.Contains("SubplotTagsJson"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD SubplotTagsJson {textType};");
            }

            if (!existingColumns.Contains("ReferencesJson"))
            {
                alterStatements.Add($"ALTER TABLE SceneCards ADD ReferencesJson {textType};");
            }

            return alterStatements;
        }
    }
}
