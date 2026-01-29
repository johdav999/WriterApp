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
using WriterApp.Data.Documents;
using WriterApp.Data.Exporting;
using WriterApp.Domain.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/export/preview")]
    [Authorize]
    public sealed class ExportPreviewController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ExportService _exportService;
        private readonly IExportTemplateResolver _templateResolver;
        private readonly ILogger<ExportPreviewController> _logger;

        public ExportPreviewController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            ExportService exportService,
            IExportTemplateResolver templateResolver,
            ILogger<ExportPreviewController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _templateResolver = templateResolver ?? throw new ArgumentNullException(nameof(templateResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost]
        public async Task<ActionResult<ExportPreviewResponse>> Preview(
            [FromBody] ExportPreviewRequest request,
            CancellationToken ct)
        {
            if (request.DocumentId == Guid.Empty)
            {
                return BadRequest(new { message = "documentId is required." });
            }

            string scope = request.Scope?.Trim() ?? "document";
            if (!string.Equals(scope, "document", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(scope, "section", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "scope must be 'document' or 'section'." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            Document? document = await BuildExportDocumentAsync(request.DocumentId, userId, scope, request.SectionId, ct);
            if (document is null)
            {
                return NotFound();
            }

            try
            {
                ExportTemplate template = await _templateResolver.ResolveAsync(userId, request.TemplateId, ct);
                ExportTemplate previewTemplate = CloneTemplate(template);
                previewTemplate.TocEnabled = request.IncludeToc;

                string bodyHtml = await _exportService.ExportHtmlBodyAsync(
                    document,
                    ExportKind.Document,
                    new ExportOptions(IncludeTitlePage: true, TemplateId: previewTemplate.Id, Template: previewTemplate),
                    userId,
                    previewTemplate.Id,
                    ct);

                string html = $"<!DOCTYPE html><html><body>{bodyHtml}</body></html>";
                _logger.LogInformation(
                    "Generated export preview for document {DocumentId} scope {Scope}.",
                    request.DocumentId,
                    scope);

                return Ok(new ExportPreviewResponse(html));
            }
            catch (ExportTemplateNotFoundException)
            {
                return NotFound(new { message = "Export template not found." });
            }
        }

        private async Task<Document?> BuildExportDocumentAsync(
            Guid documentId,
            string userId,
            string scope,
            Guid? sectionId,
            CancellationToken ct)
        {
            DocumentRecord? documentRecord = await _dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(document => document.Id == documentId && document.OwnerUserId == userId, ct);
            if (documentRecord is null)
            {
                return null;
            }

            IQueryable<SectionRecord> sectionQuery = _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex);

            if (string.Equals(scope, "section", StringComparison.OrdinalIgnoreCase))
            {
                if (sectionId.HasValue)
                {
                    sectionQuery = sectionQuery.Where(section => section.Id == sectionId.Value);
                }
                else
                {
                    SectionRecord? first = await sectionQuery.FirstOrDefaultAsync(ct);
                    if (first is null)
                    {
                        return null;
                    }

                    sectionQuery = _dbContext.Sections
                        .AsNoTracking()
                        .Where(section => section.Id == first.Id);
                }
            }

            List<SectionRecord> sections = await sectionQuery.ToListAsync(ct);
            if (sections.Count == 0)
            {
                return null;
            }

            List<Guid> sectionIds = sections.Select(section => section.Id).ToList();
            List<PageRecord> pages = await _dbContext.Pages
                .AsNoTracking()
                .Where(page => page.DocumentId == documentId && sectionIds.Contains(page.SectionId))
                .OrderBy(page => page.SectionId)
                .ThenBy(page => page.OrderIndex)
                .ToListAsync(ct);

            Dictionary<Guid, List<PageRecord>> pagesBySection = pages
                .GroupBy(page => page.SectionId)
                .ToDictionary(group => group.Key, group => group.OrderBy(page => page.OrderIndex).ToList());

            Chapter chapter = new()
            {
                Order = 0,
                Title = string.IsNullOrWhiteSpace(documentRecord.Title) ? "Draft" : documentRecord.Title,
                Sections = sections.Select(section =>
                {
                    string content = string.Join("\n", pagesBySection.TryGetValue(section.Id, out List<PageRecord>? sectionPages)
                        ? sectionPages.Select(page => page.Content ?? string.Empty)
                        : Array.Empty<string>());

                    return new Section
                    {
                        SectionId = section.Id,
                        Order = section.OrderIndex,
                        Title = section.Title,
                        Content = new SectionContent
                        {
                            Format = "html",
                            Value = content
                        },
                        Notes = section.NarrativePurpose ?? string.Empty,
                        AI = new SectionAIInfo()
                    };
                }).ToList()
            };

            return new Document
            {
                DocumentId = documentRecord.Id,
                Metadata = new DocumentMetadata
                {
                    Title = documentRecord.Title,
                    Language = "en",
                    CreatedUtc = documentRecord.CreatedAt.UtcDateTime,
                    ModifiedUtc = documentRecord.UpdatedAt.UtcDateTime
                },
                Chapters = new List<Chapter> { chapter }
            };
        }

        private static ExportTemplate CloneTemplate(ExportTemplate template)
        {
            return new ExportTemplate
            {
                Id = template.Id,
                OwnerUserId = template.OwnerUserId,
                Name = template.Name,
                PresetKey = template.PresetKey,
                PageWidthMm = template.PageWidthMm,
                PageHeightMm = template.PageHeightMm,
                MarginTopMm = template.MarginTopMm,
                MarginRightMm = template.MarginRightMm,
                MarginBottomMm = template.MarginBottomMm,
                MarginLeftMm = template.MarginLeftMm,
                FontFamily = template.FontFamily,
                BodyFontSizePt = template.BodyFontSizePt,
                LineHeight = template.LineHeight,
                ParagraphSpacingPt = template.ParagraphSpacingPt,
                HeaderEnabled = template.HeaderEnabled,
                HeaderLeft = template.HeaderLeft,
                HeaderCenter = template.HeaderCenter,
                HeaderRight = template.HeaderRight,
                FooterEnabled = template.FooterEnabled,
                FooterLeft = template.FooterLeft,
                FooterCenter = template.FooterCenter,
                FooterRight = template.FooterRight,
                PageNumbersEnabled = template.PageNumbersEnabled,
                PageNumberStart = template.PageNumberStart,
                TocEnabled = template.TocEnabled,
                TocDepth = template.TocDepth,
                CreatedAt = template.CreatedAt,
                UpdatedAt = template.UpdatedAt
            };
        }
    }
}
