using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WriterApp.AI.Abstractions;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Usage
{
    public sealed class AiQuotaService : IAiQuotaService
    {
        private const string QuotaExceededCode = "AI_QUOTA_EXCEEDED";
        private const string QuotaExceededMessage = "AI quota exceeded. Upgrade to continue.";
        private static readonly TimeSpan BillingWindow = TimeSpan.FromDays(30);

        private readonly AppDbContext _dbContext;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly IClock _clock;

        public AiQuotaService(
            AppDbContext dbContext,
            IUserEntitlementStore userEntitlementStore,
            IClock clock)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public async Task<AiQuotaDecision> CheckAsync(string userId, CancellationToken ct)
        {
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            await ResetWindowIfExpiredAsync(entitlement, ct);

            AiQuotaSnapshot snapshot = ToSnapshot(entitlement);
            if (snapshot.Used >= snapshot.Budget)
            {
                return new AiQuotaDecision(false, QuotaExceededCode, QuotaExceededMessage, snapshot);
            }

            return new AiQuotaDecision(true, null, null, snapshot);
        }

        public async Task<AiQuotaChargeResult> ChargeAsync(string userId, AiRequest request, AiResult result, CancellationToken ct)
        {
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            await ResetWindowIfExpiredAsync(entitlement, ct);

            int chargedTokens = ResolveChargedTokens(request, result);
            if (chargedTokens <= 0)
            {
                return new AiQuotaChargeResult(true, 0, ToSnapshot(entitlement), null, null);
            }

            DateTimeOffset now = _clock.UtcNow;
            int affectedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE UserEntitlements
SET AiTokensUsedThisPeriod = AiTokensUsedThisPeriod + {chargedTokens},
    UpdatedUtc = {now}
WHERE UserId = {userId}
  AND AiTokensUsedThisPeriod + {chargedTokens} <= AiMonthlyTokenBudget;", ct);

            UserEntitlement updated = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            AiQuotaSnapshot snapshot = ToSnapshot(updated);
            if (affectedRows <= 0)
            {
                return new AiQuotaChargeResult(false, 0, snapshot, QuotaExceededCode, QuotaExceededMessage);
            }

            return new AiQuotaChargeResult(true, chargedTokens, snapshot, null, null);
        }

        private async Task ResetWindowIfExpiredAsync(UserEntitlement entitlement, CancellationToken ct)
        {
            DateTimeOffset now = _clock.UtcNow;
            if (now < entitlement.PeriodStartUtc + BillingWindow)
            {
                return;
            }

            entitlement.PeriodStartUtc = now;
            entitlement.AiTokensUsedThisPeriod = 0;
            entitlement.UpdatedUtc = now;
            await _dbContext.SaveChangesAsync(ct);
        }

        private static int ResolveChargedTokens(AiRequest request, AiResult result)
        {
            int direct = Math.Max(0, result.Usage.InputTokens) + Math.Max(0, result.Usage.OutputTokens);
            if (direct > 0)
            {
                return direct;
            }

            // Fallback when provider usage is missing. We use a conservative approximation:
            // 1 token ~= 4 characters across request + response payloads.
            int requestChars = 0;
            requestChars += request.Context.OriginalText?.Length ?? 0;
            requestChars += request.Context.SelectionText?.Length ?? 0;
            requestChars += request.Context.SurroundingText?.Length ?? 0;
            requestChars += request.Context.ContainingParagraph?.Length ?? 0;
            requestChars += request.Context.SurroundingBefore?.Length ?? 0;
            requestChars += request.Context.SurroundingAfter?.Length ?? 0;

            if (request.Inputs is not null)
            {
                foreach (KeyValuePair<string, object> pair in request.Inputs)
                {
                    requestChars += pair.Value?.ToString()?.Length ?? 0;
                }
            }

            int responseChars = 0;
            if (result.Artifacts is not null)
            {
                foreach (AiArtifact artifact in result.Artifacts)
                {
                    responseChars += artifact.TextContent?.Length ?? 0;
                    responseChars += artifact.BinaryContent?.Length ?? 0;
                }
            }

            int estimated = (requestChars + responseChars + 3) / 4;
            return Math.Max(1, estimated);
        }

        private static AiQuotaSnapshot ToSnapshot(UserEntitlement entitlement)
        {
            return new AiQuotaSnapshot(
                UserEntitlementDefaults.NormalizePlanKey(entitlement.PlanKey),
                Math.Max(0, entitlement.AiMonthlyTokenBudget),
                Math.Max(0, entitlement.AiTokensUsedThisPeriod),
                entitlement.PeriodStartUtc);
        }
    }
}
