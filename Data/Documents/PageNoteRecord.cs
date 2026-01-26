using System;

namespace WriterApp.Data.Documents
{
    public sealed class PageNoteRecord
    {
        public Guid PageId { get; set; }

        public PageRecord? Page { get; set; }

        public string Notes { get; set; } = string.Empty;

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
