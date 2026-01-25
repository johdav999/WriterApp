using System;
using System.Collections.Generic;

namespace WriterApp.Data.Documents
{
    public sealed class DocumentRecord
    {
        public Guid Id { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public List<SectionRecord> Sections { get; set; } = new();
    }
}
