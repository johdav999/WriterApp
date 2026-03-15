using System;

namespace WriterApp.Data.Admin
{
    public sealed class AdminRoleAssignment
    {
        public string UserId { get; set; } = string.Empty;
        public string? AssignedByUserId { get; set; }
        public string? AssignedByEmail { get; set; }
        public DateTime AssignedUtc { get; set; }
    }
}
