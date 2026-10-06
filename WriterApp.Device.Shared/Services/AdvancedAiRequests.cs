using System.Text.Json;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Components;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using WriterApp.UI.Shared.Projects;

namespace WriterApp.Device.Shared.Services;

public enum AdvancedAiAction { Consistency, StyleQuality, SceneCard, Synopsis, Storyboard, Custom }
public enum LocalAiTarget { Analysis, SceneCard, SynopsisField }
public sealed record AdvancedAiPrepared(LocalDocument Source, Guid SectionId, Guid? NodeId, string Field,
    LocalAiTarget Target, DeviceAiPrepared Request);

public static class AdvancedAiRequests
{
    public static IReadOnlyList<LocalSection> ActiveSections(LocalDocument document) => document.Sections
        .Where(s => document.Project is null || document.Project.Nodes.Any(n => n.NodeType == "scene" && n.SectionId == s.SectionId && n.DeletionId is null))
        .OrderBy(s => s.OrderIndex).ToArray();
    public static readonly (string Key, string Field)[] SynopsisFields = WriterApp.Shared.SynopsisCoaching.Fields.Select(f => (f.Key, f.Property)).ToArray();
    public static AdvancedAiPrepared Build(LocalDocument doc, Guid sectionId, AdvancedAiAction action, string field = "logline", string instruction = "", IReadOnlyList<ConsistencyPageText>? consistencyPages = null)
    {
        if (!Enum.IsDefined(action)) throw new InvalidDataException("Unknown AI action.");
        if (instruction.Length > 2000) throw new InvalidDataException("Use at most 2,000 characters for coaching notes.");
        var section = ActiveSections(doc).SingleOrDefault(s => s.SectionId == sectionId)
            ?? throw new InvalidDataException("The selected scene is unavailable. Choose an active scene and run again.");
        var page = section.Pages.OrderBy(p => p.OrderIndex).FirstOrDefault()
            ?? throw new InvalidDataException("Add a page to this section before using AI.");
        foreach (var p in section.Pages) DeviceContentCompatibility.RequireEditable(p.Content, p.ContentFormat);
        string context = consistencyPages is not null && action is AdvancedAiAction.Consistency or AdvancedAiAction.SceneCard
            ? string.Join("\n\n", section.Pages.OrderBy(p => p.OrderIndex).Select(p => consistencyPages.Single(t => t.PageId == p.PageId).PlainText))
            : DeviceAiRequests.SectionText(section, page.PageId, DeviceAiRequests.PlainText(page));
        var baseRequest = DeviceAiRequests.Build(doc, section, page, new AiEditorSnapshot(page.Content, DeviceAiRequests.PlainText(page), "", 0, 0, 0, 0, 0), DeviceAiAction.Summarize, sectionContext: context);
        var node = doc.Project?.Nodes.SingleOrDefault(n => n.SectionId == sectionId && n.NodeType == "scene" && n.DeletionId is null);
        var parameters = new Dictionary<string, object?>(); string key; LocalAiTarget target = LocalAiTarget.Analysis;
        switch (action)
        {
            case AdvancedAiAction.Consistency: key = "continuity.check_section"; break;
            case AdvancedAiAction.StyleQuality:
                throw new InvalidDataException("Use the editor's Style & quality Coach to preview and apply a writing revision.");
            case AdvancedAiAction.Custom:
                if (string.IsNullOrWhiteSpace(instruction) || instruction.Length > 2000) throw new InvalidDataException("Enter a prompt of at most 2,000 characters.");
                key = "custom_transform"; parameters["scope"] = "section"; parameters["template"] = instruction; break;
            case AdvancedAiAction.SceneCard:
                if (node is null) throw new InvalidDataException("Select a project scene before scene-card coaching.");
                key = "scene.suggest"; target = LocalAiTarget.SceneCard;
                if (!string.IsNullOrWhiteSpace(instruction)) parameters["instruction"] = instruction;
                break;
            case AdvancedAiAction.Synopsis:
                if (doc.Project is null || !SynopsisFields.Any(x => x.Key == field)) throw new InvalidDataException("Choose an available synopsis field.");
                key = "synopsis.story_coach"; target = LocalAiTarget.SynopsisField;
                parameters["focus_field_key"] = field;
                parameters["focus_field_prompt"] = $"Propose an improved {field} consistent with the saved story intent.";
                parameters["existing_value"] = SynopsisValue(doc.Project.Synopsis ?? new(), field);
                parameters["other_fields_context"] = JsonSerializer.Serialize(doc.Project.Synopsis ?? new());
                parameters["user_notes"] = instruction; break;
            case AdvancedAiAction.Storyboard:
                if (doc.Project is null) throw new InvalidDataException("Select a project before storyboard analysis.");
                key = "storyboard.check-subplot-continuity";
                if (!string.IsNullOrWhiteSpace(instruction)) parameters["instruction"] = instruction;
                parameters["storyboard_context"] = StoryboardContext(doc); break;
            default: throw new InvalidDataException("Unknown AI action.");
        }
        var request = baseRequest with
        {
            Key = key,
            ApplyMode = "analysis",
            Request = baseRequest.Request with
            {
                Parameters = parameters,
                SurroundingText = context,
                ExpectedDocumentVersion = doc.ServerVersion
            }
        };
        return new(doc, sectionId, node?.NodeId, target == LocalAiTarget.SynopsisField ? field : "", target, request);
    }
    public static string SynopsisValue(SyncSynopsis synopsis, string field) => WriterApp.Shared.SynopsisCoaching.Value(synopsis, field);
    public static string StoryboardContext(LocalDocument doc)
    {
        var project = doc.Project ?? throw new InvalidDataException("Select a project before storyboard analysis.");
        return WriterApp.Shared.StoryboardAnalysis.Build(project.Title, project.Nodes.Where(n => n.DeletionId is null)
            .Select(n => new WriterApp.Shared.StoryboardAnalysisNode(n.NodeId, n.ParentId, n.OrderIndex, n.NodeType, n.Title,
                n.Card, n.MetadataJson)));
    }
    public static void RequireFresh(LocalDocument current, AdvancedAiPrepared prepared)
    {
        // Sync acknowledgments and no-op saves can advance the local revision while
        // retaining the exact source. Compare its payload as well as cloud identity.
        var comparable = current with
        {
            LocalRevision = prepared.Source.LocalRevision,
            UpdatedAtUtc = prepared.Source.UpdatedAtUtc,
            LastSyncedAtUtc = prepared.Source.LastSyncedAtUtc
        };
        if (current.DocumentId != prepared.Source.DocumentId || current.LocalRevision < prepared.Source.LocalRevision
            || current.DeletedAtUtc is not null || current.ServerDocumentId != prepared.Source.ServerDocumentId
            || current.ServerVersion != prepared.Source.ServerVersion || current.SyncState != LocalSyncState.Synced
            || !LocalDocumentCodec.Encode(comparable).SequenceEqual(LocalDocumentCodec.Encode(prepared.Source)))
            throw new InvalidOperationException("The manuscript or planning changed. Synchronize and run AI again.");
    }
    public static LocalDocument Apply(LocalDocument current, AdvancedAiPrepared prepared, DeviceAiProposal proposal)
    {
        RequireFresh(current, prepared);
        if (proposal.Prepared != prepared.Request || proposal.ProposedText.Length > 100000) throw new InvalidDataException("Proposal target mismatch.");
        if (prepared.Target == LocalAiTarget.SynopsisField && prepared.Request.Key == "synopsis.story_coach")
        {
            string field = SynopsisFields.Single(x => x.Key == prepared.Field).Field;
            return LocalPlanning.Synopsis(current, SynopsisFieldsComponentUpdate(current.Project!.Synopsis ?? new(), field, proposal.ProposedText));
        }
        if (prepared.Target == LocalAiTarget.SceneCard && prepared.Request.Key is "scene.suggest" or "scene.refine"
            && SceneProposal(proposal) is { } p)
        {
            var node = current.Project!.Nodes.Single(n => n.NodeId == prepared.NodeId && n.DeletionId is null);
            var c = node.Card ?? LocalPlanning.EmptyCard;
            c = c with
            {
                Summary = p.Summary ?? c.Summary,
                NarrativePurpose = p.NarrativePurpose ?? c.NarrativePurpose,
                NarrativeRole = p.NarrativeRole ?? c.NarrativeRole,
                NarrativeIntent = p.NarrativeIntent ?? c.NarrativeIntent,
                EmotionalBeat = p.EmotionalBeat ?? c.EmotionalBeat,
                KeyEvents = p.KeyEvents ?? c.KeyEvents,
                OpenQuestions = p.OpenQuestions ?? c.OpenQuestions
            };
            return LocalPlanning.Scene(current, node.NodeId, c, node.Notes ?? "");
        }
        throw new InvalidDataException("This result is analysis only, or lacks a valid typed scene-card proposal.");
    }
    public static SectionSceneCardProposalDto? SceneProposal(DeviceAiProposal proposal) =>
        SceneCardAiProposalParser.TryParse(proposal.ProposedText, out var parsed, out _)
            ? parsed : proposal.SceneCard;
    private static SyncSynopsis SynopsisFieldsComponentUpdate(SyncSynopsis value, string field, string text) =>
        WriterApp.UI.Shared.Projects.SynopsisFields.Update(value, new(field, text));
}
