using WriterApp.Application.AI;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared;

namespace WriterApp.Device.Shared.Services;

public sealed record LocalWritingPrepared(LocalDocument Source, Guid SectionId, Guid PageId, WritingCommand Command,
    DeviceAiPrepared Request, WritingStructure? Structure = null, PromptDefinition? Preset = null, string? RecommendationId = null);

public static class LocalWriting
{
    public static LocalWritingPrepared Prepare(LocalDocument source, Guid sectionId, WritingCommand command,
        IReadOnlyDictionary<Guid, TranslationPageCapture>? captures = null, QualityEditorSource? selection = null,
        IReadOnlyDictionary<Guid, string>? plainPages = null, string task = "", PromptDefinition? preset = null, string? recommendationId = null)
    {
        WritingActions.Validate(command);
        if (recommendationId is not null && (preset is not null || command.Key != "custom_transform" || command.Scope != WritingScope.Section)) throw new InvalidDataException("Recommendations use their declared saved section target.");
        if(preset is not null) {
            ReusablePrompts.ValidateForRun(preset);
            if(ReusablePrompts.Resolve(preset)!=command.Key||preset.Scope!=command.Scope||preset.ProjectId is { } project && project!=source.ServerProjectId)
                throw new InvalidOperationException("This preset belongs to another project or writing target. Choose a matching preset.");
            preset=ReusablePrompts.Parse(ReusablePrompts.Serialize(preset));
        }
        if (source.ServerDocumentId is null || source.ServerVersion is null || source.SyncState != LocalSyncState.Synced || source.DeletedAtUtc is not null)
            throw new InvalidOperationException("Synchronize this manuscript before generating writing.");
        var section = source.Sections.Single(s => s.SectionId == sectionId);
        var pages = section.Pages.OrderBy(p => p.OrderIndex).ToArray();
        if (pages.Length == 0 || section.ServerSectionId is null || pages.Any(p => p.ServerPageId is null))
            throw new InvalidOperationException("Synchronize every section and page before using writing actions.");
        var parameters = new Dictionary<string, object?> { ["tone"] = command.Settings.Tone, ["length"] = command.Settings.Length, ["preserve_terms"] = command.Settings.PreserveTerms };
        if (preset is null && command.Key == "rewrite.selection"
            && WritingActions.ToneDescriptors.Single(t => t.Value == command.Settings.Tone).SelectionRewriteInstruction is { } instruction)
            parameters["instruction"] = instruction;
        if(preset is not null) { parameters=ReusablePrompts.ExecutionParameters(preset);parameters[ReusablePrompts.Parameter]=ReusablePrompts.Serialize(preset); }
        if (recommendationId is not null) parameters = RecommendedWriting.Parameters(recommendationId);
        WritingStructure? structure = null; string text; LocalPage page; AiEditorSnapshot? editor = null;
        if (command.Scope == WritingScope.Selection) {
            if (selection is null || selection.Document.DocumentId != source.DocumentId || LocalTranslation.Signature(selection.Document) != LocalTranslation.Signature(source))
                throw new InvalidOperationException("The selected writing changed. Select it again.");
            page = pages.Single(p => p.PageId == selection.PageId); editor = selection.Editor;
            if (editor.SelectionStart >= editor.SelectionEnd || string.IsNullOrWhiteSpace(editor.SelectedText) || page.ContentFormat == LocalContentFormat.Html && page.Content != editor.Html)
                throw new InvalidOperationException("Select a supported passage in the current section first.");
            text = editor.PlainText;
        } else {
            page = pages[^1];
            // Annotations live in planning metadata, outside the captured text runs.
            // Saving reconciles their quotes against the reviewed prose without deleting them.
            if (command.Scope == WritingScope.Section) {
                if (captures is null || captures.Count != pages.Length || pages.Any(p => !captures.ContainsKey(p.PageId)))
                    throw new InvalidDataException("Capture every page before section revision.");
                structure = new(1, source.ServerDocumentId.Value, section.ServerSectionId.Value,
                    pages.Select(p => new TranslationPage(p.ServerPageId!.Value, captures[p.PageId].Runs)).ToArray());
                parameters[WritingActions.Parameter] = WritingActions.Serialize(structure);
                if (recommendationId is not null) {
                    RecommendedWriting.ValidateSource(structure, recommendationId);
                    if (pages[0].ContentFormat == LocalContentFormat.Html) RecommendedWriting.ValidateOpeningHtml(pages[0].Content, recommendationId);
                }
                text = string.Join("\n\n", structure.Pages.Select(p => string.Join("\n", p.Runs.Select(r => r.Text))));
            } else {
                if (plainPages is null || plainPages.Count != pages.Length || pages.Any(p => !plainPages.ContainsKey(p.PageId)))
                    throw new InvalidDataException("Capture every saved page before continuing.");
                text = string.Join("\n\n", pages.Select(p => plainPages[p.PageId]));
                if (string.IsNullOrWhiteSpace(text) || text.Length > 60000) throw new InvalidDataException("Add writing or continue a smaller section (maximum 60,000 characters).");
                var card = source.Project?.Nodes.SingleOrDefault(n => n.SectionId == sectionId && n.DeletionId is null)?.Card;
                parameters["narrative_purpose"] = card?.NarrativePurpose ?? section.NarrativePurpose;
                parameters["narrative_role"] = card?.NarrativeRole; parameters["narrative_intent"] = card?.NarrativeIntent;
                parameters["emotional_beat"] = card?.EmotionalBeat; parameters["key_events"] = card?.KeyEvents; parameters["open_questions"] = card?.OpenQuestions;
                if (task.Length > 2000) throw new InvalidDataException("Use at most 2,000 characters for continuation guidance.");
                parameters["instruction"] = "Write exactly one new paragraph after the saved section, preserving voice, POV, tense and facts. Do not repeat, paraphrase or recap existing writing. Return only prose. Genre: " + source.Project?.Genre + ". Craft focus: " + task;
            }
        }
        if(preset?.Kind=="custom")parameters["context"]=editor?.SelectedText??text;
        var request = new AiActionExecuteRequestDto(source.ServerDocumentId.Value, section.ServerSectionId.Value, page.ServerPageId,
            editor?.SelectionStart, editor?.SelectionEnd, editor?.SelectedText, text, null, parameters, source.ServerVersion,
            WritingOutline: LocalWritingOutline.Capture(source));
        return new(source, sectionId, page.PageId, command, new(command.Key, request, DeviceAiAction.Custom, page.PageId, page.Content,
            editor?.From ?? 0, editor?.To ?? 0, editor?.SelectedText ?? "", "analysis"), structure,preset,recommendationId);
    }
    public static LocalDocument Preview(LocalWritingPrepared prepared, IReadOnlyDictionary<Guid, string> html) {
        var expected = prepared.Command.Scope == WritingScope.Section ? prepared.Source.Sections.Single(s => s.SectionId == prepared.SectionId).Pages.Select(p => p.PageId).ToArray() : [prepared.PageId];
        if (html.Count != expected.Length || expected.Any(p => !html.ContainsKey(p))) throw new InvalidDataException("The complete writing preview is missing.");
        return prepared.Source with { Sections = prepared.Source.Sections.Select(s => s with { Pages = s.Pages.Select(p => prepared.RecommendationId is { } id && RecommendedWriting.Output(id) == RecommendedOutput.AppendParagraph && p.PageId != prepared.PageId ? p : html.TryGetValue(p.PageId, out var value)
            ? p with { Content = value, ContentFormat = LocalContentFormat.Html } : p).ToArray() }).ToArray() };
    }
    public static LocalAiHistory Entry(LocalWritingPrepared prepared, DeviceAiProposal proposal, LocalDocument after) => new(3, Guid.NewGuid(), prepared.Source.DocumentId,
        prepared.Command.Key, "Writing:" + prepared.Command.Scope.ToString().ToLowerInvariant(), prepared.Source.LocalRevision, prepared.Source.ServerVersion,
        DateTimeOffset.UtcNow, "Reviewed", prepared.Source, proposal.ProposedText, prepared.Command.Scope == WritingScope.Section ? null : prepared.PageId,
        OriginalText: prepared.Command.Scope == WritingScope.Selection ? prepared.Request.SelectedText : proposal.SourceText, After: after, SectionId: prepared.SectionId,Preset:prepared.Preset,CloudOrigin:proposal.HistoryOrigin,RecommendationId:prepared.RecommendationId);
    public static void ValidateEntry(LocalAiHistory entry) {
        if (entry.RecommendationId is { } recommended && (entry.Action != "custom_transform" || entry.Target != "Writing:section" || entry.Preset is not null || RecommendedWriting.CopyOnly(recommended))) throw new InvalidDataException("This recommendation cannot change manuscript writing.");
        var scope = entry.Target switch { "Writing:selection" => WritingScope.Selection, "Writing:section" => WritingScope.Section,
            "Writing:continuation" => WritingScope.Continuation, _ => throw new InvalidDataException("Invalid writing target.") };
        WritingActions.Validate(new WritingCommand(entry.Action, scope, new()));
        if(entry.Action=="custom_transform"&&entry.Preset is null && entry.RecommendationId is null)throw new InvalidDataException("Custom revision needs its retained preset definition or catalog recommendation.");
        if(entry.Preset is { } preset) { ReusablePrompts.ValidateForRun(preset);if(preset.Scope!=scope||ReusablePrompts.Resolve(preset)!=entry.Action||preset.ProjectId is { } project&&project!=entry.Before.ServerProjectId)throw new InvalidDataException("Preset recovery target mismatch."); }
        if (entry.Version != 3 || entry.After is null || entry.SectionId is not { } sectionId || entry.NodeId is not null || entry.After.Project?.ProjectId != entry.Before.Project?.ProjectId
            || entry.SourceRevision != entry.Before.LocalRevision || entry.ServerVersion != entry.Before.ServerVersion)
            throw new InvalidDataException("Missing writing recovery target.");
        var section = entry.Before.Sections.SingleOrDefault(s => s.SectionId == sectionId) ?? throw new InvalidDataException("Wrong writing section.");
        var expected = scope == WritingScope.Section ? section.Pages.Select(p => p.PageId).ToArray() : [entry.PageId ?? Guid.Empty];
        if (scope == WritingScope.Section && entry.PageId is not null || expected.Any(id => section.Pages.All(p => p.PageId != id))
            || scope == WritingScope.Continuation && entry.PageId != section.Pages.OrderBy(p => p.OrderIndex).Last().PageId)
            throw new InvalidDataException("Wrong writing page target.");
        var normalized = entry.After with { Sections = entry.After.Sections.Select(s => s with { Pages = s.Pages.Select(p => expected.Contains(p.PageId)
            ? p with { Content = entry.Before.Sections.SelectMany(x => x.Pages).Single(x => x.PageId == p.PageId).Content,
                ContentFormat = entry.Before.Sections.SelectMany(x => x.Pages).Single(x => x.PageId == p.PageId).ContentFormat } : p).ToArray() }).ToArray() };
        if (!LocalDocumentCodec.Encode(normalized).SequenceEqual(LocalDocumentCodec.Encode(entry.Before)))
            throw new InvalidDataException("Writing recovery changed unrelated source or planning.");
    }
    public static string ReadableProposal(LocalAiHistory entry) {
        try {
            if (entry.RecommendationId is { } id && RecommendedWriting.Output(id) == RecommendedOutput.AppendParagraph) return "Append at section end: " + RecommendedWriting.TextResult(entry.Proposed, id, entry.OriginalText ?? "").Items[0];
            if (entry.Target == "Writing:section") return string.Join("\n\n", WritingActions.Parse(entry.Proposed).Pages.Select(p =>
                (entry.Before.Sections.SelectMany(s => s.Pages).Single(page => page.ServerPageId == p.Id).Title) + "\n" + string.Concat(p.Runs.Select(r => r.Text))));
            if (entry.Target == "Writing:continuation") return WritingActions.Continuation(entry.Proposed, entry.OriginalText ?? "");
            return entry.Proposed;
        } catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or InvalidOperationException) { return "This retained proposal is invalid. Keep the original or use a recovery copy."; }
    }
}

