using System;
using System.Collections.Generic;

namespace WriterApp.Data.Documents
{
    public sealed class DocumentRecord
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public ProjectRecord? Project { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public DocumentKind DocumentKind { get; set; } = DocumentKind.Manuscript;

        public string? LanguageCode { get; set; }

        public Guid? TranslationGroupId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public long CreatedAtUnixSeconds { get; set; }

        public long UpdatedAtUnixSeconds { get; set; }

        public bool IsArchived { get; set; }

        public DateTimeOffset? ArchivedAt { get; set; }

        public DateTimeOffset? DeletedAt { get; set; }

        public List<SectionRecord> Sections { get; set; } = new();

        public List<DocumentOutlineNodeRecord> OutlineNodes { get; set; } = new();
    }
}
