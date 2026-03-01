using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public interface IPlanAssignmentService
    {
        Task<IReadOnlyList<Plan>> GetActivePlansAsync(CancellationToken cancellationToken = default);
        Task<UserPlanAssignment?> GetLatestAssignmentAsync(string userId, CancellationToken cancellationToken = default);
        Task<PlanAssignmentResult> AssignPlanAsync(
            string userId,
            string planKey,
            string assignedBy,
            string? callerName,
            CancellationToken cancellationToken = default);
    }

    public sealed record PlanAssignmentResult(
        string UserId,
        string PlanKey,
        string PlanName,
        DateTime AssignedUtc);
}
