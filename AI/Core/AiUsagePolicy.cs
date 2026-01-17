using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Application.Usage;

namespace WriterApp.AI.Core
{
    public sealed class AiUsagePolicy : IAiUsagePolicy
    {
        private const string TotalKind = "ai.total";

        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IEntitlementService _entitlementService;
        private readonly IUsageMeter _usageMeter;
        private readonly WriterAiOptions _options;

        public AiUsagePolicy(
            IHttpContextAccessor httpContextAccessor,
            IEntitlementService entitlementService,
            IUsageMeter usageMeter,
            IOptions<WriterAiOptions> options)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _usageMeter = usageMeter ?? throw new ArgumentNullException(nameof(usageMeter));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<AiUsageDecision> EvaluateAsync(IAiProvider provider, string actionId)
        {
            if (provider is null)
            {
                return new AiUsageDecision(false, string.Empty, "ai.provider_missing", "AI provider is unavailable.");
            }

            bool requiresEntitlement = provider is IAiBillingProvider billingProvider && billingProvider.RequiresEntitlement;
            string userId = _httpContextAccessor.HttpContext?.User.GetUserId() ?? string.Empty;

            if (!requiresEntitlement)
            {
                return new AiUsageDecision(true, userId, null, null);
            }

            if (!_options.Enabled)
            {
                return new AiUsageDecision(false, userId, "ai.disabled", "AI is disabled by configuration.");
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return new AiUsageDecision(false, string.Empty, "auth.required", "Sign in to use AI.");
            }

            bool aiEnabled = await _entitlementService.HasAsync(userId, "ai.enabled");
            if (!aiEnabled)
            {
                return new AiUsageDecision(false, userId, "ai.disabled", "AI is not enabled for your plan.");
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

            return new AiUsageDecision(true, userId, null, null);
        }
    }
}
