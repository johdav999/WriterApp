using System;

namespace WriterApp.Data.Exporting
{
    public sealed class ExportPreset
    {
        public Guid Id { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool IsGlobalDefault { get; set; }

        public string SettingsJson { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
