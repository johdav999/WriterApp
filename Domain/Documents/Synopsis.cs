using System;

namespace WriterApp.Domain.Documents
{
    public sealed class Synopsis
    {
        public string Premise { get; set; } = string.Empty;
        public string Protagonist { get; set; } = string.Empty;
        public string Antagonist { get; set; } = string.Empty;
        public string CentralConflict { get; set; } = string.Empty;
        public string Stakes { get; set; } = string.Empty;
        public string Arc { get; set; } = string.Empty;
        public string Setting { get; set; } = string.Empty;
        public string Ending { get; set; } = string.Empty;
        public DateTime ModifiedUtc { get; set; }
    }
}
