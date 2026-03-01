using System;

namespace WriterApp.Data.Documents
{
    public sealed class ProjectProgressDailyRecord
    {
        public Guid ProjectId { get; set; }

        public ProjectRecord? Project { get; set; }

        public string Date { get; set; } = string.Empty;

        public int WordsDelta { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
