using System;

namespace WriterApp.Data.Usage
{
    public sealed class UserEvent
    {
        public long Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string EventName { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
    }
}
