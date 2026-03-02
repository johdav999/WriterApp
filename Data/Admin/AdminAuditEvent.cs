using System;

namespace WriterApp.Data.Admin
{
    public sealed class AdminAuditEvent
    {
        public long Id { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string AdminUserId { get; set; } = string.Empty;
        public string? AdminEmail { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? TargetUserId { get; set; }
        public string? TargetEmail { get; set; }
        public string? DetailsJson { get; set; }
    }
}
