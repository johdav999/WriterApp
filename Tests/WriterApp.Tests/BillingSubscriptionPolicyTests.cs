using WriterApp.Shared.Billing;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class BillingSubscriptionPolicyTests
    {
        [Theory]
        [InlineData("active", true, "paid_active")]
        [InlineData("trialing", true, "trial_active")]
        [InlineData("past_due", false, "payment_past_due")]
        [InlineData("unpaid", false, "payment_unpaid")]
        [InlineData("incomplete", false, "payment_incomplete")]
        [InlineData("incomplete_expired", false, "payment_incomplete_expired")]
        [InlineData("canceled", false, "subscription_canceled")]
        [InlineData("unexpected", false, "status_unknown")]
        public void Evaluate_ReturnsExplicitPolicyDecision(string status, bool keepsPaidAccess, string expectedPolicyCode)
        {
            BillingSubscriptionPolicyDecision decision = BillingSubscriptionPolicy.Evaluate(status);

            Assert.Equal(keepsPaidAccess, decision.KeepsPaidAccess);
            Assert.Equal(expectedPolicyCode, decision.PolicyCode);
        }

        [Fact]
        public void BuildLifecycleMessage_PastDue_IsExplicitAboutPausedAccess()
        {
            string message = BillingSubscriptionPolicy.BuildLifecycleMessage(
                "Professional",
                "Professional",
                "past_due",
                null,
                false);

            Assert.Contains("past due", message);
            Assert.Contains("paused", message);
        }
    }
}
