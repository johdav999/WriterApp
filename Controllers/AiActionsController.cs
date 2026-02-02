using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI;
using WriterApp.Application.Commands;
using WriterApp.Application.Documents;
using WriterApp.Application.Security;
using WriterApp.Application.State;
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
        private readonly IAiActionHistoryStore _historyStore;
        private readonly IPageVersionService _pageVersions;
        private const int OutlineMaxSectionChars = 2000;
        private const int OutlineMaxSections = 60;
        private const int SceneMaxSectionChars = 4000;

        public AiActionsController(
            IAiOrchestrator orchestrator,
            IDocumentRepository documents,
            ISectionRepository sections,
            IPageRepository pages,
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IAiActionHistoryStore historyStore,
            IPageVersionService pageVersions)
        {
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _documents = documents ?? throw new ArgumentNullException(nameof(documents));
            _sections = sections ?? throw new ArgumentNullException(nameof(sections));
            _pages = pages ?? throw new ArgumentNullException(nameof(pages));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _historyStore = historyStore ?? throw new ArgumentNullException(nameof(historyStore));
            _pageVersions = pageVersions ?? throw new ArgumentNullException(nameof(pageVersions));
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
                    entry.AppliedCount))
                .ToList();

            return Ok(result);
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
                        await _pageVersions.CreateSnapshotAsync(
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

            IReadOnlyList<SectionRecord> sectionRecords = await _sections.ListByDocumentAsync(documentId, userId, ct);
            if (sectionRecords.Count == 0)
            {
                return BadRequest(new { message = "document has no sections." });
            }

            if (string.Equals(actionKey, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
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
                return BadRequest(new { message = "Selection range is required for this action." });
            }
            if (action.RequiresSelection && string.IsNullOrWhiteSpace(request.OriginalText))
            {
                return BadRequest(new { message = "originalText is required for this action." });
            }

            bool outlineTruncated = false;
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
                aiDocument = await BuildAiDocumentAsync(documentRecord, sectionRecords, userId, ct);
            }
            TextRange selectionRange = BuildSelectionRange(request);
            string selectedText = request.OriginalText ?? string.Empty;
            string? instruction = GetInstruction(request.Parameters);

            Dictionary<string, object?> options = request.Parameters is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(request.Parameters);
            if (string.Equals(actionKey, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                options["max_section_chars"] = OutlineMaxSectionChars;
                options["max_sections"] = OutlineMaxSections;
                options["truncated"] = outlineTruncated;
            }
            if (RequiresSceneMetadata(actionKey))
            {
                SectionSceneCardRecord? sceneCard = await _dbContext.SectionSceneCards
                    .FindAsync(new object?[] { sectionId }, ct);
                options["narrative_purpose"] = sceneCard?.NarrativePurpose ?? string.Empty;
                options["emotional_beat"] = sceneCard?.EmotionalBeat ?? string.Empty;
                options["key_events"] = sceneCard?.KeyEvents ?? string.Empty;
                options["open_questions"] = sceneCard?.OpenQuestions ?? string.Empty;
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

            AiExecutionResult result = await _orchestrator.ExecuteActionAsync(actionKey, input, ct);
            if (!result.Succeeded || result.Proposal is null)
            {
                string message = result.ErrorMessage ?? "AI action failed.";
                return BadRequest(new { message });
            }

            AiProposal proposal = result.Proposal;
            string? summary = string.IsNullOrWhiteSpace(proposal.UserSummary) ? proposal.SummaryLabel : proposal.UserSummary;
            IReadOnlyList<DocumentOutlineNodeDto>? outlineNodes = null;
            string? previewText = null;
            bool? wasTruncated = null;
            SectionSceneCardProposalDto? proposedSceneCard = null;
            string? proposalExplanation = null;

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
                proposalExplanation);

            string requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            string responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web));

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

            return Ok(response);
        }

        private static IReadOnlyList<string> BuildRequiredInputs(IAiAction action)
        {
            List<string> inputs = new() { "documentId" };
            if (!string.Equals(action.ActionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
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
                Chapters = new List<Chapter> { chapter }
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

                string? narrativePurpose = GetNullableString(root, "narrativePurpose");
                string? emotionalBeat = GetNullableString(root, "emotionalBeat");
                string? keyEvents = GetNullableString(root, "keyEvents");
                string? openQuestions = GetNullableString(root, "openQuestions");
                explanation = GetNullableString(root, "explanation");

                proposal = new SectionSceneCardProposalDto(
                    narrativePurpose ?? string.Empty,
                    emotionalBeat ?? string.Empty,
                    keyEvents ?? string.Empty,
                    openQuestions ?? string.Empty);

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

            if (!element.TryGetProperty(propertyName, out JsonElement value))
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

        public sealed record AiActionAppliedRequest(
            Guid? DocumentId,
            Guid? SectionId,
            Guid? PageId,
            string? BeforeContent,
            string? AfterContent);
    }
}
