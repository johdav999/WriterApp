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
using WriterApp.Domain.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/export")]
    [Authorize]
    public sealed class DocumentExportController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ExportService _exportService;
        private readonly ILogger<DocumentExportController> _logger;

        public DocumentExportController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            ExportService exportService,
            ILogger<DocumentExportController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<IActionResult> ExportDocument(
            Guid documentId,
            [FromQuery] string kind = "document",
            [FromQuery] string format = "markdown",
            [FromQuery] Guid? templateId = null,
            CancellationToken ct = default)
        {
            if (!TryParseKind(kind, out ExportKind exportKind, out string? error))
            {
                return BadRequest(new { message = error });
            }

            if (!TryParseFormat(format, out ExportFormat exportFormat, out error))
            {
                return BadRequest(new { message = error });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            Document? document = await BuildExportDocumentAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            try
            {
                ExportResult result = await _exportService.ExportAsync(
                    document,
                    exportKind,
                    exportFormat,
                    new ExportOptions(),
                    userId,
                    templateId,
                    ct);

                _logger.LogInformation(
                    "Exported document {DocumentId} for user {UserId} ({Kind}/{Format}).",
                    documentId,
                    userId,
                    exportKind,
                    exportFormat);

                return File(result.Content, result.MimeType, result.FileName);
            }
            catch (ExportTemplateNotFoundException)
            {
                return NotFound(new { message = "Export template not found." });
            }
        }

        [HttpGet("print")]
        public async Task<ActionResult<ExportPrintPayload>> ExportPrint(
            Guid documentId,
            [FromQuery] string kind = "document",
            [FromQuery] Guid? templateId = null,
            CancellationToken ct = default)
        {
            if (!TryParseKind(kind, out ExportKind exportKind, out string? error))
            {
                return BadRequest(new { message = error });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            Document? document = await BuildExportDocumentAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            try
            {
                string bodyHtml = await _exportService.ExportHtmlBodyAsync(
                    document,
                    exportKind,
                    new ExportOptions(),
                    userId,
                    templateId,
                    ct);

                string html = $"<!DOCTYPE html><html><body>{bodyHtml}</body></html>";
                return Ok(new ExportPrintPayload(html));
            }
            catch (ExportTemplateNotFoundException)
            {
                return NotFound(new { message = "Export template not found." });
            }
        }

        private async Task<Document?> BuildExportDocumentAsync(Guid documentId, string userId, CancellationToken ct)
        {
            DocumentRecord? documentRecord = await _dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(document => document.Id == documentId && document.OwnerUserId == userId, ct);
            if (documentRecord is null)
            {
                return null;
            }

            List<SectionRecord> sections = await _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

            List<PageRecord> pages = await _dbContext.Pages
                .AsNoTracking()
                .Where(page => page.DocumentId == documentId)
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

        private static bool TryParseKind(string value, out ExportKind kind, out string? error)
        {
            if (!Enum.TryParse(value, true, out kind))
            {
                error = "Invalid export kind.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryParseFormat(string value, out ExportFormat format, out string? error)
        {
            if (!Enum.TryParse(value, true, out format))
            {
                error = "Invalid export format.";
                return false;
            }

            error = null;
            return true;
        }

        public sealed record ExportPrintPayload(string Html);
    }
}
