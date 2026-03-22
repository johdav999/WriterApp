using System;
using System.Collections.Generic;
using System.Linq;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public sealed record ProjectNodeIntegrityIssue(
        Guid NodeId,
        string NodeType,
        string Title,
        string Code,
        string Message,
        Guid? ParentId,
        string? ParentNodeType,
        string? ParentTitle);

    public static class ProjectNodeHierarchyValidator
    {
        public static bool TryParseNodeType(string? value, out ProjectNodeType nodeType)
        {
            nodeType = ProjectNodeType.Scene;
            if (!ProjectNodeHierarchyRules.TryNormalizeNodeType(value, out string normalized))
            {
                return false;
            }

            switch (normalized)
            {
                case ProjectNodeHierarchyRules.Part:
                    nodeType = ProjectNodeType.Part;
                    return true;
                case ProjectNodeHierarchyRules.Chapter:
                    nodeType = ProjectNodeType.Chapter;
                    return true;
                case ProjectNodeHierarchyRules.Scene:
                    nodeType = ProjectNodeType.Scene;
                    return true;
                case ProjectNodeHierarchyRules.FrontMatterItem:
                    nodeType = ProjectNodeType.FrontMatterItem;
                    return true;
                default:
                    return false;
            }
        }

        public static string NormalizeNodeType(ProjectNodeType nodeType)
        {
            return nodeType switch
            {
                ProjectNodeType.Part => ProjectNodeHierarchyRules.Part,
                ProjectNodeType.Chapter => ProjectNodeHierarchyRules.Chapter,
                ProjectNodeType.FrontMatterItem => ProjectNodeHierarchyRules.FrontMatterItem,
                _ => ProjectNodeHierarchyRules.Scene
            };
        }

        public static bool IsPlacementAllowed(ProjectNodeType nodeType, ProjectNodeType? parentNodeType)
        {
            return ProjectNodeHierarchyRules.IsPlacementAllowed(
                NormalizeNodeType(nodeType),
                parentNodeType.HasValue ? NormalizeNodeType(parentNodeType.Value) : null);
        }

        public static bool WouldCreateCycle(Guid nodeId, Guid? proposedParentId, IReadOnlyDictionary<Guid, ProjectNodeRecord> nodesById)
        {
            if (!proposedParentId.HasValue)
            {
                return false;
            }

            HashSet<Guid> visited = new();
            Guid? current = proposedParentId;
            while (current.HasValue)
            {
                if (!visited.Add(current.Value))
                {
                    return true;
                }

                if (current.Value == nodeId)
                {
                    return true;
                }

                if (!nodesById.TryGetValue(current.Value, out ProjectNodeRecord? parent))
                {
                    return false;
                }

                current = parent.ParentId;
            }

            return false;
        }

        public static IReadOnlyList<ProjectNodeIntegrityIssue> Evaluate(IEnumerable<ProjectNodeRecord> nodes)
        {
            List<ProjectNodeRecord> materialized = nodes.ToList();
            Dictionary<Guid, ProjectNodeRecord> byId = materialized.ToDictionary(node => node.Id);
            List<ProjectNodeIntegrityIssue> issues = new();
            HashSet<string> issueKeys = new(StringComparer.Ordinal);

            void AddIssue(
                ProjectNodeRecord node,
                string code,
                string message,
                ProjectNodeRecord? parent = null)
            {
                string key = $"{node.Id:D}:{code}";
                if (!issueKeys.Add(key))
                {
                    return;
                }

                issues.Add(new ProjectNodeIntegrityIssue(
                    node.Id,
                    NormalizeNodeType(node.NodeType),
                    node.Title,
                    code,
                    message,
                    node.ParentId,
                    parent is null ? null : NormalizeNodeType(parent.NodeType),
                    parent?.Title));
            }

            foreach (ProjectNodeRecord node in materialized)
            {
                ProjectNodeRecord? parent = null;
                if (node.ParentId == node.Id)
                {
                    AddIssue(node, "self_parent", "Node cannot be its own parent.");
                }

                if (node.ParentId.HasValue)
                {
                    if (!byId.TryGetValue(node.ParentId.Value, out parent))
                    {
                        AddIssue(node, "missing_parent", "Parent node does not exist.");
                        continue;
                    }

                    if (parent.ProjectId != node.ProjectId)
                    {
                        AddIssue(node, "cross_project_parent", "Parent node belongs to a different project.", parent);
                    }

                    if (!IsPlacementAllowed(node.NodeType, parent.NodeType))
                    {
                        AddIssue(node, "invalid_parent_type", "Parent node type is not allowed for this child type.", parent);
                    }
                }
                else if (!IsPlacementAllowed(node.NodeType, parentNodeType: null))
                {
                    AddIssue(node, "invalid_root_type", "Node type is not allowed at the project root.");
                }
            }

            foreach (ProjectNodeRecord node in materialized)
            {
                if (WouldCreateCycle(node.Id, node.ParentId, byId))
                {
                    AddIssue(node, "cycle", "Node participates in a parent cycle.");
                }
            }

            return issues;
        }
    }
}
