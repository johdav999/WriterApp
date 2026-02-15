using System;

namespace WriterApp.Data.Documents
{
    public sealed class SceneVersionRecord
    {
        public Guid Id { get; set; }

        public Guid SceneNodeId { get; set; }

        public ProjectNodeRecord? SceneNode { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string Reason { get; set; } = string.Empty;

        public byte[] ContentCompressed { get; set; } = Array.Empty<byte>();

        public string ContentTextHash { get; set; } = string.Empty;

        public int SizeBytes { get; set; }

        public int WordCount { get; set; }
    }
}
