using System;

namespace WriterApp.Application.Usage
{
    public sealed class AiUsageStatus
    {
        public AiUsageStatus(
            string planKey,
            string planName,
            bool aiEnabled,
            int quotaTotal,
            int quotaUsed,
            int quotaRemaining)
        {
            PlanKey = planKey;
            PlanName = planName;
            AiEnabled = aiEnabled;
            QuotaTotal = quotaTotal;
            QuotaUsed = quotaUsed;
            QuotaRemaining = quotaRemaining;
        }

        public string PlanKey { get; }
        public string PlanName { get; }
        public bool AiEnabled { get; }
        public int QuotaTotal { get; }
        public int QuotaUsed { get; }
        public int QuotaRemaining { get; }
    }
}
