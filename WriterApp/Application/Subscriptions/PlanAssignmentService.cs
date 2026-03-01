using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public sealed class PlanAssignmentService : IPlanAssignmentService
    {
        private readonly AppDbContext _dbContext;
        private readonly IEntitlementService _entitlementService;
        private readonly ILogger<PlanAssignmentService> _logger;

        public PlanAssignmentService(
            AppDbContext dbContext,
            IEntitlementService entitlementService,
            ILogger<PlanAssignmentService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<Plan>> GetActivePlansAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Plans
                .Where(plan => plan.IsActive)
                .OrderBy(plan => plan.Name)
                .ToListAsync(cancellationToken);
        }

        public Task<UserPlanAssignment?> GetLatestAssignmentAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Task.FromResult<UserPlanAssignment?>(null);
            }

            return _dbContext.UserPlanAssignments
                .Include(assignment => assignment.Plan)
                .Where(assignment => assignment.UserId == userId)
                .OrderByDescending(assignment => assignment.AssignedUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<PlanAssignmentResult> AssignPlanAsync(
            string userId,
            string planKey,
            string assignedBy,
            string? callerName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new PlanAssignmentException(PlanAssignmentErrorCode.InvalidUserId, "userId is required.");
            }

            if (string.IsNullOrWhiteSpace(planKey))
            {
                throw new PlanAssignmentException(PlanAssignmentErrorCode.InvalidPlanKey, "planKey is required.");
            }

            Plan? plan = await _dbContext.Plans
                .FirstOrDefaultAsync(entry => entry.Key == planKey, cancellationToken);
            if (plan is null)
            {
                throw new PlanAssignmentException(PlanAssignmentErrorCode.PlanNotFound, $"Plan '{planKey}' was not found.");
            }

            if (!plan.IsActive)
            {
                throw new PlanAssignmentException(PlanAssignmentErrorCode.PlanInactive, $"Plan '{planKey}' is not active.");
            }

            bool exists = await _dbContext.UserPlanAssignments
                .AnyAsync(
                    assignment => assignment.UserId == userId && assignment.PlanId == plan.PlanId,
                    cancellationToken);
            if (exists)
            {
                throw new PlanAssignmentException(
                    PlanAssignmentErrorCode.AssignmentExists,
                    $"User '{userId}' already has plan '{planKey}'.");
            }

            UserPlanAssignment assignment = new()
            {
                UserId = userId,
                PlanId = plan.PlanId,
                AssignedUtc = DateTime.UtcNow,
                AssignedBy = assignedBy
            };

            _dbContext.UserPlanAssignments.Add(assignment);
            await _dbContext.SaveChangesAsync(cancellationToken);
            _entitlementService.InvalidateForUser(userId);

            _logger.LogInformation(
                "Plan assignment created: userId={UserId} planKey={PlanKey} assignedUtc={AssignedUtc} assignedBy={AssignedBy} callerName={CallerName}",
                userId,
                plan.Key,
                assignment.AssignedUtc,
                assignedBy,
                callerName ?? string.Empty);

            return new PlanAssignmentResult(userId, plan.Key, plan.Name, assignment.AssignedUtc);
        }
    }
}
