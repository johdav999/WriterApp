using System;

namespace WriterApp.Application.Subscriptions
{
    public sealed class EntitlementDeniedException : Exception
    {
        public EntitlementDeniedException(string featureKey, string? planKey, string message)
            : base(message)
        {
            FeatureKey = string.IsNullOrWhiteSpace(featureKey) ? "ai.feature" : featureKey.Trim();
            PlanKey = string.IsNullOrWhiteSpace(planKey) ? null : planKey.Trim();
        }

        public string FeatureKey { get; }

        public string? PlanKey { get; }
    }
}
