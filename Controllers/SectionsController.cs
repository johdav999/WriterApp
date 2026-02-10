using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Data;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Data.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO.Compression;
using WriterApp.Application.Importing;
using WriterApp.Application.Commands;
using WriterApp.Application.State;
using WriterApp.Application.Diagnostics;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/sections")]
    [Authorize]
    public sealed class SectionsController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ISectionRepository _sections;
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ISearchIndexService _searchIndex;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<SectionsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly IPageVersionService _pageVersionService;
        private readonly ISectionImportService _sectionImportService;

        public SectionsController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            ISearchIndexService searchIndex,
            AppDbContext dbContext,
            ILogger<SectionsController> logger,
            IConfiguration configuration,
            IPageVersionService pageVersionService,
            ISectionImportService sectionImportService)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _pageVersionService = pageVersionService ?? throw new ArgumentNullException(nameof(pageVersionService));
            _sectionImportService = sectionImportService ?? throw new ArgumentNullException(nameof(sectionImportService));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SectionDto>>> ListSections(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            IReadOnlyList<SectionRecord> sections = await _sections.ListByDocumentAsync(documentId, userId, ct);
            List<SectionDto> result = sections
                .Select(section => new SectionDto(
                    section.Id,
                    section.DocumentId,
                    section.Title,
                    section.NarrativePurpose,
                    section.OrderIndex,
                    section.CreatedAt,
                    section.UpdatedAt,
                    section.LanguageCode,
                    section.TranslationGroupId))
                .ToList();

            return Ok(result);
        }

        [HttpPost]
        public async Task<ActionResult<SectionDto>> CreateSection(
            Guid documentId,
            [FromBody] SectionCreateRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            Guid sectionId = request.Id ?? Guid.NewGuid();
            if (request.Id.HasValue)
            {
                SectionRecord? existing = await _sections.GetAsync(sectionId, userId, ct);
                if (existing is not null)
                {
                    if (existing.DocumentId != documentId)
                    {
                        return Conflict(new { message = "Section already exists under a different document." });
                    }

                    return Ok(new SectionDto(
                        existing.Id,
                        existing.DocumentId,
                        existing.Title,
                        existing.NarrativePurpose,
                        existing.OrderIndex,
                        existing.CreatedAt,
                        existing.UpdatedAt,
                        existing.LanguageCode,
                        existing.TranslationGroupId));
                }
            }

            string title = string.IsNullOrWhiteSpace(request.Title) ? "Section" : request.Title.Trim();
            DateTimeOffset createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow;
            DateTimeOffset updatedAt = request.UpdatedAt ?? createdAt;

            int orderIndex;
            if (request.OrderIndex.HasValue && request.OrderIndex.Value >= 0)
            {
                orderIndex = request.OrderIndex.Value;
            }
            else
            {
                IReadOnlyList<SectionRecord> existing = await _sections.ListByDocumentAsync(documentId, userId, ct);
                orderIndex = existing.Count;
            }

            SectionRecord section = new()
            {
                Id = sectionId,
                DocumentId = documentId,
                Title = title,
                NarrativePurpose = string.IsNullOrWhiteSpace(request.NarrativePurpose)
                    ? null
                    : request.NarrativePurpose.Trim(),
                OrderIndex = orderIndex,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            await _sections.CreateAsync(section, ct);

            // Ensure every section has at least one page so the editor can render content.
            PageRecord page = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                SectionId = section.Id,
                Title = "Page 1",
                Content = string.Empty,
                OrderIndex = 0,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };
            await _pages.CreateAsync(page, ct);
            await transaction.CommitAsync(ct);

            await _searchIndex.UpsertSectionAsync(section, ct);
            await _searchIndex.UpsertPageAsync(page, ct);

            SectionDto dto = new(
                section.Id,
                section.DocumentId,
                section.Title,
                section.NarrativePurpose,
                section.OrderIndex,
                section.CreatedAt,
                section.UpdatedAt,
                section.LanguageCode,
                section.TranslationGroupId);

            return Ok(dto);
        }

        [HttpGet("~/api/sections/{sectionId:guid}/translations")]
        public async Task<ActionResult<IReadOnlyList<SectionTranslationLinkDto>>> GetTranslations(
            Guid sectionId,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? section = await _sections.GetAsync(sectionId, userId, ct);
            if (section is null)
            {
                return NotFound();
            }

            if (!section.TranslationGroupId.HasValue)
            {
                return Ok(Array.Empty<SectionTranslationLinkDto>());
            }

            Guid groupId = section.TranslationGroupId.Value;
            List<SectionTranslationLinkDto> result = await _dbContext.Sections
                .AsNoTracking()
                .Where(item => item.Document!.OwnerUserId == userId && item.TranslationGroupId == groupId)
                .OrderBy(item => item.OrderIndex)
                .Select(item => new SectionTranslationLinkDto(
                    item.Id,
                    item.DocumentId,
                    item.Title,
                    item.LanguageCode,
                    groupId))
                .ToListAsync(ct);

            return Ok(result);
        }

        [HttpPost("~/api/sections/{sectionId:guid}/translations")]
        public async Task<ActionResult<TranslationDuplicateSectionResponse>> DuplicateTranslation(
            Guid sectionId,
            [FromBody] TranslationDuplicateSectionRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            SectionRecord? source = await _dbContext.Sections
                .Include(item => item.Document)
                .FirstOrDefaultAsync(item => item.Id == sectionId && item.Document!.OwnerUserId == userId, ct);
            if (source is null)
            {
                return NotFound();
            }

            Guid translationGroupId = source.TranslationGroupId ?? Guid.NewGuid();
            if (source.TranslationGroupId != translationGroupId)
            {
                source.TranslationGroupId = translationGroupId;
            }

            if (string.IsNullOrWhiteSpace(source.LanguageCode) && !string.IsNullOrWhiteSpace(request.SourceLanguage))
            {
                source.LanguageCode = request.SourceLanguage;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            int insertIndex = source.OrderIndex + 1;
            List<SectionRecord> reorder = await _dbContext.Sections
                .Where(item => item.DocumentId == source.DocumentId && item.OrderIndex >= insertIndex)
                .OrderByDescending(item => item.OrderIndex)
                .ToListAsync(ct);

            foreach (SectionRecord item in reorder)
            {
                item.OrderIndex += 1;
            }

            SectionRecord translated = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = source.DocumentId,
                Title = BuildTranslatedTitle(source.Title, request.TargetLanguage, request.Title),
                NarrativePurpose = source.NarrativePurpose,
                OrderIndex = insertIndex,
                CreatedAt = now,
                UpdatedAt = now,
                LanguageCode = request.TargetLanguage,
                TranslationGroupId = translationGroupId
            };

            PageRecord page = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = source.DocumentId,
                SectionId = translated.Id,
                Title = "Page 1",
                Content = request.Content ?? string.Empty,
                OrderIndex = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            _dbContext.Sections.Add(translated);
            _dbContext.Pages.Add(page);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await _searchIndex.UpsertSectionAsync(translated, ct);
            await _searchIndex.UpsertPageAsync(page, ct);

            SectionDto dto = new(
                translated.Id,
                translated.DocumentId,
                translated.Title,
                translated.NarrativePurpose,
                translated.OrderIndex,
                translated.CreatedAt,
                translated.UpdatedAt,
                translated.LanguageCode,
                translated.TranslationGroupId);

            return Ok(new TranslationDuplicateSectionResponse(dto, page.Id));
        }

        [HttpPost("reorder")]
        public async Task<ActionResult<IReadOnlyList<SectionDto>>> ReorderSections(
            Guid documentId,
            [FromBody] SectionReorderRequest request,
            CancellationToken ct)
        {
            Stopwatch timer = Stopwatch.StartNew();
            string correlationId = Request.Headers.TryGetValue("X-Reorder-Correlation", out var header)
                ? header.ToString()
                : Guid.NewGuid().ToString("N");
            Response.Headers["X-Reorder-Correlation"] = correlationId;

            if (request.OrderedSectionIds is null || request.OrderedSectionIds.Count == 0)
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Reject missing orderedSectionIds DocId={DocumentId} Corr={CorrelationId}",
                    documentId,
                    correlationId);
                return BadRequest(new { message = "orderedSectionIds is required." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            SectionReorderDiagnostics.LogDebug(
                _logger,
                _configuration,
                "Entry DocId={DocumentId} UserId={UserId} Count={Count} Corr={CorrelationId}",
                documentId,
                userId,
                request.OrderedSectionIds.Count,
                correlationId);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Document not found DocId={DocumentId} Corr={CorrelationId}",
                    documentId,
                    correlationId);
                return NotFound();
            }

            IReadOnlyList<SectionRecord> existing = await _sections.ListByDocumentAsync(documentId, userId, ct);
            if (existing.Count != request.OrderedSectionIds.Count)
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Count mismatch Existing={ExistingCount} Payload={PayloadCount} DocId={DocumentId} Corr={CorrelationId}",
                    existing.Count,
                    request.OrderedSectionIds.Count,
                    documentId,
                    correlationId);
                return BadRequest(new { message = "orderedSectionIds does not match document sections." });
            }

            HashSet<Guid> unique = new(request.OrderedSectionIds);
            if (unique.Count != request.OrderedSectionIds.Count)
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Duplicate ids PayloadCount={PayloadCount} UniqueCount={UniqueCount} DocId={DocumentId} Corr={CorrelationId}",
                    request.OrderedSectionIds.Count,
                    unique.Count,
                    documentId,
                    correlationId);
                return BadRequest(new { message = "orderedSectionIds contains duplicates." });
            }

            HashSet<Guid> existingIds = existing.Select(section => section.Id).ToHashSet();
            if (!existingIds.SetEquals(unique))
            {
                SectionReorderDiagnostics.LogWarning(
                    _logger,
                    _configuration,
                    "Id mismatch ExistingCount={ExistingCount} PayloadCount={PayloadCount} DocId={DocumentId} Corr={CorrelationId}",
                    existingIds.Count,
                    unique.Count,
                    documentId,
                    correlationId);
                return BadRequest(new { message = "orderedSectionIds must contain all document sections." });
            }

            SectionReorderDiagnostics.LogDebug(
                _logger,
                _configuration,
                "Before order DocId={DocumentId} FirstId={FirstId} LastId={LastId} Corr={CorrelationId}",
                documentId,
                existing.OrderBy(section => section.OrderIndex).Select(section => section.Id).FirstOrDefault(),
                existing.OrderBy(section => section.OrderIndex).Select(section => section.Id).LastOrDefault(),
                correlationId);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            List<SectionRecord> tracked = await _dbContext.Sections
                .Where(section => section.DocumentId == documentId && unique.Contains(section.Id))
                .ToListAsync(ct);

            Dictionary<Guid, int> ordering = new();
            for (int index = 0; index < request.OrderedSectionIds.Count; index++)
            {
                ordering[request.OrderedSectionIds[index]] = index;
            }

            foreach (SectionRecord section in tracked)
            {
                if (ordering.TryGetValue(section.Id, out int order))
                {
                    section.OrderIndex = order;
                    section.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            int saved = await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            List<SectionDto> result = tracked
                .OrderBy(section => section.OrderIndex)
                .Select(section => new SectionDto(
                    section.Id,
                    section.DocumentId,
                    section.Title,
                    section.NarrativePurpose,
                    section.OrderIndex,
                    section.CreatedAt,
                    section.UpdatedAt,
                    section.LanguageCode,
                    section.TranslationGroupId))
                .ToList();

            SectionReorderDiagnostics.LogDebug(
                _logger,
                _configuration,
                "Saved DocId={DocumentId} Updated={Updated} Returned={Returned} ElapsedMs={ElapsedMs} Corr={CorrelationId}",
                documentId,
                saved,
                result.Count,
                timer.ElapsedMilliseconds,
                correlationId);

            return Ok(result);
        }

        [HttpPut("{sectionId:guid}")]
        public async Task<ActionResult<SectionDto>> UpdateSection(
            Guid documentId,
            Guid sectionId,
            [FromBody] SectionUpdateRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            SectionUpdate update = new(request.Title, request.NarrativePurpose);
            SectionRecord? updated = await _sections.UpdateAsync(sectionId, userId, update, ct);
            if (updated is null)
            {
                return NotFound();
            }

            await _searchIndex.UpsertSectionAsync(updated, ct);

            SectionDto dto = new(
                updated.Id,
                updated.DocumentId,
                updated.Title,
                updated.NarrativePurpose,
                updated.OrderIndex,
                updated.CreatedAt,
                updated.UpdatedAt,
                updated.LanguageCode,
                updated.TranslationGroupId);

            return Ok(dto);
        }

        [HttpPost("{sectionId:guid}/import")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<ActionResult<SectionImportResponseDto>> ImportSectionContent(
            Guid documentId,
            Guid sectionId,
            [FromForm] SectionImportFormRequest request,
            CancellationToken ct)
        {
            const int maxFileSizeBytes = 10 * 1024 * 1024;

            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            Guid targetSectionId = request.TargetSectionId == Guid.Empty ? sectionId : request.TargetSectionId;
            SectionRecord? targetSection = await _sections.GetAsync(targetSectionId, userId, ct);
            if (targetSection is null || targetSection.DocumentId != documentId)
            {
                return NotFound(new { message = "Target section was not found." });
            }

            if (request.File is null || request.File.Length <= 0)
            {
                return BadRequest(new { message = "A file is required." });
            }

            if (request.File.Length > maxFileSizeBytes)
            {
                return StatusCode(413, new { message = "File exceeds the 10 MB size limit." });
            }

            string extension = Path.GetExtension(request.File.FileName ?? string.Empty).ToLowerInvariant();
            if (extension is not ".txt" and not ".rtf" and not ".docx")
            {
                return BadRequest(new { message = "Unsupported file type. Use TXT, RTF, or DOCX." });
            }

            await using Stream inputStream = request.File.OpenReadStream();
            byte[] bytes = await ReadAllBytesAsync(inputStream, (int)request.File.Length, ct);

            if (!ValidateSignature(extension, bytes))
            {
                return BadRequest(new { message = "File signature does not match extension." });
            }

            SectionImportOptions options = new(
                request.NormalizeWhitespace,
                request.PreserveTxtLineBreaks);
            SectionImportResult converted = await _sectionImportService.ConvertAsync(
                request.File.FileName ?? $"upload{extension}",
                bytes,
                options,
                ct);

            List<PageRecord> pages = await _dbContext.Pages
                .Where(page => page.SectionId == targetSectionId)
                .OrderBy(page => page.OrderIndex)
                .ToListAsync(ct);
            if (pages.Count == 0)
            {
                return NotFound(new { message = "Target section has no pages." });
            }

            string existingHtml = string.Join("\n\n", pages.Select(page => page.Content ?? string.Empty));
            string importedHtml = converted.Html;

            WriterApp.Domain.Documents.Document tempDocument = BuildTempDocument(targetSectionId, existingHtml);
            DocumentState tempState = new(tempDocument);
            CommandProcessor processor = new(tempState);
            if (string.Equals(request.Mode, "append", StringComparison.OrdinalIgnoreCase))
            {
                processor.Execute(new ImportAppendSectionCommand(targetSectionId, importedHtml));
            }
            else
            {
                processor.Execute(new ImportReplaceSectionCommand(targetSectionId, importedHtml));
            }

            string finalHtml = tempDocument.Chapters[0].Sections[0].Content.Value ?? string.Empty;
            DateTimeOffset now = DateTimeOffset.UtcNow;
            PageRecord primaryPage = pages[0];

            await _pageVersionService.CreateSnapshotAsync(
                userId,
                primaryPage,
                existingHtml,
                "pre-import",
                allowDuplicate: true,
                ct);

            primaryPage.Content = finalHtml;
            primaryPage.UpdatedAt = now;
            for (int i = 1; i < pages.Count; i++)
            {
                pages[i].Content = string.Empty;
                pages[i].UpdatedAt = now;
            }

            DocumentRecord? document = await _dbContext.Documents.FirstOrDefaultAsync(item => item.Id == documentId, ct);
            if (document is not null)
            {
                document.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(ct);
            await _searchIndex.UpsertPageAsync(primaryPage, ct);
            await _pageVersionService.CreateSnapshotAsync(
                userId,
                primaryPage,
                finalHtml,
                "import",
                allowDuplicate: true,
                ct);

            _logger.LogInformation(
                "[IMPORT] Imported format={Format} bytes={Bytes} user={UserId} doc={DocumentId} section={SectionId} mode={Mode} resultChars={Chars}",
                converted.Format,
                bytes.Length,
                userId,
                documentId,
                targetSectionId,
                request.Mode,
                converted.Stats.Characters);

            return Ok(new SectionImportResponseDto(
                converted.Html,
                new SectionImportStatsDto(
                    converted.Stats.Paragraphs,
                    converted.Stats.Headings,
                    converted.Stats.Lists,
                    converted.Stats.Characters),
                converted.Warnings.ToList(),
                converted.Format,
                targetSectionId));
        }

        [HttpDelete("{sectionId:guid}")]
        public async Task<IActionResult> DeleteSection(Guid documentId, Guid sectionId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            SectionRecord? target = await _sections.GetAsync(sectionId, userId, ct);
            if (target is null || target.DocumentId != documentId)
            {
                return NotFound();
            }

            List<PageRecord> pagesToDelete = await _dbContext.Pages
                .Where(page => page.SectionId == sectionId)
                .ToListAsync(ct);

            IReadOnlyList<SectionRecord> existing = await _sections.ListByDocumentAsync(documentId, userId, ct);
            if (existing.Count <= 1)
            {
                return Conflict(new { message = "Document must have at least one section." });
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            SectionRecord? removed = await _sections.DeleteAsync(sectionId, userId, ct);
            if (removed is null)
            {
                await transaction.RollbackAsync(ct);
                return NotFound();
            }

            List<SectionRecord> remaining = await _dbContext.Sections
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

            bool reordered = false;
            for (int index = 0; index < remaining.Count; index++)
            {
                if (remaining[index].OrderIndex != index)
                {
                    remaining[index].OrderIndex = index;
                    remaining[index].UpdatedAt = DateTimeOffset.UtcNow;
                    reordered = true;
                }
            }

            if (reordered)
            {
                DocumentRecord? document = await _dbContext.Documents
                    .FirstOrDefaultAsync(item => item.Id == documentId, ct);
                if (document is not null)
                {
                    document.UpdatedAt = DateTimeOffset.UtcNow;
                }

                await _dbContext.SaveChangesAsync(ct);
            }

            await transaction.CommitAsync(ct);

            await _searchIndex.DeleteByEntityAsync(SearchEntityTypes.Section, sectionId, ct);
            await _searchIndex.DeleteByEntityAsync(SearchEntityTypes.SceneCard, sectionId, ct);
            foreach (PageRecord page in pagesToDelete)
            {
                await _searchIndex.DeleteByEntityAsync(SearchEntityTypes.Page, page.Id, ct);
                await _searchIndex.DeleteByEntityAsync(SearchEntityTypes.Note, page.Id, ct);
            }
            return NoContent();
        }

        [HttpPost("{sectionId:guid}/duplicate")]
        public async Task<ActionResult<SectionDto>> DuplicateSection(Guid documentId, Guid sectionId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            if (!await _documents.ExistsAsync(documentId, userId, ct))
            {
                return NotFound();
            }

            SectionRecord? source = await _sections.GetAsync(sectionId, userId, ct);
            if (source is null || source.DocumentId != documentId)
            {
                return NotFound();
            }

            List<SectionRecord> sections = await _dbContext.Sections
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

            int sourceIndex = sections.FindIndex(section => section.Id == sectionId);
            if (sourceIndex < 0)
            {
                return NotFound();
            }

            string title = BuildDuplicateTitle(
                string.IsNullOrWhiteSpace(source.Title) ? "Section" : source.Title.Trim(),
                sections.Select(section => section.Title));
            DateTimeOffset now = DateTimeOffset.UtcNow;

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            for (int index = sourceIndex + 1; index < sections.Count; index++)
            {
                sections[index].OrderIndex += 1;
                sections[index].UpdatedAt = now;
            }

            SectionRecord duplicated = new()
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                Title = title,
                NarrativePurpose = source.NarrativePurpose,
                OrderIndex = sourceIndex + 1,
                CreatedAt = now,
                UpdatedAt = now,
                LanguageCode = source.LanguageCode,
                TranslationGroupId = source.TranslationGroupId
            };
            _dbContext.Sections.Add(duplicated);

            List<PageRecord> pages = await _dbContext.Pages
                .Where(page => page.SectionId == sectionId)
                .OrderBy(page => page.OrderIndex)
                .ToListAsync(ct);

            Dictionary<Guid, Guid> pageMap = new();
            List<PageRecord> duplicatedPages = new();
            foreach (PageRecord page in pages)
            {
                Guid newPageId = Guid.NewGuid();
                pageMap[page.Id] = newPageId;
                duplicatedPages.Add(new PageRecord
                {
                    Id = newPageId,
                    DocumentId = documentId,
                    SectionId = duplicated.Id,
                    Title = page.Title,
                    Content = page.Content,
                    OrderIndex = page.OrderIndex,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            if (duplicatedPages.Count > 0)
            {
                _dbContext.Pages.AddRange(duplicatedPages);

                List<Guid> sourcePageIds = pageMap.Keys.ToList();
                List<PageNoteRecord> notes = await _dbContext.PageNotes
                    .Where(note => sourcePageIds.Contains(note.PageId))
                    .ToListAsync(ct);

                List<PageNoteRecord> duplicatedNotes = new();
                foreach (PageNoteRecord note in notes)
                {
                    if (pageMap.TryGetValue(note.PageId, out Guid newPageId))
                    {
                        duplicatedNotes.Add(new PageNoteRecord
                        {
                            PageId = newPageId,
                            Notes = note.Notes,
                            UpdatedAt = now
                        });
                    }
                }

                if (duplicatedNotes.Count > 0)
                {
                    _dbContext.PageNotes.AddRange(duplicatedNotes);
                }
            }

            DocumentRecord? document = await _dbContext.Documents
                .FirstOrDefaultAsync(item => item.Id == documentId, ct);
            if (document is not null)
            {
                document.UpdatedAt = now;
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await _searchIndex.UpsertSectionAsync(duplicated, ct);
            foreach (PageRecord page in duplicatedPages)
            {
                await _searchIndex.UpsertPageAsync(page, ct);
            }
            if (duplicatedPages.Count > 0)
            {
                List<PageNoteRecord> newNotes = await _dbContext.PageNotes
                    .Where(note => duplicatedPages.Select(page => page.Id).Contains(note.PageId))
                    .ToListAsync(ct);
                foreach (PageNoteRecord note in newNotes)
                {
                    PageRecord? page = duplicatedPages.FirstOrDefault(item => item.Id == note.PageId);
                    if (page is not null)
                    {
                        await _searchIndex.UpsertPageNotesAsync(page, note, ct);
                    }
                }
            }

            SectionDto dto = new(
                duplicated.Id,
                duplicated.DocumentId,
                duplicated.Title,
                duplicated.NarrativePurpose,
                duplicated.OrderIndex,
                duplicated.CreatedAt,
                duplicated.UpdatedAt,
                duplicated.LanguageCode,
                duplicated.TranslationGroupId);

            return Ok(dto);
        }

        private static string BuildTranslatedTitle(string originalTitle, string? languageCode, string? overrideTitle)
        {
            if (!string.IsNullOrWhiteSpace(overrideTitle))
            {
                return overrideTitle.Trim();
            }

            string normalized = string.IsNullOrWhiteSpace(originalTitle) ? "Section" : originalTitle.Trim();
            string lang = string.IsNullOrWhiteSpace(languageCode) ? string.Empty : languageCode.Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(lang) ? normalized : $"{normalized} ({lang})";
        }

        private static string BuildDuplicateTitle(string baseTitle, IEnumerable<string> existingTitles)
        {
            HashSet<string> titles = new(existingTitles.Where(title => !string.IsNullOrWhiteSpace(title)),
                StringComparer.OrdinalIgnoreCase);
            string candidate = $"{baseTitle} (Copy)";
            int counter = 2;
            while (titles.Contains(candidate))
            {
                candidate = $"{baseTitle} (Copy {counter})";
                counter++;
            }

            return candidate;
        }

        private static async Task<byte[]> ReadAllBytesAsync(Stream stream, int expectedSize, CancellationToken ct)
        {
            using MemoryStream buffer = expectedSize > 0 ? new MemoryStream(expectedSize) : new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            return buffer.ToArray();
        }

        private static bool ValidateSignature(string extension, byte[] bytes)
        {
            if (extension == ".txt")
            {
                return true;
            }

            if (extension == ".rtf")
            {
                string prefix = Encoding.ASCII.GetString(bytes.AsSpan(0, Math.Min(bytes.Length, 8)));
                return prefix.StartsWith(@"{\rtf", StringComparison.OrdinalIgnoreCase);
            }

            if (extension == ".docx")
            {
                if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B)
                {
                    return false;
                }

                try
                {
                    using MemoryStream ms = new(bytes, writable: false);
                    using ZipArchive zip = new(ms, ZipArchiveMode.Read, leaveOpen: true);
                    return zip.Entries.Any(entry =>
                        string.Equals(entry.FullName, "word/document.xml", StringComparison.OrdinalIgnoreCase));
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static WriterApp.Domain.Documents.Document BuildTempDocument(Guid sectionId, string existingHtml)
        {
            return new WriterApp.Domain.Documents.Document
            {
                DocumentId = Guid.NewGuid(),
                Metadata = new Domain.Documents.DocumentMetadata(),
                Chapters = new List<Domain.Documents.Chapter>
                {
                    new()
                    {
                        Order = 0,
                        Title = "Import",
                        Sections = new List<Domain.Documents.Section>
                        {
                            new()
                            {
                                SectionId = sectionId,
                                Order = 0,
                                Title = "Import",
                                Content = new Domain.Documents.SectionContent
                                {
                                    Format = "html",
                                    Value = existingHtml ?? string.Empty
                                }
                            }
                        }
                    }
                }
            };
        }
    }

    public sealed record SectionImportFormRequest(
        IFormFile? File,
        Guid TargetSectionId,
        string Mode,
        bool NormalizeWhitespace = true,
        bool PreserveTxtLineBreaks = false);

    public sealed record SectionImportStatsDto(
        int Paragraphs,
        int Headings,
        int Lists,
        int Characters);

    public sealed record SectionImportResponseDto(
        string Html,
        SectionImportStatsDto Stats,
        List<string> Warnings,
        string Format,
        Guid TargetSectionId);
}
