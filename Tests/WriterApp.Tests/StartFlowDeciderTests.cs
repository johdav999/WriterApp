using WriterApp.Client.Services;
using WriterApp.Client.Utilities;
using Xunit;

namespace WriterApp.Tests
{
    public sealed class StartFlowDeciderTests
    {
        [Fact]
        public void NewFreeCustomer_RoutesToOnboarding()
        {
            StartFlowDecision decision = StartFlowDecider.Decide(
                isAuthenticated: true,
                isDeletedAccount: false,
                requestedPlan: StartPlanTier.Free,
                currentPlan: StartPlanTier.Free,
                onboardingState: OnboardingState.Default,
                hasProjects: false);

            Assert.Equal(StartFlowTarget.Onboarding, decision.Target);
        }

        [Fact]
        public void ReturningUser_RoutesToRequestedReturnUrl()
        {
            StartFlowDecision decision = StartFlowDecider.Decide(
                isAuthenticated: true,
                isDeletedAccount: false,
                requestedPlan: StartPlanTier.Free,
                currentPlan: StartPlanTier.Free,
                onboardingState: new OnboardingState(true, 10, "Novel", null, null),
                hasProjects: true);

            Assert.Equal(StartFlowTarget.ReturnUrl, decision.Target);
        }

        [Fact]
        public void DeletedUser_DoesNotEnterOnboarding()
        {
            StartFlowDecision decision = StartFlowDecider.Decide(
                isAuthenticated: true,
                isDeletedAccount: true,
                requestedPlan: StartPlanTier.Free,
                currentPlan: StartPlanTier.Free,
                onboardingState: OnboardingState.Default,
                hasProjects: false);

            Assert.Equal(StartFlowTarget.DeletedAccount, decision.Target);
        }

        [Fact]
        public void FreeUser_RequestingPaidPlan_RoutesToCheckout()
        {
            StartFlowDecision decision = StartFlowDecider.Decide(
                isAuthenticated: true,
                isDeletedAccount: false,
                requestedPlan: StartPlanTier.Pro,
                currentPlan: StartPlanTier.Free,
                onboardingState: new OnboardingState(true, 10, "Novel", null, null),
                hasProjects: true);

            Assert.Equal(StartFlowTarget.Checkout, decision.Target);
        }
    }
}
