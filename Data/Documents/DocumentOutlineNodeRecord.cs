using System;
using System.Collections.Generic;

namespace WriterApp.Data.Documents
{
    public sealed class DocumentOutlineNodeRecord
    {
        public Guid Id { get; set; }

        public Guid DocumentId { get; set; }

        public DocumentRecord? Document { get; set; }

        public Guid? ParentId { get; set; }

        public DocumentOutlineNodeRecord? Parent { get; set; }

        public List<DocumentOutlineNodeRecord> Children { get; set; } = new();

        public int Order { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Notes { get; set; }

        public string? MetadataJson { get; set; }

        public Guid? LinkedSectionId { get; set; }

        public SectionRecord? LinkedSection { get; set; }
    }
}
