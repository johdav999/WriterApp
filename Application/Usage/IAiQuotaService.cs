using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;

namespace WriterApp.Application.Usage
{
    public interface IAiQuotaService
    {
        Task<AiQuotaDecision> EnsureAiAllowedAsync(string userId, int estimatedTokens, CancellationToken ct);
        Task<AiQuotaChargeResult> ChargeActualUsageAsync(string userId, AiRequest request, AiResult result, CancellationToken ct);
    }

    public sealed record AiAccessError(
        bool UpgradeRequired,
        string CurrentPlan,
        int Limit,
        int Used,
        DateTimeOffset ResetAt)
    {
        public IReadOnlyDictionary<string, object?> ToDetails()
        {
            return new Dictionary<string, object?>
            {
                ["upgrade_required"] = UpgradeRequired,
                ["current_plan"] = CurrentPlan,
                ["limit"] = Limit,
                ["used"] = Used,
                ["reset_at"] = ResetAt
            };
        }
    }

    public sealed record AiQuotaSnapshot(
        string PlanKey,
        int Budget,
        int Used,
        DateTimeOffset PeriodStartUtc);

    public sealed record AiQuotaDecision(
        bool Allowed,
        string? ErrorCode,
        string? ErrorMessage,
        AiQuotaSnapshot Snapshot,
        AiAccessError? Error)
    {
        public IReadOnlyDictionary<string, object?> ToErrorDetails()
        {
            Dictionary<string, object?> details = new()
            {
                ["planKey"] = Snapshot.PlanKey,
                ["budget"] = Snapshot.Budget,
                ["used"] = Snapshot.Used,
                ["periodStartUtc"] = Snapshot.PeriodStartUtc
            };

            if (Error is not null)
            {
                foreach ((string key, object? value) in Error.ToDetails())
                {
                    details[key] = value;
                }
            }

            return details;
        }
    }

    public sealed record AiQuotaChargeResult(
        bool Applied,
        int ChargedTokens,
        AiQuotaSnapshot Snapshot,
        string? ErrorCode,
        string? ErrorMessage,
        AiAccessError? Error)
    {
        public IReadOnlyDictionary<string, object?> ToErrorDetails()
        {
            Dictionary<string, object?> details = new()
            {
                ["planKey"] = Snapshot.PlanKey,
                ["budget"] = Snapshot.Budget,
                ["used"] = Snapshot.Used,
                ["chargedTokens"] = ChargedTokens,
                ["periodStartUtc"] = Snapshot.PeriodStartUtc
            };

            if (Error is not null)
            {
                foreach ((string key, object? value) in Error.ToDetails())
                {
                    details[key] = value;
                }
            }

            return details;
        }
    }
}
