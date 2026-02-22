using System;

namespace WriterApp.Data.Documents
{
    public sealed class PageAnnotationRecord
    {
        public Guid Id { get; set; }

        public Guid DocumentId { get; set; }

        public Guid PageId { get; set; }

        public string Kind { get; set; } = "comment";

        public string Status { get; set; } = "open";

        public int AnchorFrom { get; set; }

        public int AnchorTo { get; set; }

        public string AnchorText { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public string AuthorUserId { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset? ResolvedAt { get; set; }

        public DocumentRecord? Document { get; set; }

        public PageRecord? Page { get; set; }
    }
}
