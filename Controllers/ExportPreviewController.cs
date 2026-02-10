using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
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
        private readonly IProjectSceneLinkingService _projectSceneLinkingService;
        private readonly ExportService _exportService;
        private readonly IExportTemplateResolver _templateResolver;
        private readonly ILogger<ExportPreviewController> _logger;

        public ExportPreviewController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IProjectSceneLinkingService projectSceneLinkingService,
            ExportService exportService,
            IExportTemplateResolver templateResolver,
            ILogger<ExportPreviewController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _projectSceneLinkingService = projectSceneLinkingService ?? throw new ArgumentNullException(nameof(projectSceneLinkingService));
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

            string scope = request.ScopeType?.Trim() ?? "document";
            if (!TryValidateScope(scope, request.ScopeIds, request.SelectionText, out string? error))
            {
                return BadRequest(new { message = error });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            Document? document = await BuildExportDocumentAsync(
                request.DocumentId,
                userId,
                scope,
                request.ScopeIds,
                request.SelectionText,
                ct);
            if (document is null)
            {
                return NotFound();
            }

            try
            {
                ExportTemplate template = await _templateResolver.ResolveAsync(userId, request.TemplateId, ct);
                ExportTemplate previewTemplate = CloneTemplate(template);
                previewTemplate.TocEnabled = request.IncludeToc;
                if (request.TocDepth > 0)
                {
                    previewTemplate.TocDepth = request.TocDepth;
                }

                string bodyHtml = await _exportService.ExportHtmlBodyAsync(
                    document,
                    ExportKind.Document,
                    new ExportOptions(
                        IncludeTitlePage: request.IncludeTitlePage,
                        IncludeToc: request.IncludeToc,
                        TocDepth: request.TocDepth,
                        ChapterBreakRules: request.ChapterBreakRules,
                        TitlePageTitle: request.TitlePageTitle,
                        TitlePageSubtitle: request.TitlePageSubtitle,
                        TitlePageAuthor: request.TitlePageAuthor,
                        TitlePageDraftLabel: request.TitlePageDraftLabel,
                        TitlePageDate: request.TitlePageDate,
                        TemplateId: previewTemplate.Id,
                        Template: previewTemplate),
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
                string safeText = System.Net.WebUtility.HtmlEncode(selectionText ?? string.Empty);
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
