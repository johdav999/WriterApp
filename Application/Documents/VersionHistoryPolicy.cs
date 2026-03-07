using System;

namespace WriterApp.Application.Documents
{
    public sealed record VersionHistoryPolicy(
        string PlanKey,
        string PlanName,
        bool Enabled,
        int? MaxVersions,
        int? RetentionDays,
        bool CanRestoreVersions,
        bool CanCompareVersions,
        bool CanUseHistoryUi,
        bool CanSaveManualVersions)
    {
        public bool HasLimitedHistory => Enabled && (MaxVersions.GetValueOrDefault() > 0 || RetentionDays.GetValueOrDefault() > 0);
    }
}
