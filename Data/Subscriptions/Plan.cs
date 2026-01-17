using System;
using System.Collections.Generic;

namespace WriterApp.Data.Subscriptions
{
    public sealed class Plan
    {
        public Guid PlanId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }

        public List<PlanEntitlement> Entitlements { get; set; } = new();
    }
}
