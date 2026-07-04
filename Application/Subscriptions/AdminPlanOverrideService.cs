using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Billing;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared;

namespace WriterApp.Application.Subscriptions
{
    public sealed class AdminPlanOverrideService
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly IEntitlementService _entitlementService;
        private readonly IStripePriceResolver _stripePriceResolver;
        private readonly ILogger<AdminPlanOverrideService> _logger;

        public AdminPlanOverrideService(
            AppDbContext dbContext,
            IUserEntitlementStore userEntitlementStore,
            IEntitlementService entitlementService,
            IStripePriceResolver stripePriceResolver,
            ILogger<AdminPlanOverrideService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _stripePriceResolver = stripePriceResolver ?? throw new ArgumentNullException(nameof(stripePriceResolver));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AdminPlanOverrideResponse> SetOverride(
            string userId,
            string? planKey,
            string adminCallerId,
            string? adminCallerEmail,
            string? reason,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            string trimmedUserId = userId.Trim();
            DateTime now = DateTime.UtcNow;

            UserPlanAssignment[] existingAssignments = await _dbContext.UserPlanAssignments
                .Where(item => item.UserId == trimmedUserId)
                .ToArrayAsync(ct);

            if (existingAssignments.Length > 0)
            {
                _dbContext.UserPlanAssignments.RemoveRange(existingAssignments);
            }

            string? normalizedPlanKey = null;
            if (!string.IsNullOrWhiteSpace(planKey))
            {
                normalizedPlanKey = NormalizeRequestedPlanKey(planKey!);
                string lookupKey = UserEntitlementDefaults.ToPlanLookupKey(normalizedPlanKey);
                Plan? plan = await _dbContext.Plans.FirstOrDefaultAsync(item => item.Key == lookupKey, ct);
                if (plan is null)
                {
                    throw new InvalidOperationException($"Plan '{lookupKey}' was not found.");
                }

                _dbContext.UserPlanAssignments.Add(new UserPlanAssignment
                {
                    UserId = trimmedUserId,
                    PlanId = plan.PlanId,
                    AssignedUtc = now,
                    AssignedBy = string.IsNullOrWhiteSpace(adminCallerId) ? "admin" : adminCallerId
                });
            }

            await _dbContext.SaveChangesAsync(ct);
            _entitlementService.InvalidateForUser(trimmedUserId);

            if (normalizedPlanKey is null)
            {
                await RevertToStripeOrDefaultPlanAsync(trimmedUserId, ct);
            }

            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(trimmedUserId, ct);
            _entitlementService.InvalidateForUser(trimmedUserId);

            UserPlanAssignment? currentOverride = await GetCurrentOverrideInternal(trimmedUserId, ct);

            _logger.LogInformation(
                "Admin plan override set. userId={UserId} newPlanKey={PlanKey} reason={Reason} adminCallerId={AdminCallerId} adminCallerEmail={AdminCallerEmail} atUtc={AtUtc}",
                trimmedUserId,
                normalizedPlanKey ?? "(cleared)",
                reason ?? string.Empty,
                adminCallerId ?? string.Empty,
                adminCallerEmail ?? string.Empty,
                now);

            return BuildResponse(trimmedUserId, currentOverride, entitlement);
        }

        public async Task<AdminPlanOverrideResponse> GetOverride(string userId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("userId is required.", nameof(userId));
            }

            string trimmedUserId = userId.Trim();
            UserPlanAssignment? currentOverride = await GetCurrentOverrideInternal(trimmedUserId, ct);
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(trimmedUserId, ct);
            return BuildResponse(trimmedUserId, currentOverride, entitlement);
        }

        private async Task<UserPlanAssignment?> GetCurrentOverrideInternal(string userId, CancellationToken ct)
        {
            return await _dbContext.UserPlanAssignments
                .AsNoTracking()
                .Include(item => item.Plan)
                .Where(item => item.UserId == userId)
                .OrderByDescending(item => item.AssignedUtc)
                .ThenByDescending(item => item.PlanId)
                .FirstOrDefaultAsync(ct);
        }

