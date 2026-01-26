using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data;
using WriterApp.Data.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/outline")]
    [Authorize]
    public sealed class DocumentOutlineController : ControllerBase
    {
        private readonly IDocumentRepository _documents;
        private readonly IUserIdResolver _userIdResolver;
        private readonly AppDbContext _dbContext;

        public DocumentOutlineController(
            IDocumentRepository documents,
            IUserIdResolver userIdResolver,
            AppDbContext dbContext)
        {
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        [HttpGet]
        public async Task<ActionResult<DocumentOutlineDto>> GetOutline(Guid documentId, CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            DocumentOutlineRecord? outline = await _dbContext.DocumentOutlines
                .FindAsync(new object?[] { documentId }, ct);

            if (outline is null)
            {
                return Ok(new DocumentOutlineDto(documentId, string.Empty, DateTimeOffset.UtcNow));
            }

            return Ok(new DocumentOutlineDto(outline.DocumentId, outline.Outline, outline.UpdatedAt));
        }

        [HttpPut]
        public async Task<ActionResult<DocumentOutlineDto>> UpdateOutline(
            Guid documentId,
            [FromBody] DocumentOutlineDto request,
            CancellationToken ct)
        {
            string userId = _userIdResolver.ResolveUserId(User);
            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            string outlineText = request.Outline ?? string.Empty;
            DocumentOutlineRecord? outline = await _dbContext.DocumentOutlines
                .FindAsync(new object?[] { documentId }, ct);

            if (outline is null)
            {
                outline = new DocumentOutlineRecord
                {
                    DocumentId = documentId,
                    Outline = outlineText,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.DocumentOutlines.Add(outline);
            }
            else
            {
                outline.Outline = outlineText;
                outline.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await _dbContext.SaveChangesAsync(ct);
            return Ok(new DocumentOutlineDto(documentId, outline.Outline, outline.UpdatedAt));
        }
    }
}
