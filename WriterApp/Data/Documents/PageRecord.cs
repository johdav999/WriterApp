using System;

namespace WriterApp.Data.Documents
{
    public sealed class PageRecord
    {
        public Guid Id { get; set; }

        public Guid DocumentId { get; set; }

        public DocumentRecord? Document { get; set; }

        public Guid SectionId { get; set; }

        public SectionRecord? Section { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public int OrderIndex { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
