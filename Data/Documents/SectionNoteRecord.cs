using System;

namespace WriterApp.Data.Documents
{
    public sealed class SectionNoteRecord
    {
        public Guid SectionId { get; set; }

        public SectionRecord? Section { get; set; }

        public string NotesText { get; set; } = string.Empty;

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
