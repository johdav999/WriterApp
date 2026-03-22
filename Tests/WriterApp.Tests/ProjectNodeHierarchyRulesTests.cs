using System;
using System.Collections.Generic;
using WriterApp.Application.Documents;
using WriterApp.Data.Documents;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class ProjectNodeHierarchyRulesTests
    {
        [Theory]
        [InlineData(ProjectNodeHierarchyRules.Part, null)]
        [InlineData(ProjectNodeHierarchyRules.Chapter, null)]
        [InlineData(ProjectNodeHierarchyRules.Chapter, ProjectNodeHierarchyRules.Part)]
        [InlineData(ProjectNodeHierarchyRules.Scene, ProjectNodeHierarchyRules.Chapter)]
        public void IsPlacementAllowed_ReturnsTrue_ForValidPlacements(string childType, string? parentType)
        {
            bool allowed = ProjectNodeHierarchyRules.IsPlacementAllowed(childType, parentType);

            Assert.True(allowed);
        }

        [Theory]
        [InlineData(ProjectNodeHierarchyRules.Part, ProjectNodeHierarchyRules.Part)]
        [InlineData(ProjectNodeHierarchyRules.Part, ProjectNodeHierarchyRules.Chapter)]
        [InlineData(ProjectNodeHierarchyRules.Chapter, ProjectNodeHierarchyRules.Scene)]
        [InlineData(ProjectNodeHierarchyRules.Scene, null)]
        [InlineData(ProjectNodeHierarchyRules.Scene, ProjectNodeHierarchyRules.Part)]
        [InlineData("mysteryNode", null)]
        public void IsPlacementAllowed_ReturnsFalse_ForInvalidPlacements(string childType, string? parentType)
        {
            bool allowed = ProjectNodeHierarchyRules.IsPlacementAllowed(childType, parentType);

            Assert.False(allowed);
        }

        [Fact]
        public void TryNormalizeNodeType_ReturnsFalse_ForUnknownNodeType()
        {
            bool success = ProjectNodeHierarchyRules.TryNormalizeNodeType("unknown", out string normalized);

            Assert.False(success);
            Assert.Equal(string.Empty, normalized);
        }

        [Fact]
        public void Evaluate_ReturnsNoIssues_ForValidMixedStructure()
        {
            Guid projectId = Guid.NewGuid();
            ProjectNodeRecord rootChapter = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeType = ProjectNodeType.Chapter,
                Title = "Prelude",
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            ProjectNodeRecord rootChapterScene = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentId = rootChapter.Id,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene A",
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            ProjectNodeRecord part = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeType = ProjectNodeType.Part,
                Title = "Act I",
                OrderIndex = 1,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            ProjectNodeRecord nestedChapter = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentId = part.Id,
                NodeType = ProjectNodeType.Chapter,
                Title = "Chapter 1",
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            ProjectNodeRecord nestedScene = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentId = nestedChapter.Id,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene B",
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };

            IReadOnlyList<ProjectNodeIntegrityIssue> issues = ProjectNodeHierarchyValidator.Evaluate(new[]
            {
                rootChapter,
                rootChapterScene,
                part,
                nestedChapter,
                nestedScene
            });

            Assert.Empty(issues);
        }

        [Fact]
        public void Evaluate_ReturnsIssues_ForSceneUnderPart()
        {
            Guid projectId = Guid.NewGuid();
            ProjectNodeRecord part = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                NodeType = ProjectNodeType.Part,
                Title = "Act I",
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            ProjectNodeRecord invalidScene = new()
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                ParentId = part.Id,
                NodeType = ProjectNodeType.Scene,
                Title = "Scene 1",
                OrderIndex = 0,
                UpdatedUtc = DateTimeOffset.UtcNow
            };

            IReadOnlyList<ProjectNodeIntegrityIssue> issues = ProjectNodeHierarchyValidator.Evaluate(new[]
            {
                part,
                invalidScene
            });

            ProjectNodeIntegrityIssue issue = Assert.Single(issues);
            Assert.Equal("invalid_parent_type", issue.Code);
            Assert.Equal(invalidScene.Id, issue.NodeId);
        }
    }
}
