using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WriterApp.AI.Abstractions;

namespace WriterApp.Application.Usage
{
    public interface IAiQuotaService
    {
        Task<AiQuotaDecision> CheckAsync(string userId, CancellationToken ct);
        Task<AiQuotaChargeResult> ChargeAsync(string userId, AiRequest request, AiResult result, CancellationToken ct);
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
        AiQuotaSnapshot Snapshot)
    {
        public IReadOnlyDictionary<string, object?> ToErrorDetails()
        {
            return new Dictionary<string, object?>
            {
                ["planKey"] = Snapshot.PlanKey,
                ["budget"] = Snapshot.Budget,
                ["used"] = Snapshot.Used,
                ["periodStartUtc"] = Snapshot.PeriodStartUtc
            };
        }
    }

    public sealed record AiQuotaChargeResult(
        bool Applied,
        int ChargedTokens,
        AiQuotaSnapshot Snapshot,
        string? ErrorCode,
        string? ErrorMessage)
    {
        public IReadOnlyDictionary<string, object?> ToErrorDetails()
        {
            return new Dictionary<string, object?>
            {
                ["planKey"] = Snapshot.PlanKey,
                ["budget"] = Snapshot.Budget,
                ["used"] = Snapshot.Used,
                ["chargedTokens"] = ChargedTokens,
                ["periodStartUtc"] = Snapshot.PeriodStartUtc
            };
        }
    }
}
