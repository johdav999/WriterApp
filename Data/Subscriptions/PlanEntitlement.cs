using System;

namespace WriterApp.Data.Subscriptions
{
    public sealed class PlanEntitlement
    {
        public Guid PlanId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;

        public Plan? Plan { get; set; }
    }
}
