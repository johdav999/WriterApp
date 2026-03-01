using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Application.Documents;
using WriterApp.Application.Search;
using WriterApp.Application.Security;
using WriterApp.Application.State;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents")]
    [Authorize]
    public sealed class DocumentsController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly ISectionRepository _sections;
        private readonly IPageRepository _pages;
        private readonly IDocumentLifecycleService _lifecycle;
        private readonly IUserIdResolver _userIdResolver;
        private readonly ISearchIndexService _searchIndex;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IDocumentLifecycleService lifecycle,
            IUserIdResolver userIdResolver,
            ISearchIndexService searchIndex,
            AppDbContext dbContext,
            ILogger<DocumentsController> logger)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<DocumentListItemDto>>> ListDocuments(
            [FromQuery] string? view,
            CancellationToken ct)
        {
            string traceId = HttpContext.TraceIdentifier;
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentListView listView = DocumentListViewParser.Parse(view);
            _logger.LogInformation(
                "ListDocuments start TraceId={TraceId} UserId={UserId}.",
                traceId,
                userId);

            IReadOnlyList<DocumentRecord> documents = await _documents.ListAsync(userId, listView, ct);

            Dictionary<Guid, int> wordCounts = new();
            foreach (DocumentRecord document in documents)
            {
                wordCounts[document.Id] = 0;
            }

            foreach (DocumentRecord document in documents)
            {
                IReadOnlyList<SectionRecord> sections = await _sections.ListByDocumentAsync(document.Id, userId, ct);
                foreach (SectionRecord section in sections)
                {
                    IReadOnlyList<PageRecord> pages = await _pages.ListBySectionAsync(section.Id, userId, ct);
                    foreach (PageRecord page in pages)
                    {
                        wordCounts[document.Id] += CountWords(page.Content);
                    }
                }
            }

            List<DocumentListItemDto> result = documents
                .Select(document => new DocumentListItemDto(
                    document.Id,
                    document.Title,
                    document.CreatedAt,
                    document.UpdatedAt,
                    wordCounts.TryGetValue(document.Id, out int count) ? count : 0,
                    document.IsArchived,
                    document.ArchivedAt,
                    ToDeletedAtOffset(document.DeletedAtUtc),
                    document.ProjectId,
                    NormalizeDocumentKind(document.DocumentKind)))
                .ToList();

            _logger.LogInformation(
                "ListDocuments end TraceId={TraceId} UserId={UserId} Count={Count}.",
                traceId,
                userId,
                result.Count);
            return Ok(result);
        }

        [HttpGet("{documentId:guid}")]
        public async Task<ActionResult<DocumentDetailDto>> GetDocument(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            return Ok(new DocumentDetailDto(
                document.Id,
                document.Title,
                document.CreatedAt,
                document.UpdatedAt,
                document.LanguageCode,
                document.TranslationGroupId,
                document.IsArchived,
                document.ArchivedAt,
                ToDeletedAtOffset(document.DeletedAtUtc),
                document.ProjectId,
                NormalizeDocumentKind(document.DocumentKind)));
        }

        [HttpGet("{documentId:guid}/translations")]
        public async Task<ActionResult<IReadOnlyList<DocumentTranslationLinkDto>>> GetTranslations(
            Guid documentId,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            if (!document.TranslationGroupId.HasValue)
            {
                return Ok(Array.Empty<DocumentTranslationLinkDto>());
            }

            Guid groupId = document.TranslationGroupId.Value;
            List<DocumentTranslationLinkDto> result = await _dbContext.Documents
                .AsNoTracking()
                .Where(item => item.OwnerUserId == userId && item.TranslationGroupId == groupId)
                .OrderBy(item => item.Title)
                .Select(item => new DocumentTranslationLinkDto(
                    item.Id,
                    item.Title,
                    item.LanguageCode,
                    groupId))
                .ToListAsync(ct);

            return Ok(result);
        }

        [HttpGet("{documentId:guid}/heading-outline")]
        public async Task<ActionResult<HeadingPrefixCountersDto>> GetHeadingPrefix(
            Guid documentId,
            [FromQuery] Guid upToPageId,
            CancellationToken ct)
        {
            string traceId = Request.Headers["X-Trace-Id"].FirstOrDefault()
                ?? HttpContext.TraceIdentifier;

            var timer = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogDebug(
                "HeadingPrefix START TraceId={TraceId} DocumentId={DocumentId} PageId={PageId}",
                traceId,
                documentId,
                upToPageId);

            if (upToPageId == Guid.Empty)
            {
                return BadRequest(new { message = "upToPageId is required." });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            List<SectionRecord> sections = await _dbContext.Sections
                .AsNoTracking()
                .Where(section => section.DocumentId == documentId)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

            HeadingPrefixCountersService countersService = new();
            int[] counters = countersService.CreateCounters();
            bool found = false;
            int pagesScanned = 0;
            int totalHeadings = 0;

            _logger.LogDebug(
                "HeadingPrefix SectionsLoaded TraceId={TraceId} SectionCount={SectionCount}",
                traceId,
                sections.Count);

            foreach (SectionRecord section in sections)
            {
                List<PageRecord> pages = await _dbContext.Pages
                    .AsNoTracking()
                    .Where(page => page.SectionId == section.Id)
                    .OrderBy(page => page.OrderIndex)
                    .ToListAsync(ct);

                foreach (PageRecord page in pages)
                {
                    if (page.Id == upToPageId)
                    {
                        found = true;
                        break;
                    }

                    pagesScanned += 1;
                    bool jsonParseFailed;
                    int headingsInPage = countersService.CountHeadings(page.Content, counters, out jsonParseFailed);
                    totalHeadings += headingsInPage;
                    if (jsonParseFailed)
                    {
                        _logger.LogDebug(
                            "HeadingPrefix JsonParseFailed TraceId={TraceId} PageId={PageId}",
                            traceId,
                            page.Id);
                    }
                }

                if (found)
                {
                    break;
                }
            }

            if (!found)
            {
                _logger.LogDebug(
                    "HeadingPrefix NotFound TraceId={TraceId} DocumentId={DocumentId} PageId={PageId}",
                    traceId,
                    documentId,
                    upToPageId);
                return NotFound();
            }

            timer.Stop();
            _logger.LogDebug(
                "HeadingPrefix END TraceId={TraceId} PagesScanned={PagesScanned} HeadingsBeforeTarget={Headings} Counters={Counters} DurationMs={DurationMs}",
                traceId,
                pagesScanned,
                totalHeadings,
                string.Join(",", counters.Skip(1)),
                timer.ElapsedMilliseconds);

            _logger.LogDebug(
                "HeadingPrefix Response TraceId={TraceId} Status={Status} Counters={Counters}",
                traceId,
                "200",
                string.Join(",", counters.Skip(1)));

            return Ok(new HeadingPrefixCountersDto(counters));
        }

        [HttpPost("{documentId:guid}/translations/duplicate")]
        public async Task<ActionResult<TranslationDuplicateDocumentResponse>> DuplicateTranslation(
            Guid documentId,
            [FromBody] TranslationDuplicateDocumentRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? source = await _dbContext.Documents
                .Include(doc => doc.Sections)
                .FirstOrDefaultAsync(doc => doc.Id == documentId && doc.OwnerUserId == userId, ct);
            if (source is null)
            {
                return NotFound();
            }

            List<SectionRecord> sourceSections = await _dbContext.Sections
                .Where(section => section.DocumentId == source.Id)
                .OrderBy(section => section.OrderIndex)
                .ToListAsync(ct);

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
            DocumentKind translatedKind = source.DocumentKind == DocumentKind.Manuscript
                ? DocumentKind.Other
                : source.DocumentKind;
            DocumentRecord translated = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = source.ProjectId,
                OwnerUserId = userId,
                Title = BuildTranslatedTitle(source.Title, request.TargetLanguage, request.Title),
                DocumentKind = translatedKind,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                UpdatedAtUnixSeconds = now.ToUnixTimeSeconds(),
                IsArchived = false,
                ArchivedAt = null,
                DeletedAtUtc = null,
                LanguageCode = request.TargetLanguage,
                TranslationGroupId = translationGroupId
            };

            Dictionary<Guid, TranslatedSectionPayload> payloadMap = request.Sections
                .GroupBy(item => item.SectionId)
                .Select(group => group.First())
                .ToDictionary(item => item.SectionId, item => item);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            _dbContext.Documents.Add(translated);

            Guid? firstSectionId = null;
            Guid? firstPageId = null;
            List<SectionRecord> createdSections = new();
            List<PageRecord> createdPages = new();

            foreach (SectionRecord sourceSection in sourceSections)
            {
                Guid newSectionId = Guid.NewGuid();
                TranslatedSectionPayload? translatedSection = payloadMap.TryGetValue(sourceSection.Id, out TranslatedSectionPayload? payload)
                    ? payload
                    : null;
                string content = translatedSection?.Content ?? string.Empty;
                string title = translatedSection?.Title ?? sourceSection.Title;

                SectionRecord newSection = new()
                {
                    Id = newSectionId,
                    DocumentId = translated.Id,
                    Title = title,
                    NarrativePurpose = sourceSection.NarrativePurpose,
                    OrderIndex = sourceSection.OrderIndex,
                    CreatedAt = now,
                    UpdatedAt = now,
                    LanguageCode = request.TargetLanguage,
                    TranslationGroupId = translationGroupId
                };
                _dbContext.Sections.Add(newSection);
                createdSections.Add(newSection);

                PageRecord page = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = translated.Id,
                    SectionId = newSectionId,
                    Title = "Page 1",
                    Content = content,
                    OrderIndex = 0,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Pages.Add(page);
                createdPages.Add(page);

                if (!firstSectionId.HasValue)
                {
                    firstSectionId = newSectionId;
                    firstPageId = page.Id;
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            await _searchIndex.UpsertDocumentAsync(translated, ct);
            foreach (SectionRecord section in createdSections)
            {
                await _searchIndex.UpsertSectionAsync(section, ct);
            }
            foreach (PageRecord page in createdPages)
            {
                await _searchIndex.UpsertPageAsync(page, ct);
            }

            return Ok(new TranslationDuplicateDocumentResponse(
                new DocumentDetailDto(
                    translated.Id,
                    translated.Title,
                    translated.CreatedAt,
                    translated.UpdatedAt,
                    translated.LanguageCode,
                    translated.TranslationGroupId,
                    translated.IsArchived,
                    translated.ArchivedAt,
                    ToDeletedAtOffset(translated.DeletedAtUtc),
                    translated.ProjectId,
                    NormalizeDocumentKind(translated.DocumentKind)),
                firstSectionId,
                firstPageId));
        }

        [HttpPost]
        public async Task<ActionResult<DocumentCreateResponse>> CreateDocument(
            [FromBody] DocumentCreateRequest request,
            CancellationToken ct)
        {
            string traceId = HttpContext.TraceIdentifier;
            string userId = _userIdResolver.ResolveUserId(User);
            _logger.LogInformation(
                "CreateDocument start TraceId={TraceId} UserId={UserId} RequestId={RequestId} DefaultStructure={DefaultStructure}.",
                traceId,
                userId,
                request.Id,
                request.CreateDefaultStructure);

            Guid documentId = request.Id ?? Guid.NewGuid();
            string title = string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title.Trim();

            if (await _documents.ExistsAsync(documentId, userId, ct))
            {
                DocumentRecord? existing = await _documents.GetAsync(documentId, userId, ct);
                if (existing is null)
                {
                    _logger.LogWarning(
                        "CreateDocument conflict TraceId={TraceId} UserId={UserId} DocumentId={DocumentId}.",
                        traceId,
                        userId,
                        documentId);
                    return Conflict(new { message = "Document already exists." });
                }

                _logger.LogInformation(
                    "CreateDocument existing TraceId={TraceId} UserId={UserId} DocumentId={DocumentId}.",
                    traceId,
                    userId,
                    existing.Id);
                return Ok(new DocumentCreateResponse(
                    new DocumentDetailDto(
                        existing.Id,
                        existing.Title,
                        existing.CreatedAt,
                        existing.UpdatedAt,
                        existing.LanguageCode,
                        existing.TranslationGroupId,
                        existing.IsArchived,
                        existing.ArchivedAt,
                        ToDeletedAtOffset(existing.DeletedAtUtc),
                        existing.ProjectId,
                        NormalizeDocumentKind(existing.DocumentKind)),
                    null,
                    null));
            }

            DateTimeOffset createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow;
            DateTimeOffset updatedAt = request.UpdatedAt ?? createdAt;
            DocumentKind documentKind = ParseDocumentKind(request.Kind);

            Guid projectId;
            ProjectRecord? project = null;
            if (request.ProjectId.HasValue)
            {
                project = await _dbContext.Projects
                    .FirstOrDefaultAsync(item => item.Id == request.ProjectId.Value && item.OwnerUserId == userId, ct);
                if (project is null)
                {
                    return BadRequest(new { message = "Project not found." });
                }

                projectId = project.Id;
            }
            else
            {
                project = new ProjectRecord
                {
                    Id = Guid.NewGuid(),
                    OwnerUserId = userId,
                    Title = $"{title} Project",
                    Subtitle = null,
                    AuthorName = null,
                    Language = null,
                    Genre = null,
                    DefaultExportSettingsJson = null,
                    CreatedUtc = createdAt,
                    UpdatedUtc = updatedAt
                };
                _dbContext.Projects.Add(project);
                projectId = project.Id;
            }

            if (documentKind == DocumentKind.Manuscript)
            {
                bool hasManuscript = await _dbContext.Documents
                    .AnyAsync(item => item.ProjectId == projectId && item.DocumentKind == DocumentKind.Manuscript, ct);
                if (hasManuscript)
                {
                    return Conflict(new { message = "Project already has a manuscript document." });
                }
            }

            DocumentRecord document = new()
            {
                Id = documentId,
                ProjectId = projectId,
                OwnerUserId = userId,
                Title = title,
                DocumentKind = documentKind,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                CreatedAtUnixSeconds = createdAt.ToUnixTimeSeconds(),
                UpdatedAtUnixSeconds = updatedAt.ToUnixTimeSeconds(),
                IsArchived = false,
                ArchivedAt = null,
                DeletedAtUtc = null
            };

            if (project is not null)
            {
                project.UpdatedUtc = updatedAt;
            }

            await _documents.CreateAsync(document, ct);
            await _searchIndex.UpsertDocumentAsync(document, ct);

            Guid? defaultSectionId = null;
            Guid? defaultPageId = null;

            if (request.CreateDefaultStructure)
            {
                SectionRecord section = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    Title = "Draft",
                    NarrativePurpose = null,
                    OrderIndex = 0,
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt
                };
                await _sections.CreateAsync(section, ct);
                await _searchIndex.UpsertSectionAsync(section, ct);

                PageRecord page = new()
                {
                    Id = Guid.NewGuid(),
                    DocumentId = document.Id,
                    SectionId = section.Id,
                    Title = "Page 1",
                    Content = string.Empty,
                    OrderIndex = 0,
                    CreatedAt = createdAt,
                    UpdatedAt = updatedAt
                };
                await _pages.CreateAsync(page, ct);
                await _searchIndex.UpsertPageAsync(page, ct);

                defaultSectionId = section.Id;
                defaultPageId = page.Id;
            }

            _logger.LogInformation(
                "CreateDocument end TraceId={TraceId} UserId={UserId} DocumentId={DocumentId} DefaultSectionId={DefaultSectionId} DefaultPageId={DefaultPageId}.",
                traceId,
                userId,
                document.Id,
                defaultSectionId,
                defaultPageId);
            return Ok(new DocumentCreateResponse(
                new DocumentDetailDto(
                    document.Id,
                    document.Title,
                    document.CreatedAt,
                    document.UpdatedAt,
                    document.LanguageCode,
                    document.TranslationGroupId,
                    document.IsArchived,
                    document.ArchivedAt,
                    ToDeletedAtOffset(document.DeletedAtUtc),
                    document.ProjectId,
                    NormalizeDocumentKind(document.DocumentKind)),
                defaultSectionId,
                defaultPageId));
        }

        [HttpPut("{documentId:guid}")]
        public async Task<ActionResult<DocumentDetailDto>> UpdateDocument(
            Guid documentId,
            [FromBody] DocumentUpdateRequest request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            string? title = request.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return BadRequest(new { message = "Title is required." });
            }

            DocumentRecord? document = await _documents.UpdateTitleAsync(documentId, userId, title, ct);
            if (document is null)
            {
                return NotFound();
            }

            await _searchIndex.UpsertDocumentAsync(document, ct);

            return Ok(new DocumentDetailDto(
                document.Id,
                document.Title,
                document.CreatedAt,
                document.UpdatedAt,
                document.LanguageCode,
                document.TranslationGroupId,
                document.IsArchived,
                document.ArchivedAt,
                ToDeletedAtOffset(document.DeletedAtUtc),
                document.ProjectId,
                NormalizeDocumentKind(document.DocumentKind)));
        }

        [HttpPost("{documentId:guid}/archive")]
        public async Task<ActionResult<DocumentDetailDto>> ArchiveDocument(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _lifecycle.ArchiveAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            return Ok(new DocumentDetailDto(
                document.Id,
                document.Title,
                document.CreatedAt,
                document.UpdatedAt,
                document.LanguageCode,
                document.TranslationGroupId,
                document.IsArchived,
                document.ArchivedAt,
                ToDeletedAtOffset(document.DeletedAtUtc),
                document.ProjectId,
                NormalizeDocumentKind(document.DocumentKind)));
        }

        [HttpPost("{documentId:guid}/unarchive")]
        public async Task<ActionResult<DocumentDetailDto>> UnarchiveDocument(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _lifecycle.UnarchiveAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            return Ok(new DocumentDetailDto(
                document.Id,
                document.Title,
                document.CreatedAt,
                document.UpdatedAt,
                document.LanguageCode,
                document.TranslationGroupId,
                document.IsArchived,
                document.ArchivedAt,
                ToDeletedAtOffset(document.DeletedAtUtc),
                document.ProjectId,
                NormalizeDocumentKind(document.DocumentKind)));
        }

        [HttpPost("{documentId:guid}/trash")]
        public async Task<ActionResult<DocumentDetailDto>> MoveToTrash(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _lifecycle.MoveToTrashAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            return Ok(new DocumentDetailDto(
                document.Id,
                document.Title,
                document.CreatedAt,
                document.UpdatedAt,
                document.LanguageCode,
                document.TranslationGroupId,
                document.IsArchived,
                document.ArchivedAt,
                ToDeletedAtOffset(document.DeletedAtUtc),
                document.ProjectId,
                NormalizeDocumentKind(document.DocumentKind)));
        }

        [HttpPost("{documentId:guid}/restore")]
        public async Task<ActionResult<DocumentDetailDto>> RestoreFromTrash(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _lifecycle.RestoreAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            return Ok(new DocumentDetailDto(
                document.Id,
                document.Title,
                document.CreatedAt,
                document.UpdatedAt,
                document.LanguageCode,
                document.TranslationGroupId,
                document.IsArchived,
                document.ArchivedAt,
                ToDeletedAtOffset(document.DeletedAtUtc),
                document.ProjectId,
                NormalizeDocumentKind(document.DocumentKind)));
        }

        [HttpDelete("{documentId:guid}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> PermanentlyDeleteDocument(Guid documentId, CancellationToken ct)
        {
            bool removed = await _lifecycle.PermanentlyDeleteAsync(documentId, ct);
            return removed ? NoContent() : NotFound();
        }

        private static int CountWords(string? html)
        {
            if (string.IsNullOrWhiteSpace(html))
            {
                return 0;
            }

            string decoded = PlainTextMapper.ToPlainText(html);
            MatchCollection matches = Regex.Matches(decoded, @"\b[\p{L}\p{N}']+\b");
            return matches.Count;
        }

        private static string BuildTranslatedTitle(string originalTitle, string? languageCode, string? overrideTitle)
        {
            if (!string.IsNullOrWhiteSpace(overrideTitle))
            {
                return overrideTitle.Trim();
            }

            string normalized = string.IsNullOrWhiteSpace(originalTitle) ? "Untitled" : originalTitle.Trim();
            string lang = string.IsNullOrWhiteSpace(languageCode) ? string.Empty : languageCode.Trim().ToUpperInvariant();
            return string.IsNullOrWhiteSpace(lang) ? normalized : $"{normalized} ({lang})";
        }

        private static DateTimeOffset? ToDeletedAtOffset(DateTime? deletedAtUtc)
        {
            if (!deletedAtUtc.HasValue)
            {
                return null;
            }

            DateTime normalized = deletedAtUtc.Value.Kind switch
            {
                DateTimeKind.Utc => deletedAtUtc.Value,
                DateTimeKind.Local => deletedAtUtc.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(deletedAtUtc.Value, DateTimeKind.Utc)
            };

            return new DateTimeOffset(normalized);
        }

        private static DocumentKind ParseDocumentKind(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return DocumentKind.Manuscript;
            }

            return value.Trim().ToLowerInvariant() switch
            {
                "manuscript" => DocumentKind.Manuscript,
                "synopsis" => DocumentKind.Synopsis,
                "notes" => DocumentKind.Notes,
                "outline" => DocumentKind.Outline,
                "other" => DocumentKind.Other,
                _ => DocumentKind.Other
            };
        }

        private static string NormalizeDocumentKind(DocumentKind kind)
        {
            return kind switch
            {
                DocumentKind.Manuscript => "manuscript",
                DocumentKind.Synopsis => "synopsis",
                DocumentKind.Notes => "notes",
                DocumentKind.Outline => "outline",
                _ => "other"
            };
        }
    }
}
