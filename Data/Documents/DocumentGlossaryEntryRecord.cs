using System;

namespace WriterApp.Data.Documents
{
    public sealed class DocumentGlossaryEntryRecord
    {
        public Guid Id { get; set; }
        public Guid DocumentId { get; set; }
        public string Term { get; set; } = string.Empty;
        public string NormalizedTerm { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        public DocumentRecord? Document { get; set; }
    }
}
