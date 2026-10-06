using System.Text.Json;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Canon;

namespace WriterApp.Device.Shared.Services;

public sealed record LocalSceneReview(AdvancedAiPrepared Prepared, DeviceCanonContext Canon, IReadOnlyList<SceneEntity> Entities, SceneCoachingProposal Proposal, IReadOnlyList<SceneFieldChange> Changes, string Raw);
public static class LocalSceneCoaching
{
    public static IReadOnlyList<SceneEntity> Catalogue(LocalDocument doc, DeviceCanonContext canon) {
        if (doc.Project is null || canon.DocumentId != doc.ServerDocumentId || canon.DocumentVersion != doc.ServerVersion)
            throw new InvalidDataException("Scene canon does not match this project and manuscript revision.");
        var result = new List<SceneEntity>();
        foreach (var pair in canon.Snapshots) {
            var snapshot = pair.Value;
            if (!Enum.IsDefined(pair.Key) || snapshot.ContractVersion != 1 || !snapshot.Exists || snapshot.Kind != pair.Key || snapshot.DocumentId != canon.DocumentId || snapshot.SourceDocumentVersion != canon.DocumentVersion
                || snapshot.CheckedDocumentVersion != canon.DocumentVersion || string.IsNullOrWhiteSpace(snapshot.SnapshotVersion))
                throw new InvalidDataException("Scene canon identity or source changed.");
            var kind = pair.Key switch { CanonKind.Character => SceneEntityKind.Character, CanonKind.Place => SceneEntityKind.Place, _ => SceneEntityKind.Timeline };
            result.AddRange(CanonContent.Parse(pair.Key, snapshot.ContentJson).Entries.Where(e => e.Id is not null).Select(e => new SceneEntity(kind, e.Id!, e.Name)));
        }
        // Links persist in the server namespace; local scene/section IDs are used only to select the authored target.
        result.AddRange(doc.Project.Nodes.Where(n => n.DeletionId is null && n.NodeType is "scene" or "chapter" or "part")
            .Select(n => new SceneEntity(n.NodeType switch { "scene" => SceneEntityKind.Scene, "chapter" => SceneEntityKind.Chapter, _ => SceneEntityKind.Part }, (n.ServerNodeId ?? n.NodeId).ToString(), n.Title)));
        result.AddRange(AdvancedAiRequests.ActiveSections(doc).Select(s => new SceneEntity(SceneEntityKind.Section, (s.ServerSectionId ?? s.SectionId).ToString(), s.Title)));
        return result;
    }
    public static AdvancedAiPrepared Prepare(LocalDocument doc, Guid sectionId, SceneCoachingField? field, string instruction, bool refine,
        DeviceCanonContext canon, IReadOnlyList<ConsistencyPageText> pages) {
        var prepared = AdvancedAiRequests.Build(doc, sectionId, AdvancedAiAction.SceneCard, consistencyPages: pages);
        if (field is { } f && !Enum.IsDefined(f)) throw new InvalidDataException("Unknown scene coaching field.");
        if (instruction.Length > 2000) throw new InvalidDataException("Use at most 2,000 characters of coaching notes.");
        var section = doc.Sections.Single(s => s.SectionId == sectionId);
        if (pages.Count != section.Pages.Count || pages.Select(p => p.PageId).Distinct().Count() != pages.Count || pages.Any(p => !section.Pages.Any(s => s.PageId == p.PageId)))
            throw new InvalidDataException("Include each saved scene page exactly once.");
        string text = string.Join("\n\n", section.Pages.OrderBy(p => p.OrderIndex).Select(p => pages.Single(x => x.PageId == p.PageId).PlainText));
        if (string.IsNullOrWhiteSpace(text) || text.Length > 100_000) throw new InvalidDataException("Scene coaching supports 1–100,000 characters of complete section text.");
        var entities = Catalogue(doc, canon);
        string catalogue = JsonSerializer.Serialize(entities.Select(e => new { kind = e.Kind.ToString().ToLowerInvariant(), id = e.Id, name = e.Name }));
        string card = JsonSerializer.Serialize(doc.Project!.Nodes.Single(n => n.NodeId == prepared.NodeId).Card ?? LocalPlanning.EmptyCard);
        if (catalogue.Length > 100_000 || card.Length > 100_000) throw new InvalidDataException("Scene planning or canon exceeds the supported context size.");
        var parameters = new Dictionary<string, object?> {
            ["instruction"] = (field is { } selected ? $"Suggest ONLY {SceneCoaching.Key(selected)}. Leave every other field unchanged. " : "Suggest scene fields for individual approval. ") + instruction,
            ["scene_coaching_version"] = 1, ["focus_field"] = field is { } focus ? SceneCoaching.Key(focus) : "",
            ["current_scene_card"] = card, ["scene_entities_json"] = catalogue };
        foreach (var kind in Enum.GetValues<CanonKind>()) parameters[kind.ToString().ToLowerInvariant() + "_bible_json"] = canon.Snapshots.TryGetValue(kind, out var snapshot) ? snapshot.ContentJson : "{}";
        if (Enum.GetValues<CanonKind>().Sum(kind => parameters[kind.ToString().ToLowerInvariant() + "_bible_json"]!.ToString()!.Length) > 250_000)
            throw new InvalidDataException("Canon exceeds the supported scene context size; no snapshots were truncated.");
        return prepared with { Field = field is { } target ? SceneCoaching.Key(target) : "whole", Request = prepared.Request with {
            Key = field == SceneCoachingField.OpenQuestions ? "scene.find-open-questions" : refine ? "scene.refine" : "scene.suggest",
            Request = prepared.Request.Request with { OriginalText = card, SurroundingText = text, Parameters = parameters,
                ExpectedCanonVersions = canon.Snapshots.ToDictionary(p => p.Key, p => p.Value.SnapshotVersion) } } };
    }
    public static LocalSceneReview Review(AdvancedAiPrepared prepared, DeviceAiProposal result, DeviceCanonContext canon) {
        if (result.Prepared != prepared.Request || prepared.Target != LocalAiTarget.SceneCard) throw new InvalidDataException("Scene proposal target mismatch.");
        if (prepared.Request.Key is not ("scene.suggest" or "scene.refine" or "scene.find-open-questions")
            || prepared.Field != "whole" && !Enum.GetValues<SceneCoachingField>().Any(f => SceneCoaching.Key(f) == prepared.Field))
            throw new InvalidDataException("Unknown scene coaching action or scope.");
        var entities = Catalogue(prepared.Source, canon);
        var proposal = SceneCoaching.Parse(result.ProposedText, entities);
        var card = prepared.Source.Project!.Nodes.Single(n => n.NodeId == prepared.NodeId).Card ?? LocalPlanning.EmptyCard;
        var changes = new List<SceneFieldChange>();
        foreach (var field in Enum.GetValues<SceneCoachingField>()) {
            if (prepared.Field != "whole" && prepared.Field != SceneCoaching.Key(field)) continue;
            string? before = SceneCoaching.Value(card, field);
            if (proposal.Errors.TryGetValue(field, out var error)) changes.Add(new(field, SceneCoaching.Display(field, before, entities), "Unavailable", error));
            else if (proposal.Values.TryGetValue(field, out var value) && before != value)
                changes.Add(new(field, SceneCoaching.Display(field, before, entities), SceneCoaching.Display(field, value, entities)));
        }
        return new(prepared, canon, entities, proposal, changes, result.ProposedText);
    }
    public static LocalDocument Preview(LocalDocument current, LocalSceneReview review, IReadOnlyList<SceneCoachingField> approved) {
        AdvancedAiRequests.RequireFresh(current, review.Prepared);
        if (approved.Count == 0 || approved.Distinct().Count() != approved.Count || approved.Any(f => !review.Changes.Any(c => c.Field == f && c.Error is null)))
            throw new InvalidDataException("Approve at least one valid changed field from this review.");
        var node = current.Project!.Nodes.Single(n => n.NodeId == review.Prepared.NodeId && n.DeletionId is null && n.SectionId == review.Prepared.SectionId);
        var card = node.Card ?? LocalPlanning.EmptyCard;
        foreach (var f in approved) card = SceneCoaching.Set(card, f, review.Proposal.Values[f]);
        return LocalPlanning.Scene(current, node.NodeId, card, node.Notes ?? "");
    }
    public static void ValidateEntry(LocalAiHistory entry) {
        if (entry.Version != 4 || !entry.Target.StartsWith("SceneCard:", StringComparison.Ordinal) || entry.Action is not ("scene.suggest" or "scene.refine" or "scene.find-open-questions")
            || entry.PageId is not null || entry.Before.Project is null || entry.NodeId is null || entry.SectionId is null
            || entry.SourceRevision != entry.Before.LocalRevision || entry.ServerVersion != entry.Before.ServerVersion)
            throw new InvalidDataException("Invalid scene history target.");
        if (entry.Target != "SceneCard:whole" && !Enum.GetValues<SceneCoachingField>().Any(f => entry.Target == "SceneCard:" + SceneCoaching.Key(f))
            || entry.Action == "scene.find-open-questions" && entry.Target != "SceneCard:openQuestions") throw new InvalidDataException("Unknown scene history scope.");
        if (entry.SceneEntities is null || entry.SceneEntities.Count > 15_000 || entry.SceneEntities.Any(e => e is null || !Enum.IsDefined(e.Kind) || string.IsNullOrWhiteSpace(e.Id)
            || e.Id.Length > 200 || string.IsNullOrWhiteSpace(e.Name) || e.Name.Length > 1000) || entry.SceneEntities.Select(e => (e.Kind,e.Id)).Distinct().Count() != entry.SceneEntities.Count)
            throw new InvalidDataException("Invalid retained scene entity catalogue.");
        var proposal = SceneCoaching.Parse(entry.Proposed, entry.SceneEntities);
        var before = entry.Before.Project.Nodes.SingleOrDefault(n => n.NodeId == entry.NodeId && n.SectionId == entry.SectionId && n.DeletionId is null && n.NodeType == "scene")
            ?? throw new InvalidDataException("Scene history target is unavailable.");
        if (entry.After is null) {
            if (entry.Status != "Reviewed" || entry.SceneFields is not null) throw new InvalidDataException("Scene approval lacks its durable result.");
            return;
        }
        if (entry.SceneFields is not { Count: > 0 } fields || fields.Distinct().Count() != fields.Count || fields.Any(f => !Enum.IsDefined(f)
            || entry.Target != "SceneCard:whole" && entry.Target != "SceneCard:" + SceneCoaching.Key(f)))
            throw new InvalidDataException("Scene approval fields do not match the reviewed scope.");
        var after = entry.After.Project?.Nodes.SingleOrDefault(n => n.NodeId == before.NodeId) ?? throw new InvalidDataException("Scene result target changed.");
        var card = before.Card ?? LocalPlanning.EmptyCard;
        foreach (var f in fields) {
            string? value = SceneCoaching.Value(after.Card ?? LocalPlanning.EmptyCard, f);
            if (!proposal.Values.TryGetValue(f,out var proposed) || proposed != value || SceneCoaching.Value(card,f) == value)
                throw new InvalidDataException("Approved scene result does not match its validated proposal.");
            card = SceneCoaching.Set(card, f, value);
        }
        var expected = LocalPlanning.Scene(entry.Before, before.NodeId, card, before.Notes ?? "");
        if (!LocalDocumentCodec.Encode(expected).SequenceEqual(LocalDocumentCodec.Encode(entry.After)))
            throw new InvalidDataException("Scene approval changes writing or unapproved planning.");
    }
    public static async Task ResumeAsync(LocalDocumentRepository documents, LocalAiStore store, LocalAiHistory entry, CancellationToken ct = default) {
        ValidateEntry(entry);
        if (entry.Status != "Applying" || entry.After is null) throw new InvalidOperationException("Only an explicitly approved interrupted scene save can be finished.");
        var current = await documents.LoadAsync(entry.DocumentId, ct) ?? throw new IOException("Document unavailable.");
        if (current.DeletedAtUtc is not null || current.SyncState == LocalSyncState.Conflict || current.ServerDocumentId != entry.Before.ServerDocumentId || current.ServerVersion != entry.Before.ServerVersion)
            throw new InvalidOperationException("Scene recovery source changed. Recover the original as a copy.");
        if (Signature(current) != Signature(entry.After)) {
            if (Signature(current) != Signature(entry.Before)) throw new InvalidOperationException("Scene planning or writing has later changes. Recover the original as a copy.");
            await documents.SaveAsync(entry.After, ct);
        }
        await store.SaveHistoryAsync(entry with { Status = "Applied" }, CancellationToken.None);
    }
    private static string Signature(LocalDocument doc) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(LocalDocumentCodec.Encode(doc with {
        LocalRevision = 1, UpdatedAtUtc = doc.CreatedAtUtc, LastSyncedAtUtc = null, SyncState = LocalSyncState.Synced })));
    public static string Readable(LocalAiHistory entry, bool original) {
        ValidateEntry(entry);
        var proposal = SceneCoaching.Parse(entry.Proposed, entry.SceneEntities!);
        var card = entry.Before.Project!.Nodes.Single(n => n.NodeId == entry.NodeId).Card ?? LocalPlanning.EmptyCard;
        var fields = entry.SceneFields ?? Enum.GetValues<SceneCoachingField>().Where(f => (entry.Target == "SceneCard:whole" || entry.Target == "SceneCard:" + SceneCoaching.Key(f))
            && (proposal.Values.ContainsKey(f) || proposal.Errors.ContainsKey(f))).ToArray();
        return string.Join("\n\n", fields.Select(f => SceneCoaching.Label(f) + ": " + (original ? SceneCoaching.Display(f, SceneCoaching.Value(card,f),entry.SceneEntities!)
            : proposal.Errors.TryGetValue(f,out var error) ? error : SceneCoaching.Display(f, proposal.Values[f],entry.SceneEntities!))));
    }
    public static void RequireCurrentLinks(LocalDocument current, LocalAiHistory entry, DeviceCanonContext canon) {
        if (entry.Version != 4 || entry.SceneFields is null) return;
        var links = entry.SceneFields.Where(f => f is SceneCoachingField.PovCharacterId or SceneCoachingField.PlaceId or SceneCoachingField.TimelineEventId or SceneCoachingField.References).ToArray();
        if (links.Length == 0) return;
        var proposal = SceneCoaching.Parse(entry.Proposed, Catalogue(current,canon));
        foreach (var field in links) if (proposal.Errors.TryGetValue(field,out var error)) throw new InvalidOperationException(error + " Refresh canon before reapplying this history entry.");
    }
}
