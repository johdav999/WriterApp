using System;

namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// Serialized snapshot of a document for future undo and version history.
    /// </summary>
    public record DocumentVersion
    {
        public Guid VersionId { get; init; } = Guid.NewGuid();

        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;

        public string Comment { get; init; } = string.Empty;

        /// <summary>
        /// Serialized JSON snapshot for later restoration.
        /// </summary>
        public string SnapshotJson { get; init; } = string.Empty;

        public DocumentVersion()
        {
        }
    }
}
