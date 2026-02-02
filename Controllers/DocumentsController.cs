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
        private readonly IUserIdResolver _userIdResolver;
        private readonly ISearchIndexService _searchIndex;
        private readonly AppDbContext _dbContext;
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            ISearchIndexService searchIndex,
            AppDbContext dbContext,
            ILogger<DocumentsController> logger)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _searchIndex = searchIndex ?? throw new ArgumentNullException(nameof(searchIndex));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<DocumentListItemDto>>> ListDocuments(CancellationToken ct)
        {
            string traceId = HttpContext.TraceIdentifier;
            string userId = _userIdResolver.ResolveUserId(User);
            _logger.LogInformation(
                "ListDocuments start TraceId={TraceId} UserId={UserId}.",
                traceId,
                userId);

            IReadOnlyList<DocumentRecord> documents = await _documents.ListAsync(userId, ct);

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
                    wordCounts.TryGetValue(document.Id, out int count) ? count : 0))
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
                document.TranslationGroupId));
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
            DocumentRecord translated = new()
            {
                Id = Guid.NewGuid(),
                OwnerUserId = userId,
                Title = BuildTranslatedTitle(source.Title, request.TargetLanguage, request.Title),
                CreatedAt = now,
                UpdatedAt = now,
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
                    translated.TranslationGroupId),
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
                        existing.TranslationGroupId),
                    null,
                    null));
            }

            DateTimeOffset createdAt = request.CreatedAt ?? DateTimeOffset.UtcNow;
            DateTimeOffset updatedAt = request.UpdatedAt ?? createdAt;

            DocumentRecord document = new()
            {
                Id = documentId,
                OwnerUserId = userId,
                Title = title,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            };

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
                    document.TranslationGroupId),
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
                document.TranslationGroupId));
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
    }
}
