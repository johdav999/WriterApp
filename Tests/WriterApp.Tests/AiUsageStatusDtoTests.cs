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
                AiEnabled = true,
                UiEnabled = true,
                QuotaRemaining = 0
            };

            Assert.True(status.ShouldShowAiLimitMessage);
            Assert.False(status.ShouldShowAiUpgradeHint);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void DisabledAi_DoesNotClaimPaidQuotaWasConsumed(bool enabled, bool uiEnabled)
        {
            var status = new AiUsageStatusDto
            {
                PlanKey = UserEntitlementDefaults.StandardPlanKey,
                AiEnabled = enabled,
                UiEnabled = uiEnabled,
                QuotaRemaining = 0
            };

            Assert.False(status.ShouldShowAiLimitMessage);
        }
    }
}
