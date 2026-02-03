using System;

namespace WriterApp.Data.Documents
{
    public sealed class PageQualityIssueDismissalRecord
    {
        public string UserId { get; set; } = string.Empty;
        public Guid PageId { get; set; }
        public string IssueKey { get; set; } = string.Empty;
        public DateTimeOffset DismissedAt { get; set; }

        public PageRecord? Page { get; set; }
    }
}
