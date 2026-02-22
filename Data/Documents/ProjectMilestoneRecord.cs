using System;

namespace WriterApp.Data.Documents
{
    public sealed class ProjectMilestoneRecord
    {
        public Guid Id { get; set; }

        public Guid ProjectId { get; set; }

        public ProjectRecord? Project { get; set; }

        public string Title { get; set; } = string.Empty;

        public int? TargetWords { get; set; }

        public Guid? TargetNodeId { get; set; }

        public ProjectMilestoneStatus Status { get; set; } = ProjectMilestoneStatus.Pending;

        public DateTimeOffset? CompletedUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
