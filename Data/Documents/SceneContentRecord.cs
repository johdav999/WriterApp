using System;

namespace WriterApp.Data.Documents
{
    public sealed class SceneContentRecord
    {
        public Guid SceneNodeId { get; set; }

        public ProjectNodeRecord? SceneNode { get; set; }

        public string ContentJson { get; set; } = string.Empty;

        public string? LanguageCode { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
