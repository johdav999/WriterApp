using System;

namespace WriterApp.Data.Documents
{
    public sealed class ProjectGoalRecord
    {
        public Guid ProjectId { get; set; }

        public ProjectRecord? Project { get; set; }

        public int DailyTargetWords { get; set; }

        public int WeeklyTargetWords { get; set; }

        public string Timezone { get; set; } = "UTC";

        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
