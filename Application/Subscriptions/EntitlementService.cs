using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Subscriptions
{
    public sealed class EntitlementService : IEntitlementService
    {
        private readonly IPlanRepository _planRepository;
        private readonly IMemoryCache _cache;

        public EntitlementService(IPlanRepository planRepository, IMemoryCache cache)
        {
            _planRepository = planRepository ?? throw new ArgumentNullException(nameof(planRepository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public async Task<UserEntitlements> GetEntitlementsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new UserEntitlements(string.Empty, "free", "Free", new Dictionary<string, string>());
            }

            string cacheKey = $"entitlements:{userId}";
            if (_cache.TryGetValue(cacheKey, out UserEntitlements? cached) && cached is not null)
            {
                return cached;
            }

            Plan? plan = await _planRepository.GetPlanForUserAsync(userId)
                ?? await _planRepository.GetPlanByKeyAsync("free");

            string planKey = plan?.Key ?? "free";
            string planName = plan?.Name ?? "Free";
            Dictionary<string, string> entitlements = new(StringComparer.OrdinalIgnoreCase);

            if (plan?.Entitlements is not null)
            {
                foreach (PlanEntitlement entitlement in plan.Entitlements)
                {
                    entitlements[entitlement.Key] = entitlement.Value;
                }
            }

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
