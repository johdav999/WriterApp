using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.Synopsis;
using WriterApp.Application.State;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
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
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IEntitlementService _entitlementService;
        private readonly IAiActionHistoryStore _historyStore;
        private readonly IVersionHistoryService _versionHistory;
        private readonly ILogger<AiActionsController> _logger;
        private const int OutlineMaxSectionChars = 2000;
        private const int OutlineMaxSections = 60;
        private const int SceneMaxSectionChars = 4000;
        private static readonly HashSet<string> HiddenSynopsisActions = new(StringComparer.Ordinal)
        {
            StoryCoachAction.ActionIdValue,
            SynopsisEvaluateAction.ActionIdValue,
            SynopsisQuestionsAction.ActionIdValue
        };

        public AiActionsController(
            IAiOrchestrator orchestrator,
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IEntitlementService entitlementService,
            IAiActionHistoryStore historyStore,
            IVersionHistoryService versionHistory,
            ILogger<AiActionsController> logger)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
            _versionHistory = versionHistory ?? throw new ArgumentNullException(nameof(versionHistory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet]
        public ActionResult<IReadOnlyList<AiActionDescriptorDto>> ListActions()
        {
            List<AiActionDescriptorDto> actions = _orchestrator.Actions
                .Where(action => _orchestrator.CanRunAction(action.ActionId))
                .Where(action => !HiddenSynopsisActions.Contains(action.ActionId))
                .Select(action => new AiActionDescriptorDto(
                    action.ActionId,
                    action.DisplayName,
                    action.RequiresSelection,
                    action.Modalities.Select(modality => modality.ToString()).ToList(),
                    BuildRequiredInputs(action)))
                .ToList();

            return Ok(actions);
        }

        [HttpGet("history")]
        public async Task<ActionResult<IReadOnlyList<AiActionHistoryEntryDto>>> ListHistory(
            [FromQuery] Guid documentId,
            CancellationToken ct)
        {
            if (documentId == Guid.Empty)
            {
                return BadRequest(new { message = "documentId is required." });
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

            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.AiActionHistory, "ai.history");
            if (gate is not null)
            {
                return gate;
            }

            string correlationId = Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
            try
            {
                IReadOnlyList<AiActionHistoryEntry> entries = await _historyStore.ListAsync(userId, documentId, ct);
                List<AiActionHistoryEntryDto> result = entries
                    .OrderByDescending(entry => entry.CreatedUtc)
                    .Select(entry => new AiActionHistoryEntryDto(
                        entry.ProposalId,
                        entry.ActionKey,
                        entry.Summary,
                        entry.OriginalText,
                        entry.ProposedText,
                        entry.CreatedUtc,
                        entry.IsApplied,
                        entry.LastAppliedAt,
                        entry.AppliedCount,
                        entry.IsApplied ? AiCommandStatusDto.Applied : AiCommandStatusDto.Succeeded))
                    .ToList();

                _logger.LogInformation(
                    "AI history listed. TraceId={TraceId}, CorrelationId={CorrelationId}, UserId={UserId}, DocumentId={DocumentId}, Count={Count}",
                    HttpContext.TraceIdentifier,
                    correlationId,
                    userId,
                    documentId,
                    result.Count);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "AI history load failed. TraceId={TraceId}, CorrelationId={CorrelationId}, UserId={UserId}, DocumentId={DocumentId}",
                    HttpContext.TraceIdentifier,
                    correlationId,
                    userId,
                    documentId);

                ProblemDetails problem = BuildProblemDetails(
                    StatusCodes.Status503ServiceUnavailable,
                    "History unavailable",
                    "AI history is temporarily unavailable. Please retry.",
                    "ai.history_unavailable");
                return StatusCode(problem.Status!.Value, problem);
            }
        }

        [HttpPost("history/{historyEntryId:guid}/applied")]
        public async Task<IActionResult> RecordAppliedEvent(
            Guid historyEntryId,
            [FromBody] AiActionAppliedRequest? request,
            CancellationToken ct)
        {
            if (historyEntryId == Guid.Empty)
            {
                return BadRequest(new { message = "historyEntryId is required." });
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

            try
            {
                if (request?.PageId is not null && !string.IsNullOrWhiteSpace(request.BeforeContent))
                {
                    PageRecord? page = await _pages.GetAsync(request.PageId.Value, userId, ct);
                    if (page is not null)
                    {
                        await _versionHistory.CreateCheckpointAsync(
                            userId,
                            page,
                            request.BeforeContent,
                            "pre-ai",
                            allowDuplicate: true,
                            ct);
                    }
                }

                await _historyStore.AddAppliedEventAsync(
                    userId,
                    historyEntryId,
                    DateTimeOffset.UtcNow,
                    request?.DocumentId,
                    request?.SectionId,
                    request?.PageId,
                    request?.BeforeContent,
                    request?.AfterContent,
                    ct);
            }
            catch (InvalidOperationException)
            {
                return NotFound();
            }

            return NoContent();
        }

        [HttpPost("history/undo")]
        public async Task<ActionResult<AiActionUndoRedoResponseDto>> Undo(
            [FromBody] AiActionUndoRedoRequestDto request,
            CancellationToken ct)
        {
            if (request.DocumentId is null || request.SectionId is null)
            {
                return BadRequest(new { message = "documentId and sectionId are required." });
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

            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.AiUndoRedo, "ai.undo");
            if (gate is not null)
            {
                return gate;
            }

            AiActionUndoRedoResult? result = await _historyStore.UndoAsync(
                userId,
                request.DocumentId.Value,
                request.SectionId.Value,
                request.PageId,
                ct);

            if (result is null)
            {
                return NoContent();
            }

            return Ok(new AiActionUndoRedoResponseDto(result.HistoryEntryId, result.Content));
        }

        [HttpPost("history/redo")]
        public async Task<ActionResult<AiActionUndoRedoResponseDto>> Redo(
            [FromBody] AiActionUndoRedoRequestDto request,
            CancellationToken ct)
        {
            if (request.DocumentId is null || request.SectionId is null)
            {
                return BadRequest(new { message = "documentId and sectionId are required." });
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

            ActionResult? gate = await EnsureFeatureAllowedAsync(userId, FeatureKey.AiUndoRedo, "ai.redo");
            if (gate is not null)
            {
                return gate;
            }

            AiActionUndoRedoResult? result = await _historyStore.RedoAsync(
                userId,
                request.DocumentId.Value,
                request.SectionId.Value,
                request.PageId,
                ct);

            if (result is null)
            {
                return NoContent();
            }

            return Ok(new AiActionUndoRedoResponseDto(result.HistoryEntryId, result.Content));
        }

        [HttpPost("{actionKey}/execute")]
        public async Task<ActionResult<AiActionExecuteResponseDto>> ExecuteAction(
            string actionKey,
            [FromBody] AiActionExecuteRequestDto request,
            CancellationToken ct)
        {
            var actionTimer = System.Diagnostics.Stopwatch.StartNew();
            string correlationId = Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
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

            FeatureKey? gatedFeature = ResolveFeatureForAction(actionKey);
            if (gatedFeature.HasValue)
            {
                ActionResult? gate = await EnsureFeatureAllowedAsync(userId, gatedFeature.Value, actionKey);
                if (gate is not null)
                {
                    return gate;
                }
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

            IReadOnlyList<SectionRecord> sectionRecords = await _sections.ListByDocumentAsync(documentId, userId, ct);
            if (sectionRecords.Count == 0)
            {
                return BadRequest(new { message = "document has no sections." });
            }

            if (string.Equals(actionKey, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionKey, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                resolvedSectionId ??= sectionRecords.First().Id;
            }
            else if (resolvedSectionId is null)
            {
                return BadRequest(new { message = "sectionId or pageId is required." });
            }

            Guid sectionId = resolvedSectionId!.Value;
            if (!sectionRecords.Any(section => section.Id == sectionId))
            {
                return NotFound();
            }

            if (action.RequiresSelection && (!request.SelectionStart.HasValue || !request.SelectionEnd.HasValue))
            {
                return BadRequest(new
                {
                    message = "Selection range is required for this action.",
                    action = actionKey,
                    required = new[] { "selectionStart", "selectionEnd" }
                });
            }
            if (action.RequiresSelection && string.IsNullOrWhiteSpace(request.OriginalText))
            {
                return BadRequest(new
                {
                    message = "originalText is required for this action.",
                    action = actionKey,
                    required = new[] { "originalText" }
                });
            }

            bool outlineTruncated = false;
            DocumentSynopsisRecord? synopsisRecord = await _dbContext.DocumentSynopses
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.DocumentId == documentId, ct);
            Document aiDocument;
            if (string.Equals(actionKey, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                (aiDocument, outlineTruncated) = await BuildAiOutlineDocumentAsync(
                    documentRecord,
                    sectionRecords,
                    userId,
                    ct);
            }
            else
            {
                aiDocument = await BuildAiDocumentAsync(documentRecord, sectionRecords, userId, synopsisRecord, ct);
            }
            TextRange selectionRange = BuildSelectionRange(request);
            string selectedText = request.OriginalText ?? string.Empty;
            string? instruction = GetInstruction(request.Parameters);

            Dictionary<string, object?> options = request.Parameters is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(request.Parameters);
            if (!string.IsNullOrWhiteSpace(request.SurroundingText))
            {
                options["section_text_override"] = request.SurroundingText;
            }
            if (string.Equals(actionKey, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                options["max_section_chars"] = OutlineMaxSectionChars;
                options["max_sections"] = OutlineMaxSections;
                options["truncated"] = outlineTruncated;
            }
            if (RequiresSceneMetadata(actionKey))
            {
                SectionSceneCardRecord? sceneCard = null;
                try
                {
                    sceneCard = await _dbContext.SectionSceneCards
                        .FindAsync(new object?[] { sectionId }, ct);
                }
                catch (SqliteException ex) when (
                    ex.Message.Contains("no such column", StringComparison.OrdinalIgnoreCase)
                    || ex.Message.Contains("no such table", StringComparison.OrdinalIgnoreCase))
                {
                    // Production-safe fallback for transient schema drift while migrations are being reconciled.
                    _logger.LogWarning(
                        ex,
                        "Scene metadata table/columns unavailable. Falling back to empty scene card context. TraceId={TraceId}, CorrelationId={CorrelationId}, ActionKey={ActionKey}, DocumentId={DocumentId}, SectionId={SectionId}",
                        HttpContext.TraceIdentifier,
                        correlationId,
                        actionKey,
                        documentId,
                        sectionId);
                }
                options["narrative_purpose"] = sceneCard?.NarrativePurpose ?? string.Empty;
                options["emotional_beat"] = sceneCard?.EmotionalBeat ?? string.Empty;
                options["key_events"] = sceneCard?.KeyEvents ?? string.Empty;
                options["open_questions"] = sceneCard?.OpenQuestions ?? string.Empty;
                options["pov_character_id"] = sceneCard?.PovCharacterId ?? string.Empty;
                options["place_id"] = sceneCard?.PlaceId ?? string.Empty;
                options["timeline_event_id"] = sceneCard?.TimelineEventId ?? string.Empty;
                options["time_ref"] = sceneCard?.TimeRef ?? string.Empty;
                options["tags_json"] = sceneCard?.TagsJson ?? "[]";
                options["references_json"] = sceneCard?.ReferencesJson ?? "[]";
                if (IsSceneCardAction(actionKey))
                {
                    options["max_section_chars"] = SceneMaxSectionChars;
                }
            }

            AiActionInput input = new(
                aiDocument,
                sectionId,
                selectionRange,
                selectedText,
                instruction,
                options);

            if (RequiresSectionText(actionKey))
            {
                string sectionTextForValidation = ResolveSectionTextForValidation(aiDocument, sectionId, request.SurroundingText);
                if (string.IsNullOrWhiteSpace(sectionTextForValidation))
                {
                    return CreateAiProblem(
                        StatusCodes.Status400BadRequest,
                        "Missing scene text",
                        "This action needs section text. Add content to the scene and try again.",
                        "ai.missing_section_text");
                }
            }

            _logger.LogInformation(
                "AI action request {ActionKey}. TraceId={TraceId}, CorrelationId={CorrelationId}, UserId={UserId}, DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, RequiresSelection={RequiresSelection}, SelectionLength={SelectionLength}, SurroundingLength={SurroundingLength}, ParameterCount={ParameterCount}",
                actionKey,
                HttpContext.TraceIdentifier,
                correlationId,
                userId,
                documentId,
                sectionId,
                request.PageId,
                action.RequiresSelection,
                selectedText.Length,
                request.SurroundingText?.Length ?? 0,
                request.Parameters?.Count ?? 0);

            AiExecutionResult result;
            try
            {
                result = await _orchestrator.ExecuteActionAsync(actionKey, input, ct);
            }
            catch (AiProviderException ex)
            {
                (int statusCode, string errorCode, string detail) = MapProviderException(ex);
                _logger.LogWarning(
                    ex,
                    "AI provider failure for {ActionKey}. TraceId={TraceId}, CorrelationId={CorrelationId}, ProviderId={ProviderId}, DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, DurationMs={DurationMs}",
                    actionKey,
                    HttpContext.TraceIdentifier,
                    correlationId,
                    ex.ProviderId,
                    documentId,
                    sectionId,
                    request.PageId,
                    actionTimer.ElapsedMilliseconds);
                return CreateAiProblem(statusCode, "AI request failed", detail, errorCode);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "AI action timeout for {ActionKey}. TraceId={TraceId}, CorrelationId={CorrelationId}, DocumentId={DocumentId}, SectionId={SectionId}, DurationMs={DurationMs}",
                    actionKey,
                    HttpContext.TraceIdentifier,
                    correlationId,
                    documentId,
                    sectionId,
                    actionTimer.ElapsedMilliseconds);
                return CreateAiProblem(
                    StatusCodes.Status504GatewayTimeout,
                    "AI request timed out",
                    "The AI provider timed out. Try again.",
                    "ai.timeout");
            }
            catch (TimeoutException ex)
            {
                _logger.LogWarning(
                    ex,
                    "AI action timeout for {ActionKey}. TraceId={TraceId}, CorrelationId={CorrelationId}, DocumentId={DocumentId}, SectionId={SectionId}, DurationMs={DurationMs}",
                    actionKey,
                    HttpContext.TraceIdentifier,
                    correlationId,
                    documentId,
                    sectionId,
                    actionTimer.ElapsedMilliseconds);
                return CreateAiProblem(
                    StatusCodes.Status504GatewayTimeout,
                    "AI request timed out",
                    "The AI provider timed out. Try again.",
                    "ai.timeout");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Unhandled AI execution failure for {ActionKey}. TraceId={TraceId}, CorrelationId={CorrelationId}, DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, DurationMs={DurationMs}",
                    actionKey,
                    HttpContext.TraceIdentifier,
                    correlationId,
                    documentId,
                    sectionId,
                    request.PageId,
                    actionTimer.ElapsedMilliseconds);
                return CreateAiProblem(
                    StatusCodes.Status503ServiceUnavailable,
                    "AI service unavailable",
                    "The AI service is currently unavailable. Try again shortly.",
                    "ai.unavailable");
            }

            if (!result.Succeeded || result.Proposal is null)
            {
                string errorCode = string.IsNullOrWhiteSpace(result.ErrorCode) ? "ai.blocked" : result.ErrorCode!;
                string message = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "AI action failed." : result.ErrorMessage!;
                int statusCode = MapBlockedErrorStatusCode(errorCode);
                _logger.LogWarning(
                    "AI action blocked {ActionKey}. TraceId={TraceId}, CorrelationId={CorrelationId}, Code={ErrorCode}, DocumentId={DocumentId}, SectionId={SectionId}, PageId={PageId}, DurationMs={DurationMs}",
                    actionKey,
                    HttpContext.TraceIdentifier,
                    correlationId,
                    errorCode,
                    documentId,
                    sectionId,
                    request.PageId,
                    actionTimer.ElapsedMilliseconds);
                return CreateAiProblem(statusCode, "AI request blocked", message, errorCode, result.ErrorDetails);
            }

            AiProposal proposal = result.Proposal;
            string? summary = string.IsNullOrWhiteSpace(proposal.UserSummary) ? proposal.SummaryLabel : proposal.UserSummary;
            IReadOnlyList<DocumentOutlineNodeDto>? outlineNodes = null;
            string? previewText = null;
            bool? wasTruncated = null;
            SectionSceneCardProposalDto? proposedSceneCard = null;
            string? proposalExplanation = null;
            IReadOnlyList<AiTextOperationDto>? operations = BuildTextOperations(
                proposal.Operations,
                proposal.OriginalText ?? request.OriginalText);

            if (string.Equals(actionKey, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                List<DocumentOutlineNodeDto>? parsed = null;
                if (TryParseOutlineNodes(proposal.ProposedText, out List<DocumentOutlineNodeDto> parsedJson))
                {
                    parsed = parsedJson;
                }
                else if (TryParseOutlineText(proposal.ProposedText, documentId, out List<DocumentOutlineNodeDto> parsedText))
                {
                    parsed = parsedText;
                }

                if (parsed is not null)
                {
                    HashSet<Guid> sectionIds = sectionRecords.Select(section => section.Id).ToHashSet();
                    List<DocumentOutlineNodeDto> normalized =
                        NormalizeOutlineNodes(documentId, parsed, sectionIds);
                    outlineNodes = normalized;
                    previewText = BuildOutlinePreview(normalized);
                }

                wasTruncated = outlineTruncated;
            }
            else if (string.Equals(actionKey, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                if (OutlineDraftParser.TryParse(proposal.ProposedText, out OutlineDraft? outline) && outline is not null)
                {
                    outlineNodes = BuildNodesFromOutlineDraft(documentId, outline);
                    previewText = BuildOutlinePreview(outlineNodes);
                }
            }
            else if (IsSceneCardAction(actionKey))
            {
                if (TryParseSceneCardProposal(proposal.ProposedText, out SectionSceneCardProposalDto? parsed, out string? explanation))
                {
                    proposedSceneCard = parsed;
                    proposalExplanation = explanation;
                }
            }

            var response = new AiActionExecuteResponseDto(
                proposal.ProposalId,
                proposal.OriginalText ?? request.OriginalText,
                proposal.ProposedText,
                summary,
                new DateTimeOffset(proposal.CreatedUtc),
                actionKey,
                outlineNodes,
                previewText,
                wasTruncated,
                proposedSceneCard,
                proposalExplanation,
                operations);

            string requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            string responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            try
            {
                await _historyStore.AddAsync(new AiActionHistoryEntry(
                    proposal.ProposalId,
                    proposal.ActionId,
                    userId,
                    documentId,
                    sectionId,
                    response.CreatedUtc,
                    summary,
                    proposal.OriginalText ?? request.OriginalText,
                    proposal.ProposedText,
                    PageId: request.PageId,
                    ProviderId: proposal.ProviderId,
                    ModelId: null,
                    RequestJson: requestJson,
                    ResultJson: responseJson), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "AI history persistence failed. TraceId={TraceId}, CorrelationId={CorrelationId}, ActionKey={ActionKey}, ProposalId={ProposalId}, DocumentId={DocumentId}, SectionId={SectionId}",
                    HttpContext.TraceIdentifier,
                    correlationId,
                    actionKey,
                    proposal.ProposalId,
                    documentId,
                    sectionId);
            }

            _logger.LogInformation(
                "AI action completed {ActionKey}. TraceId={TraceId}, CorrelationId={CorrelationId}, DocumentId={DocumentId}, SectionId={SectionId}, DurationMs={DurationMs}",
                actionKey,
                HttpContext.TraceIdentifier,
                correlationId,
                documentId,
                sectionId,
                actionTimer.ElapsedMilliseconds);

            return Ok(response);
        }

        private ActionResult<AiActionExecuteResponseDto> CreateAiProblem(
            int statusCode,
            string title,
            string detail,
            string code,
            IReadOnlyDictionary<string, object?>? extra = null)
        {
            Dictionary<string, object?>? mergedExtra = null;
            if (string.Equals(code, "plan_upgrade_required", StringComparison.OrdinalIgnoreCase))
            {
                mergedExtra = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["upgradePath"] = EntitlementDeniedApiError.BuildUpgradePath("ai.actions")
                };
                if (extra is not null)
                {
                    foreach ((string key, object? value) in extra)
                    {
                        mergedExtra[key] = value;
                    }
                }
            }

            ProblemDetails problem = BuildProblemDetails(statusCode, title, detail, code, extra);
            if (mergedExtra is not null)
            {
                problem = BuildProblemDetails(statusCode, title, detail, code, mergedExtra);
            }

            return new ObjectResult(problem)
            {
                StatusCode = statusCode
            };
        }

        private ProblemDetails BuildProblemDetails(
            int statusCode,
            string title,
            string detail,
            string code,
            IReadOnlyDictionary<string, object?>? extra = null)
        {
            ProblemDetails problem = new()
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;
            problem.Extensions["correlationId"] = Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
            if (extra is not null)
            {
                foreach ((string key, object? value) in extra)
                {
                    problem.Extensions[key] = value;
                }
            }
            return problem;
        }

        private static int MapBlockedErrorStatusCode(string errorCode)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
            {
                return StatusCodes.Status400BadRequest;
            }

            return errorCode switch
            {
                "ai.rate_limited" => StatusCodes.Status429TooManyRequests,
                "ai.quota_exceeded" => StatusCodes.Status429TooManyRequests,
                "AI_QUOTA_EXCEEDED" => StatusCodes.Status402PaymentRequired,
                "AI_SUBSCRIPTION_INACTIVE" => StatusCodes.Status402PaymentRequired,
                "plan_upgrade_required" => StatusCodes.Status402PaymentRequired,
                "ai.provider_unavailable" => StatusCodes.Status503ServiceUnavailable,
                "ai.provider_missing" => StatusCodes.Status503ServiceUnavailable,
                "ai.action_missing" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest
            };
        }

        private static (int StatusCode, string ErrorCode, string Detail) MapProviderException(AiProviderException exception)
        {
            string message = exception.Message ?? string.Empty;
            string normalized = message.ToLowerInvariant();

            if (normalized.Contains("api key is not configured", StringComparison.Ordinal)
                || normalized.Contains("status 401", StringComparison.Ordinal)
                || normalized.Contains("status 403", StringComparison.Ordinal))
            {
                return (
                    StatusCodes.Status503ServiceUnavailable,
                    "ai.misconfigured",
                    "AI provider is not configured for this environment.");
            }

            if (normalized.Contains("timed out", StringComparison.Ordinal)
                || exception.InnerException is TimeoutException)
            {
                return (
                    StatusCodes.Status504GatewayTimeout,
                    "ai.timeout",
                    "The AI provider timed out. Try again.");
            }

            if (normalized.Contains("rate limit", StringComparison.Ordinal)
                || normalized.Contains("status 429", StringComparison.Ordinal))
            {
                return (
                    StatusCodes.Status429TooManyRequests,
                    "ai.rate_limited",
                    "AI rate limit reached. Try again in a moment.");
            }

            if (normalized.Contains("quota", StringComparison.Ordinal))
            {
                return (
                    StatusCodes.Status429TooManyRequests,
                    "ai.quota_exceeded",
                    "AI quota exceeded for this account.");
            }

            if (normalized.Contains("status 400", StringComparison.Ordinal)
                || normalized.Contains("context length", StringComparison.Ordinal)
                || normalized.Contains("too many tokens", StringComparison.Ordinal)
                || normalized.Contains("invalid request", StringComparison.Ordinal))
            {
                return (
                    StatusCodes.Status400BadRequest,
                    "ai.invalid_request",
                    "AI request was invalid or too large.");
            }

            return (
                StatusCodes.Status503ServiceUnavailable,
                "ai.provider_error",
                "AI provider request failed.");
        }

        private static bool RequiresSectionText(string actionKey)
        {
            return IsSceneCardAction(actionKey)
                || string.Equals(actionKey, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal);
        }

        private static string ResolveSectionTextForValidation(Document document, Guid sectionId, string? sectionTextOverride)
        {
            if (!string.IsNullOrWhiteSpace(sectionTextOverride))
            {
                return sectionTextOverride.Trim();
            }

            Section? section = document.Chapters
                .SelectMany(chapter => chapter.Sections)
                .FirstOrDefault(item => item.SectionId == sectionId);
            return PlainTextMapper.ToPlainText(section?.Content.Value ?? string.Empty).Trim();
        }

        private static IReadOnlyList<AiTextOperationDto>? BuildTextOperations(
            IReadOnlyList<ProposedOperation>? proposalOperations,
            string? originalText)
        {
            if (proposalOperations is null || proposalOperations.Count == 0)
            {
                return null;
            }

            List<AiTextOperationDto> operations = new();
            foreach (ProposedOperation operation in proposalOperations)
            {
                if (operation is not ReplaceTextRangeOperation replace)
                {
                    continue;
                }

                int from = Math.Max(0, replace.Range.Start);
                int to = Math.Max(from, replace.Range.Start + Math.Max(0, replace.Range.Length));
                string? expected = TryExtractExpectedText(originalText, from, to);
                operations.Add(new AiTextOperationDto("replace", from, to, replace.NewText, expected));
            }

            return operations.Count == 0 ? null : operations;
        }

        private static string? TryExtractExpectedText(string? source, int from, int to)
        {
            if (string.IsNullOrEmpty(source))
            {
                return null;
            }

            int start = Math.Clamp(from, 0, source.Length);
            int end = Math.Clamp(to, start, source.Length);
            if (end <= start)
            {
                return string.Empty;
            }

            return source.Substring(start, end - start);
        }

        private static IReadOnlyList<string> BuildRequiredInputs(IAiAction action)
        {
            List<string> inputs = new() { "documentId" };
            if (!string.Equals(action.ActionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal)
                && !string.Equals(action.ActionId, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                inputs.Add("sectionId");
            }

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
            DocumentSynopsisRecord? synopsisRecord,
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
                    Language = string.IsNullOrWhiteSpace(record.LanguageCode) ? "en" : record.LanguageCode,
                    CreatedUtc = record.CreatedAt.UtcDateTime,
                    ModifiedUtc = record.UpdatedAt.UtcDateTime
                },
                Chapters = new List<Chapter> { chapter },
                Synopsis = MapSynopsis(synopsisRecord)
            };
        }

        private async Task<(Document Document, bool Truncated)> BuildAiOutlineDocumentAsync(
            DocumentRecord record,
            IReadOnlyList<SectionRecord> sections,
            string ownerUserId,
            CancellationToken ct)
        {
            bool truncated = false;
            List<Section> domainSections = new();
            foreach (SectionRecord sectionRecord in sections.OrderBy(section => section.OrderIndex).Take(OutlineMaxSections))
            {
                IReadOnlyList<PageRecord> pages = await _pages.ListBySectionAsync(sectionRecord.Id, ownerUserId, ct);
                string html = string.Join("\n\n", pages.Select(page => page.Content ?? string.Empty));
                string plain = PlainTextMapper.ToPlainText(html);
                if (plain.Length > OutlineMaxSectionChars)
                {
                    truncated = true;
                    plain = plain.Substring(0, OutlineMaxSectionChars);
                }

                domainSections.Add(new Section
                {
                    SectionId = sectionRecord.Id,
                    Order = sectionRecord.OrderIndex,
                    Title = sectionRecord.Title,
                    Content = new SectionContent
                    {
                        Format = "text",
                        Value = plain
                    },
                    Notes = sectionRecord.NarrativePurpose ?? string.Empty,
                    CreatedUtc = sectionRecord.CreatedAt.UtcDateTime,
                    ModifiedUtc = sectionRecord.UpdatedAt.UtcDateTime
                });
            }

            if (sections.Count > OutlineMaxSections)
            {
                truncated = true;
            }

            Chapter chapter = new()
            {
                Order = 0,
                Title = string.IsNullOrWhiteSpace(record.Title) ? "Draft" : record.Title,
                Sections = domainSections
            };

            Document doc = new()
            {
                DocumentId = record.Id,
                Metadata = new DocumentMetadata
                {
                    Title = record.Title,
                    Language = string.IsNullOrWhiteSpace(record.LanguageCode) ? "en" : record.LanguageCode,
                    CreatedUtc = record.CreatedAt.UtcDateTime,
                    ModifiedUtc = record.UpdatedAt.UtcDateTime
                },
                Chapters = new List<Chapter> { chapter }
            };

            return (doc, truncated);
        }

        private static Synopsis MapSynopsis(DocumentSynopsisRecord? record)
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

        private static List<DocumentOutlineNodeDto> BuildNodesFromOutlineDraft(Guid documentId, OutlineDraft outline)
        {
            List<DocumentOutlineNodeDto> nodes = new();
            for (int i = 0; i < outline.Items.Count; i++)
            {
                OutlineItemDraft item = outline.Items[i];
                string notes = item.Summary;
                if (item.Beats.Count > 0)
                {
                    string beats = string.Join('\n', item.Beats.Select(beat => $"- {beat}"));
                    notes = string.IsNullOrWhiteSpace(notes) ? beats : $"{notes}\n{beats}";
                }

                nodes.Add(new DocumentOutlineNodeDto(
                    Guid.NewGuid(),
                    documentId,
                    null,
                    i,
                    string.IsNullOrWhiteSpace(item.Title) ? $"Item {i + 1}" : item.Title.Trim(),
                    string.IsNullOrWhiteSpace(notes) ? null : notes,
                    null,
                    JsonSerializer.Serialize(new
                    {
                        purpose = item.StoryRole,
                        emotionalBeat = item.Summary,
                        keyEvents = item.Beats,
                        openQuestions = Array.Empty<string>(),
                        povCharacterId = string.IsNullOrWhiteSpace(item.Pov) ? null : item.Pov,
                        placeId = string.IsNullOrWhiteSpace(item.Setting) ? null : item.Setting,
                        timeRef = string.Empty,
                        tags = Array.Empty<string>()
                    })));
            }

            return nodes;
        }

        private static bool TryParseOutlineNodes(string? json, out List<DocumentOutlineNodeDto> nodes)
        {
            nodes = new List<DocumentOutlineNodeDto>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                if (TryExtractJsonPayload(json, out string payload))
                {
                    using JsonDocument doc = JsonDocument.Parse(payload);
                    JsonElement root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Array)
                    {
                        ParseNodeArray(root, null, nodes);
                        return nodes.Count > 0;
                    }

                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        if (TryGetProperty(root, "nodes", out JsonElement nodesElement)
                            || TryGetProperty(root, "outline", out nodesElement)
                            || TryGetProperty(root, "items", out nodesElement)
                            || TryGetProperty(root, "children", out nodesElement))
                        {
                            if (nodesElement.ValueKind == JsonValueKind.Array)
                            {
                                ParseNodeArray(nodesElement, null, nodes);
                                return nodes.Count > 0;
                            }
                        }

                        ParseNodeObject(root, null, nodes, 0);
                        return nodes.Count > 0;
                    }
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool IsSceneCardAction(string actionKey)
        {
            return string.Equals(actionKey, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionKey, SceneRefineAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionKey, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal);
        }

        private static bool RequiresSceneMetadata(string actionKey)
        {
            return IsSceneCardAction(actionKey)
                || string.Equals(actionKey, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal);
        }

        private static bool TryParseSceneCardProposal(
            string? json,
            out SectionSceneCardProposalDto? proposal,
            out string? explanation)
        {
            proposal = null;
            explanation = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                JsonElement payload = TryGetObject(root, "sceneCard", out JsonElement sceneCard)
                    ? sceneCard
                    : root;

                string? narrativePurpose = GetFirstNullableString(payload, "narrativePurpose", "narrative_purpose");
                string? emotionalBeat = GetFirstNullableString(payload, "emotionalBeat", "emotional_beat");
                string? keyEvents = GetFirstNullableString(payload, "keyEvents", "key_events");
                string? openQuestions = GetFirstNullableString(payload, "openQuestions", "open_questions");
                string? povCharacterId = GetFirstNullableString(payload, "povCharacterId", "povCharacter", "pov_character_id", "pov");
                string? placeId = GetFirstNullableString(payload, "placeId", "settingPlace", "setting", "location", "place_id");
                string? timelineEventId = GetFirstNullableString(payload, "timelineEventId", "timeline_event_id", "eventId");
                string? timeRef = GetFirstNullableString(payload, "timeRef", "timelineMarker", "timeline_marker", "time_ref");
                List<string> tags = GetStringArray(payload, "tags");
                if (tags.Count == 0)
                {
                    string? tagsText = GetFirstNullableString(payload, "tags", "tagsCsv", "tagList", "sceneTags");
                    if (!string.IsNullOrWhiteSpace(tagsText))
                    {
                        tags = ParseCsvList(tagsText);
                    }
                }

                List<SceneCardReferenceDto> references = GetReferenceArray(payload, "references");
                explanation = GetFirstNullableString(root, "explanation", "reasoning", "summary");

                proposal = new SectionSceneCardProposalDto(
                    narrativePurpose ?? string.Empty,
                    emotionalBeat ?? string.Empty,
                    keyEvents ?? string.Empty,
                    openQuestions ?? string.Empty,
                    povCharacterId ?? string.Empty,
                    placeId ?? string.Empty,
                    timelineEventId ?? string.Empty,
                    timeRef ?? string.Empty,
                    tags,
                    references);

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string? GetNullableString(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!TryGetPropertyIgnoreCase(element, propertyName, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => value.GetRawText()
            };
        }

        private static string? GetFirstNullableString(JsonElement element, params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                string? value = GetNullableString(element, propertyName);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        private static List<string> GetStringArray(JsonElement element, string propertyName)
        {
            List<string> values = new();
            if (element.ValueKind != JsonValueKind.Object
                || !TryGetPropertyIgnoreCase(element, propertyName, out JsonElement value)
                || value.ValueKind != JsonValueKind.Array)
            {
                return values;
            }

            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    string? text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        values.Add(text.Trim());
                    }
                }
            }

            return values;
        }

        private static List<SceneCardReferenceDto> GetReferenceArray(JsonElement element, string propertyName)
        {
            List<SceneCardReferenceDto> values = new();
            if (element.ValueKind != JsonValueKind.Object
                || !TryGetPropertyIgnoreCase(element, propertyName, out JsonElement value)
                || value.ValueKind != JsonValueKind.Array)
            {
                return values;
            }

            foreach (JsonElement item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string? kind = GetNullableString(item, "kind");
                string? targetId = GetNullableString(item, "targetId");
                if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(targetId))
                {
                    continue;
                }

                values.Add(new SceneCardReferenceDto(kind.Trim(), targetId.Trim(), GetNullableString(item, "note")));
            }

            return values;
        }

        private static bool TryGetObject(JsonElement element, string propertyName, out JsonElement value)
        {
            value = default;
            return element.ValueKind == JsonValueKind.Object
                && TryGetPropertyIgnoreCase(element, propertyName, out value)
                && value.ValueKind == JsonValueKind.Object;
        }

        private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
        {
            value = default;
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            return false;
        }

        private static List<string> ParseCsvList(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new List<string>();
            }

            return text
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryExtractJsonPayload(string input, out string payload)
        {
            payload = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string trimmed = input.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLineBreak = trimmed.IndexOf('\n');
                if (firstLineBreak >= 0)
                {
                    int endFence = trimmed.IndexOf("```", firstLineBreak + 1, StringComparison.Ordinal);
                    if (endFence > firstLineBreak)
                    {
                        trimmed = trimmed.Substring(firstLineBreak + 1, endFence - firstLineBreak - 1).Trim();
                    }
                }
            }

            int startArray = trimmed.IndexOf('[');
            int startObject = trimmed.IndexOf('{');
            int start;
            char open;
            char close;

            if (startArray == -1 && startObject == -1)
            {
                return false;
            }

            if (startArray >= 0 && (startObject == -1 || startArray < startObject))
            {
                start = startArray;
                open = '[';
                close = ']';
            }
            else
            {
                start = startObject;
                open = '{';
                close = '}';
            }

            int end = FindMatchingJsonEnd(trimmed, start, open, close);
            if (end <= start)
            {
                return false;
            }

            payload = trimmed.Substring(start, end - start + 1).Trim();
            return payload.Length > 0;
        }

        private static int FindMatchingJsonEnd(string text, int startIndex, char open, char close)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = startIndex; i < text.Length; i++)
            {
                char ch = text[i];

                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (ch == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == open)
                {
                    depth++;
                }
                else if (ch == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private static void ParseNodeArray(
            JsonElement array,
            Guid? parentId,
            List<DocumentOutlineNodeDto> nodes)
        {
            if (array.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            int order = 0;
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    ParseNodeObject(item, parentId, nodes, order++);
                }
                else if (item.ValueKind == JsonValueKind.String)
                {
                    string? title = item.GetString();
                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        nodes.Add(new DocumentOutlineNodeDto(
                            Guid.Empty,
                            Guid.Empty,
                            parentId,
                            order++,
                            title.Trim(),
                            null,
                            null));
                    }
                }
            }
        }

        private static void ParseNodeObject(
            JsonElement nodeElement,
            Guid? parentId,
            List<DocumentOutlineNodeDto> nodes,
            int order)
        {
            string title = GetString(nodeElement, "title")
                           ?? GetString(nodeElement, "name")
                           ?? GetString(nodeElement, "label")
                           ?? "Outline node";

            Guid id = GetGuid(nodeElement, "id");
            Guid? linkedSectionId = GetNullableGuid(nodeElement, "linkedSectionId")
                                    ?? GetNullableGuid(nodeElement, "linked_section_id")
                                    ?? GetNullableGuid(nodeElement, "sectionId");

            string? notes = GetString(nodeElement, "notes");

            if (id == Guid.Empty)
            {
                id = Guid.NewGuid();
            }

            nodes.Add(new DocumentOutlineNodeDto(
                id,
                Guid.Empty,
                parentId,
                GetInt(nodeElement, "order", order),
                title.Trim(),
                string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                linkedSectionId));

            if (TryGetProperty(nodeElement, "children", out JsonElement childrenElement)
                && childrenElement.ValueKind == JsonValueKind.Array)
            {
                ParseNodeArray(childrenElement, id, nodes);
            }
        }

        private static string? GetString(JsonElement element, string name)
        {
            if (TryGetProperty(element, name, out JsonElement value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static Guid GetGuid(JsonElement element, string name)
        {
            if (TryGetProperty(element, name, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.String
                    && Guid.TryParse(value.GetString(), out Guid parsed))
                {
                    return parsed;
                }
            }

            return Guid.Empty;
        }

        private static Guid? GetNullableGuid(JsonElement element, string name)
        {
            if (TryGetProperty(element, name, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.String
                    && Guid.TryParse(value.GetString(), out Guid parsed))
                {
                    return parsed;
                }

                if (value.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }
            }

            return null;
        }

        private static int GetInt(JsonElement element, string name, int fallback)
        {
            if (TryGetProperty(element, name, out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int parsed))
                {
                    return parsed;
                }

                if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed))
                {
                    return parsed;
                }
            }

            return fallback;
        }

        private static bool TryParseOutlineText(
            string? text,
            Guid documentId,
            out List<DocumentOutlineNodeDto> nodes)
        {
            nodes = new List<DocumentOutlineNodeDto>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            List<(Guid Id, Guid? ParentId, int Depth)> stack = new();
            Dictionary<Guid, int> orderByParent = new();

            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (string rawLine in lines)
            {
                if (string.IsNullOrWhiteSpace(rawLine))
                {
                    continue;
                }

                int depth = 0;
                int index = 0;
                while (index < rawLine.Length)
                {
                    char ch = rawLine[index];
                    if (ch == ' ')
                    {
                        depth++;
                        index++;
                        continue;
                    }

                    if (ch == '\t')
                    {
                        depth += 2;
                        index++;
                        continue;
                    }

                    break;
                }

                string trimmed = rawLine.Trim();
                trimmed = TrimListMarker(trimmed);
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                int level = depth / 2;
                if (level < 0)
                {
                    level = 0;
                }

                while (stack.Count > level)
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                Guid? parentId = stack.Count == 0 ? null : stack[^1].Id;
                Guid parentKey = parentId ?? Guid.Empty;
                int order = orderByParent.TryGetValue(parentKey, out int current) ? current : 0;
                orderByParent[parentKey] = order + 1;

                Guid id = Guid.NewGuid();
                nodes.Add(new DocumentOutlineNodeDto(
                    id,
                    documentId,
                    parentId,
                    order,
                    trimmed,
                    null,
                    null));

                stack.Add((id, parentId, level));
            }

            return nodes.Count > 0;
        }

        private static string TrimListMarker(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            string trimmed = text.TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal)
                || trimmed.StartsWith("* ", StringComparison.Ordinal)
                || trimmed.StartsWith("• ", StringComparison.Ordinal))
            {
                return trimmed.Substring(2).Trim();
            }

            int dotIndex = trimmed.IndexOf('.');
            if (dotIndex > 0 && dotIndex <= 3)
            {
                bool numeric = true;
                for (int i = 0; i < dotIndex; i++)
                {
                    if (!char.IsDigit(trimmed[i]))
                    {
                        numeric = false;
                        break;
                    }
                }

                if (numeric && dotIndex + 1 < trimmed.Length && trimmed[dotIndex + 1] == ' ')
                {
                    return trimmed.Substring(dotIndex + 2).Trim();
                }
            }

            return trimmed.Trim();
        }

        private static List<DocumentOutlineNodeDto> NormalizeOutlineNodes(
            Guid documentId,
            IEnumerable<DocumentOutlineNodeDto> nodes,
            HashSet<Guid> validSectionIds)
        {
            List<DocumentOutlineNodeDto> sanitized = new();
            Dictionary<Guid, Guid> idMap = new();
            HashSet<Guid> usedIds = new();

            foreach (DocumentOutlineNodeDto node in nodes)
            {
                Guid id = node.Id;
                if (id == Guid.Empty || usedIds.Contains(id))
                {
                    id = Guid.NewGuid();
                }

                usedIds.Add(id);
                if (node.Id != Guid.Empty && !idMap.ContainsKey(node.Id))
                {
                    idMap[node.Id] = id;
                }

                Guid? parentId = null;
                if (node.ParentId.HasValue && idMap.TryGetValue(node.ParentId.Value, out Guid mappedParent))
                {
                    parentId = mappedParent;
                }

                Guid? linkedSectionId = node.LinkedSectionId.HasValue && validSectionIds.Contains(node.LinkedSectionId.Value)
                    ? node.LinkedSectionId
                    : null;

                string title = string.IsNullOrWhiteSpace(node.Title) ? "Outline node" : node.Title.Trim();
                string? notes = string.IsNullOrWhiteSpace(node.Notes) ? null : node.Notes.Trim();

                sanitized.Add(new DocumentOutlineNodeDto(
                    id,
                    documentId,
                    parentId,
                    Math.Max(0, node.Order),
                    title,
                    notes,
                    linkedSectionId));
            }

            List<DocumentOutlineNodeDto> ordered = new();
            foreach (IGrouping<Guid?, DocumentOutlineNodeDto> group in sanitized.GroupBy(node => node.ParentId))
            {
                int index = 0;
                foreach (DocumentOutlineNodeDto node in group.OrderBy(node => node.Order))
                {
                    ordered.Add(node with { Order = index++ });
                }
            }

            return ordered;
        }

        private static string BuildOutlinePreview(IEnumerable<DocumentOutlineNodeDto> nodes)
        {
            List<DocumentOutlineNodeDto> roots = nodes
                .Where(node => node.ParentId is null)
                .OrderBy(node => node.Order)
                .ToList();

            Dictionary<Guid, List<DocumentOutlineNodeDto>> byParent = nodes
                .Where(node => node.ParentId.HasValue)
                .GroupBy(node => node.ParentId!.Value)
                .ToDictionary(
                    group => group.Key,
                    group => group.OrderBy(node => node.Order).ToList());

            List<string> lines = new();
            void Walk(Guid? parentId, int depth)
            {
                List<DocumentOutlineNodeDto> children;
                if (parentId is null)
                {
                    children = roots;
                }
                else if (!byParent.TryGetValue(parentId.Value, out children))
                {
                    return;
                }

                foreach (DocumentOutlineNodeDto child in children)
                {
                    string indent = new string(' ', depth * 2);
                    lines.Add($"{indent}- {child.Title}");
                    Walk(child.Id, depth + 1);
                }
            }

            Walk(null, 0);
            return string.Join(Environment.NewLine, lines);
        }

        private static string? GetInstruction(Dictionary<string, object?>? parameters)
        {
            if (parameters is null || !parameters.TryGetValue("instruction", out object? value) || value is null)
            {
                return null;
            }

            return value.ToString();
        }

        private async Task<ActionResult?> EnsureFeatureAllowedAsync(string userId, FeatureKey feature, string featureCode)
        {
            UserEntitlements entitlements = await _entitlementService.GetEntitlementsAsync(userId);
            PlanTier userTier = _entitlementService.GetUserTier(entitlements);
            if (FeatureRegistry.IsFeatureAllowed(feature, userTier))
            {
                return null;
            }

            PlanTier requiredTier = FeatureRegistry.FeatureMinimumTier[feature];
            _logger.LogInformation(
                "FeatureAccessDenied FeatureKey={FeatureKey} UserTier={UserTier} RequiredTier={RequiredTier}",
                feature,
                userTier,
                requiredTier);

            ProblemDetails problem = EntitlementDeniedApiError.ForFeature(
                featureCode,
                $"Available in {requiredTier} plan.");
            problem.Extensions["code"] = "entitlement_denied";
            problem.Extensions["traceId"] = HttpContext.TraceIdentifier;

            ObjectResult result = new(problem)
            {
                StatusCode = StatusCodes.Status402PaymentRequired
            };
            result.ContentTypes.Add("application/problem+json");
            return result;
        }

        private static FeatureKey? ResolveFeatureForAction(string actionKey)
        {
            return actionKey switch
            {
                RewriteSelectionAction.ActionIdValue => FeatureKey.RewriteSelection,
                TranslateSelectionAction.ActionIdValue => FeatureKey.TranslateText,
                TranslateSectionAction.ActionIdValue => FeatureKey.TranslateText,
                TranslateDocumentAction.ActionIdValue => FeatureKey.TranslateText,
                ProposeNextParagraphAction.ActionIdValue => FeatureKey.NextParagraph,
                StoryCoachAction.ActionIdValue => FeatureKey.StoryCoach,
                GenerateOutlineAction.ActionIdValue => FeatureKey.GenerateOutline,
                GenerateOutlineFromSynopsisAction.ActionIdValue => FeatureKey.GenerateOutline,
                SceneSuggestAction.ActionIdValue => FeatureKey.SceneAiSuggestions,
                SceneRefineAction.ActionIdValue => FeatureKey.SceneAiSuggestions,
                SceneFindOpenQuestionsAction.ActionIdValue => FeatureKey.SceneAiSuggestions,
                "custom_transform" => FeatureKey.PromptLibrary,
                "expand.selection" => FeatureKey.AdvancedReviseTools,
                "expand.section" => FeatureKey.AdvancedReviseTools,
                "tighten.selection" => FeatureKey.AdvancedReviseTools,
                "tighten.section" => FeatureKey.AdvancedReviseTools,
                "change_tone.selection" => FeatureKey.AdvancedReviseTools,
                "change_tone.section" => FeatureKey.AdvancedReviseTools,
                "show_dont_tell.selection" => FeatureKey.AdvancedReviseTools,
                "show_dont_tell.section" => FeatureKey.AdvancedReviseTools,
                _ => null
            };
        }

        public sealed record AiActionAppliedRequest(
            Guid? DocumentId,
            Guid? SectionId,
            Guid? PageId,
            string? BeforeContent,
            string? AfterContent);
    }
}
