using System;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.AI.StoryCoach;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Synopsis;
using WriterApp.Data;
using WriterApp.Data.Documents;
using WriterApp.Domain.Documents;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/documents/{documentId:guid}/synopsis")]
    [Authorize]
    public sealed class DocumentSynopsisController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IDocumentRepository _documents;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IAiOrchestrator _orchestrator;
        private readonly SynopsisAiContextBuilder _synopsisContextBuilder;
        private readonly StoryCoachContextBuilder _storyCoachContextBuilder;
        private readonly IAiActionHistoryStore _historyStore;
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public DocumentSynopsisController(
            AppDbContext dbContext,
            IDocumentRepository documents,
            IUserIdResolver userIdResolver,
            IAiOrchestrator orchestrator,
            SynopsisAiContextBuilder synopsisContextBuilder,
            StoryCoachContextBuilder storyCoachContextBuilder,
            IAiActionHistoryStore historyStore)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _synopsisContextBuilder = synopsisContextBuilder ?? throw new ArgumentNullException(nameof(synopsisContextBuilder));
            _storyCoachContextBuilder = storyCoachContextBuilder ?? throw new ArgumentNullException(nameof(storyCoachContextBuilder));
            _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
        }

        [HttpGet]
        public async Task<ActionResult<DocumentSynopsisDto>> GetSynopsis(Guid documentId, CancellationToken ct)
        {
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            DocumentSynopsisRecord? synopsis = await _dbContext.DocumentSynopses
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.DocumentId == documentId, ct);

            return Ok(MapToDto(documentId, synopsis));
        }

        [HttpPut]
        public async Task<ActionResult<DocumentSynopsisDto>> UpdateSynopsis(
            Guid documentId,
            [FromBody] DocumentSynopsisDto request,
            CancellationToken ct)
        {
            if (request is null)
            {
                return BadRequest(new { message = "Request body is required." });
            }

            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            DocumentSynopsisRecord? synopsis = await _dbContext.DocumentSynopses
                .FirstOrDefaultAsync(item => item.DocumentId == documentId, ct);

            if (synopsis is null)
            {
                synopsis = new DocumentSynopsisRecord
                {
                    DocumentId = documentId
                };
                _dbContext.DocumentSynopses.Add(synopsis);
            }

            ApplyDto(request, synopsis);
            synopsis.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return Ok(MapToDto(documentId, synopsis));
        }

        [HttpPost("ai/evaluate")]
        public async Task<ActionResult<SynopsisAiResponseDto>> EvaluateSynopsis(
            Guid documentId,
            [FromBody] SynopsisAiRequestDto? request,
            CancellationToken ct)
        {
            return await RunSynopsisAiAsync(
                documentId,
                SynopsisEvaluateAction.ActionIdValue,
                "evaluate",
                request,
                ct);
        }

        [HttpPost("ai/questions")]
        public async Task<ActionResult<SynopsisAiResponseDto>> AskQuestions(
            Guid documentId,
            [FromBody] SynopsisAiRequestDto? request,
            CancellationToken ct)
        {
            return await RunSynopsisAiAsync(
                documentId,
                SynopsisQuestionsAction.ActionIdValue,
                "questions",
                request,
                ct);
        }

        [HttpPost("ai/suggest")]
        public async Task<ActionResult<SynopsisAiResponseDto>> SuggestField(
            Guid documentId,
            [FromBody] SynopsisAiRequestDto? request,
            CancellationToken ct)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.FocusFieldKey))
            {
                return BadRequest(new { message = "focusFieldKey is required." });
            }

            return await RunSynopsisAiAsync(
                documentId,
                StoryCoachAction.ActionIdValue,
                "suggest",
                request,
                ct);
        }

        private async Task<ActionResult<SynopsisAiResponseDto>> RunSynopsisAiAsync(
            Guid documentId,
            string actionId,
            string mode,
            SynopsisAiRequestDto? request,
            CancellationToken ct)
        {
            string userId;
            try
            {
                userId = _userIdResolver.ResolveUserId(User);
            }
            catch (SecurityException)
            {
                return Unauthorized();
            }

            DocumentRecord? document = await _documents.GetAsync(documentId, userId, ct);
            if (document is null)
            {
                return NotFound();
            }

            DocumentSynopsisRecord? synopsisRecord = await _dbContext.DocumentSynopses
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.DocumentId == documentId, ct);

            Synopsis synopsis = MapToDomain(synopsisRecord);
            string synopsisContext = _synopsisContextBuilder.Build(synopsis);

            IAiAction? action = _orchestrator.GetAction(actionId);
            if (action is null)
            {
                return BadRequest(new { message = $"Unknown AI action '{actionId}'." });
            }

            Dictionary<string, object?> options = new()
            {
                ["synopsis_context"] = synopsisContext,
                ["user_notes"] = request?.UserNotes ?? string.Empty
            };

            if (string.Equals(actionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                string focusFieldKey = request?.FocusFieldKey ?? string.Empty;
                if (!SynopsisFieldCatalog.TryGetValue(synopsis, focusFieldKey, out string existingValue))
                {
                    return BadRequest(new { message = $"Unknown synopsis field '{focusFieldKey}'." });
                }

                StoryCoachContext context = _storyCoachContextBuilder.Build(synopsis, focusFieldKey);

                options["focus_field_key"] = focusFieldKey;
                options["focus_field_prompt"] = context.FocusFieldPrompt;
                options["other_fields_context"] = context.OtherFieldsContext;
                options["existing_value"] = existingValue;
            }

            Document aiDocument = BuildAiDocument(document, synopsis);
            AiActionInput input = new(
                aiDocument,
                Guid.Empty,
                new TextRange(0, 0),
                string.Empty,
                action.DisplayName,
                options);

            AiExecutionResult result = await _orchestrator.ExecuteActionAsync(actionId, input, ct);
            if (!result.Succeeded || result.Proposal is null)
            {
                string message = result.ErrorMessage ?? "AI action failed.";
                string code = string.IsNullOrWhiteSpace(result.ErrorCode) ? "ai.blocked" : result.ErrorCode!;
                int statusCode = string.Equals(code, "AI_QUOTA_EXCEEDED", StringComparison.Ordinal)
                    ? StatusCodes.Status402PaymentRequired
                    : StatusCodes.Status400BadRequest;
                ProblemDetails problem = new()
                {
                    Status = statusCode,
                    Title = "AI request blocked",
                    Detail = message
                };
                problem.Extensions["code"] = code;
                if (result.ErrorDetails is not null)
                {
                    foreach ((string key, object? value) in result.ErrorDetails)
                    {
                        problem.Extensions[key] = value;
                    }
                }

                return StatusCode(statusCode, problem);
            }

            AiProposal proposal = result.Proposal;
            string outputText = proposal.ProposedText ?? string.Empty;
            string? proposedText = string.Equals(actionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal)
                ? outputText
                : null;

            SynopsisAiResponseDto response = new(
                mode,
                outputText,
                request?.FocusFieldKey,
                proposedText);

            AiActionExecuteResponseDto historyResponse = new(
                proposal.ProposalId,
                null,
                outputText,
                action.DisplayName,
                new DateTimeOffset(proposal.CreatedUtc),
                actionId);

            string requestJson = JsonSerializer.Serialize(request ?? new SynopsisAiRequestDto(null, null), JsonOptions);
            string responseJson = JsonSerializer.Serialize(historyResponse, JsonOptions);

            await _historyStore.AddAsync(new AiActionHistoryEntry(
                proposal.ProposalId,
                proposal.ActionId,
                userId,
                documentId,
                Guid.Empty,
                new DateTimeOffset(proposal.CreatedUtc),
                action.DisplayName,
                null,
                outputText,
                PageId: null,
                ProviderId: proposal.ProviderId,
                ModelId: null,
                RequestJson: requestJson,
                ResultJson: responseJson), ct);

            return Ok(response);
        }

        private static Document BuildAiDocument(DocumentRecord record, Synopsis synopsis)
        {
            return new Document
            {
                DocumentId = record.Id,
                Metadata = new DocumentMetadata
                {
                    Title = record.Title,
                    Language = record.LanguageCode ?? "en",
                    CreatedUtc = record.CreatedAt.UtcDateTime,
                    ModifiedUtc = record.UpdatedAt.UtcDateTime
                },
                Synopsis = synopsis
            };
        }

        private static Synopsis MapToDomain(DocumentSynopsisRecord? record)
        {
            if (record is null)
            {
                return new Synopsis { ModifiedUtc = DateTime.UtcNow };
            }

            return new Synopsis
            {
                Logline = record.Logline ?? string.Empty,
                Premise = record.Premise ?? string.Empty,
                Theme = record.Theme ?? string.Empty,
                ProtagonistArc = record.ProtagonistArc ?? string.Empty,
                CentralConflict = record.CentralConflict ?? string.Empty,
                Stakes = record.Stakes ?? string.Empty,
                Setting = record.Setting ?? string.Empty,
                EndingIntent = record.EndingIntent ?? string.Empty,
                OpenQuestions = record.OpenQuestions ?? string.Empty,
                Notes = record.Notes ?? string.Empty,
                ModifiedUtc = record.UpdatedAt.UtcDateTime
            };
        }

        private static DocumentSynopsisDto MapToDto(Guid documentId, DocumentSynopsisRecord? record)
        {
            if (record is null)
            {
                return new DocumentSynopsisDto(
                    documentId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    DateTimeOffset.UtcNow);
            }

            return new DocumentSynopsisDto(
                record.DocumentId,
                record.Logline ?? string.Empty,
                record.Premise ?? string.Empty,
                record.Theme ?? string.Empty,
                record.ProtagonistArc ?? string.Empty,
                record.CentralConflict ?? string.Empty,
                record.Stakes ?? string.Empty,
                record.Setting ?? string.Empty,
                record.EndingIntent ?? string.Empty,
                record.OpenQuestions ?? string.Empty,
                record.Notes ?? string.Empty,
                record.UpdatedAt);
        }

        private static void ApplyDto(DocumentSynopsisDto request, DocumentSynopsisRecord record)
        {
            record.Logline = request.Logline ?? string.Empty;
            record.Premise = request.Premise ?? string.Empty;
            record.Theme = request.Theme ?? string.Empty;
            record.ProtagonistArc = request.ProtagonistArc ?? string.Empty;
            record.CentralConflict = request.CentralConflict ?? string.Empty;
            record.Stakes = request.Stakes ?? string.Empty;
            record.Setting = request.Setting ?? string.Empty;
            record.EndingIntent = request.EndingIntent ?? string.Empty;
            record.OpenQuestions = request.OpenQuestions ?? string.Empty;
            record.Notes = request.Notes ?? string.Empty;
        }
    }
}
