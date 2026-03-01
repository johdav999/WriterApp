using System;

namespace WriterApp.Data.Exporting
{
    public sealed class ProjectExportSettings
    {
        public Guid DocumentId { get; set; }

        public string UserId { get; set; } = string.Empty;

        public Guid? DefaultPresetId { get; set; }

        public string? OverridesJson { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
