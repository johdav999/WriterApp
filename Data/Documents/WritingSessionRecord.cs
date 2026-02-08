using System;

namespace WriterApp.Data.Documents
{
    public sealed class WritingSessionRecord
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public ProjectRecord? Project { get; set; }

        public DateTime StartedUtc { get; set; }

        public DateTime? EndedUtc { get; set; }

        public int DurationSeconds { get; set; }

        public int WordsDelta { get; set; }

        public int StartWordCount { get; set; }

        public string? Notes { get; set; }
    }
}
