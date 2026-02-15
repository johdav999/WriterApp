using System;

namespace WriterApp.Data.Documents
{
    public sealed class SceneAnnotationRecord
    {
        public Guid Id { get; set; }

        public Guid SceneNodeId { get; set; }

        public ProjectNodeRecord? SceneNode { get; set; }

        public string Kind { get; set; } = "comment";

        public string Status { get; set; } = "open";

        public int AnchorFrom { get; set; }

        public int AnchorTo { get; set; }

        public string AnchorText { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string AuthorUserId { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }
    }
}
