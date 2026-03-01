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
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.AI;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/ai/presets")]
    [Authorize]
    public sealed class AiPresetsController : ControllerBase
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;

        public AiPresetsController(AppDbContext dbContext, IUserIdResolver userIdResolver)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<PromptPresetDto>>> List(
            [FromQuery] Guid? projectId,
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

            IQueryable<PromptPresetRecord> query = _dbContext.PromptPresets
                .AsNoTracking()
                .Where(preset => preset.OwnerUserId == userId);
            if (projectId.HasValue && projectId.Value != Guid.Empty)
            {
                query = query.Where(preset => preset.ProjectId == projectId.Value || preset.ProjectId == null);
            }

            List<PromptPresetDto> presets = await query
                .OrderBy(preset => preset.Category)
                .ThenBy(preset => preset.Name)
                .Select(preset => Map(preset))
                .ToListAsync(ct);

            return Ok(presets);
        }

        [HttpPost]
        public async Task<ActionResult<PromptPresetDto>> Create([FromBody] UpsertPromptPresetRequest request, CancellationToken ct)
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

            if (!TryValidateRequest(request, out string? error))
            {
                return BadRequest(new { message = error });
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            PromptPresetRecord record = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                ProjectId = request.ProjectId,
                Name = request.Name.Trim(),
                Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
                Kind = request.Kind.Trim().ToLowerInvariant(),
                BuiltinActionId = string.IsNullOrWhiteSpace(request.BuiltinActionId) ? null : request.BuiltinActionId.Trim(),
                TemplateText = string.IsNullOrWhiteSpace(request.TemplateText) ? null : request.TemplateText,
                ParametersJson = SerializeParameters(request.Parameters),
                CreatedUtc = now,
                UpdatedUtc = now
            };

            _dbContext.PromptPresets.Add(record);
            await _dbContext.SaveChangesAsync(ct);
            return Ok(Map(record));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<PromptPresetDto>> Update(Guid id, [FromBody] UpsertPromptPresetRequest request, CancellationToken ct)
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { message = "id is required." });
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

            PromptPresetRecord? record = await _dbContext.PromptPresets
                .FirstOrDefaultAsync(preset => preset.Id == id && preset.OwnerUserId == userId, ct);
            if (record is null)
            {
                return NotFound();
            }

            if (!TryValidateRequest(request, out string? error))
            {
                return BadRequest(new { message = error });
            }

            record.ProjectId = request.ProjectId;
            record.Name = request.Name.Trim();
            record.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
            record.Kind = request.Kind.Trim().ToLowerInvariant();
            record.BuiltinActionId = string.IsNullOrWhiteSpace(request.BuiltinActionId) ? null : request.BuiltinActionId.Trim();
            record.TemplateText = string.IsNullOrWhiteSpace(request.TemplateText) ? null : request.TemplateText;
            record.ParametersJson = SerializeParameters(request.Parameters);
            record.UpdatedUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            return Ok(Map(record));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (id == Guid.Empty)
            {
                return BadRequest(new { message = "id is required." });
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

            PromptPresetRecord? record = await _dbContext.PromptPresets
                .FirstOrDefaultAsync(preset => preset.Id == id && preset.OwnerUserId == userId, ct);
            if (record is null)
            {
                return NotFound();
            }

            _dbContext.PromptPresets.Remove(record);
            await _dbContext.SaveChangesAsync(ct);
            return NoContent();
        }

        private static PromptPresetDto Map(PromptPresetRecord record)
        {
            return new PromptPresetDto(
                record.Id,
                record.ProjectId,
                record.Name,
                record.Category,
                record.Kind,
                record.BuiltinActionId,
                record.TemplateText,
                DeserializeParameters(record.ParametersJson),
                record.CreatedUtc,
                record.UpdatedUtc);
        }

        private static bool TryValidateRequest(UpsertPromptPresetRequest request, out string? error)
        {
            error = null;
            if (request is null)
            {
                error = "Request is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                error = "Name is required.";
                return false;
            }

            string kind = request.Kind?.Trim().ToLowerInvariant() ?? string.Empty;
            if (kind is not ("builtin" or "custom"))
            {
                error = "Kind must be 'builtin' or 'custom'.";
                return false;
            }

            if (kind == "builtin" && string.IsNullOrWhiteSpace(request.BuiltinActionId))
            {
                error = "BuiltinActionId is required for builtin presets.";
                return false;
            }

            if (kind == "custom" && string.IsNullOrWhiteSpace(request.TemplateText))
            {
                error = "TemplateText is required for custom presets.";
                return false;
            }

            return true;
        }

        private static string SerializeParameters(Dictionary<string, object?>? parameters)
        {
            Dictionary<string, object?> normalized = parameters is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(parameters);
            return JsonSerializer.Serialize(normalized, JsonOptions);
        }

        private static Dictionary<string, object?> DeserializeParameters(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new Dictionary<string, object?>();
            }

            try
            {
                Dictionary<string, object?>? result = JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
                return result ?? new Dictionary<string, object?>();
            }
            catch (JsonException)
            {
                return new Dictionary<string, object?>();
            }
        }
    }

    public sealed record PromptPresetDto(
        Guid Id,
        Guid? ProjectId,
        string Name,
        string? Category,
        string Kind,
        string? BuiltinActionId,
        string? TemplateText,
        Dictionary<string, object?> Parameters,
        DateTimeOffset CreatedUtc,
        DateTimeOffset UpdatedUtc);

    public sealed record UpsertPromptPresetRequest(
        Guid? ProjectId,
        string Name,
        string? Category,
        string Kind,
        string? BuiltinActionId,
        string? TemplateText,
        Dictionary<string, object?>? Parameters);
}
