using System;
using System.Collections.Generic;

namespace WriterApp.Application.Documents
{
    public static class ProjectNodeHierarchyRules
    {
        public const string Part = "part";
        public const string Chapter = "chapter";
        public const string Scene = "scene";
        public const string FrontMatterItem = "frontMatterItem";

        private static readonly IReadOnlyList<string> RootNodeTypes = new[]
        {
            Part,
            Chapter,
            FrontMatterItem
        };

        public static IReadOnlyList<string> AllowedRootNodeTypes => RootNodeTypes;

        public static bool TryNormalizeNodeType(string? value, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case Part:
                    normalized = Part;
                    return true;
                case Chapter:
                    normalized = Chapter;
                    return true;
                case Scene:
                    normalized = Scene;
                    return true;
                case "frontmatteritem":
                case "front_matter_item":
                case "front-matter-item":
                    normalized = FrontMatterItem;
                    return true;
                default:
                    return false;
            }
        }

        public static bool CanExistAtRoot(string nodeType)
        {
            return TryNormalizeNodeType(nodeType, out string normalized)
                && normalized is Part or Chapter or FrontMatterItem;
        }

        public static bool IsPlacementAllowed(string childNodeType, string? parentNodeType)
        {
            if (!TryNormalizeNodeType(childNodeType, out string child))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(parentNodeType))
            {
                return CanExistAtRoot(child);
            }

            if (!TryNormalizeNodeType(parentNodeType, out string parent))
            {
                return false;
            }

            return child switch
            {
                Part => false,
                Chapter => parent == Part,
                Scene => parent == Chapter,
                FrontMatterItem => false,
                _ => false
            };
        }

        public static IReadOnlyList<string> GetAllowedChildTypes(string? parentNodeType)
        {
            if (string.IsNullOrWhiteSpace(parentNodeType))
            {
                return RootNodeTypes;
            }

            if (!TryNormalizeNodeType(parentNodeType, out string normalizedParent))
            {
                return Array.Empty<string>();
            }

            return normalizedParent switch
            {
                Part => new[] { Chapter },
                Chapter => new[] { Scene },
                _ => Array.Empty<string>()
            };
        }
    }
}
