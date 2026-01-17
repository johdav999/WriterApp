using System.Collections.Generic;

namespace WriterApp.Application.Subscriptions
{
    public sealed class UserEntitlements
    {
        public UserEntitlements(string userId, string planKey, string planName, IReadOnlyDictionary<string, string> entitlements)
        {
            UserId = userId;
            PlanKey = planKey;
            PlanName = planName;
            Entitlements = entitlements;
        }

        public string UserId { get; }
        public string PlanKey { get; }
        public string PlanName { get; }
        public IReadOnlyDictionary<string, string> Entitlements { get; }
    }
}
