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
        private readonly IOutlineOrderResolver _outlineOrderResolver;
        private readonly ExportService _exportService;
        private readonly IExportTemplateResolver _templateResolver;
        private readonly ILogger<ExportPreviewController> _logger;

        public ExportPreviewController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IOutlineOrderResolver outlineOrderResolver,
            ExportService exportService,
            IExportTemplateResolver templateResolver,
            ILogger<ExportPreviewController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _outlineOrderResolver = outlineOrderResolver ?? throw new ArgumentNullException(nameof(outlineOrderResolver));
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
                string? coverImageUrl = await ResolveProjectCoverImageUrlAsync(request.DocumentId, userId, scope, ct);
                bool includeCover = SupportsCoverInPreview(request.Format) && request.IncludeCover;
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
                        Template: previewTemplate,
                        IncludeCover: includeCover,
                        CoverImageUrl: coverImageUrl),
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

        private static bool SupportsCoverInPreview(string? format)
        {
            return (format ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "html" => true,
                "pdf" => true,
                "docx" => true,
                "epub" => true,
                _ => false
            };
        }

        private async Task<string?> ResolveProjectCoverImageUrlAsync(
            Guid documentId,
            string userId,
            string scope,
            CancellationToken ct)
        {
            string normalized = scope.Trim().ToLowerInvariant();
            if (normalized is not ("document" or "manuscript"))
            {
                return null;
            }

            return await _dbContext.Documents
                .AsNoTracking()
                .Where(document => document.Id == documentId && document.OwnerUserId == userId)
                .Join(
                    _dbContext.Projects.AsNoTracking(),
                    document => document.ProjectId,
                    project => project.Id,
                    (_, project) => project.CoverImageUrl)
                .FirstOrDefaultAsync(ct);
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
                    .ToListAsync(ct);
                if (allSections.Count == 0)
                {
                    return allSections;
                }

                OutlineSectionOrderResult outlineOrder = await _outlineOrderResolver.ResolveForManuscriptAsync(
                    documentRecord.ProjectId,
                    documentRecord.Id,
                    ct);
                HashSet<Guid> orderedIds = outlineOrder.OrderedSectionIds.ToHashSet();
                Dictionary<Guid, SectionRecord> sectionsById = allSections.ToDictionary(section => section.Id);
                List<SectionRecord> orderedSections = outlineOrder.OrderedSectionIds
                    .Where(sectionId => sectionsById.ContainsKey(sectionId))
                    .Select(sectionId => sectionsById[sectionId])
                    .ToList();
                orderedSections.AddRange(
                    allSections
                        .Where(section => !orderedIds.Contains(section.Id))
                        .OrderBy(section => section.OrderIndex));

                for (int index = 0; index < orderedSections.Count; index++)
                {
                    SectionRecord section = orderedSections[index];
                    section.OrderIndex = index;
                    if (outlineOrder.TitleBySectionId.TryGetValue(section.Id, out string? outlineTitle)
                        && !string.IsNullOrWhiteSpace(outlineTitle))
                    {
                        section.Title = outlineTitle.Trim();
                    }
                }

                return orderedSections;
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
            Dictionary<Guid, string> sceneContentBySection = await LoadSceneContentBySectionAsync(
                documentRecord.ProjectId,
                sectionIds,
                ct);

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
                if (string.IsNullOrWhiteSpace(content)
                    && sceneContentBySection.TryGetValue(section.Id, out string? fallbackSceneContent)
                    && !string.IsNullOrWhiteSpace(fallbackSceneContent))
                {
                    content = fallbackSceneContent;
                }

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

        private async Task<Dictionary<Guid, string>> LoadSceneContentBySectionAsync(
            Guid projectId,
            IReadOnlyCollection<Guid> sectionIds,
            CancellationToken ct)
        {
            if (projectId == Guid.Empty || sectionIds.Count == 0)
            {
                return new Dictionary<Guid, string>();
            }

            List<(Guid SectionId, string ContentJson, DateTimeOffset UpdatedAtUtc)> rows =
                (await _dbContext.ProjectNodes
                    .AsNoTracking()
                    .Where(node =>
                        node.ProjectId == projectId
                        && node.NodeType == ProjectNodeType.Scene
                        && node.LinkedSectionId.HasValue
                        && sectionIds.Contains(node.LinkedSectionId.Value))
                    .Join(
                        _dbContext.SceneContents.AsNoTracking(),
                        node => node.Id,
                        content => content.SceneNodeId,
                        (node, content) => new
                        {
                            SectionId = node.LinkedSectionId!.Value,
                            ContentJson = content.ContentJson ?? string.Empty,
                            content.UpdatedAtUtc
                        })
                    .ToListAsync(ct))
                .Select(item => (item.SectionId, item.ContentJson, item.UpdatedAtUtc))
                .ToList();

            return rows
                .GroupBy(item => item.SectionId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(item => item.UpdatedAtUtc)
                        .Select(item => item.ContentJson)
                        .FirstOrDefault() ?? string.Empty);
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
