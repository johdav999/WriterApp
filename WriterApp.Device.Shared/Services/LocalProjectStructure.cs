using System.Text.Json;
using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public enum LocalProjectAction { RenameProject, Create, Rename, Move, Up, Down, Delete, Restore, RememberPage }
public sealed record LocalProjectChange(LocalProjectAction Action, Guid Id = default,
    Guid? ParentId = null, string? NodeType = null, string? Title = null, Guid? BeforeNodeId = null);

public static class LocalProjectStructure
{
    private static string Title(string? title) => title?.Trim() is { Length: > 0 and <= 200 } text
        ? text : throw new InvalidOperationException("Use a title of 1–200 characters.");

    public static LocalDocument Attach(LocalDocument document, string title)
    {
        if (document.Project is not null || document.DeletedAtUtc is not null)
            throw new InvalidOperationException("Choose an active standalone document.");
        if (document.ServerDocumentId is not null || document.SyncState != LocalSyncState.LocalOnly)
            throw new InvalidOperationException("Duplicate the cloud document first to create an independent project.");
        return AttachToProject(document, new() { Version = 3, ProjectId = Guid.NewGuid(), ManuscriptId = document.DocumentId,
            PrimaryDocumentId = document.DocumentId, Title = Title(title) });
    }

    internal static LocalDocument AttachToProject(LocalDocument document, LocalProject project)
    {
        if (document.Project is not null || document.DeletedAtUtc is not null)
            throw new InvalidOperationException("Choose an active standalone document.");
        var chapter = new LocalProjectNode { NodeId = Guid.NewGuid(), NodeType = "chapter", Title = "Chapter 1" };
        var nodes = new List<LocalProjectNode> { chapter };
        nodes.AddRange(document.Sections.OrderBy(s => s.OrderIndex).Select((s, index) => new LocalProjectNode
        { NodeId = Guid.NewGuid(), ParentId = chapter.NodeId, NodeType = "scene", Title = s.Title, SectionId = s.SectionId, OrderIndex = index }));
        return document with { Project = project with { Version = 3, ManuscriptId = document.DocumentId,
            Nodes = document.Kind == "manuscript" ? nodes : [], Synopsis = null, LastPageId = null, ExtensionData = null } };
    }

    public static LocalDocument Apply(LocalDocument document, LocalProjectChange change, DateTimeOffset now)
    {
        if (document.DeletedAtUtc is not null) throw new InvalidOperationException("Restore the manuscript first.");
        var project = document.Project ?? throw new InvalidOperationException("No local project.");
        var nodes = project.Nodes.ToList();
        LocalProjectNode Node() => nodes.SingleOrDefault(n => n.NodeId == change.Id)
            ?? throw new InvalidOperationException("Node no longer exists. Reload the project.");
        void Replace(LocalProjectNode node) => nodes[nodes.FindIndex(n => n.NodeId == node.NodeId)] = node;
        int Next(Guid? parent) => checked(nodes.Where(n => n.ParentId == parent && n.DeletionId is null).Select(n => n.OrderIndex).DefaultIfEmpty(-1).Max() + 1);
        if (change.Action == LocalProjectAction.RenameProject) project = project with { Title = Title(change.Title) };
        else if (change.Action == LocalProjectAction.RememberPage)
        {
            if (!document.Sections.SelectMany(s => s.Pages).Any(p => p.PageId == change.Id)) throw new InvalidOperationException("Page no longer exists.");
            project = project with { LastPageId = change.Id };
        }
        else if (change.Action == LocalProjectAction.Create)
        {
            Guid? sectionId = null;
            if (change.NodeType == "scene")
            {
                document = LocalDocumentStructure.Apply(document, new(LocalStructureAction.CreateSection, Title: Title(change.Title)), now);
                sectionId = document.Sections.MaxBy(s => s.OrderIndex)!.SectionId;
            }
            nodes.Add(new() { NodeId = Guid.NewGuid(), ParentId = change.ParentId, NodeType = change.NodeType ?? "",
                Title = Title(change.Title), OrderIndex = Next(change.ParentId), SectionId = sectionId });
        }
        else
        {
            var node = Node();
            if (change.Action != LocalProjectAction.Restore && node.DeletionId is not null) throw new InvalidOperationException("Restore the node first.");
            switch (change.Action)
            {
                case LocalProjectAction.Rename:
                    Replace(node with { Title = Title(change.Title) });
                    if (node.SectionId is { } renamed) document = document with { Sections = document.Sections.Select(s => s.SectionId == renamed ? s with { Title = Title(change.Title) } : s).ToArray() };
                    break;
                case LocalProjectAction.Move:
                    var destination = nodes.Where(n => n.ParentId == change.ParentId && n.DeletionId is null && n.NodeId != node.NodeId)
                        .OrderBy(n => n.OrderIndex).ToList();
                    int insert = change.BeforeNodeId is { } before ? destination.FindIndex(n => n.NodeId == before) : destination.Count;
                    if (insert < 0) throw new InvalidOperationException("Drop target no longer exists in this parent. Reload the project.");
                    destination.Insert(insert, node with { ParentId = change.ParentId });
                    for (int i = 0; i < destination.Count; i++) Replace(destination[i] with { OrderIndex = i });
                    if (node.ParentId != change.ParentId)
                    {
                        var remaining = nodes.Where(n => n.ParentId == node.ParentId && n.DeletionId is null && n.NodeId != node.NodeId)
                            .OrderBy(n => n.OrderIndex).ToArray();
                        for (int i = 0; i < remaining.Length; i++) Replace(remaining[i] with { OrderIndex = i });
                    }
                    break;
                case LocalProjectAction.Up:
                case LocalProjectAction.Down:
                    var siblings = nodes.Where(n => n.ParentId == node.ParentId && n.DeletionId is null).OrderBy(n => n.OrderIndex).ToArray();
                    int adjacent = Array.IndexOf(siblings, node) + (change.Action == LocalProjectAction.Up ? -1 : 1);
                    if (adjacent < 0 || adjacent >= siblings.Length) throw new InvalidOperationException("Already at the end of the list.");
                    var other = siblings[adjacent]; Replace(node with { OrderIndex = other.OrderIndex }); Replace(other with { OrderIndex = node.OrderIndex }); break;
                case LocalProjectAction.Delete:
                    var ids = new HashSet<Guid> { node.NodeId };
                    while (true) { int count = ids.Count; foreach (var child in nodes.Where(n => n.DeletionId is null && n.ParentId is { } p && ids.Contains(p))) ids.Add(child.NodeId); if (count == ids.Count) break; }
                    Guid deletion = Guid.NewGuid();
                    foreach (var removed in nodes.Where(n => ids.Contains(n.NodeId)).ToArray()) Replace(removed with { DeletionId = deletion });
                    break;
                case LocalProjectAction.Restore:
                    if (node.DeletionId is null) throw new InvalidOperationException("This node is already active.");
                    var restoring = nodes.Where(n => n.DeletionId == node.DeletionId).ToArray();
                    foreach (var restored in restoring) Replace(restored with { DeletionId = null, OrderIndex = Next(restored.ParentId) });
                    break;
                default: throw new InvalidOperationException("Unsupported project operation.");
            }
        }
        var result = document with { Project = project with { Nodes = nodes } };
        Validate(result);
        if (change.Action is not (LocalProjectAction.RememberPage or LocalProjectAction.RenameProject))
        {
            IEnumerable<Guid> OrderedSections(Guid? parent)
            {
                foreach (var node in nodes.Where(n => n.ParentId == parent && n.DeletionId is null).OrderBy(n => n.OrderIndex))
                { if (node.SectionId is { } id) yield return id; foreach (var child in OrderedSections(node.NodeId)) yield return child; }
            }
            var ordered = OrderedSections(null).ToArray();
            result = result with { Sections = ordered.Select(id => document.Sections.Single(s => s.SectionId == id))
                .Concat(document.Sections.OrderBy(s => s.OrderIndex).Where(s => !ordered.Contains(s.SectionId)))
                .Select((s, index) => s with { OrderIndex = index }).ToArray() };
        }
        return result;
    }

