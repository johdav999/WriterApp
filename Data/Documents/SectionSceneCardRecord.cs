using System;

namespace WriterApp.Data.Documents
{
    public sealed class SectionSceneCardRecord
    {
        public Guid SectionId { get; set; }

        public SectionRecord? Section { get; set; }

        public string? NarrativePurpose { get; set; }

        public string? EmotionalBeat { get; set; }

        public string? KeyEvents { get; set; }

        public string? OpenQuestions { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }
    }
}
