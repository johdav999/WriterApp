using System;

namespace WriterApp.Data.Subscriptions
{
    public sealed class UserPlanAssignment
    {
        public string UserId { get; set; } = string.Empty;
        public Guid PlanId { get; set; }
        public DateTime AssignedUtc { get; set; }
        public string AssignedBy { get; set; } = string.Empty;

        public Plan? Plan { get; set; }
    }
}
