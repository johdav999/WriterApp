using System.Text.Json;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;
using WriterApp.UI.Shared.Projects;

namespace WriterApp.Device.Shared.Services;

/// <summary>Durable, target-scoped AI undo. Never replaces a whole document snapshot.</summary>
public sealed class LocalAiHistoryActions(LocalDocumentRepository documents, LocalAiStore history)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task ChangeAsync(Guid documentId, Guid entryId, bool redo, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var entry = (await history.HistoryAsync(documentId, ct)).Single(h => h.Id == entryId);
            var current = await documents.LoadAsync(documentId, ct) ?? throw new IOException("Document unavailable.");
            var next = Change(current, entry, redo);
            // Record intent first. A retry after interruption can finish an already saved change.
            await history.SaveHistoryAsync(entry with { Status = redo ? "Redoing" : "Undoing" }, ct);
            if (!LocalDocumentCodec.Encode(next).SequenceEqual(LocalDocumentCodec.Encode(current)))
                await documents.SaveAsync(next, ct);
            await history.SaveHistoryAsync(entry with { Status = redo ? "Applied" : "Undone" }, CancellationToken.None);
        }
        finally { _gate.Release(); }
    }

    public static string? UnavailableReason(LocalDocument current, LocalAiHistory entry, bool redo)
    {
        try { Change(current, entry, redo); return null; }
        catch (Exception e) when (e is InvalidOperationException or InvalidDataException or JsonException) { return e.Message; }
    }

    public static LocalDocument Change(LocalDocument current, LocalAiHistory entry, bool redo)
    {
        if (current.DocumentId != entry.DocumentId || current.DeletedAtUtc is not null || current.SyncState == LocalSyncState.Conflict)
            throw new InvalidOperationException("Restore the document and resolve any sync conflict before changing AI history.");
        if (redo ? entry.Status is not ("Undone" or "Redoing") : entry.Status is not ("Applied" or "Undoing"))
            throw new InvalidOperationException(entry.Status == "Reviewed" ? "This result was reviewed without changing your document." : "This operation has no completed change to undo.");
        bool retry = entry.Status is "Undoing" or "Redoing";
        var after = AppliedSnapshot(entry);
        var expected = redo ? entry.Before : after;
        var desired = redo ? after : entry.Before;
        bool changed = false;
        T Replace<T>(T actual, T before, T result)
        {
            if (EqualityComparer<T>.Default.Equals(before, result)) return actual;
            changed = true;
            if (retry && EqualityComparer<T>.Default.Equals(actual, result)) return actual;
            if (!EqualityComparer<T>.Default.Equals(actual, before))
                throw new InvalidOperationException("This writing or planning has changed since the AI action. Use a recovery copy to keep your later edits.");
            return result;
        }
        LocalDocument next;
        if (entry.PageId is { } pageId)
        {
            var page = current.Sections.SelectMany(s => s.Pages).SingleOrDefault(p => p.PageId == pageId)
                ?? throw new InvalidOperationException("The affected page is no longer available.");
            var from = expected.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == pageId);
            var to = desired.Sections.SelectMany(s => s.Pages).Single(p => p.PageId == pageId);
            DeviceContentCompatibility.RequireEditable(page.Content, page.ContentFormat);
            DeviceContentCompatibility.RequireEditable(to.Content, to.ContentFormat);
            // Compare format together with content, even when only one changed.
            var value = Replace((page.Content, page.ContentFormat), (from.Content, from.ContentFormat), (to.Content, to.ContentFormat));
            next = current with { Sections = current.Sections.Select(s => s with { Pages = s.Pages.Select(p => p.PageId == pageId
                ? p with { Content = value.Item1, ContentFormat = value.Item2 } : p).ToArray() }).ToArray() };
        }
        else if (entry.Target.StartsWith("SynopsisField:", StringComparison.Ordinal))
        {
            RequireProject(current, entry);
            string field = entry.Target["SynopsisField:".Length..];
            var synopsis = current.Project!.Synopsis ?? new();
            string text = Replace(AdvancedAiRequests.SynopsisValue(synopsis, field),
                AdvancedAiRequests.SynopsisValue(expected.Project!.Synopsis ?? new(), field),
                AdvancedAiRequests.SynopsisValue(desired.Project!.Synopsis ?? new(), field));
            string name = AdvancedAiRequests.SynopsisFields.Single(f => f.Key == field).Field;
            next = LocalPlanning.Synopsis(current, SynopsisFields.Update(synopsis, new(name, text)));
        }
        else if (entry.Target.StartsWith("SceneCard:", StringComparison.Ordinal))
        {
            RequireProject(current, entry);
            Guid id = SceneNode(entry);
            var node = current.Project!.Nodes.SingleOrDefault(n => n.NodeId == id && n.DeletionId is null)
                ?? throw new InvalidOperationException("The affected scene is no longer available.");
            var actual = node.Card ?? LocalPlanning.EmptyCard;
            var from = expected.Project!.Nodes.Single(n => n.NodeId == id).Card ?? LocalPlanning.EmptyCard;
            var to = desired.Project!.Nodes.Single(n => n.NodeId == id).Card ?? LocalPlanning.EmptyCard;
            var card = actual with {
                Summary = Replace(actual.Summary, from.Summary, to.Summary),
                NarrativePurpose = Replace(actual.NarrativePurpose, from.NarrativePurpose, to.NarrativePurpose),
                NarrativeRole = Replace(actual.NarrativeRole, from.NarrativeRole, to.NarrativeRole),
                NarrativeIntent = Replace(actual.NarrativeIntent, from.NarrativeIntent, to.NarrativeIntent),
                EmotionalBeat = Replace(actual.EmotionalBeat, from.EmotionalBeat, to.EmotionalBeat),
                KeyEvents = Replace(actual.KeyEvents, from.KeyEvents, to.KeyEvents),
                OpenQuestions = Replace(actual.OpenQuestions, from.OpenQuestions, to.OpenQuestions)
            };
            next = LocalPlanning.Scene(current, id, card, node.Notes ?? "");
        }
        else throw new InvalidOperationException("This analysis did not change writing or planning.");
        if (!changed) throw new InvalidOperationException("This operation made no changes to undo.");
        return next;
    }

    private static void RequireProject(LocalDocument current, LocalAiHistory entry)
    {
        if (current.Project is null || current.Project.ProjectId != entry.Before.Project?.ProjectId)
            throw new InvalidOperationException("The original project is no longer available.");
    }
    private static Guid SceneNode(LocalAiHistory entry)
    {
        if (entry.NodeId is { } id) return id;
        // Older records did not store the node identity. Only use unambiguous evidence.
        var matches = entry.Before.Project?.Nodes.Where(n => n.NodeType == "scene" && n.DeletionId is null
            && JsonSerializer.Serialize(n.Card) == entry.OriginalText).ToArray();
        if (matches?.Length != 1) throw new InvalidOperationException("This older operation has no unique scene target. Recover its original as a copy.");
        return matches[0].NodeId;
    }
    private static LocalDocument AppliedSnapshot(LocalAiHistory entry)
    {
        if (entry.After is { } after) return after;
        if (entry.PageId is { } pageId && entry.AfterHtml is { } html)
            return entry.Before with { Sections = entry.Before.Sections.Select(s => s with { Pages = s.Pages.Select(p => p.PageId == pageId
                ? p with { Content = html, ContentFormat = LocalContentFormat.Html } : p).ToArray() }).ToArray() };
        if (entry.Target.StartsWith("SynopsisField:", StringComparison.Ordinal))
        {
            string field = entry.Target["SynopsisField:".Length..];
            string name = AdvancedAiRequests.SynopsisFields.SingleOrDefault(f => f.Key == field).Field
                ?? throw new InvalidDataException("This older operation has no synopsis field target.");
            return LocalPlanning.Synopsis(entry.Before, SynopsisFields.Update(entry.Before.Project!.Synopsis ?? new(), new(name, entry.Proposed)));
        }
        if (entry.Target.StartsWith("SceneCard:", StringComparison.Ordinal)
            && SceneCardAiProposalParser.TryParse(entry.Proposed, out var p, out _) && p is not null)
        {
            Guid id = SceneNode(entry);
            var node = entry.Before.Project!.Nodes.Single(n => n.NodeId == id);
            var c = node.Card ?? LocalPlanning.EmptyCard;
            return LocalPlanning.Scene(entry.Before, id, c with { Summary = p.Summary ?? c.Summary,
                NarrativePurpose = p.NarrativePurpose ?? c.NarrativePurpose, NarrativeRole = p.NarrativeRole ?? c.NarrativeRole,
                NarrativeIntent = p.NarrativeIntent ?? c.NarrativeIntent, EmotionalBeat = p.EmotionalBeat ?? c.EmotionalBeat,
                KeyEvents = p.KeyEvents ?? c.KeyEvents, OpenQuestions = p.OpenQuestions ?? c.OpenQuestions }, node.Notes ?? "");
        }
        throw new InvalidDataException("This operation has no saved result to undo. Recover its original as a copy.");
    }
}
