using System;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Subscriptions;
using WriterApp.Client.State;

namespace WriterApp.Client.Services
{
    internal sealed class FeatureAccessService
    {
        private readonly AuthMeStateService _authMeStateService;
        private readonly ILogger<FeatureAccessService> _logger;
        private readonly HashSet<string> _loggedDeniedFeatures = new(StringComparer.Ordinal);

        public FeatureAccessService(
            AuthMeStateService authMeStateService,
            ILogger<FeatureAccessService> logger)
        {
            _authMeStateService = authMeStateService ?? throw new ArgumentNullException(nameof(authMeStateService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool CanUse(FeatureKey feature)
        {
            PlanTier userTier = GetCurrentTier();
            PlanTier requiredTier = GetRequiredTier(feature);
            bool allowed = FeatureRegistry.IsFeatureAllowed(feature, userTier);
            if (!allowed)
            {
                string fingerprint = $"{feature}:{userTier}:{requiredTier}";
                if (_loggedDeniedFeatures.Add(fingerprint))
                {
                    _logger.LogInformation(
                        "FeatureAccessDenied FeatureKey={FeatureKey} UserTier={UserTier} RequiredTier={RequiredTier}",
                        feature,
                        userTier,
                        requiredTier);
                }
            }

            return allowed;
        }

        public PlanTier GetCurrentTier()
        {
            return NormalizePlanKey(_authMeStateService.PlanKey);
        }

        public PlanTier GetRequiredTier(FeatureKey feature)
        {
            if (!FeatureRegistry.FeatureMinimumTier.TryGetValue(feature, out PlanTier requiredTier))
            {
                throw new ArgumentOutOfRangeException(nameof(feature), feature, "Feature is not registered.");
            }

            return requiredTier;
        }

        public string GetUpgradeMessage(FeatureKey feature)
        {
            return $"Available in {GetPlanLabel(GetRequiredTier(feature))} plan";
        }

        public string GetUpgradePath()
        {
            return "/upgrade";
        }

        private static PlanTier NormalizePlanKey(string? planKey)
        {
            if (string.IsNullOrWhiteSpace(planKey))
            {
                return PlanTier.Free;
            }

            return planKey.Trim().ToLowerInvariant() switch
            {
                "standard" => PlanTier.Standard,
                "professional" => PlanTier.Professional,
                "pro" => PlanTier.Professional,
                _ => PlanTier.Free
            };
        }

        private static string GetPlanLabel(PlanTier tier)
        {
            return tier switch
            {
                PlanTier.Standard => "Standard",
                PlanTier.Professional => "Professional",
                _ => "Free"
            };
        }
    }
}
