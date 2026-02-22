using System;
using System.Collections.Generic;

namespace WriterApp.Data.Documents
{
    public sealed class ProjectNodeRecord
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public ProjectRecord? Project { get; set; }

        public Guid? ParentId { get; set; }

        public ProjectNodeRecord? Parent { get; set; }

        public List<ProjectNodeRecord> Children { get; set; } = new();

        public ProjectNodeType NodeType { get; set; } = ProjectNodeType.Scene;

        public string Title { get; set; } = string.Empty;

        public int OrderIndex { get; set; }

        public Guid? LinkedSectionId { get; set; }

        public SectionRecord? LinkedSection { get; set; }

        public string? MetadataJson { get; set; }

        public int WordCountCache { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
