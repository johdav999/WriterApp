using System;

namespace WriterApp.Data.Documents
{
    public sealed class SceneNoteRecord
    {
        public Guid SceneNodeId { get; set; }

        public ProjectNodeRecord? SceneNode { get; set; }

        public string NotesText { get; set; } = string.Empty;

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
