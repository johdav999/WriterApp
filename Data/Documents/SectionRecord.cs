using System;
using System.Collections.Generic;

namespace WriterApp.Data.Documents
{
    public sealed class SectionRecord
    {
        public Guid Id { get; set; }

        public Guid DocumentId { get; set; }

        public DocumentRecord? Document { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? NarrativePurpose { get; set; }

        public int OrderIndex { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public List<PageRecord> Pages { get; set; } = new();
    }
}