        private async Task RevertToStripeOrDefaultPlanAsync(string userId, CancellationToken ct)
        {
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);

            string fallbackPlan = UserEntitlementDefaults.FreePlanKey;
            if (string.Equals(entitlement.SubscriptionStatus, "canceled", StringComparison.OrdinalIgnoreCase))
            {
                fallbackPlan = UserEntitlementDefaults.FreePlanKey;
            }
            else if (!string.IsNullOrWhiteSpace(entitlement.StripePriceId))
            {
                string? resolved = _stripePriceResolver.ResolvePlanKey(entitlement.StripePriceId);
                if (!string.IsNullOrWhiteSpace(resolved))
                {
                    fallbackPlan = UserEntitlementDefaults.NormalizePlanKey(resolved);
                }
                else if (IsPaidPlan(entitlement.PlanKey))
                {
                    fallbackPlan = UserEntitlementDefaults.NormalizePlanKey(entitlement.PlanKey);
                    _logger.LogError(
                        "Admin plan override revert found unmapped Stripe price id and preserved current paid plan. UserId={UserId} PriceId={PriceId} PreservedPlanKey={PreservedPlanKey}",
                        userId,
                        entitlement.StripePriceId,
                        fallbackPlan);
                }
            }

            fallbackPlan = UserEntitlementDefaults.NormalizePlanKey(fallbackPlan);
            if (string.Equals(entitlement.PlanKey, fallbackPlan, StringComparison.Ordinal))
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.UtcNow;
            entitlement.PlanKey = fallbackPlan;
            entitlement.AiMonthlyTokenBudget = UserEntitlementDefaults.ResolveMonthlyTokenBudget(fallbackPlan);
            entitlement.AiTokensUsedThisPeriod = 0;
            entitlement.PeriodStartUtc = now;
            entitlement.UpdatedUtc = now;
            await _dbContext.SaveChangesAsync(ct);
            _entitlementService.InvalidateForUser(userId);
        }

        private static bool IsPaidPlan(string? planKey)
        {
            string normalizedPlan = UserEntitlementDefaults.NormalizePlanKey(planKey);
            return string.Equals(normalizedPlan, UserEntitlementDefaults.StandardPlanKey, StringComparison.Ordinal)
                || string.Equals(normalizedPlan, UserEntitlementDefaults.ProfessionalPlanKey, StringComparison.Ordinal);
        }

        private static string NormalizeRequestedPlanKey(string rawPlanKey)
        {
            string normalized = UserEntitlementDefaults.NormalizePlanKey(rawPlanKey);
            string trimmed = rawPlanKey.Trim();
            bool isKnown =
                trimmed.Equals("free", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("standard", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("pro", StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("professional", StringComparison.OrdinalIgnoreCase);
            if (!isKnown)
            {
                throw new ArgumentException("planKey must be one of: Free, Standard, Pro.", nameof(rawPlanKey));
            }

            return normalized;
        }

        private static AdminPlanOverrideResponse BuildResponse(
            string userId,
            UserPlanAssignment? currentOverride,
            UserEntitlement entitlement)
        {
            string? overridePlanKey = currentOverride?.Plan?.Key;
            if (!string.IsNullOrWhiteSpace(overridePlanKey))
            {
                overridePlanKey = UserEntitlementDefaults.NormalizePlanKey(overridePlanKey);
            }

            return new AdminPlanOverrideResponse(
                userId,
                overridePlanKey,
                currentOverride?.AssignedUtc,
                currentOverride?.AssignedBy,
                UserEntitlementDefaults.NormalizePlanKey(entitlement.PlanKey),
                UserEntitlementDefaults.NormalizeSubscriptionStatus(entitlement.SubscriptionStatus),
                entitlement.AiMonthlyTokenBudget,
                entitlement.PeriodStartUtc,
                currentOverride is not null);
        }
    }
}
