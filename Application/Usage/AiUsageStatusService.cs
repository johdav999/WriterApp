using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Subscriptions;

namespace WriterApp.Application.Usage
{
    public sealed class AiUsageStatusService : IAiUsageStatusService
    {
        private const string TotalKind = "ai.total";

        private readonly IEntitlementService _entitlementService;
        private readonly IUsageMeter _usageMeter;
        private readonly ILogger<AiUsageStatusService> _logger;

        public AiUsageStatusService(
            IEntitlementService entitlementService,
            IUsageMeter usageMeter,
            ILogger<AiUsageStatusService> logger)
        {
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _usageMeter = usageMeter ?? throw new ArgumentNullException(nameof(usageMeter));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<AiUsageStatus> GetStatusAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                AiUsageStatus emptyStatus = new("Free", false, 0, 0, 0);
                _logger.LogInformation(
                    "AI entitlements resolved: userId={UserId} plan={PlanName} ai.enabled={AiEnabled} quotaTotal={QuotaTotal} quotaRemaining={QuotaRemaining}",
                    userId,
                    emptyStatus.PlanName,
                    emptyStatus.AiEnabled,
                    emptyStatus.QuotaTotal,
                    emptyStatus.QuotaRemaining);
                return emptyStatus;
            }

            UserEntitlements entitlements = await _entitlementService.GetEntitlementsAsync(userId);
            bool aiEnabled = await _entitlementService.HasAsync(userId, "ai.enabled");
            int quotaTotal = await _entitlementService.GetIntAsync(userId, "ai.monthly_tokens") ?? 0;

            UsageSnapshot snapshot = await _usageMeter.GetCurrentPeriodAsync(userId, TotalKind);
            int quotaUsed = snapshot.TotalInputTokens + snapshot.TotalOutputTokens;
            int quotaRemaining = Math.Max(0, quotaTotal - quotaUsed);

            AiUsageStatus status = new(
                entitlements.PlanName,
                aiEnabled,
                quotaTotal,
                quotaUsed,
                quotaRemaining);
            _logger.LogInformation(
                "AI entitlements resolved: userId={UserId} plan={PlanName} ai.enabled={AiEnabled} quotaTotal={QuotaTotal} quotaRemaining={QuotaRemaining}",
                userId,
                status.PlanName,
                status.AiEnabled,
                status.QuotaTotal,
                status.QuotaRemaining);
            return status;
        }
    }
}