public sealed class LocalWritingActions(LocalDocumentRepository documents, LocalAiStore history, DeviceAiService ai, IDeviceAiApi api)
{
    public async Task<DeviceAiProposal> ProposeAsync(LocalWritingPrepared prepared, CancellationToken ct) {
        var available = await api.GetWritingAvailabilityAsync(ct);
        if (prepared.RecommendationId is not null && !available.Recommendations) throw new DeviceAiException(DeviceAiFailure.Upgrade, "Update the backend to support recommended writing tools. No generation was sent.");
        if(!available.SavedOutlineContext)throw new DeviceAiException(DeviceAiFailure.Upgrade,"Update the backend to support source-checked saved writing outline context. No generation was sent.");
        LocalWritingOutline.RequireCurrent(await documents.LoadAsync(prepared.Source.DocumentId,ct) ?? throw new IOException("Manuscript unavailable."),prepared.Request.Request.WritingOutline!);
        bool demo=OnboardingAiDemoRequest.IsRequested(prepared.Command.Key,prepared.Request.Request.Parameters);
        if (!demo && (!available.AllowedActions.Contains(prepared.Command.Key) || prepared.Structure is not null && (!available.StructuredSections || prepared.Preset?.Kind=="custom"&&!available.StructuredPresets)))
            throw new DeviceAiException(DeviceAiFailure.Upgrade, "This writing action is unavailable for your plan or backend. Refresh availability, check your plan or update the backend. No generation was sent.");
        var proposal=await ai.ProposeAsync(prepared.Request, ct);
        LocalWritingOutline.RequireCurrent(await documents.LoadAsync(prepared.Source.DocumentId,ct) ?? throw new IOException("Manuscript unavailable."),prepared.Request.Request.WritingOutline!);
        return proposal;
    }
    public async Task ApplyAsync(LocalAiHistory entry, DeviceAiProposal proposal, CancellationToken ct) {
        LocalWriting.ValidateEntry(entry);
        ai.RequireCurrentAccount(proposal); ct.ThrowIfCancellationRequested();
        var submitted=proposal.Prepared.Request.Parameters?.GetValueOrDefault(ReusablePrompts.Parameter)?.ToString();
        if (RecommendedWriting.From(proposal.Prepared.Request.Parameters)?.ToolId != entry.RecommendationId) throw new InvalidOperationException("Review the exact recommended tool again before applying.");
        if(entry.Preset is not null ? submitted is null||ReusablePrompts.Canonical(ReusablePrompts.Parse(submitted))!=ReusablePrompts.Canonical(entry.Preset) : submitted is not null)
            throw new InvalidOperationException("Review the exact retained preset again before applying.");
        var section = entry.Before.Sections.Single(s => s.SectionId == entry.SectionId);
        var page = section.Pages.Single(p => p.PageId == (entry.PageId ?? section.Pages.OrderBy(p => p.OrderIndex).Last().PageId));
        if (entry.Version != 3 || entry.After is null || entry.Status != "Reviewed" || !entry.Target.StartsWith("Writing:", StringComparison.Ordinal)
            || proposal.Prepared.Key != entry.Action || proposal.ProposedText != entry.Proposed || proposal.Prepared.Request.DocumentId != entry.Before.ServerDocumentId
            || proposal.Prepared.Request.ExpectedDocumentVersion != entry.Before.ServerVersion || proposal.Prepared.Request.SectionId != section.ServerSectionId
            || proposal.Prepared.LocalPageId != page.PageId || proposal.Prepared.Request.PageId != page.ServerPageId)
            throw new InvalidOperationException("Review the intended writing target again before applying.");
        LocalTranslation.RequireFresh(await documents.LoadAsync(entry.DocumentId, ct) ?? throw new IOException("Manuscript unavailable."), entry.Before);
        var outline=proposal.Prepared.Request.WritingOutline ?? throw new InvalidOperationException("Review with the saved outline contract before applying.");
        if(outline.Fingerprint!=LocalWritingOutline.Capture(entry.Before).Fingerprint)throw new InvalidOperationException("The reviewed outline does not match its recovery source.");
        await history.SaveHistoryAsync(entry with { Status = "Applying" }, ct);
        ai.RequireCurrentAccount(proposal); ct.ThrowIfCancellationRequested();
        await documents.SaveAsync(entry.After, ct);
        await history.SaveHistoryAsync(entry with { Status = "Applied" }, CancellationToken.None);
    }
    public static async Task ResumeAsync(LocalDocumentRepository documents, LocalAiStore history, LocalAiHistory entry, CancellationToken ct) {
        LocalWriting.ValidateEntry(entry);
        if (entry.Version != 3 || entry.Status != "Applying" || entry.After is null || !entry.Target.StartsWith("Writing:", StringComparison.Ordinal)) throw new InvalidOperationException("No approved writing save is pending.");
        var current = await documents.LoadAsync(entry.DocumentId, ct) ?? throw new IOException("Manuscript unavailable.");
        // A completed save also reconciles annotation anchors. Compare with that
        // persisted result so a crash before the history commit can resume once.
        var savedAfter = LocalPlanning.Reconcile(entry.Before, entry.After);
        if (LocalTranslation.Signature(current) != LocalTranslation.Signature(savedAfter)) {
            LocalTranslation.RequireFresh(current, entry.Before); await documents.SaveAsync(entry.After, ct);
        }
        await history.SaveHistoryAsync(entry with { Status = "Applied" }, CancellationToken.None);
    }
}
