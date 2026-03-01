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
        private const string SubscriptionInactiveCode = "AI_SUBSCRIPTION_INACTIVE";
        private const string QuotaExceededMessage = "AI quota exceeded. Upgrade to continue.";
        private const string SubscriptionInactiveMessage = "Your subscription is not active. Update billing to continue.";
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

        public async Task<AiQuotaDecision> EnsureAiAllowedAsync(string userId, int estimatedTokens, CancellationToken ct)
        {
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            await ResetWindowIfExpiredAsync(entitlement, ct);

            AiQuotaSnapshot snapshot = ToSnapshot(entitlement);
            string normalizedPlan = UserEntitlementDefaults.NormalizePlanKey(entitlement.PlanKey);
            string normalizedStatus = NormalizeSubscriptionStatus(entitlement.SubscriptionStatus);
            bool paidPlan = IsPaidPlan(normalizedPlan);
            bool subscriptionIsActive = !paidPlan || string.Equals(normalizedStatus, "active", StringComparison.Ordinal);
            if (!subscriptionIsActive)
            {
                AiAccessError error = BuildAccessError(snapshot, upgradeRequired: true);
                return new AiQuotaDecision(false, SubscriptionInactiveCode, SubscriptionInactiveMessage, snapshot, error);
            }

            int boundedEstimate = Math.Max(0, estimatedTokens);
            if (snapshot.Used + boundedEstimate > snapshot.Budget)
            {
                AiAccessError error = BuildAccessError(snapshot, upgradeRequired: true);
                return new AiQuotaDecision(false, QuotaExceededCode, QuotaExceededMessage, snapshot, error);
            }

            return new AiQuotaDecision(true, null, null, snapshot, null);
        }

        public async Task<AiQuotaChargeResult> ChargeActualUsageAsync(string userId, AiRequest request, AiResult result, CancellationToken ct)
        {
            int chargedTokens = ResolveChargedTokens(request, result);
            if (chargedTokens <= 0)
            {
                UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
                await ResetWindowIfExpiredAsync(entitlement, ct);
                return new AiQuotaChargeResult(true, 0, ToSnapshot(entitlement), null, null, null);
            }

            const int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
                UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
                await ResetWindowIfExpiredAsync(entitlement, ct);

                AiQuotaSnapshot snapshot = ToSnapshot(entitlement);
                string normalizedPlan = UserEntitlementDefaults.NormalizePlanKey(entitlement.PlanKey);
                string normalizedStatus = NormalizeSubscriptionStatus(entitlement.SubscriptionStatus);
                bool paidPlan = IsPaidPlan(normalizedPlan);
                if (paidPlan && !string.Equals(normalizedStatus, "active", StringComparison.Ordinal))
                {
                    await transaction.RollbackAsync(ct);
                    AiAccessError inactiveError = BuildAccessError(snapshot, upgradeRequired: true);
                    return new AiQuotaChargeResult(false, 0, snapshot, SubscriptionInactiveCode, SubscriptionInactiveMessage, inactiveError);
                }

                if (snapshot.Used + chargedTokens > snapshot.Budget)
                {
                    await transaction.RollbackAsync(ct);
                    AiAccessError accessError = BuildAccessError(snapshot, upgradeRequired: true);
                    return new AiQuotaChargeResult(false, 0, snapshot, QuotaExceededCode, QuotaExceededMessage, accessError);
                }

                int originalUsed = entitlement.AiTokensUsedThisPeriod;
                DateTimeOffset originalPeriodStart = entitlement.PeriodStartUtc;
                DateTimeOffset now = _clock.UtcNow;
                int affectedRows = await _dbContext.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE UserEntitlements
SET AiTokensUsedThisPeriod = {originalUsed + chargedTokens},
    UpdatedUtc = {now}
WHERE UserId = {userId}
  AND AiTokensUsedThisPeriod = {originalUsed}
  AND PeriodStartUtc = {originalPeriodStart};", ct);

                if (affectedRows > 0)
                {
                    await transaction.CommitAsync(ct);
                    UserEntitlement updated = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
                    return new AiQuotaChargeResult(true, chargedTokens, ToSnapshot(updated), null, null, null);
                }

                await transaction.RollbackAsync(ct);
            }

            UserEntitlement latest = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            AiQuotaSnapshot latestSnapshot = ToSnapshot(latest);
            AiAccessError error = BuildAccessError(latestSnapshot, upgradeRequired: true);
            return new AiQuotaChargeResult(false, 0, latestSnapshot, QuotaExceededCode, QuotaExceededMessage, error);
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

        private static bool IsPaidPlan(string planKey)
        {
            return string.Equals(planKey, UserEntitlementDefaults.StandardPlanKey, StringComparison.Ordinal)
                || string.Equals(planKey, UserEntitlementDefaults.ProfessionalPlanKey, StringComparison.Ordinal);
        }

        private static string NormalizeSubscriptionStatus(string? rawStatus)
        {
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                return "active";
            }

            string normalized = rawStatus.Trim().ToLowerInvariant();
            if (string.Equals(normalized, "trialing", StringComparison.Ordinal))
            {
                return "active";
            }

            return normalized;
        }

        private static AiAccessError BuildAccessError(AiQuotaSnapshot snapshot, bool upgradeRequired)
        {
            DateTimeOffset resetAt = snapshot.PeriodStartUtc + BillingWindow;
            return new AiAccessError(
                upgradeRequired,
                snapshot.PlanKey,
                snapshot.Budget,
                snapshot.Used,
                resetAt);
        }
    }
}
