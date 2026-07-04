using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class AiUsageStatusDtoTests
    {
        [Fact]
        public void ShouldShowAiLimitMessage_IsFalse_ForFreePlanAtLimit()
        {
            AiUsageStatusDto status = new()
            {
                PlanKey = UserEntitlementDefaults.FreePlanKey,
                QuotaRemaining = 0
            };

            Assert.False(status.ShouldShowAiLimitMessage);
            Assert.True(status.ShouldShowAiUpgradeHint);
        }

        [Fact]
        public void ShouldShowAiLimitMessage_IsTrue_ForPaidPlanAtLimit()
        {
            AiUsageStatusDto status = new()
            {
                PlanKey = UserEntitlementDefaults.StandardPlanKey,
                QuotaRemaining = 0
            };

            Assert.True(status.ShouldShowAiLimitMessage);
            Assert.False(status.ShouldShowAiUpgradeHint);
        }
    }
}
