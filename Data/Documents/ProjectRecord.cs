using System;
using System.Collections.Generic;

namespace WriterApp.Data.Documents
{
    public sealed class ProjectRecord
    {
        public Guid Id { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;

        public string Title { get; set; } = string.Empty;

        public string? Subtitle { get; set; }

        public string? AuthorName { get; set; }

        public string? Language { get; set; }

        public string? Genre { get; set; }

        public string? DefaultExportSettingsJson { get; set; }

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }

        public List<ProjectNodeRecord> Nodes { get; set; } = new();

        public ProjectGoalRecord? Goal { get; set; }

        public List<ProjectProgressDailyRecord> ProgressDays { get; set; } = new();

        public List<ProjectMilestoneRecord> Milestones { get; set; } = new();

        public List<WritingSessionRecord> Sessions { get; set; } = new();
    }
}
