using System;

namespace WriterApp.Data.Continuity
{
    public sealed class BibleSnapshotRecord
    {
        public Guid Id { get; set; }

        public Guid DocumentId { get; set; }

        public string BibleType { get; set; } = string.Empty;

        public int SchemaVersion { get; set; }

        public string ContentJson { get; set; } = string.Empty;

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }

        public DateTimeOffset? LastRefreshUtc { get; set; }

        public string LastRefreshSourceHash { get; set; } = string.Empty;

        public string LastRefreshStatsJson { get; set; } = string.Empty;

        public string LastRefreshCursorJson { get; set; } = string.Empty;
    }
}