    public static void Validate(LocalDocument document)
    {
        if (document.Project is not { } project) return;
        if (project.Version is not (1 or 2 or 3) || project.ProjectId == Guid.Empty || project.ManuscriptId != document.DocumentId
            || project.ServerProjectId == Guid.Empty || string.IsNullOrWhiteSpace(project.Title) || project.Nodes is null)
            throw new JsonException("Invalid or unsupported local project. Original file preserved.");
        LocalPlanning.Validate(project);
        if (document.Kind != "manuscript" && project.Nodes.Count != 0)
            throw new JsonException("Supporting documents cannot contain manuscript structure.");
        var ids = new HashSet<Guid>(); var sections = new HashSet<Guid>();
        foreach (var node in project.Nodes)
        {
            if (node is null || node.NodeId == Guid.Empty || !ids.Add(node.NodeId) || node.ServerNodeId == Guid.Empty
                || string.IsNullOrWhiteSpace(node.Title) || node.OrderIndex < 0 || node.DeletionId == Guid.Empty
                || node.NodeType is not ("part" or "chapter" or "scene")) throw new JsonException("Invalid project node.");
        }
        foreach (var node in project.Nodes)
        {
            var parent = node.ParentId is { } id ? project.Nodes.SingleOrDefault(n => n.NodeId == id) : null;
            if ((node.ParentId is not null && parent is null) || !ProjectNodeHierarchyRules.IsPlacementAllowed(node.NodeType, parent?.NodeType)
                || (node.DeletionId is null && parent?.DeletionId is not null)) throw new JsonException("Invalid tree parent or cycle.");
            // Allowed Part -> Chapter -> Scene placement also rules out every possible cycle.
            if (node.NodeType == "scene")
            {
                if (node.SectionId is not { } section || !sections.Add(section) || !document.Sections.Any(s => s.SectionId == section))
                    throw new JsonException("A scene must reference a unique active manuscript section. Restore/remove its project node before removing writing.");
            }
            else if (node.SectionId is not null) throw new JsonException("Only scenes reference writing.");
        }
        if (project.Nodes.Where(n => n.DeletionId is null).GroupBy(n => (n.ParentId, n.OrderIndex)).Any(g => g.Count() > 1))
            throw new JsonException("Ambiguous project ordering.");
    }
}
