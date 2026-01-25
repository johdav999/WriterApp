using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Documents;
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
        private readonly ILogger<DocumentsController> _logger;

        public DocumentsController(
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            ILogger<DocumentsController> logger)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
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

            return Ok(new DocumentDetailDto(document.Id, document.Title, document.CreatedAt, document.UpdatedAt));
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
                    new DocumentDetailDto(existing.Id, existing.Title, existing.CreatedAt, existing.UpdatedAt),
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
                new DocumentDetailDto(document.Id, document.Title, document.CreatedAt, document.UpdatedAt),
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

            return Ok(new DocumentDetailDto(document.Id, document.Title, document.CreatedAt, document.UpdatedAt));
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
    }
}
