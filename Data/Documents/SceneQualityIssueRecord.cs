using System;

namespace WriterApp.Data.Documents
{
    public sealed class SceneQualityIssueRecord
    {
        public Guid Id { get; set; }

        public Guid SceneNodeId { get; set; }

        public ProjectNodeRecord? SceneNode { get; set; }

        public string Scope { get; set; } = "scene";

        public string IssueKey { get; set; } = string.Empty;

        public string RuleId { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Severity { get; set; } = "info";

        public string Message { get; set; } = string.Empty;

        public string? Suggestion { get; set; }

        public string? AnchorText { get; set; }

        public int StartOffset { get; set; }

        public int EndOffset { get; set; }

        public string ContentHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }
}
