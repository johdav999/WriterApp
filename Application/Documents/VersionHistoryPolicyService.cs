using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using WriterApp.Application.Subscriptions;

namespace WriterApp.Application.Documents
{
    public sealed class VersionHistoryPolicyService : IVersionHistoryPolicyService
    {
        private readonly IEntitlementService _entitlementService;

        public VersionHistoryPolicyService(IEntitlementService entitlementService)
        {
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
        }

        public async Task<VersionHistoryPolicy> GetPolicyAsync(string userId)
        {
            UserEntitlements entitlements = await _entitlementService.GetEntitlementsAsync(userId);
            string planKey = UserEntitlementDefaults.NormalizePlanKey(entitlements.PlanKey);
            VersionHistoryPolicy defaults = GetDefaults(planKey, entitlements.PlanName);

            // Keep the product model explicit in code, but still allow seeded plan entitlements
            // to override the defaults without requiring schema changes.
            bool enabled = GetBool(entitlements.Entitlements, "history.enabled") ?? defaults.Enabled;
            int? maxVersions = GetInt(entitlements.Entitlements, "history.max_versions") ?? defaults.MaxVersions;
            int? retentionDays = GetInt(entitlements.Entitlements, "history.retention_days") ?? defaults.RetentionDays;

            if (!enabled)
            {
                return defaults with
                {
                    Enabled = false,
                    MaxVersions = maxVersions,
                    RetentionDays = retentionDays,
                    CanRestoreVersions = false,
                    CanCompareVersions = false,
                    CanUseHistoryUi = false,
                    CanSaveManualVersions = false
                };
            }

            return defaults with
            {
                MaxVersions = maxVersions,
                RetentionDays = retentionDays
            };
        }

        private static VersionHistoryPolicy GetDefaults(string planKey, string planName)
        {
            return planKey switch
            {
                UserEntitlementDefaults.StandardPlanKey => new VersionHistoryPolicy(
                    planKey,
                    NormalizePlanName(planName, planKey),
                    Enabled: true,
                    MaxVersions: 100,
                    RetentionDays: 30,
                    CanRestoreVersions: true,
                    CanCompareVersions: true,
                    CanUseHistoryUi: true,
                    CanSaveManualVersions: true),
                UserEntitlementDefaults.ProfessionalPlanKey => new VersionHistoryPolicy(
                    planKey,
                    NormalizePlanName(planName, planKey),
                    Enabled: true,
                    MaxVersions: 250,
                    RetentionDays: 90,
                    CanRestoreVersions: true,
                    CanCompareVersions: true,
                    CanUseHistoryUi: true,
                    CanSaveManualVersions: true),
                _ => new VersionHistoryPolicy(
                    UserEntitlementDefaults.FreePlanKey,
                    NormalizePlanName(planName, UserEntitlementDefaults.FreePlanKey),
                    Enabled: true,
                    MaxVersions: 5,
                    RetentionDays: 7,
                    CanRestoreVersions: true,
                    CanCompareVersions: false,
                    CanUseHistoryUi: false,
                    CanSaveManualVersions: false)
            };
        }

        private static bool? GetBool(IReadOnlyDictionary<string, string> entitlements, string key)
        {
            if (!entitlements.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return bool.TryParse(value, out bool parsed) ? parsed : null;
        }

        private static int? GetInt(IReadOnlyDictionary<string, string> entitlements, string key)
        {
            if (!entitlements.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : null;
        }

        private static string NormalizePlanName(string planName, string fallbackPlanKey)
        {
            return string.IsNullOrWhiteSpace(planName) ? fallbackPlanKey : planName.Trim();
        }
    }
}
