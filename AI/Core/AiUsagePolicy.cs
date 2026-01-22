using System;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.AI.Actions;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;

namespace WriterApp.AI.Core
{
    public sealed class AiUsagePolicy : IAiUsagePolicy
    {
        private const string TotalKind = "ai.total";
        private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IEntitlementService _entitlementService;
        private readonly IUsageMeter _usageMeter;
        private readonly IMemoryCache _cache;
        private readonly IClock _clock;
        private readonly WriterAiOptions _options;

        public AiUsagePolicy(
            IHttpContextAccessor httpContextAccessor,
            IUserIdResolver userIdResolver,
            IEntitlementService entitlementService,
            IUsageMeter usageMeter,
            IMemoryCache cache,
            IClock clock,
            IOptions<WriterAiOptions> options)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _usageMeter = usageMeter ?? throw new ArgumentNullException(nameof(usageMeter));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<AiUsageDecision> EvaluateAsync(IAiProvider provider, string actionId)
        {
            if (provider is null)
            {
                return new AiUsageDecision(false, string.Empty, "ai.provider_missing", "AI provider is unavailable.");
            }

            bool requiresEntitlement = provider is IAiBillingProvider billingProvider && billingProvider.RequiresEntitlement;
            ClaimsPrincipal? user = _httpContextAccessor.HttpContext?.User;
            bool isAuthenticated = user?.Identity?.IsAuthenticated == true;
            string userId = string.Empty;
            if (isAuthenticated && user is not null)
            {
                userId = _userIdResolver.ResolveUserId(user);
            }

            if (!requiresEntitlement)
            {
                return new AiUsageDecision(true, userId, null, null);
            }

            if (!_options.Enabled)
            {
                return new AiUsageDecision(false, userId, "ai.disabled", "AI is disabled by configuration.");
            }

            if (!isAuthenticated)
            {
                return new AiUsageDecision(false, string.Empty, "auth.required", "Sign in to use AI.");
            }

            bool aiEnabled = await _entitlementService.HasAsync(userId, "ai.enabled");
            if (!aiEnabled)
            {
                return new AiUsageDecision(false, userId, "ai.disabled", "AI is not enabled for your plan.");
            }

            if (string.Equals(actionId, GenerateCoverImageAction.ActionIdValue, StringComparison.Ordinal))
            {
                bool imagesEnabled = await _entitlementService.HasAsync(userId, "ai.images.cover");
                if (!imagesEnabled)
                {
                    return new AiUsageDecision(false, userId, "ai.images.cover_disabled", "Cover image generation is not enabled for your plan.");
                }
            }

            int requestsPerMinute = Math.Max(1, _options.RateLimiting.RequestsPerMinute);
            if (IsRateLimited(userId, requestsPerMinute))
            {
                return new AiUsageDecision(false, userId, "ai.rate_limited", "Too many AI requests. Try again in a minute.");
            }

            int? monthlyTokens = await _entitlementService.GetIntAsync(userId, "ai.monthly_tokens");
            int limit = monthlyTokens ?? 0;
            if (limit <= 0)
            {
                return new AiUsageDecision(false, userId, "ai.quota_exceeded", "AI usage quota is exhausted.");
            }

            UsageSnapshot snapshot = await _usageMeter.GetCurrentPeriodAsync(userId, TotalKind);
            int used = snapshot.TotalInputTokens + snapshot.TotalOutputTokens;
            if (used >= limit)
            {
                return new AiUsageDecision(false, userId, "ai.quota_exceeded", "AI usage quota is exhausted.");
            }

            int? dailyCap = await _entitlementService.GetIntAsync(userId, "ai.daily_tokens_cap");
            if (dailyCap is > 0)
            {
                DateTime now = _clock.UtcNow;
                DateTime dayStart = new(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
                DateTime dayEnd = dayStart.AddDays(1);
                UsageSnapshot dailySnapshot = await _usageMeter.GetRangeAsync(userId, TotalKind, dayStart, dayEnd);
                int dailyUsed = dailySnapshot.TotalInputTokens + dailySnapshot.TotalOutputTokens;
                if (dailyUsed >= dailyCap.Value)
                {
                    return new AiUsageDecision(false, userId, "ai.quota_exceeded", "Daily AI usage cap reached.");
                }
            }

            return new AiUsageDecision(true, userId, null, null);
        }

        private bool IsRateLimited(string userId, int limit)
        {
            string cacheKey = $"ai-rate:{userId}";
            RateLimitState state = _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.SlidingExpiration = RateLimitWindow;
                return new RateLimitState(_clock.UtcNow);
            }) ?? new RateLimitState(_clock.UtcNow);

            lock (state.Gate)
            {
                DateTime now = _clock.UtcNow;
                if (now - state.WindowStartUtc >= RateLimitWindow)
                {
                    state.WindowStartUtc = now;
                    state.Count = 0;
                }

                state.Count += 1;
                return state.Count > limit;
            }
        }

        private sealed class RateLimitState
        {
            public RateLimitState(DateTime windowStartUtc)
            {
                WindowStartUtc = windowStartUtc;
            }

            public DateTime WindowStartUtc { get; set; }
            public int Count { get; set; }
            public object Gate { get; } = new();
        }
    }
}
