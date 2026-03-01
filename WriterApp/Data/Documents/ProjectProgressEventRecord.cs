using System;

namespace WriterApp.Data.Documents
{
    public sealed class ProjectProgressEventRecord
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public ProjectRecord? Project { get; set; }

        public string EventKey { get; set; } = string.Empty;

        public string Date { get; set; } = string.Empty;

        public int WordsDelta { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }
    }
}
