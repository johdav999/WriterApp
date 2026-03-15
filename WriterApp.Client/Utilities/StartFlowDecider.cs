using System;
using WriterApp.Client.Services;

namespace WriterApp.Client.Utilities
{
    public static class StartFlowDecider
    {
        public static StartFlowDecision Decide(
            bool isAuthenticated,
            bool isDeletedAccount,
            StartPlanTier requestedPlan,
            StartPlanTier currentPlan,
            OnboardingState onboardingState,
            bool hasProjects)
        {
            if (isDeletedAccount)
            {
                return new StartFlowDecision(StartFlowTarget.DeletedAccount);
            }

            if (!isAuthenticated)
            {
                return new StartFlowDecision(StartFlowTarget.Anonymous);
            }

            if (requestedPlan == StartPlanTier.Free)
            {
                bool shouldOnboard =
                    !onboardingState.HasCompletedOnboarding
                    && !hasProjects;

                return shouldOnboard
                    ? new StartFlowDecision(StartFlowTarget.Onboarding)
                    : new StartFlowDecision(StartFlowTarget.ReturnUrl);
            }

            return currentPlan >= requestedPlan
                ? new StartFlowDecision(StartFlowTarget.ReturnUrl)
                : new StartFlowDecision(StartFlowTarget.Checkout);
        }
    }

    public sealed record StartFlowDecision(StartFlowTarget Target);

    public enum StartFlowTarget
    {
        Anonymous = 0,
        DeletedAccount = 1,
        Onboarding = 2,
        ReturnUrl = 3,
        Checkout = 4
    }

    public enum StartPlanTier
    {
        Free = 0,
        Standard = 1,
        Pro = 2
    }
}
