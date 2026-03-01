using System;

namespace WriterApp.Data.Documents
{
    public sealed class SceneCardRecord
    {
        public Guid SceneNodeId { get; set; }

        public ProjectNodeRecord? SceneNode { get; set; }

        public string? NarrativePurpose { get; set; }

        public string? EmotionalBeat { get; set; }

        public string? KeyEvents { get; set; }

        public string? OpenQuestions { get; set; }

        public string? PovCharacterId { get; set; }

        public string? PlaceId { get; set; }

        public string? TimelineEventId { get; set; }

        public string? TimeRef { get; set; }

        public string? TagsJson { get; set; }

        public string? ReferencesJson { get; set; }

        public DateTimeOffset UpdatedAtUtc { get; set; }
    }
}
