using System;

namespace WriterApp.Data.Documents
{
    public sealed class OutlineTemplateRecord
    {
        public Guid Id { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string TemplateJson { get; set; } = string.Empty;

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
