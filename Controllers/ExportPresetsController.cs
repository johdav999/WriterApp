using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Exporting;
using WriterApp.Application.Security;
using WriterApp.Data.Exporting;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/export/presets")]
    [Authorize]
    public sealed class ExportPresetsController : ControllerBase
    {
        private readonly IExportPresetService _presetService;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ILogger<ExportPresetsController> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public ExportPresetsController(
            IExportPresetService presetService,
            IUserIdResolver userIdResolver,
            ILogger<ExportPresetsController> logger)
        {
            _presetService = presetService ?? throw new ArgumentNullException(nameof(presetService));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ExportPresetDto>>> ListPresets(CancellationToken ct)
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

            IReadOnlyList<ExportPreset> presets = await _presetService.ListAsync(userId, ct);
            List<ExportPresetDto> result = presets.Select(MapToDto).ToList();
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ExportPresetDto>> CreatePreset(
            [FromBody] ExportPresetCreateRequest request,
            CancellationToken ct)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Preset name is required." });
            }

            if (request.Settings is null)
            {
                return BadRequest(new { message = "Preset settings are required." });
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

            ExportPreset preset = await _presetService.CreateAsync(userId, request, ct);
            return Ok(MapToDto(preset));
        }

        [HttpPut("{presetId:guid}")]
        public async Task<ActionResult<ExportPresetDto>> UpdatePreset(
            Guid presetId,
            [FromBody] ExportPresetUpdateRequest request,
            CancellationToken ct)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "Preset name is required." });
            }

            if (request.Settings is null)
            {
                return BadRequest(new { message = "Preset settings are required." });
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

            ExportPreset? preset = await _presetService.UpdateAsync(userId, presetId, request, ct);
            if (preset is null)
            {
                return NotFound();
            }

            return Ok(MapToDto(preset));
        }

        [HttpDelete("{presetId:guid}")]
        public async Task<IActionResult> DeletePreset(Guid presetId, CancellationToken ct)
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

            bool removed = await _presetService.DeleteAsync(userId, presetId, ct);
            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }

        private ExportPresetDto MapToDto(ExportPreset preset)
        {
            ExportPresetSettingsDto settings = ParseSettings(preset);
            return new ExportPresetDto(
                preset.Id,
                preset.Name,
                preset.IsGlobalDefault,
                settings,
                preset.CreatedAt,
                preset.UpdatedAt);
        }

        private ExportPresetSettingsDto ParseSettings(ExportPreset preset)
        {
            try
            {
                ExportPresetSettingsDto? settings = JsonSerializer.Deserialize<ExportPresetSettingsDto>(
                    preset.SettingsJson,
                    JsonOptions);
                return settings ?? EmptySettings();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse export preset settings. PresetId={PresetId}", preset.Id);
                return EmptySettings();
            }
        }

        private static ExportPresetSettingsDto EmptySettings()
        {
            return new ExportPresetSettingsDto(
                "html",
                null,
                "document",
                false,
                0,
                false,
                false,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                null,
                null,
                null);
        }
    }
}
