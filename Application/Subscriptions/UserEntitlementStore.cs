using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public sealed class UserEntitlementStore : IUserEntitlementStore
    {
        private readonly AppDbContext _dbContext;
        private readonly IClock _clock;

        public UserEntitlementStore(AppDbContext dbContext, IClock clock)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<UserEntitlement> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            UserEntitlement? entitlement = await _dbContext.UserEntitlements
                .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

            if (entitlement is null)
            {
                DateTimeOffset now = _clock.UtcNow;
                string planKey = await ResolvePlanKeyFromAssignmentsAsync(userId, cancellationToken);
                entitlement = new UserEntitlement
                {
                    UserId = userId,
                    PlanKey = planKey,
                    AiMonthlyTokenBudget = UserEntitlementDefaults.ResolveMonthlyTokenBudget(planKey),
                    AiTokensUsedThisPeriod = 0,
                    PeriodStartUtc = now,
                    UpdatedUtc = now
                };

                _dbContext.UserEntitlements.Add(entitlement);
                await _dbContext.SaveChangesAsync(cancellationToken);
                return entitlement;
            }

            bool dirty = false;
            string assignedPlanKey = await ResolvePlanKeyFromAssignmentsAsync(userId, cancellationToken);
            if (!string.Equals(entitlement.PlanKey, assignedPlanKey, StringComparison.Ordinal))
            {
                entitlement.PlanKey = assignedPlanKey;
                entitlement.AiMonthlyTokenBudget = UserEntitlementDefaults.ResolveMonthlyTokenBudget(assignedPlanKey);
                dirty = true;
            }

            string normalizedPlan = UserEntitlementDefaults.NormalizePlanKey(entitlement.PlanKey);
            if (!string.Equals(entitlement.PlanKey, normalizedPlan, StringComparison.Ordinal))
            {
                entitlement.PlanKey = normalizedPlan;
                dirty = true;
            }

            int expectedBudget = UserEntitlementDefaults.ResolveMonthlyTokenBudget(entitlement.PlanKey);
            if (entitlement.AiMonthlyTokenBudget < 0)
            {
                entitlement.AiMonthlyTokenBudget = expectedBudget;
                dirty = true;
            }

            if (dirty)
            {
                entitlement.UpdatedUtc = _clock.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return entitlement;
        }

        private async Task<string> ResolvePlanKeyFromAssignmentsAsync(string userId, CancellationToken cancellationToken)
        {
            string? assignedKey = await _dbContext.UserPlanAssignments
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Join(
                    _dbContext.Plans.AsNoTracking(),
                    assignment => assignment.PlanId,
                    plan => plan.PlanId,
                    (_, plan) => plan.Key)
                .FirstOrDefaultAsync(cancellationToken);

            return UserEntitlementDefaults.NormalizePlanKey(assignedKey);
        }
    }
}
