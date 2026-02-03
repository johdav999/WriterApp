using System;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
using WriterApp.Application.Exporting;
using WriterApp.Application.Security;
using WriterApp.Data.Exporting;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/export-settings")]
    [Authorize]
    public sealed class DocumentExportSettingsController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly IExportPresetService _presetService;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ILogger<DocumentExportSettingsController> _logger;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public DocumentExportSettingsController(
            IDocumentRepository documents,
            IExportPresetService presetService,
            IUserIdResolver userIdResolver,
            ILogger<DocumentExportSettingsController> logger)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _presetService = presetService ?? throw new ArgumentNullException(nameof(presetService));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<ProjectExportSettingsDto>> GetSettings(Guid documentId, CancellationToken ct)
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

            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            ProjectExportSettingsDto dto = await BuildSettingsDtoAsync(userId, documentId, ct);
            return Ok(dto);
        }

        [HttpPut]
        public async Task<ActionResult<ProjectExportSettingsDto>> UpdateSettings(
            Guid documentId,
            [FromBody] ProjectExportSettingsUpdateRequest request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Settings payload is required." });
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

            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            if (request.DefaultPresetId.HasValue)
            {
                ExportPresetDto? preset = await TryGetPresetAsync(userId, request.DefaultPresetId.Value, ct);
                if (preset is null)
                {
                    return BadRequest(new { message = "Preset not found for this user." });
                }
            }

            await _presetService.SetProjectSettingsAsync(userId, documentId, request, ct);
            ProjectExportSettingsDto dto = await BuildSettingsDtoAsync(userId, documentId, ct);
            return Ok(dto);
        }

        private async Task<ProjectExportSettingsDto> BuildSettingsDtoAsync(
            string userId,
            Guid documentId,
            CancellationToken ct)
        {
            ProjectExportSettings? settings = await _presetService.GetProjectSettingsAsync(userId, documentId, ct);
            if (settings is null)
            {
                return new ProjectExportSettingsDto(documentId, null, null, null);
            }

            ExportPresetSettingsDto? overrides = null;
            if (!string.IsNullOrWhiteSpace(settings.OverridesJson))
            {
                try
                {
                    overrides = JsonSerializer.Deserialize<ExportPresetSettingsDto>(settings.OverridesJson!, JsonOptions);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse export settings overrides. DocumentId={DocumentId}", documentId);
                }
            }

            return new ProjectExportSettingsDto(
                documentId,
                settings.DefaultPresetId,
                overrides,
                settings.UpdatedAt);
        }

        private async Task<ExportPresetDto?> TryGetPresetAsync(string userId, Guid presetId, CancellationToken ct)
        {
            ExportPreset? preset = await _presetService.GetAsync(userId, presetId, ct);
            if (preset is null)
            {
                return null;
            }

            ExportPresetSettingsDto settings;
            try
            {
                settings = JsonSerializer.Deserialize<ExportPresetSettingsDto>(preset.SettingsJson, JsonOptions)
                    ?? new ExportPresetSettingsDto("html", null, "document", null, null, false, 0, false, false, null, null, null, false, null, null, null, null, null, null);
            }
            catch
            {
                settings = new ExportPresetSettingsDto("html", null, "document", null, null, false, 0, false, false, null, null, null, false, null, null, null, null, null, null);
            }

            return new ExportPresetDto(
                preset.Id,
                preset.Name,
                preset.IsGlobalDefault,
                settings,
                preset.CreatedAt,
                preset.UpdatedAt);
        }
    }
}
