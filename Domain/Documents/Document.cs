using System;
using System.Collections.Generic;

namespace WriterApp.Domain.Documents
{
    /// <summary>
    /// Aggregate root for a writer document, suitable for JSON serialization and versioning.
    /// </summary>
    public record Document
    {
        public string SchemaVersion { get; init; } = "1.0";

        public Guid DocumentId { get; init; } = Guid.NewGuid();

        public DocumentMetadata Metadata { get; init; } = new();

        public DocumentSettings Settings { get; init; } = new();

        public List<Chapter> Chapters { get; init; } = new();

        public Guid? CoverImageId { get; set; }

        public List<DocumentArtifact> Artifacts { get; init; } = new();

        /// <summary>
        /// Historical snapshots for future undo and audit features.
        /// </summary>
        public List<DocumentVersion> Versions { get; init; } = new();

        public Document()
        {
        }
    }
}
