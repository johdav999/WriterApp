using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.AI.StoryCoach;
using WriterApp.Application.Commands;
using WriterApp.Application.Synopsis;

namespace WriterApp.AI.Core
{
    public sealed class AiActionExecutor : IAiActionExecutor
    {
        private readonly IAiRouter _router;
        private readonly IArtifactStore _artifactStore;
        private readonly ILogger<AiActionExecutor> _logger;

        public AiActionExecutor(IAiRouter router, IArtifactStore artifactStore, ILogger<AiActionExecutor> logger)
        {
            _router = router ?? throw new ArgumentNullException(nameof(router));
            _artifactStore = artifactStore ?? throw new ArgumentNullException(nameof(artifactStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AiExecutionOutcome> ExecuteAsync(IAiAction action, AiActionInput input, CancellationToken ct)
        {
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            AiRequest request = action.BuildRequest(input);
            if (string.Equals(action.ActionId, ApplyContinuityFixAction.ActionIdValue, StringComparison.Ordinal))
            {
                return BuildApplyContinuityFixOutcome(action, input, request);
            }

            AiProviderSelection selection = _router.Route(request);
            AiResult result = string.Equals(action.ActionId, ContinuityCheckAction.ActionIdValue, StringComparison.Ordinal)
                ? await ExecuteContinuityCheckWithChunkCoverageAsync(request, selection.Provider, ct)
                : await selection.Provider.ExecuteAsync(request, ct);
            return BuildOutcome(action, input, request, result, selection.SelectedProviderId);
        }

        private async Task<AiResult> ExecuteContinuityCheckWithChunkCoverageAsync(
            AiRequest request,
            IAiProvider provider,
            CancellationToken ct)
        {
            const int chunkSize = 3200;
            const int overlap = 400;
            const int splitThreshold = 4500;
            const int maxMergedIssues = 60;

            string fullText = GetInputValue(request, "section_text");
            if (string.IsNullOrWhiteSpace(fullText) || fullText.Length <= splitThreshold)
            {
                return await provider.ExecuteAsync(request, ct);
            }

            List<ContinuityChunk> chunks = BuildContinuityChunks(fullText, chunkSize, overlap);
            if (chunks.Count <= 1)
            {
                return await provider.ExecuteAsync(request, ct);
            }

            int totalInputTokens = 0;
            int totalOutputTokens = 0;
            TimeSpan totalLatency = TimeSpan.Zero;
            string schemaVersion = "1.0";
            AiResult? firstResult = null;
            AiArtifact? firstTextArtifact = null;
            Dictionary<string, ContinuityIssue> mergedIssues = new(StringComparer.Ordinal);

            foreach (ContinuityChunk chunk in chunks)
            {
                Dictionary<string, object> chunkInputs = new(request.Inputs)
                {
                    ["section_text"] = chunk.Text
                };

                AiRequest chunkRequest = request with
                {
                    RequestId = Guid.NewGuid(),
                    Context = request.Context with
                    {
                        Range = new TextRange(0, chunk.Text.Length),
                        OriginalText = chunk.Text,
                        SelectionText = chunk.Text,
                        SelectionStart = 0,
                        SelectionLength = chunk.Text.Length
                    },
                    Inputs = chunkInputs
                };

                AiResult chunkResult = await provider.ExecuteAsync(chunkRequest, ct);
                firstResult ??= chunkResult;
                firstTextArtifact ??= chunkResult.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);

                totalInputTokens += chunkResult.Usage.InputTokens;
                totalOutputTokens += chunkResult.Usage.OutputTokens;
                totalLatency += chunkResult.Usage.Latency;

                AiArtifact? textArtifact = chunkResult.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                if (textArtifact is null || string.IsNullOrWhiteSpace(textArtifact.TextContent))
                {
                    continue;
                }

                if (!TryParseContinuityReport(textArtifact.TextContent, out ContinuityReport? report) || report is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(report.SchemaVersion))
                {
                    schemaVersion = report.SchemaVersion;
                }

                foreach (ContinuityIssue issue in report.Issues ?? new List<ContinuityIssue>())
                {
                    ContinuityIssue adjustedIssue = NormalizeContinuityIssue(issue, fullText, chunk.Start);

                    string key = BuildContinuityIssueMergeKey(adjustedIssue);
                    if (!mergedIssues.TryGetValue(key, out ContinuityIssue? existing))
                    {
                        mergedIssues[key] = adjustedIssue;
                        continue;
                    }

                    // Keep the higher-severity duplicate when chunk overlap returns the same issue.
                    if (GetContinuitySeverityRank(adjustedIssue.Severity) > GetContinuitySeverityRank(existing.Severity))
                    {
                        mergedIssues[key] = adjustedIssue;
                    }
                }
            }

            if (firstResult is null || firstTextArtifact is null || mergedIssues.Count == 0)
            {
                return await provider.ExecuteAsync(request, ct);
            }

            List<ContinuityIssue> sortedIssues = mergedIssues.Values
                .OrderByDescending(issue => GetContinuitySeverityRank(issue.Severity))
                .ThenBy(issue => issue.Anchor.PlainTextStart)
                .Take(maxMergedIssues)
                .ToList();

            string mergedJson = JsonSerializer.Serialize(new ContinuityReport(schemaVersion, sortedIssues));
            AiArtifact mergedArtifact = new(
                Guid.NewGuid(),
                AiModality.Text,
                firstTextArtifact.MimeType ?? "application/json",
                mergedJson,
                null,
                firstTextArtifact.Metadata);

            Dictionary<string, object> mergedMeta = new(firstResult.ProviderMeta)
            {
                ["continuity_chunk_count"] = chunks.Count,
                ["continuity_chunked"] = true
            };

            _logger.LogInformation(
                "[Continuity] Chunked section check ran {ChunkCount} chunks and produced {IssueCount} merged issues.",
                chunks.Count,
                sortedIssues.Count);

            return new AiResult(
                request.RequestId,
                new List<AiArtifact> { mergedArtifact },
                new AiUsage(totalInputTokens, totalOutputTokens, totalLatency),
                mergedMeta);
        }

        private static List<ContinuityChunk> BuildContinuityChunks(string text, int chunkSize, int overlap)
        {
            List<ContinuityChunk> chunks = new();
            if (string.IsNullOrEmpty(text))
            {
                return chunks;
            }

            int safeChunk = Math.Max(512, chunkSize);
            int safeOverlap = Math.Clamp(overlap, 0, safeChunk / 2);
            int step = safeChunk - safeOverlap;
            int start = 0;

            while (start < text.Length)
            {
                int length = Math.Min(safeChunk, text.Length - start);
                chunks.Add(new ContinuityChunk(start, text.Substring(start, length)));
                if (start + length >= text.Length)
                {
                    break;
                }

                start += step;
            }

            return chunks;
        }

        private static bool TryParseContinuityReport(string raw, out ContinuityReport? report)
        {
            report = null;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            foreach (string candidate in EnumerateContinuityJsonCandidates(raw))
            {
                if (!IsStrictContinuityReportJson(candidate))
                {
                    continue;
                }

                try
                {
                    ContinuityReport? parsed = JsonSerializer.Deserialize<ContinuityReport>(candidate);
                    if (parsed?.Issues is null)
                    {
                        continue;
                    }

                    report = parsed;
                    return true;
                }
                catch (JsonException)
                {
                    // Ignore invalid fragments and continue trying other candidates.
                }
            }

            return false;
        }

        private static bool IsStrictContinuityReportJson(string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return false;
                }

                HashSet<string> rootAllowed = new(StringComparer.Ordinal)
                {
                    "schemaVersion",
                    "issues"
                };

                foreach (JsonProperty property in doc.RootElement.EnumerateObject())
                {
                    if (!rootAllowed.Contains(property.Name))
                    {
                        return false;
                    }
                }

                if (!doc.RootElement.TryGetProperty("issues", out JsonElement issuesElement) || issuesElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                HashSet<string> issueAllowed = new(StringComparer.Ordinal)
                {
                    "severity",
                    "type",
                    "message",
                    "evidence",
                    "suggestedFix",
                    "anchor"
                };
                HashSet<string> evidenceAllowed = new(StringComparer.Ordinal) { "sectionId", "quote" };
                HashSet<string> anchorAllowed = new(StringComparer.Ordinal) { "plainTextStart", "plainTextLength" };

                foreach (JsonElement issue in issuesElement.EnumerateArray())
                {
                    if (issue.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }

                    foreach (JsonProperty property in issue.EnumerateObject())
                    {
                        if (!issueAllowed.Contains(property.Name))
                        {
                            return false;
                        }
                    }

                    if (!issue.TryGetProperty("evidence", out JsonElement evidenceElement) || evidenceElement.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }

                    foreach (JsonProperty property in evidenceElement.EnumerateObject())
                    {
                        if (!evidenceAllowed.Contains(property.Name))
                        {
                            return false;
                        }
                    }

                    if (!issue.TryGetProperty("anchor", out JsonElement anchorElement) || anchorElement.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }

                    foreach (JsonProperty property in anchorElement.EnumerateObject())
                    {
                        if (!anchorAllowed.Contains(property.Name))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static IEnumerable<string> EnumerateContinuityJsonCandidates(string raw)
        {
            string trimmed = raw.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                yield return trimmed;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                int firstNewline = trimmed.IndexOf('\n');
                string fenced = firstNewline >= 0 && firstNewline < trimmed.Length - 1
                    ? trimmed[(firstNewline + 1)..]
                    : trimmed;

                if (fenced.EndsWith("```", StringComparison.Ordinal))
                {
                    fenced = fenced[..^3];
                }

                fenced = fenced.Trim();
                if (!string.IsNullOrWhiteSpace(fenced))
                {
                    yield return fenced;
                }
            }

            int firstBrace = trimmed.IndexOf('{');
            int lastBrace = trimmed.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                string objectSlice = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
                if (!string.IsNullOrWhiteSpace(objectSlice))
                {
                    yield return objectSlice;
                }
            }
        }

        private static string BuildContinuityIssueMergeKey(ContinuityIssue issue)
        {
            string type = issue.Type?.Trim() ?? string.Empty;
            string message = issue.Message?.Trim() ?? string.Empty;
            string quote = issue.Evidence?.Quote?.Trim() ?? string.Empty;
            int bucket = Math.Max(0, issue.Anchor.PlainTextStart / 40);
            return $"{type}|{message}|{quote}|{bucket}";
        }

        private static int GetContinuitySeverityRank(string? severity)
        {
            if (string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            if (string.Equals(severity, "high", StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.Equals(severity, "medium", StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            return 1;
        }

        private AiExecutionOutcome BuildOutcome(
            IAiAction action,
            AiActionInput input,
            AiRequest request,
            AiResult result,
            string providerId)
        {
            List<ProposedOperation> operations = new();
            List<Guid> artifactIds = new();
            string summaryLabel = string.IsNullOrWhiteSpace(input.Instruction) ? action.DisplayName : input.Instruction;
            string? originalText = null;
            string? proposedText = null;

            if (string.Equals(action.ActionId, RewriteSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = input.SelectedText;
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
            }
            else if (string.Equals(action.ActionId, TranslateSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.SelectionText ?? request.Context.OriginalText ?? input.SelectedText;
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
            }
            else if (string.Equals(action.ActionId, TranslateSectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, TranslateDocumentAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.OriginalText ?? input.SelectedText;
            }
            else if (string.Equals(action.ActionId, CustomTransformAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.SelectionText ?? request.Context.OriginalText ?? input.SelectedText;

                if (input.SelectionRange.Length > 0 && !string.IsNullOrWhiteSpace(input.SelectedText))
                {
                    operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
                }
                else
                {
                    TextRange sectionRange = new(0, (originalText ?? string.Empty).Length);
                    operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, sectionRange, proposedText));
                }
            }
            else if (IsReviseSelectionAction(action.ActionId))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.SelectionText ?? request.Context.OriginalText ?? input.SelectedText;
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, input.SelectionRange, proposedText));
            }
            else if (IsReviseSectionAction(action.ActionId))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = request.Context.OriginalText ?? string.Empty;
                TextRange sectionRange = new(0, (originalText ?? string.Empty).Length);
                operations.Add(new ReplaceTextRangeOperation(input.ActiveSectionId, sectionRange, proposedText));
            }
            else if (string.Equals(action.ActionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = GetInputValue(request, "existing_value");
                string fieldKey = GetInputValue(request, "focus_field_key");

                if (string.IsNullOrWhiteSpace(fieldKey))
                {
                    _logger.LogWarning("Story Coach output rejected: missing target field key.");
                    return AiExecutionOutcome.Rejected(
                        result,
                        providerId,
                        "ai.story_coach_rejected",
                        "Story Coach output rejected: missing target field key.");
                }

                if (!StoryCoachOutputValidator.TryValidate(proposedText, fieldKey, originalText ?? string.Empty, out string reason))
                {
                    _logger.LogWarning(
                        "Story Coach output rejected: fieldKey={FieldKey} reason={Reason}",
                        fieldKey,
                        reason);

                    return AiExecutionOutcome.Rejected(
                        result,
                        providerId,
                        "ai.story_coach_rejected",
                        $"Story Coach output rejected: {reason}");
                }

                proposedText = proposedText.Trim();
                operations.Add(new ReplaceSynopsisFieldOperation(fieldKey, proposedText));
            }
            else if (string.Equals(action.ActionId, SynopsisEvaluateAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, SynopsisQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                originalText = null;
            }
            else if (string.Equals(action.ActionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
            }
            else if (string.Equals(action.ActionId, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                string rawJson = textArtifact?.TextContent ?? string.Empty;
                if (!OutlineDraftParser.TryParse(rawJson, out OutlineDraft? outline))
                {
                    _logger.LogWarning("Synopsis outline output rejected: invalid JSON contract.");
                    return AiExecutionOutcome.Rejected(
                        result,
                        providerId,
                        "ai.outline_parse_failed",
                        "Outline response was not valid JSON.");
                }

                string canonicalJson = OutlineDraftParser.ToCanonicalJson(outline!);
                originalText = input.Document.Synopsis?.OutlineDraft ?? string.Empty;
                proposedText = canonicalJson;
                operations.Add(new ReplaceSynopsisFieldOperation("outline_draft", canonicalJson));
            }
            else if (string.Equals(action.ActionId, ExtractCharacterBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, ExtractPlaceBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, ExtractTimelineBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, RefreshCharacterBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, RefreshPlaceBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, RefreshTimelineBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, ContinuityCheckAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
                if (string.Equals(action.ActionId, ContinuityCheckAction.ActionIdValue, StringComparison.Ordinal)
                    && TryParseContinuityReport(proposedText, out ContinuityReport? parsedReport)
                    && parsedReport is not null)
                {
                    string sectionText = request.Context.SelectionText ?? request.Context.OriginalText ?? string.Empty;
                    int sanitizedFixCount = 0;
                    List<ContinuityIssue> normalizedIssues = new();
                    foreach (ContinuityIssue issue in parsedReport.Issues)
                    {
                        ContinuityIssue normalizedIssue = NormalizeContinuityIssue(issue, sectionText, 0);
                        if (!string.IsNullOrWhiteSpace(issue.SuggestedFix)
                            && string.IsNullOrWhiteSpace(normalizedIssue.SuggestedFix))
                        {
                            sanitizedFixCount++;
                        }

                        normalizedIssues.Add(normalizedIssue);
                    }

                    if (sanitizedFixCount > 0)
                    {
                        _logger.LogWarning(
                            "Continuity check sanitized {SanitizedCount} instruction-like suggestions.",
                            sanitizedFixCount);
                    }

                    proposedText = JsonSerializer.Serialize(parsedReport with { Issues = normalizedIssues });
                }
            }
            else if (string.Equals(action.ActionId, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
            }
            else if (string.Equals(action.ActionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, SceneRefineAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(action.ActionId, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? textArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Text);
                proposedText = textArtifact?.TextContent ?? string.Empty;
            }
            else if (string.Equals(action.ActionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
            {
                AiArtifact? imageArtifact = result.Artifacts.FirstOrDefault(artifact => artifact.Modality == AiModality.Image);
                if (imageArtifact is not null)
                {
                    _artifactStore.Store(imageArtifact);
                    artifactIds.Add(imageArtifact.ArtifactId);
                    operations.Add(new AttachImageOperation(input.ActiveSectionId, imageArtifact.ArtifactId, "cover"));
                }
            }

            string? proposalReason = IsReviseAction(action.ActionId)
                ? BuildReviseReason(action.ActionId)
                : input.Instruction;

            AiProposal proposal = new(
                Guid.NewGuid(),
                input.ActiveSectionId,
                summaryLabel,
                action.ActionId,
                providerId,
                request.RequestId,
                DateTime.UtcNow,
                proposalReason,
                operations,
                artifactIds,
                BuildUserSummary(action.ActionId, input.Instruction, input.Options),
                BuildTargetScope(action.ActionId),
                input.Instruction,
                originalText,
                proposedText);

            return AiExecutionOutcome.Success(proposal, result, providerId);
        }

        private static string BuildTargetScope(string actionId)
        {
            if (string.Equals(actionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, TranslateDocumentAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Document";
            }

            if (string.Equals(actionId, TranslateSectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, CustomTransformAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (IsReviseSectionAction(actionId))
            {
                return "Section";
            }

            if (IsReviseSelectionAction(actionId))
            {
                return "Selection";
            }

            if (string.Equals(actionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis";
            }

            if (string.Equals(actionId, SynopsisEvaluateAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, SynopsisQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis";
            }

            if (string.Equals(actionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Document";
            }

            if (string.Equals(actionId, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis";
            }

            if (string.Equals(actionId, ExtractCharacterBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ExtractPlaceBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ExtractTimelineBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, RefreshCharacterBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, RefreshPlaceBibleAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, RefreshTimelineBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Document";
            }

            if (string.Equals(actionId, ContinuityCheckAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, ApplyContinuityFixAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, SceneRefineAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            if (string.Equals(actionId, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Section";
            }

            return "Selection";
        }

        private static string BuildUserSummary(string actionId, string? instruction, Dictionary<string, object?>? options)
        {
            if (string.Equals(actionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Generate cover image";
            }

            if (string.Equals(actionId, TranslateSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Translate selection";
            }

            if (string.Equals(actionId, TranslateSectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Translate section";
            }

            if (string.Equals(actionId, TranslateDocumentAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Translate document";
            }

            if (string.Equals(actionId, CustomTransformAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Run custom prompt";
            }

            if (string.Equals(actionId, StoryCoachAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Story Coach suggestion";
            }

            if (string.Equals(actionId, SynopsisEvaluateAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis evaluation";
            }

            if (string.Equals(actionId, SynopsisQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Synopsis questions";
            }

            if (string.Equals(actionId, GenerateOutlineAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Generate outline";
            }

            if (string.Equals(actionId, GenerateOutlineFromSynopsisAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Generate synopsis outline";
            }

            if (string.Equals(actionId, ExtractCharacterBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Build character bible";
            }

            if (string.Equals(actionId, ExtractPlaceBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Build place bible";
            }

            if (string.Equals(actionId, ExtractTimelineBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Build timeline bible";
            }

            if (string.Equals(actionId, RefreshCharacterBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Refresh character bible";
            }

            if (string.Equals(actionId, RefreshPlaceBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Refresh place bible";
            }

            if (string.Equals(actionId, RefreshTimelineBibleAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Refresh timeline bible";
            }

            if (string.Equals(actionId, ContinuityCheckAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Check continuity";
            }

            if (string.Equals(actionId, ApplyContinuityFixAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Apply continuity fix";
            }

            if (string.Equals(actionId, SceneSuggestAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Suggest scene card";
            }

            if (string.Equals(actionId, SceneRefineAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Refine scene card";
            }

            if (string.Equals(actionId, SceneFindOpenQuestionsAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Find open questions";
            }

            if (string.Equals(actionId, ProposeNextParagraphAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Propose next paragraph";
            }

            if (IsReviseAction(actionId))
            {
                string tone = GetOption(options, "tone");
                string reviseLabel = BuildReviseReason(actionId).Replace('_', ' ');
                if (string.Equals(reviseLabel, "change tone", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(tone))
                {
                    return $"Change tone to {tone}";
                }

                return $"{char.ToUpperInvariant(reviseLabel[0])}{reviseLabel.Substring(1)}";
            }

            if (!string.Equals(actionId, RewriteSelectionAction.ActionIdValue, StringComparison.Ordinal))
            {
                return "Apply AI change";
            }

            string normalized = instruction?.Trim().ToLowerInvariant() ?? string.Empty;
            string toneOption = GetOption(options, "tone");
            string length = GetOption(options, "length");

            if (normalized.Contains("shorten", StringComparison.Ordinal))
            {
                return "Shorten selected text";
            }

            if (normalized.Contains("fix grammar", StringComparison.Ordinal) || normalized.Contains("grammar", StringComparison.Ordinal))
            {
                return "Fix grammar in selected text";
            }

            if (normalized.Contains("summarize", StringComparison.Ordinal) || normalized.Contains("summary", StringComparison.Ordinal))
            {
                return "Summarize selected text";
            }

            if (string.Equals(length, "Shorter", StringComparison.OrdinalIgnoreCase))
            {
                return "Shorten selected text";
            }

            if (!string.IsNullOrWhiteSpace(toneOption) && !string.Equals(toneOption, "Neutral", StringComparison.OrdinalIgnoreCase))
            {
                return $"Rewrite selected text in a more {toneOption} tone";
            }

            if (normalized.Contains("rewrite", StringComparison.Ordinal))
            {
                return "Rewrite selected text";
            }

            if (!string.IsNullOrWhiteSpace(instruction))
            {
                return $"Rewrite selected text: {instruction}";
            }

            return "Rewrite selected text";
        }

        private static string GetOption(Dictionary<string, object?>? options, string key)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }

        private static string GetInputValue(AiRequest request, string key)
        {
            if (request.Inputs is null || !request.Inputs.TryGetValue(key, out object? value) || value is null)
            {
                return string.Empty;
            }

            return value.ToString() ?? string.Empty;
        }

        private static bool IsReviseSelectionAction(string actionId)
        {
            return string.Equals(actionId, TightenSelectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ExpandSelectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ChangeToneSelectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ShowDontTellSelectionAction.ActionIdValue, StringComparison.Ordinal);
        }

        private static bool IsReviseSectionAction(string actionId)
        {
            return string.Equals(actionId, TightenSectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ExpandSectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ChangeToneSectionAction.ActionIdValue, StringComparison.Ordinal)
                || string.Equals(actionId, ShowDontTellSectionAction.ActionIdValue, StringComparison.Ordinal);
        }

        private static bool IsReviseAction(string actionId) => IsReviseSelectionAction(actionId) || IsReviseSectionAction(actionId);

        private static string BuildReviseReason(string actionId)
        {
            int separator = actionId.IndexOf('.');
            if (separator <= 0)
            {
                return actionId;
            }

            return actionId.Substring(0, separator);
        }

        private static ContinuityIssue NormalizeContinuityIssue(ContinuityIssue issue, string sourceText, int startOffsetAdjustment)
        {
            string text = sourceText ?? string.Empty;
            int max = Math.Max(0, text.Length);
            int rawStart = Math.Max(0, issue.Anchor.PlainTextStart) + Math.Max(0, startOffsetAdjustment);
            int start = Math.Clamp(rawStart, 0, max);
            int length = Math.Max(0, issue.Anchor.PlainTextLength);
            if (start + length > max)
            {
                length = Math.Max(0, max - start);
            }

            string excerpt = BuildReadableAnchorExcerpt(text, start, length, 380);
            ContinuityEvidence evidence = issue.Evidence ?? new ContinuityEvidence(string.Empty, string.Empty);
            string normalizedFix = NormalizeContinuitySuggestedFix(issue.SuggestedFix);
            return issue with
            {
                Anchor = new ContinuityAnchor(start, length),
                Evidence = evidence with
                {
                    Quote = string.IsNullOrWhiteSpace(excerpt) ? evidence.Quote : excerpt
                },
                SuggestedFix = normalizedFix
            };
        }

        private static string BuildReadableAnchorExcerpt(string text, int start, int length, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int safeMax = Math.Max(80, maxChars);
            int safeStart = Math.Clamp(start, 0, text.Length);
            int safeEnd = Math.Clamp(safeStart + Math.Max(0, length), safeStart, text.Length);
            if (safeEnd <= safeStart)
            {
                safeEnd = Math.Min(text.Length, safeStart + 1);
            }

            int leftSentenceBreak = LastBoundaryIndex(text, safeStart);
            int rightSentenceBreak = NextBoundaryIndex(text, safeEnd);
            int snippetStart = leftSentenceBreak < 0 ? 0 : leftSentenceBreak + 1;
            int snippetEnd = rightSentenceBreak < 0 ? text.Length : rightSentenceBreak + 1;

            string snippet = text.Substring(snippetStart, Math.Max(0, snippetEnd - snippetStart)).Trim();
            if (snippet.Length <= safeMax)
            {
                return snippet;
            }

            int contextStart = Math.Max(0, safeStart - (safeMax / 2));
            int contextEnd = Math.Min(text.Length, safeEnd + (safeMax / 2));
            if (contextEnd - contextStart > safeMax)
            {
                contextEnd = Math.Min(text.Length, contextStart + safeMax);
            }

            return text.Substring(contextStart, Math.Max(0, contextEnd - contextStart)).Trim();
        }

        private static int LastBoundaryIndex(string text, int fromExclusive)
        {
            for (int index = Math.Min(fromExclusive - 1, text.Length - 1); index >= 0; index--)
            {
                char ch = text[index];
                if (ch == '.' || ch == '!' || ch == '?' || ch == '\n')
                {
                    return index;
                }
            }

            return -1;
        }

        private static int NextBoundaryIndex(string text, int fromInclusive)
        {
            for (int index = Math.Max(0, fromInclusive); index < text.Length; index++)
            {
                char ch = text[index];
                if (ch == '.' || ch == '!' || ch == '?' || ch == '\n')
                {
                    return index;
                }
            }

            return -1;
        }

        private static AiExecutionOutcome BuildApplyContinuityFixOutcome(IAiAction action, AiActionInput input, AiRequest request)
        {
            string rawSuggestedFix = GetOption(input.Options, "suggested_fix");
            string suggestedFix = NormalizeContinuitySuggestedFix(rawSuggestedFix);
            string issueType = GetOption(input.Options, "issue_type");
            string issueMessage = GetOption(input.Options, "issue_message");
            bool duplicateIssue = IsLikelyDuplicateIssue(issueType, issueMessage);
            bool hadInstructionLikeFix = !string.IsNullOrWhiteSpace(rawSuggestedFix)
                && string.IsNullOrWhiteSpace(suggestedFix);

            if (string.IsNullOrWhiteSpace(suggestedFix) && !duplicateIssue)
            {
                AiResult rejectedResult = new(
                    request.RequestId,
                    new List<AiArtifact>(),
                    new AiUsage(0, 0, TimeSpan.Zero),
                    new Dictionary<string, object>
                    {
                        ["provider"] = "local"
                    });
                return AiExecutionOutcome.Rejected(
                    rejectedResult,
                    "local",
                    hadInstructionLikeFix ? "ai.continuity_fix_rejected_instruction_text" : "ai.continuity_fix_missing_text",
                    hadInstructionLikeFix
                        ? "Continuity fix was rejected because replacement text looked like instructions."
                        : "Continuity fix requires suggested text.");
            }

            string replacementText = suggestedFix;

            int start = Math.Max(0, ParseIntOption(input.Options, "anchor_start", input.SelectionRange.Start));
            int length = Math.Max(0, ParseIntOption(input.Options, "anchor_length", input.SelectionRange.Length));
            Guid sectionId = ParseGuidOption(input.Options, "section_id", input.ActiveSectionId);
            TextRange targetRange = new(start, length);
            List<ProposedOperation> operations = new()
            {
                new ReplaceTextRangeOperation(sectionId, targetRange, replacementText)
            };

            AiProposal proposal = new(
                Guid.NewGuid(),
                sectionId,
                "Apply continuity fix",
                action.ActionId,
                "local",
                request.RequestId,
                DateTime.UtcNow,
                "continuity_fix",
                operations,
                new List<Guid>(),
                "Apply continuity fix",
                "Section",
                input.Instruction,
                input.SelectedText,
                replacementText);

            AiResult result = new(
                request.RequestId,
                new List<AiArtifact>(),
                new AiUsage(0, 0, TimeSpan.Zero),
                new Dictionary<string, object>
                {
                    ["provider"] = "local"
                });

            return AiExecutionOutcome.Success(proposal, result, "local");
        }

        private static bool LooksLikeInstructionText(string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return false;
            }

            string lowered = normalized.ToLowerInvariant();
            string[] imperativeStarts =
            {
                "adjust ",
                "change ",
                "fix ",
                "rewrite ",
                "update ",
                "make ",
                "ensure ",
                "move ",
                "remove ",
                "replace ",
                "delete ",
                "insert "
            };

            if (imperativeStarts.Any(prefix => lowered.StartsWith(prefix, StringComparison.Ordinal)))
            {
                return true;
            }

            if (Regex.IsMatch(normalized, @"^\s*(?:-|\*|\d+\.)\s+", RegexOptions.Multiline))
            {
                return true;
            }

            string firstLine = normalized.Split('\n')[0].Trim();
            if (firstLine.Length <= 80 && firstLine.EndsWith(":", StringComparison.Ordinal))
            {
                return true;
            }

            return lowered.Contains("remove duplicate paragraph", StringComparison.Ordinal)
                || lowered.Contains("remove repeated paragraph", StringComparison.Ordinal)
                || lowered.Contains("remove the repeated paragraphs", StringComparison.Ordinal)
                || lowered.Contains("maintain narrative clarity", StringComparison.Ordinal)
                || lowered.Contains("improve narrative clarity", StringComparison.Ordinal)
                || lowered.Contains("as an ai", StringComparison.Ordinal)
                || lowered.Contains("arrives before", StringComparison.Ordinal)
                || lowered.Contains("arrives after", StringComparison.Ordinal)
                || lowered.StartsWith("instruction:", StringComparison.Ordinal)
                || lowered.StartsWith("analysis:", StringComparison.Ordinal)
                || lowered.StartsWith("explanation:", StringComparison.Ordinal);
        }

        private static string NormalizeContinuitySuggestedFix(string text)
        {
            string candidate = (text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            if (TryExtractRevisedTextCandidate(candidate, out string extracted))
            {
                candidate = extracted.Trim();
            }

            return LooksLikeInstructionText(candidate) ? string.Empty : candidate;
        }

        private static bool TryExtractRevisedTextCandidate(string source, out string revised)
        {
            revised = string.Empty;
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            string value = source.Trim();
            int revisedStart = value.IndexOf("<<REVISED>>", StringComparison.OrdinalIgnoreCase);
            int revisedEnd = value.IndexOf("<<END>>", StringComparison.OrdinalIgnoreCase);
            if (revisedStart >= 0 && revisedEnd > revisedStart)
            {
                int contentStart = revisedStart + "<<REVISED>>".Length;
                revised = value.Substring(contentStart, revisedEnd - contentStart).Trim();
                return !string.IsNullOrWhiteSpace(revised);
            }

            if (value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal))
            {
                try
                {
                    using JsonDocument doc = JsonDocument.Parse(value);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object
                        && doc.RootElement.TryGetProperty("revisedText", out JsonElement revisedText)
                        && revisedText.ValueKind == JsonValueKind.String)
                    {
                        revised = revisedText.GetString()?.Trim() ?? string.Empty;
                        return !string.IsNullOrWhiteSpace(revised);
                    }
                }
                catch (JsonException)
                {
                }
            }

            MatchCollection quotedMatches = Regex.Matches(value, "\"([^\"]{24,})\"");
            if (quotedMatches.Count > 0)
            {
                Match longest = quotedMatches
                    .Cast<Match>()
                    .OrderByDescending(match => match.Groups[1].Value.Length)
                    .First();
                revised = longest.Groups[1].Value.Trim();
                return !string.IsNullOrWhiteSpace(revised);
            }

            return false;
        }

        private static bool IsLikelyDuplicateIssue(string issueType, string issueMessage)
        {
            string type = (issueType ?? string.Empty).Trim().ToLowerInvariant();
            string message = (issueMessage ?? string.Empty).Trim().ToLowerInvariant();
            return type.Contains("repeat", StringComparison.Ordinal)
                || type.Contains("duplicate", StringComparison.Ordinal)
                || message.Contains("repeat", StringComparison.Ordinal)
                || message.Contains("duplicate", StringComparison.Ordinal)
                || message.Contains("same paragraph", StringComparison.Ordinal)
                || message.Contains("repeated paragraph", StringComparison.Ordinal);
        }

        private static int ParseIntOption(Dictionary<string, object?>? options, string key, int fallback)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return fallback;
            }

            if (value is int intValue)
            {
                return intValue;
            }

            return int.TryParse(value.ToString(), out int parsed) ? parsed : fallback;
        }

        private static Guid ParseGuidOption(Dictionary<string, object?>? options, string key, Guid fallback)
        {
            if (options is null || !options.TryGetValue(key, out object? value) || value is null)
            {
                return fallback;
            }

            if (value is Guid guidValue)
            {
                return guidValue;
            }

            return Guid.TryParse(value.ToString(), out Guid parsed) ? parsed : fallback;
        }

        private sealed record ContinuityChunk(int Start, string Text);

        private sealed record ContinuityReport(string SchemaVersion, List<ContinuityIssue> Issues);

        private sealed record ContinuityIssue(
            string Severity,
            string Type,
            string Message,
            ContinuityEvidence Evidence,
            string SuggestedFix,
            ContinuityAnchor Anchor);

        private sealed record ContinuityEvidence(string SectionId, string Quote);

        private sealed record ContinuityAnchor(int PlainTextStart, int PlainTextLength);
    }
}
