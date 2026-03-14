using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Security;
using WriterApp.Application.Usage;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public sealed class UserEntitlementStore : IUserEntitlementStore
    {
        private readonly AppDbContext _dbContext;
        private readonly IClock _clock;
        private readonly IDeletedUserIdentityService _deletedUserIdentityService;
        private readonly ILogger<UserEntitlementStore>? _logger;

        public UserEntitlementStore(
            AppDbContext dbContext,
            IClock clock,
            IDeletedUserIdentityService deletedUserIdentityService,
            ILogger<UserEntitlementStore>? logger = null)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _deletedUserIdentityService = deletedUserIdentityService ?? throw new ArgumentNullException(nameof(deletedUserIdentityService));
            _logger = logger;
        }

        public async Task<UserEntitlement> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            await _deletedUserIdentityService.ThrowIfDeletedAsync(userId, cancellationToken);

            UserEntitlement? entitlement = await _dbContext.UserEntitlements
                .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);

            if (entitlement is null)
            {
                DateTimeOffset now = _clock.UtcNow;
                string? initialAssignedPlanKey = await ResolvePlanKeyFromAssignmentsAsync(userId, cancellationToken);
                // Precedence:
                // 1) Manual assignment override wins when present.
                // 2) Otherwise keep stored entitlement plan (Stripe source).
                // 3) For first-time rows with no source, fall back to Free.
                string planKey = string.IsNullOrWhiteSpace(initialAssignedPlanKey)
                    ? UserEntitlementDefaults.FreePlanKey
                    : initialAssignedPlanKey;
                entitlement = new UserEntitlement
                {
                    UserId = userId,
                    PlanKey = planKey,
                    SubscriptionStatus = UserEntitlementDefaults.ActiveSubscriptionStatus,
                    CreatedAt = now,
                    AiMonthlyTokenBudget = UserEntitlementDefaults.ResolveMonthlyTokenBudget(planKey),
                    AiTokensUsedThisPeriod = 0,
                    PeriodStartUtc = now,
                    UpdatedUtc = now
                };

                _dbContext.UserEntitlements.Add(entitlement);

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    return entitlement;
                }
                catch (DbUpdateException ex) when (IsUniqueViolation(ex))
                {
                    _dbContext.Entry(entitlement).State = EntityState.Detached;
                    entitlement = await _dbContext.UserEntitlements
                        .FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
                    if (entitlement is null)
                    {
                        throw;
                    }
                }
            }

            bool dirty = false;
            string entitlementPlanBefore = entitlement.PlanKey ?? string.Empty;
            string? assignedPlanKey = await ResolvePlanKeyFromAssignmentsAsync(userId, cancellationToken);
            bool didOverrideFromAssignment = false;
            // Only explicit assignment rows are treated as manual overrides.
            // No assignment means keep the existing entitlement plan value.
            if (!string.IsNullOrWhiteSpace(assignedPlanKey)
                && !string.Equals(entitlement.PlanKey, assignedPlanKey, StringComparison.Ordinal))
            {
                DateTimeOffset now = _clock.UtcNow;
                string previousPlanKey = entitlement.PlanKey ?? string.Empty;
                entitlement.PlanKey = assignedPlanKey;
                entitlement.AiMonthlyTokenBudget = UserEntitlementDefaults.ResolveMonthlyTokenBudget(assignedPlanKey);
                entitlement.AiTokensUsedThisPeriod = 0;
                entitlement.PeriodStartUtc = now;
                dirty = true;
                didOverrideFromAssignment = true;
                _logger?.LogInformation(
                    "Plan override applied from assignment. UserId={UserId} AssignedPlanKey={AssignedPlanKey} PreviousPlanKey={PreviousPlanKey}",
                    userId,
                    assignedPlanKey,
                    previousPlanKey);
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

            string normalizedStatus = UserEntitlementDefaults.NormalizeSubscriptionStatus(entitlement.SubscriptionStatus);
            if (!string.Equals(entitlement.SubscriptionStatus, normalizedStatus, StringComparison.Ordinal))
            {
                entitlement.SubscriptionStatus = normalizedStatus;
                dirty = true;
            }

            if (entitlement.CreatedAt == default)
            {
                entitlement.CreatedAt = entitlement.PeriodStartUtc == default
                    ? _clock.UtcNow
                    : entitlement.PeriodStartUtc;
                dirty = true;
            }

            string entitlementPlanAfter = entitlement.PlanKey ?? string.Empty;
            _logger?.LogInformation(
                "Entitlement plan resolution. UserId={UserId} EntitlementPlanBefore={EntitlementPlanBefore} AssignedPlanKey={AssignedPlanKey} EntitlementPlanAfter={EntitlementPlanAfter} DidOverrideFromAssignment={DidOverrideFromAssignment}",
                userId,
                entitlementPlanBefore,
                assignedPlanKey ?? string.Empty,
                entitlementPlanAfter,
                didOverrideFromAssignment);

            if (dirty)
            {
                entitlement.UpdatedUtc = _clock.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return entitlement;
        }

        private async Task<string?> ResolvePlanKeyFromAssignmentsAsync(string userId, CancellationToken cancellationToken)
        {
            string? assignedKey = await _dbContext.UserPlanAssignments
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Join(
                    _dbContext.Plans.AsNoTracking(),
                    assignment => assignment.PlanId,
                    plan => plan.PlanId,
                    (assignment, plan) => new
                    {
                        assignment.AssignedUtc,
                        assignment.PlanId,
                        PlanKey = plan.Key
                    })
                .OrderByDescending(item => item.AssignedUtc)
                .ThenByDescending(item => item.PlanId)
                .Select(item => item.PlanKey)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(assignedKey))
            {
                return null;
            }

            return UserEntitlementDefaults.NormalizePlanKey(assignedKey);
        }

        private static bool IsUniqueViolation(DbUpdateException ex)
        {
            if (ex.InnerException is SqliteException sqliteEx)
            {
                return sqliteEx.SqliteErrorCode == 19
                    || sqliteEx.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase);
            }

            return ex.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
