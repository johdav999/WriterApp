using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
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
        private readonly IProjectSceneLinkingService _projectSceneLinkingService;
        private readonly ExportService _exportService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<DocumentExportController> _logger;

        public DocumentExportController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IProjectSceneLinkingService projectSceneLinkingService,
            ExportService exportService,
            IConfiguration configuration,
            ILogger<DocumentExportController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _projectSceneLinkingService = projectSceneLinkingService ?? throw new ArgumentNullException(nameof(projectSceneLinkingService));
            _exportService = exportService ?? throw new ArgumentNullException(nameof(exportService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

            if (!IsFormatEnabled(exportFormat))
            {
                return BadRequest(new { message = "Export format is disabled." });
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

                int sectionCount = document.Chapters.SelectMany(chapter => chapter.Sections).Count();
                _logger.LogInformation(
                    "Exported document {DocumentId} for user {UserId} ({Kind}/{Format}) sections={SectionCount} bytes={Bytes}.",
                    documentId,
                    userId,
                    exportKind,
                    exportFormat,
                    sectionCount,
                    result.Content.Length);

                return File(result.Content, result.MimeType, result.FileName);
            }
            catch (ExportTemplateNotFoundException)
            {
                return NotFound(new { message = "Export template not found." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExportDocumentPost(
            Guid documentId,
            [FromBody] ExportDocumentRequest request,
            CancellationToken ct = default)
        {
            if (request is null || documentId == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid export request." });
            }

            if (!TryParseFormat(request.Format, out ExportFormat exportFormat, out string? error))
            {
                return BadRequest(new { message = error });
            }

            if (!IsFormatEnabled(exportFormat))
            {
                return BadRequest(new { message = "Export format is disabled." });
            }

            string scopeType = request.ScopeType?.Trim() ?? "document";
            if (!TryValidateScope(scopeType, request.ScopeIds, request.SelectionText, out error))
            {
                return BadRequest(new { message = error });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            Document? document = await BuildExportDocumentAsync(
                documentId,
                userId,
                scopeType,
                request.ScopeIds,
                request.SelectionText,
                ct);
            if (document is null)
            {
                return NotFound();
            }

            try
            {
                ExportResult result = await _exportService.ExportAsync(
                    document,
                    ExportKind.Document,
                    exportFormat,
                    BuildExportOptions(request),
                    userId,
                    request.TemplateId,
                    ct);

                int sectionCount = document.Chapters.SelectMany(chapter => chapter.Sections).Count();
                _logger.LogInformation(
                    "Exported document {DocumentId} for user {UserId} ({Scope}/{Format}) sections={SectionCount} bytes={Bytes}.",
                    documentId,
                    userId,
                    scopeType,
                    exportFormat,
                    sectionCount,
                    result.Content.Length);

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

        [HttpPost("print")]
        public async Task<ActionResult<ExportPrintPayload>> ExportPrintPost(
            Guid documentId,
            [FromBody] ExportDocumentRequest request,
            CancellationToken ct = default)
        {
            if (request is null || documentId == Guid.Empty)
            {
                return BadRequest(new { message = "Invalid export request." });
            }

            string scopeType = request.ScopeType?.Trim() ?? "document";
            if (!TryValidateScope(scopeType, request.ScopeIds, request.SelectionText, out string? error))
            {
                return BadRequest(new { message = error });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            Document? document = await BuildExportDocumentAsync(
                documentId,
                userId,
                scopeType,
                request.ScopeIds,
                request.SelectionText,
                ct);
            if (document is null)
            {
                return NotFound();
            }

            try
            {
                string bodyHtml = await _exportService.ExportHtmlBodyAsync(
                    document,
                    ExportKind.Document,
                    BuildExportOptions(request),
                    userId,
                    request.TemplateId,
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

            DocumentSynopsisRecord? synopsisRecord = await _dbContext.DocumentSynopses
                .AsNoTracking()
                .FirstOrDefaultAsync(synopsis => synopsis.DocumentId == documentId, ct);

            List<SectionRecord> sections = await ResolveSectionsAsync(
                documentRecord,
                userId,
                scope: "document",
                scopeIds: null,
                ct);

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
                Synopsis = synopsisRecord is null
                    ? new Synopsis { ModifiedUtc = DateTime.UtcNow }
                    : new Synopsis
                    {
                        Logline = synopsisRecord.Logline,
                        Premise = synopsisRecord.Premise,
                        Theme = synopsisRecord.Theme,
                        ProtagonistArc = synopsisRecord.ProtagonistArc,
                        CentralConflict = synopsisRecord.CentralConflict,
                        Stakes = synopsisRecord.Stakes,
                        Setting = synopsisRecord.Setting,
                        EndingIntent = synopsisRecord.EndingIntent,
                        OpenQuestions = synopsisRecord.OpenQuestions,
                        Notes = synopsisRecord.Notes,
                        ModifiedUtc = synopsisRecord.UpdatedAt.UtcDateTime
                    },
                Chapters = new List<Chapter> { chapter }
            };
        }

        private async Task<Document?> BuildExportDocumentAsync(
            Guid documentId,
            string userId,
            string scope,
            IReadOnlyList<Guid>? scopeIds,
            string? selectionText,
            CancellationToken ct)
        {
            DocumentRecord? documentRecord = await _dbContext.Documents
                .AsNoTracking()
                .FirstOrDefaultAsync(document => document.Id == documentId && document.OwnerUserId == userId, ct);
            if (documentRecord is null)
            {
                return null;
            }

            DocumentSynopsisRecord? synopsisRecord = await _dbContext.DocumentSynopses
                .AsNoTracking()
                .FirstOrDefaultAsync(synopsis => synopsis.DocumentId == documentId, ct);

            List<SectionRecord> sections = await ResolveSectionsAsync(documentRecord, userId, scope, scopeIds, ct);
            if (sections.Count == 0)
            {
                return null;
            }

            Chapter chapter = await BuildChapterAsync(documentRecord, sections, scope, scopeIds, selectionText, ct);
            if (chapter.Sections.Count == 0)
            {
                return null;
            }

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
                Synopsis = synopsisRecord is null
                    ? new Synopsis { ModifiedUtc = DateTime.UtcNow }
                    : new Synopsis
                    {
                        Logline = synopsisRecord.Logline,
                        Premise = synopsisRecord.Premise,
                        Theme = synopsisRecord.Theme,
                        ProtagonistArc = synopsisRecord.ProtagonistArc,
                        CentralConflict = synopsisRecord.CentralConflict,
                        Stakes = synopsisRecord.Stakes,
                        Setting = synopsisRecord.Setting,
                        EndingIntent = synopsisRecord.EndingIntent,
                        OpenQuestions = synopsisRecord.OpenQuestions,
                        Notes = synopsisRecord.Notes,
                        ModifiedUtc = synopsisRecord.UpdatedAt.UtcDateTime
                    },
                Chapters = new List<Chapter> { chapter }
            };
        }

        private async Task<List<SectionRecord>> ResolveSectionsAsync(
            DocumentRecord documentRecord,
            string userId,
            string scope,
            IReadOnlyList<Guid>? scopeIds,
            CancellationToken ct)
        {
            if (ShouldUseOutlineOrder(documentRecord, scope))
            {
                List<SectionRecord> allSections = await _dbContext.Sections
                    .AsNoTracking()
                    .Where(section => section.DocumentId == documentRecord.Id)
                    .OrderBy(section => section.OrderIndex)
                    .ToListAsync(ct);
                if (allSections.Count == 0)
                {
                    return allSections;
                }

                IReadOnlyList<ManuscriptSceneSectionItem> sceneSections =
                    await _projectSceneLinkingService.GetManuscriptSceneSectionsAsync(documentRecord.ProjectId, userId, ct);
                Dictionary<Guid, string> outlineTitleBySectionId = sceneSections
                    .Where(item => !string.IsNullOrWhiteSpace(item.SceneNode.Title))
                    .GroupBy(item => item.Section.Id)
                    .ToDictionary(group => group.Key, group => group.First().SceneNode.Title);

                foreach (SectionRecord section in allSections)
                {
                    if (outlineTitleBySectionId.TryGetValue(section.Id, out string? outlineTitle)
                        && !string.IsNullOrWhiteSpace(outlineTitle))
                    {
                        section.Title = outlineTitle.Trim();
                    }
                }

                return allSections;
            }

            IQueryable<SectionRecord> sectionQuery = _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.DocumentId == documentRecord.Id)
                .OrderBy(section => section.OrderIndex);

            if (string.Equals(scope, "section", StringComparison.OrdinalIgnoreCase))
            {
                Guid sectionId = scopeIds?.FirstOrDefault() ?? Guid.Empty;
                if (sectionId != Guid.Empty)
                {
                    sectionQuery = sectionQuery.Where(section => section.Id == sectionId);
                }
            }

            if (string.Equals(scope, "sections", StringComparison.OrdinalIgnoreCase) && scopeIds is not null)
            {
                sectionQuery = sectionQuery.Where(section => scopeIds.Contains(section.Id));
            }

            if (string.Equals(scope, "page", StringComparison.OrdinalIgnoreCase) && scopeIds is not null)
            {
                Guid pageId = scopeIds.FirstOrDefault();
                if (pageId == Guid.Empty)
                {
                    return new List<SectionRecord>();
                }

                Guid? sectionId = await _dbContext.Pages
                    .AsNoTracking()
                    .Where(page => page.Id == pageId && page.DocumentId == documentRecord.Id)
                    .Select(page => (Guid?)page.SectionId)
                    .FirstOrDefaultAsync(ct);

                if (!sectionId.HasValue)
                {
                    return new List<SectionRecord>();
                }

                sectionQuery = sectionQuery.Where(section => section.Id == sectionId.Value);
            }

            return await sectionQuery.ToListAsync(ct);
        }

        private static bool ShouldUseOutlineOrder(DocumentRecord documentRecord, string scope)
        {
            if (documentRecord.DocumentKind != DocumentKind.Manuscript)
            {
                return false;
            }

            string normalized = scope.Trim().ToLowerInvariant();
            return normalized is "document" or "manuscript";
        }

        private async Task<Chapter> BuildChapterAsync(
            DocumentRecord documentRecord,
            List<SectionRecord> sections,
            string scope,
            IReadOnlyList<Guid>? scopeIds,
            string? selectionText,
            CancellationToken ct)
        {
            List<Guid> sectionIds = sections.Select(section => section.Id).ToList();
            Dictionary<Guid, List<PageRecord>> pagesBySection = new();

            if (!string.Equals(scope, "selection", StringComparison.OrdinalIgnoreCase))
            {
                List<PageRecord> pages = await _dbContext.Pages
                    .AsNoTracking()
                    .Where(page => page.DocumentId == documentRecord.Id && sectionIds.Contains(page.SectionId))
                    .OrderBy(page => page.SectionId)
                    .ThenBy(page => page.OrderIndex)
                    .ToListAsync(ct);

                if (string.Equals(scope, "page", StringComparison.OrdinalIgnoreCase) && scopeIds is not null)
                {
                    Guid pageId = scopeIds.FirstOrDefault();
                    pages = pages.Where(page => page.Id == pageId).ToList();
                }

                pagesBySection = pages
                    .GroupBy(page => page.SectionId)
                    .ToDictionary(group => group.Key, group => group.OrderBy(page => page.OrderIndex).ToList());
            }

            if (string.Equals(scope, "selection", StringComparison.OrdinalIgnoreCase))
            {
                string safeText = WebUtility.HtmlEncode(selectionText ?? string.Empty);
                return new Chapter
                {
                    Order = 0,
                    Title = string.IsNullOrWhiteSpace(documentRecord.Title) ? "Draft" : documentRecord.Title,
                    Sections = new List<Section>
                    {
                        new()
                        {
                            SectionId = Guid.Empty,
                            Order = 0,
                            Title = "Selection",
                            Content = new SectionContent
                            {
                                Format = "html",
                                Value = $"<p>{safeText}</p>"
                            },
                            Notes = string.Empty,
                            AI = new SectionAIInfo()
                        }
                    }
                };
            }

            List<Section> exportSections = sections.Select(section =>
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
            }).ToList();

            return new Chapter
            {
                Order = 0,
                Title = string.IsNullOrWhiteSpace(documentRecord.Title) ? "Draft" : documentRecord.Title,
                Sections = exportSections
            };
        }

        private static bool TryValidateScope(
            string scope,
            IReadOnlyList<Guid>? scopeIds,
            string? selectionText,
            out string? error)
        {
            error = null;
            string normalized = scope.Trim().ToLowerInvariant();
            if (normalized is not ("document" or "manuscript" or "section" or "page" or "sections" or "selection"))
            {
                error = "scopeType is invalid.";
                return false;
            }

            if (normalized is "sections" && (scopeIds is null || scopeIds.Count == 0))
            {
                error = "scopeIds are required for selected sections.";
                return false;
            }

            if (normalized is "section" or "page")
            {
                if (scopeIds is null || scopeIds.Count == 0)
                {
                    error = "scopeIds are required for the selected scope.";
                    return false;
                }
            }

            if (normalized is "selection" && string.IsNullOrWhiteSpace(selectionText))
            {
                error = "selectionText is required for selection scope.";
                return false;
            }

            return true;
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

        private static ExportOptions BuildExportOptions(ExportDocumentRequest request)
        {
            return new ExportOptions(
                IncludeTitlePage: request.IncludeTitlePage,
                IncludeToc: request.IncludeToc,
                TocDepth: request.TocDepth,
                ChapterBreakRules: request.ChapterBreakRules,
                TitlePageTitle: request.TitlePageTitle,
                TitlePageSubtitle: request.TitlePageSubtitle,
                TitlePageAuthor: request.TitlePageAuthor,
                TitlePageDraftLabel: request.TitlePageDraftLabel,
                TitlePageDate: request.TitlePageDate,
                TemplateId: request.TemplateId);
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

        private bool IsFormatEnabled(ExportFormat format)
        {
            return format switch
            {
                ExportFormat.Docx => _configuration.GetValue<bool?>("Exports:DocxEnabled") ?? false,
                ExportFormat.Epub => _configuration.GetValue<bool?>("Exports:EpubEnabled") ?? false,
                _ => true
            };
        }

        public sealed record ExportPrintPayload(string Html);
    }
}
