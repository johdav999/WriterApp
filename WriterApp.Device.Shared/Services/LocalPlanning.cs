using System.Text.Json;
using WriterApp.Device.Shared.Storage;
using WriterApp.Shared.Sync;

namespace WriterApp.Device.Shared.Services;

public static class LocalPlanning
{
    public static readonly SyncSceneCard EmptyCard = new(null, null, null, null, null, null, null, "Draft", null, null, null, null, null, null, null);
    public static LocalDocument Scene(LocalDocument document, Guid nodeId, SyncSceneCard card, string notes)
    {
        var project = Active(document);
        if (!project.Nodes.Any(n => n.NodeId == nodeId && n.NodeType == "scene" && n.DeletionId is null)) throw new InvalidOperationException("Select an active scene.");
        return document with { Project = project with { Version = Math.Max(2, project.Version), Nodes = project.Nodes.Select(n => n.NodeId == nodeId ? n with { Card = card, Notes = notes } : n).ToArray() } };
    }
    public static LocalDocument Synopsis(LocalDocument document, SyncSynopsis synopsis) =>
        document with { Project = Active(document) with { Version = Math.Max(2, document.Project!.Version), Synopsis = synopsis } };
    public static LocalDocument AddAnnotation(LocalDocument document, Guid nodeId, string kind, string text, string quote)
    {
        var project = Active(document);
        var scene = project.Nodes.Single(n => n.NodeId == nodeId && n.NodeType == "scene" && n.DeletionId is null);
        if (string.IsNullOrWhiteSpace(text)) throw new InvalidOperationException("Enter a comment or task.");
        Guid id = Guid.NewGuid();
        // Device quotes are explicit text evidence, not ProseMirror positions. Never fabricate a web range.
        var value = new SyncSceneAnnotation(id, kind, "open", 0, 0, quote, text, "", DateTimeOffset.UtcNow,
            AnchorDetached: quote.Length > 0 && !UniqueQuote(SceneText(document, scene), quote));
        return document with { Project = project with { Version = Math.Max(2, project.Version), Nodes = project.Nodes.Select(n => n.NodeId == nodeId
            ? n with { Annotations = n.Annotations.Append(new LocalSceneAnnotation(id, null, value)).ToArray() } : n).ToArray() } };
    }
    public static LocalDocument Resolve(LocalDocument document, Guid nodeId, Guid annotationId, bool resolved)
    {
        var project = Active(document);
        if (!project.Nodes.Any(n => n.NodeId == nodeId && n.DeletionId is null && n.Annotations.Any(a => a.LocalId == annotationId)))
            throw new InvalidOperationException("Annotation unavailable.");
        return document with { Project = project with { Version = Math.Max(2, project.Version), Nodes = project.Nodes.Select(n => n.NodeId == nodeId ? n with
        { Annotations = n.Annotations.Select(a => a.LocalId == annotationId ? a with { Value = a.Value with
            { Status = resolved ? "resolved" : "open", ResolvedAt = resolved ? DateTimeOffset.UtcNow : null } } : a).ToArray() } : n).ToArray() } };
    }
    public static string SceneText(LocalDocument document, LocalProjectNode node) => string.Join("\n\n",
        document.Sections.Where(s => s.SectionId == node.SectionId).SelectMany(s => s.Pages.OrderBy(p => p.OrderIndex)).Select(LocalDocumentPreview.PlainText));
    public static bool UniqueQuote(string text, string quote)
    {
        if (string.IsNullOrEmpty(quote)) return false;
        int index = text.IndexOf(quote, StringComparison.Ordinal);
        return index >= 0 && text.IndexOf(quote, index + 1, StringComparison.Ordinal) < 0;
    }
    public static LocalDocument Reconcile(LocalDocument previous, LocalDocument next)
    {
        if (next.Project is not { } project) return next;
        return next with { Project = project with { Nodes = project.Nodes.Select(n =>
        {
            if (n.NodeType != "scene") return n;
            var before = previous.Project?.Nodes.FirstOrDefault(x => x.NodeId == n.NodeId);
            bool changed = before is not null && SceneText(previous, before) != SceneText(next, n);
            return n with { Annotations = n.Annotations.Select(a => a with { Value = a.Value with {
                AnchorDetached = a.Value.AnchorDetached || (a.Value.AnchorText.Length > 0 && !UniqueQuote(SceneText(next, n), a.Value.AnchorText)),
                AnchorFrom = changed ? 0 : a.Value.AnchorFrom, AnchorTo = changed ? 0 : a.Value.AnchorTo } }).ToArray() };
        }).ToArray() } };
    }
    private static LocalProject Active(LocalDocument document) => document.DeletedAtUtc is null && document.Project is { } project
        ? project : throw new InvalidOperationException("Select an active project.");
    public static void Validate(LocalProject project)
    {
        if (project.Nodes.Any(n => n is null || n.Annotations is null)) throw new JsonException("Invalid scene planning metadata.");
        if (project.Version < 2 && (project.Synopsis is not null || project.Nodes.Any(n => n.Annotations.Count > 0))) throw new JsonException("Planning requires project version 2.");
        var ids = new HashSet<Guid>();
        foreach (var node in project.Nodes)
        {
            if (node.Annotations is null || node.Notes?.Length > 100_000 || node.Card?.Status?.Length > 16 || node.Card?.TimeRef?.Length > 120)
                throw new JsonException("Invalid scene planning metadata.");
            foreach (var a in node.Annotations)
                if (a is null || a.Value is null || a.LocalId == Guid.Empty || a.ServerId == Guid.Empty || !ids.Add(a.LocalId)
                    || node.NodeType != "scene" || a.Value.Kind is not ("comment" or "todo" or "highlight") || a.Value.Status is not ("open" or "resolved")
                    || a.Value.Content is null || a.Value.Content.Length > 100_000 || a.Value.AnchorText is null || a.Value.AnchorText.Length > 100_000
                    || a.Value.CreatedAt == default || a.Value.AnchorFrom < 0 || a.Value.AnchorTo < a.Value.AnchorFrom)
                    throw new JsonException("Invalid scene annotation.");
        }
    }
}
