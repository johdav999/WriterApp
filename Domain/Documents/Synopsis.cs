using System;

namespace WriterApp.Domain.Documents
{
    public sealed class Synopsis
    {
        public string Logline { get; set; } = string.Empty;
        public string Premise { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
        public string ProtagonistArc { get; set; } = string.Empty;
        public string CentralConflict { get; set; } = string.Empty;
        public string Stakes { get; set; } = string.Empty;
        public string Setting { get; set; } = string.Empty;
        public string EndingIntent { get; set; } = string.Empty;
        public string OpenQuestions { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string OutlineDraft { get; set; } = string.Empty;
        public DateTime ModifiedUtc { get; set; }
    }
}
