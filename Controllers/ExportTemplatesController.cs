using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Exporting;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Exporting;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/export/templates")]
    [Authorize]
    public sealed class ExportTemplatesController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IExportTemplateSeeder _seeder;
        private readonly ILogger<ExportTemplatesController> _logger;

        public ExportTemplatesController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IExportTemplateSeeder seeder,
            ILogger<ExportTemplatesController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _seeder = seeder ?? throw new ArgumentNullException(nameof(seeder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<ExportTemplateDto>>> ListTemplates(CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            await _seeder.EnsureDefaultsAsync(userId, ct);

            List<ExportTemplate> templates = await _dbContext.ExportTemplates
                .AsNoTracking()
                .Where(template => template.OwnerUserId == userId)
                .OrderBy(template => template.Name)
                .ToListAsync(ct);

            templates = templates
                .OrderBy(template => template.Name)
                .ThenBy(template => template.CreatedAt)
                .ToList();

            List<ExportTemplateDto> result = templates.Select(ToDto).ToList();
            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<ExportTemplateDto>> CreateTemplate(
            [FromBody] ExportTemplateCreateRequest request,
            CancellationToken ct)
        {
            ExportTemplatePresetDefinition? preset = ExportTemplatePresets.GetByKey(request.PresetKey);
            if (preset is not null && string.IsNullOrWhiteSpace(request.Name))
            {
                request = ApplyPreset(request, preset);
            }

            if (!TryValidate(request, out string? error))
            {
                return BadRequest(new { message = error });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            string fontFamily = NormalizeString(request.FontFamily) ?? "Georgia";

            ExportTemplate template = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Name = request.Name!.Trim(),
                PresetKey = NormalizeString(request.PresetKey),
                PageWidthMm = request.PageWidthMm,
                PageHeightMm = request.PageHeightMm,
                MarginTopMm = request.MarginTopMm,
                MarginRightMm = request.MarginRightMm,
                MarginBottomMm = request.MarginBottomMm,
                MarginLeftMm = request.MarginLeftMm,
                FontFamily = fontFamily,
                BodyFontSizePt = request.BodyFontSizePt,
                LineHeight = request.LineHeight,
                ParagraphSpacingPt = request.ParagraphSpacingPt,
                HeaderEnabled = request.HeaderEnabled,
                HeaderLeft = NormalizeString(request.HeaderLeft),
                HeaderCenter = NormalizeString(request.HeaderCenter),
                HeaderRight = NormalizeString(request.HeaderRight),
                FooterEnabled = request.FooterEnabled,
                FooterLeft = NormalizeString(request.FooterLeft),
                FooterCenter = NormalizeString(request.FooterCenter),
                FooterRight = NormalizeString(request.FooterRight),
                PageNumbersEnabled = request.PageNumbersEnabled,
                PageNumberStart = request.PageNumberStart,
                TocEnabled = request.TocEnabled,
                TocDepth = request.TocDepth,
                CreatedAt = now,
                UpdatedAt = now
            };

            _dbContext.ExportTemplates.Add(template);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Created export template {TemplateId} for user {UserId} ({Name}).",
                template.Id,
                userId,
                template.Name);

            return Ok(ToDto(template));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ExportTemplateDto>> UpdateTemplate(
            Guid id,
            [FromBody] ExportTemplateUpdateRequest request,
            CancellationToken ct)
        {
            if (!TryValidate(request, out string? error))
            {
                return BadRequest(new { message = error });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            ExportTemplate? template = await _dbContext.ExportTemplates
                .FirstOrDefaultAsync(item => item.Id == id && item.OwnerUserId == userId, ct);
            if (template is null)
            {
                return NotFound();
            }

            template.Name = request.Name!.Trim();
            template.PageWidthMm = request.PageWidthMm;
            template.PageHeightMm = request.PageHeightMm;
            template.MarginTopMm = request.MarginTopMm;
            template.MarginRightMm = request.MarginRightMm;
            template.MarginBottomMm = request.MarginBottomMm;
            template.MarginLeftMm = request.MarginLeftMm;
            template.FontFamily = NormalizeString(request.FontFamily) ?? "Georgia";
            template.BodyFontSizePt = request.BodyFontSizePt;
            template.LineHeight = request.LineHeight;
            template.ParagraphSpacingPt = request.ParagraphSpacingPt;
            template.HeaderEnabled = request.HeaderEnabled;
            template.HeaderLeft = NormalizeString(request.HeaderLeft);
            template.HeaderCenter = NormalizeString(request.HeaderCenter);
            template.HeaderRight = NormalizeString(request.HeaderRight);
            template.FooterEnabled = request.FooterEnabled;
            template.FooterLeft = NormalizeString(request.FooterLeft);
            template.FooterCenter = NormalizeString(request.FooterCenter);
            template.FooterRight = NormalizeString(request.FooterRight);
            template.PageNumbersEnabled = request.PageNumbersEnabled;
            template.PageNumberStart = request.PageNumberStart;
            template.TocEnabled = request.TocEnabled;
            template.TocDepth = request.TocDepth;
            template.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Updated export template {TemplateId} for user {UserId} ({Name}).",
                template.Id,
                userId,
                template.Name);

            return Ok(ToDto(template));
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            ExportTemplate? template = await _dbContext.ExportTemplates
                .FirstOrDefaultAsync(item => item.Id == id && item.OwnerUserId == userId, ct);
            if (template is null)
            {
                return NotFound();
            }

            _dbContext.ExportTemplates.Remove(template);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Deleted export template {TemplateId} for user {UserId} ({Name}).",
                template.Id,
                userId,
                template.Name);

            return NoContent();
        }

        private static ExportTemplateDto ToDto(ExportTemplate template)
        {
            return new ExportTemplateDto(
                template.Id,
                template.Name,
                template.PresetKey,
                template.PageWidthMm,
                template.PageHeightMm,
                template.MarginTopMm,
                template.MarginRightMm,
                template.MarginBottomMm,
                template.MarginLeftMm,
                template.FontFamily,
                template.BodyFontSizePt,
                template.LineHeight,
                template.ParagraphSpacingPt,
                template.HeaderEnabled,
                template.HeaderLeft,
                template.HeaderCenter,
                template.HeaderRight,
                template.FooterEnabled,
                template.FooterLeft,
                template.FooterCenter,
                template.FooterRight,
                template.PageNumbersEnabled,
                template.PageNumberStart,
                template.TocEnabled,
                template.TocDepth,
                template.CreatedAt,
                template.UpdatedAt);
        }

        private static ExportTemplateCreateRequest ApplyPreset(
            ExportTemplateCreateRequest request,
            ExportTemplatePresetDefinition preset)
        {
            return new ExportTemplateCreateRequest(
                preset.Name,
                preset.Key,
                preset.PageWidthMm,
                preset.PageHeightMm,
                preset.MarginTopMm,
                preset.MarginRightMm,
                preset.MarginBottomMm,
                preset.MarginLeftMm,
                preset.FontFamily,
                preset.BodyFontSizePt,
                preset.LineHeight,
                preset.ParagraphSpacingPt,
                preset.HeaderEnabled,
                preset.HeaderLeft,
                preset.HeaderCenter,
                preset.HeaderRight,
                preset.FooterEnabled,
                preset.FooterLeft,
                preset.FooterCenter,
                preset.FooterRight,
                preset.PageNumbersEnabled,
                preset.PageNumberStart,
                preset.TocEnabled,
                preset.TocDepth);
        }

        private static bool TryValidate(ExportTemplateCreateRequest request, out string? error)
        {
            if (request is null)
            {
                error = "Request body is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                error = "name is required.";
                return false;
            }

            if (!ValidateDimensions(request.PageWidthMm, request.PageHeightMm, request.MarginTopMm,
                request.MarginRightMm, request.MarginBottomMm, request.MarginLeftMm, out error))
            {
                return false;
            }

            if (request.BodyFontSizePt <= 0)
            {
                error = "bodyFontSizePt must be greater than 0.";
                return false;
            }

            if (request.LineHeight <= 0)
            {
                error = "lineHeight must be greater than 0.";
                return false;
            }

            if (request.ParagraphSpacingPt < 0)
            {
                error = "paragraphSpacingPt must be 0 or greater.";
                return false;
            }

            if (request.PageNumberStart < 1)
            {
                error = "pageNumberStart must be 1 or greater.";
                return false;
            }

            if (request.TocDepth < 1)
            {
                error = "tocDepth must be 1 or greater.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryValidate(ExportTemplateUpdateRequest request, out string? error)
        {
            if (request is null)
            {
                error = "Request body is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                error = "name is required.";
                return false;
            }

            if (!ValidateDimensions(request.PageWidthMm, request.PageHeightMm, request.MarginTopMm,
                request.MarginRightMm, request.MarginBottomMm, request.MarginLeftMm, out error))
            {
                return false;
            }

            if (request.BodyFontSizePt <= 0)
            {
                error = "bodyFontSizePt must be greater than 0.";
                return false;
            }

            if (request.LineHeight <= 0)
            {
                error = "lineHeight must be greater than 0.";
                return false;
            }

            if (request.ParagraphSpacingPt < 0)
            {
                error = "paragraphSpacingPt must be 0 or greater.";
                return false;
            }

            if (request.PageNumberStart < 1)
            {
                error = "pageNumberStart must be 1 or greater.";
                return false;
            }

            if (request.TocDepth < 1)
            {
                error = "tocDepth must be 1 or greater.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool ValidateDimensions(
            int width,
            int height,
            int marginTop,
            int marginRight,
            int marginBottom,
            int marginLeft,
            out string? error)
        {
            if (width <= 0 || height <= 0)
            {
                error = "pageWidthMm and pageHeightMm must be greater than 0.";
                return false;
            }

            if (marginTop < 0 || marginRight < 0 || marginBottom < 0 || marginLeft < 0)
            {
                error = "Margins must be 0 or greater.";
                return false;
            }

            error = null;
            return true;
        }

        private static string? NormalizeString(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
