using System;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.Data.Documents;

namespace WriterApp.Application.Documents
{
    public interface IProjectGoalsService
    {
        Task<ProjectGoalDto?> UpsertGoalAsync(
            string ownerUserId,
            Guid projectId,
            ProjectGoalUpdateRequest request,
            CancellationToken ct);

        Task<ProjectProgressDashboardDto?> GetDashboardAsync(
            string ownerUserId,
            Guid projectId,
            CancellationToken ct);

        Task<ProjectMilestoneDto?> CreateMilestoneAsync(
            string ownerUserId,
            Guid projectId,
            ProjectMilestoneCreateRequest request,
            CancellationToken ct);

        Task<ProjectMilestoneDto?> UpdateMilestoneAsync(
            string ownerUserId,
            Guid projectId,
            Guid milestoneId,
            ProjectMilestoneUpdateRequest request,
            CancellationToken ct);

        Task<bool> DeleteMilestoneAsync(
            string ownerUserId,
            Guid projectId,
            Guid milestoneId,
            CancellationToken ct);

        Task<WritingSessionDto?> StartSessionAsync(
            string ownerUserId,
            Guid projectId,
            CancellationToken ct);

        Task<WritingSessionDto?> StopSessionAsync(
            string ownerUserId,
            Guid projectId,
            Guid sessionId,
            string? notes,
            CancellationToken ct);

        Task TrackPageDeltaAsync(
            PageRecord? beforePage,
            PageRecord? afterPage,
            string eventKey,
            CancellationToken ct);
    }
}
