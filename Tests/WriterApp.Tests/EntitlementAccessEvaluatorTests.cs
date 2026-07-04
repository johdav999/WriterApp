using System;
using WriterApp.Application.Subscriptions;
using WriterApp.Data.Subscriptions;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class EntitlementAccessEvaluatorTests
    {
        [Theory]
        [InlineData("active", true, "Professional", EntitlementAccessBlockReason.None)]
        [InlineData("trialing", true, "Professional", EntitlementAccessBlockReason.None)]
        [InlineData("past_due", false, "Free", EntitlementAccessBlockReason.PaymentPastDue)]
        [InlineData("unpaid", false, "Free", EntitlementAccessBlockReason.PaymentUnpaid)]
        [InlineData("incomplete", false, "Free", EntitlementAccessBlockReason.SubscriptionIncomplete)]
        [InlineData("incomplete_expired", false, "Free", EntitlementAccessBlockReason.SubscriptionIncompleteExpired)]
        [InlineData("canceled", false, "Free", EntitlementAccessBlockReason.SubscriptionCanceled)]
        public void Evaluate_UsesExplicitSubscriptionPolicy(string subscriptionStatus, bool paidAccessActive, string expectedEffectivePlan, EntitlementAccessBlockReason expectedBlockReason)
        {
            UserEntitlement entitlement = CreatePaidEntitlement(subscriptionStatus);

            EvaluatedEntitlementAccess result = EntitlementAccessEvaluator.Evaluate(entitlement);

            Assert.Equal(expectedEffectivePlan, result.EffectivePlanKey);
            Assert.Equal(paidAccessActive, result.IsPaidAccessActive);
            Assert.Equal(expectedBlockReason, result.BlockReason);
        }

        private static UserEntitlement CreatePaidEntitlement(string subscriptionStatus)
        {
            return new UserEntitlement
            {
                UserId = "user-1",
                PlanKey = UserEntitlementDefaults.ProfessionalPlanKey,
                SubscriptionStatus = subscriptionStatus,
                CreatedAt = DateTimeOffset.UtcNow,
                AiMonthlyTokenBudget = UserEntitlementDefaults.ProfessionalMonthlyTokenBudget,
                AiTokensUsedThisPeriod = 0,
                PeriodStartUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
        }
    }
}
