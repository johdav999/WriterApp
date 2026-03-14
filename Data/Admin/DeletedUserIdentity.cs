using System;

namespace WriterApp.Data.Admin
{
    public sealed class DeletedUserIdentity
    {
        public string UserId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public string? DeletedByAdminUserId { get; set; }
        public string? DeletedByAdminEmail { get; set; }
        public string? Reason { get; set; }
        public DateTime DeletedAtUtc { get; set; }
    }
}
