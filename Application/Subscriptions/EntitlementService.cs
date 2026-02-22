using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public sealed class EntitlementService : IEntitlementService
    {
        private sealed class PlanRepositoryBackedUserEntitlementStore : IUserEntitlementStore
        {
            private readonly IPlanRepository _planRepository;

            public PlanRepositoryBackedUserEntitlementStore(IPlanRepository planRepository)
            {
                _planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
            }

            public async Task<UserEntitlement> GetOrCreateAsync(string userId, CancellationToken cancellationToken = default)
            {
                Plan? plan = await _planRepository.GetPlanForUserAsync(userId)
                    ?? await _planRepository.GetPlanByKeyAsync("free");
                string normalizedPlan = UserEntitlementDefaults.NormalizePlanKey(plan?.Key);
                return new UserEntitlement
                {
                    UserId = userId,
                    PlanKey = normalizedPlan,
                    AiMonthlyTokenBudget = UserEntitlementDefaults.ResolveMonthlyTokenBudget(normalizedPlan),
                    AiTokensUsedThisPeriod = 0,
                    PeriodStartUtc = DateTimeOffset.UtcNow,
                    UpdatedUtc = DateTimeOffset.UtcNow
                };
            }
        }

        private const string DefaultPlanName = "Free";
        private readonly IPlanRepository _planRepository;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly IMemoryCache _cache;

        public EntitlementService(
            IPlanRepository planRepository,
            IUserEntitlementStore userEntitlementStore,
            IMemoryCache cache)
        {
            _planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public EntitlementService(
            IPlanRepository planRepository,
            IMemoryCache cache)
            : this(
                planRepository,
                new PlanRepositoryBackedUserEntitlementStore(planRepository),
                cache)
        {
        }

        public async Task<UserEntitlements> GetEntitlementsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new UserEntitlements(string.Empty, "free", DefaultPlanName, new Dictionary<string, string>());
            }

            string cacheKey = $"entitlements:{userId}";
            if (_cache.TryGetValue(cacheKey, out UserEntitlements? cached) && cached is not null)
            {
                return cached;
            }

            UserEntitlement userEntitlement = await _userEntitlementStore.GetOrCreateAsync(userId);
            string planLookupKey = UserEntitlementDefaults.ToPlanLookupKey(userEntitlement.PlanKey);
            Plan? plan = await _planRepository.GetPlanByKeyAsync(planLookupKey);

            string planKey = plan?.Key ?? planLookupKey;
            string planName = plan?.Name ?? UserEntitlementDefaults.NormalizePlanKey(userEntitlement.PlanKey);
            Dictionary<string, string> entitlements = new(StringComparer.OrdinalIgnoreCase);

            if (plan?.Entitlements is not null)
            {
                foreach (PlanEntitlement entitlement in plan.Entitlements)
                {
                    entitlements[entitlement.Key] = entitlement.Value;
                }
            }

            entitlements["ai.monthly_tokens"] = userEntitlement.AiMonthlyTokenBudget.ToString(CultureInfo.InvariantCulture);
            entitlements["ai.enabled"] = userEntitlement.AiMonthlyTokenBudget > 0 ? "true" : "false";
            entitlements["ai.tokens_used_this_period"] = userEntitlement.AiTokensUsedThisPeriod.ToString(CultureInfo.InvariantCulture);

            UserEntitlements result = new(userId, planKey, planName, entitlements);
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(1));
            return result;
        }

        public async Task<bool> HasAsync(string userId, string entitlementKey)
        {
            if (string.IsNullOrWhiteSpace(entitlementKey))
            {
                return false;
            }

            UserEntitlements entitlements = await GetEntitlementsAsync(userId);
            if (!entitlements.Entitlements.TryGetValue(entitlementKey, out string? value))
            {
                return false;
            }

            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<int?> GetIntAsync(string userId, string entitlementKey)
        {
            if (string.IsNullOrWhiteSpace(entitlementKey))
            {
                return null;
            }

            UserEntitlements entitlements = await GetEntitlementsAsync(userId);
            if (!entitlements.Entitlements.TryGetValue(entitlementKey, out string? value))
            {
                return null;
            }

            return int.TryParse(value, out int parsed) ? parsed : null;
        }

        public void InvalidateForUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            string cacheKey = $"entitlements:{userId}";
            _cache.Remove(cacheKey);
        }
    }
}
