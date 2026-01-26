using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.AI.Abstractions;
using WriterApp.Application.AI;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Data.Documents;
using WriterApp.Domain.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/ai/actions")]
    [Authorize]
    public sealed class AiActionsController : ControllerBase
    {
        private readonly IAiOrchestrator _orchestrator;
        private readonly IDocumentRepository _documents;
        private readonly ISectionRepository _sections;
        private readonly IPageRepository _pages;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IAiActionHistoryStore _historyStore;

        public AiActionsController(
            IAiOrchestrator orchestrator,
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            IUserIdResolver userIdResolver,
            IAiActionHistoryStore historyStore)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        }

        [HttpGet]
        public ActionResult<IReadOnlyList<AiActionDescriptorDto>> ListActions()
        {
            List<AiActionDescriptorDto> actions = _orchestrator.Actions
                .Where(action => _orchestrator.CanRunAction(action.ActionId))
                .Select(action => new AiActionDescriptorDto(
                    action.ActionId,
                    action.DisplayName,
                    action.RequiresSelection,
                    action.Modalities.Select(modality => modality.ToString()).ToList(),
                    BuildRequiredInputs(action)))
                .ToList();

            return Ok(actions);
        }

        [HttpPost("{actionKey}/execute")]
        public async Task<ActionResult<AiActionExecuteResponseDto>> ExecuteAction(
            string actionKey,
            [FromBody] AiActionExecuteRequestDto request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            IAiAction? action = _orchestrator.GetAction(actionKey);
            if (action is null)
            {
                return BadRequest(new { message = $"Unknown AI action '{actionKey}'." });
            }

            if (request.DocumentId is null || request.DocumentId == Guid.Empty)
            {
                return BadRequest(new { message = "documentId is required." });
            }

            Guid documentId = request.DocumentId.Value;
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            DocumentRecord? documentRecord = await _documents.GetAsync(documentId, userId, ct);
            if (documentRecord is null)
            {
                return NotFound();
            }

            Guid? resolvedSectionId = request.SectionId;
            if (resolvedSectionId is null && request.PageId is not null)
            {
                PageRecord? page = await _pages.GetAsync(request.PageId.Value, userId, ct);
                if (page is not null)
                {
                    resolvedSectionId = page.SectionId;
                }
            }

            if (resolvedSectionId is null)
            {
                return BadRequest(new { message = "sectionId or pageId is required." });
            }

            Guid sectionId = resolvedSectionId.Value;
            IReadOnlyList<SectionRecord> sectionRecords = await _sections.ListByDocumentAsync(documentId, userId, ct);
            if (!sectionRecords.Any(section => section.Id == sectionId))
            {
                return NotFound();
            }

            if (action.RequiresSelection && (!request.SelectionStart.HasValue || !request.SelectionEnd.HasValue))
            {
                return BadRequest(new { message = "Selection range is required for this action." });
            }
            if (action.RequiresSelection && string.IsNullOrWhiteSpace(request.OriginalText))
            {
                return BadRequest(new { message = "originalText is required for this action." });
            }

            Document aiDocument = await BuildAiDocumentAsync(documentRecord, sectionRecords, userId, ct);
            TextRange selectionRange = BuildSelectionRange(request);
            string selectedText = request.OriginalText ?? string.Empty;
            string? instruction = GetInstruction(request.Parameters);

            AiActionInput input = new(
                aiDocument,
                sectionId,
                selectionRange,
                selectedText,
                instruction,
                request.Parameters);

            AiExecutionResult result = await _orchestrator.ExecuteActionAsync(actionKey, input, ct);
            if (!result.Succeeded || result.Proposal is null)
            {
                string message = result.ErrorMessage ?? "AI action failed.";
                return BadRequest(new { message });
            }

            AiProposal proposal = result.Proposal;
            string? summary = string.IsNullOrWhiteSpace(proposal.UserSummary) ? proposal.SummaryLabel : proposal.UserSummary;
            var response = new AiActionExecuteResponseDto(
                proposal.ProposalId,
                proposal.OriginalText ?? request.OriginalText,
                proposal.ProposedText,
                summary,
                new DateTimeOffset(proposal.CreatedUtc),
                actionKey);

            await _historyStore.AddAsync(new AiActionHistoryEntry(
                proposal.ProposalId,
                proposal.ActionId,
                userId,
                documentId,
                sectionId,
                response.CreatedUtc,
                summary,
                proposal.OriginalText ?? request.OriginalText,
                proposal.ProposedText), ct);

            return Ok(response);
        }

        private static IReadOnlyList<string> BuildRequiredInputs(IAiAction action)
        {
            List<string> inputs = new() { "documentId", "sectionId" };
            if (action.RequiresSelection)
            {
                inputs.Add("selectionStart");
                inputs.Add("selectionEnd");
                inputs.Add("originalText");
            }

            return inputs;
        }

        private static TextRange BuildSelectionRange(AiActionExecuteRequestDto request)
        {
            int start = request.SelectionStart ?? 0;
            int end = request.SelectionEnd ?? start;
            if (end < start)
            {
                (start, end) = (end, start);
            }

            return new TextRange(start, Math.Max(0, end - start));
        }

        private async Task<Document> BuildAiDocumentAsync(
            DocumentRecord record,
            IReadOnlyList<SectionRecord> sections,
            string ownerUserId,
            CancellationToken ct)
        {
            List<Section> domainSections = new();
            foreach (SectionRecord sectionRecord in sections.OrderBy(section => section.OrderIndex))
            {
                IReadOnlyList<PageRecord> pages = await _pages.ListBySectionAsync(sectionRecord.Id, ownerUserId, ct);
                string content = string.Join("\n\n", pages.Select(page => page.Content ?? string.Empty));

                domainSections.Add(new Section
                {
                    SectionId = sectionRecord.Id,
                    Order = sectionRecord.OrderIndex,
                    Title = sectionRecord.Title,
                    Content = new SectionContent
                    {
                        Format = "html",
                        Value = content
                    },
                    Notes = sectionRecord.NarrativePurpose ?? string.Empty,
                    CreatedUtc = sectionRecord.CreatedAt.UtcDateTime,
                    ModifiedUtc = sectionRecord.UpdatedAt.UtcDateTime
                });
            }

            Chapter chapter = new()
            {
                Order = 0,
                Title = string.IsNullOrWhiteSpace(record.Title) ? "Draft" : record.Title,
                Sections = domainSections
            };

            return new Document
            {
                DocumentId = record.Id,
                Metadata = new DocumentMetadata
                {
                    Title = record.Title,
                    Language = "en",
                    CreatedUtc = record.CreatedAt.UtcDateTime,
                    ModifiedUtc = record.UpdatedAt.UtcDateTime
                },
                Chapters = new List<Chapter> { chapter }
            };
        }

        private static string? GetInstruction(Dictionary<string, object?>? parameters)
        {
            if (parameters is null || !parameters.TryGetValue("instruction", out object? value) || value is null)
            {
                return null;
            }

            return value.ToString();
        }
    }
}
