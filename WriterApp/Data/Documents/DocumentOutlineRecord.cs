using System;

namespace WriterApp.Data.Documents
{
    public sealed class DocumentOutlineRecord
    {
        public Guid DocumentId { get; set; }

        public DocumentRecord? Document { get; set; }

        public string Outline { get; set; } = string.Empty;

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
