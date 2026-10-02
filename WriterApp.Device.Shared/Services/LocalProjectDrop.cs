using WriterApp.Application.Documents;
using WriterApp.Device.Shared.Storage;

namespace WriterApp.Device.Shared.Services;

public static class LocalProjectDrop
{
    public static LocalProjectChange? Resolve(LocalProject project, Guid id, Guid? targetId, string placement)
    {
        var node = project.Nodes.SingleOrDefault(n => n.NodeId == id && n.DeletionId is null)
            ?? throw new InvalidOperationException("Dragged item no longer exists. Reload the project.");
        var target = targetId is { } targetNodeId
            ? project.Nodes.SingleOrDefault(n => n.NodeId == targetNodeId && n.DeletionId is null)
                ?? throw new InvalidOperationException("Drop target no longer exists. Reload the project.")
            : null;
        if (target?.NodeId == id) return null;
        Guid? parent;
        Guid? before;
        switch (placement)
        {
            case "inside": parent = target?.NodeId; before = null; break;
            case "before" when target is not null: parent = target.ParentId; before = target.NodeId; break;
            case "after" when target is not null:
                parent = target.ParentId;
                before = project.Nodes.Where(n => n.ParentId == parent && n.DeletionId is null && n.NodeId != id)
                    .OrderBy(n => n.OrderIndex).SkipWhile(n => n.NodeId != target.NodeId).Skip(1).FirstOrDefault()?.NodeId;
                break;
            default: throw new InvalidOperationException("Choose a valid outline drop position.");
        }
        var parentNode = parent is { } parentId ? project.Nodes.Single(n => n.NodeId == parentId) : null;
        if (!ProjectNodeHierarchyRules.IsPlacementAllowed(node.NodeType, parentNode?.NodeType))
            throw new InvalidOperationException("Scenes belong in chapters; chapters belong at the root or in parts; parts belong at the root.");
        var siblings = project.Nodes.Where(n => n.ParentId == parent && n.DeletionId is null).OrderBy(n => n.OrderIndex).Select(n => n.NodeId).ToList();
        var order = siblings.Where(n => n != id).ToList();
        order.Insert(before is { } beforeId ? order.IndexOf(beforeId) : order.Count, id);
        return node.ParentId == parent && order.SequenceEqual(siblings) ? null
            : new(LocalProjectAction.Move, id, parent, BeforeNodeId: before);
    }
}
