using System;

namespace WriterApp.Shared
{
    public sealed record AdminSetPlanOverrideRequest(
        string? PlanKey,
        string? Reason);

    public sealed record AdminPlanOverrideResponse(
        string UserId,
        string? OverridePlanKey,
        DateTime? OverrideAssignedUtc,
        string? OverrideAssignedBy,
        string ResolvedPlanKey,
        string SubscriptionStatus,
        int AiMonthlyTokenBudget,
        DateTimeOffset PeriodStartUtc,
        bool IsManuallyOverridden);
}
