using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared.Billing;

namespace WriterApp.Application.Billing
{
    public sealed class StripeEntitlementSyncService
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly IEntitlementService _entitlementService;
        private readonly IStripePriceResolver _stripePriceResolver;
        private readonly StripeOptions _stripeOptions;
        private readonly ILogger<StripeEntitlementSyncService> _logger;

        public StripeEntitlementSyncService(
            AppDbContext dbContext,
            IUserEntitlementStore userEntitlementStore,
            IEntitlementService entitlementService,
            IStripePriceResolver stripePriceResolver,
            StripeOptions stripeOptions,
            ILogger<StripeEntitlementSyncService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _stripePriceResolver = stripePriceResolver ?? throw new ArgumentNullException(nameof(stripePriceResolver));
            _stripeOptions = stripeOptions ?? throw new ArgumentNullException(nameof(stripeOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserEntitlement> SyncFromSubscriptionAsync(
            string userId,
            string? stripeCustomerId,
            JsonElement subscription,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                throw new ArgumentException("User id is required.", nameof(userId));
            }

            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            string planBefore = entitlement.PlanKey ?? string.Empty;
            string statusBefore = EntitlementAccessEvaluator.NormalizeSubscriptionStatus(entitlement.SubscriptionStatus);
            BillingSubscriptionPolicyDecision accessBefore = BillingSubscriptionPolicy.Evaluate(statusBefore);
            string activeMode = StripeBillingEnvironment.Normalize(_stripeOptions.Mode);
            string previousStripeMode = StripeBillingEnvironment.ResolveStoredMode(entitlement, _stripeOptions);
            if (StripeBillingEnvironment.IsModeMismatch(previousStripeMode, activeMode))
            {
                _logger.LogWarning(
                    "Stripe entitlement sync overwriting opposite-mode linkage. UserId={UserId} StoredStripeMode={StoredStripeMode} ActiveStripeMode={ActiveStripeMode} StoredCustomerId={StoredCustomerId} StoredSubscriptionId={StoredSubscriptionId}",
                    userId,
                    previousStripeMode,
                    activeMode,
                    entitlement.StripeCustomerId ?? string.Empty,
                    entitlement.StripeSubscriptionId ?? string.Empty);
            }

            string? subscriptionId = ReadString(subscription, "id");
            string? resolvedCustomerId = stripeCustomerId;
            if (string.IsNullOrWhiteSpace(resolvedCustomerId))
            {
                resolvedCustomerId = ReadString(subscription, "customer");
            }

            string? stripePriceId = ReadString(subscription, "items", "data", 0, "price", "id");
            string normalizedStatus = NormalizeStripeStatus(ReadString(subscription, "status"));
            BillingSubscriptionPolicyDecision accessAfter = BillingSubscriptionPolicy.Evaluate(normalizedStatus);
            DateTimeOffset? nextPeriodStartUtc = ReadUnixTimestamp(subscription, "current_period_start");
            DateTimeOffset? nextCurrentPeriodEndUtc = ReadUnixTimestamp(subscription, "current_period_end");
            bool cancelAtPeriodEnd = ReadBool(subscription, "cancel_at_period_end") ?? false;

            string nextPlanKey = ResolvePlanKey(entitlement, stripePriceId, normalizedStatus);
            int nextBudget = ResolveBudget(nextPlanKey);

            if (nextPeriodStartUtc.HasValue && entitlement.PeriodStartUtc != nextPeriodStartUtc.Value)
            {
                entitlement.AiTokensUsedThisPeriod = 0;
            }

            entitlement.PlanKey = nextPlanKey;
            entitlement.SubscriptionStatus = normalizedStatus;
            entitlement.AiMonthlyTokenBudget = nextBudget;
            entitlement.StripeMode = activeMode;
            if (!string.IsNullOrWhiteSpace(resolvedCustomerId))
            {
                entitlement.StripeCustomerId = resolvedCustomerId;
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                entitlement.StripeSubscriptionId = subscriptionId;
            }

            entitlement.StripePriceId = stripePriceId;
            entitlement.CurrentPeriodEndUtc = nextCurrentPeriodEndUtc;
            entitlement.CancelAtPeriodEnd = cancelAtPeriodEnd;
            if (nextPeriodStartUtc.HasValue)
            {
                entitlement.PeriodStartUtc = nextPeriodStartUtc.Value;
            }

            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            _entitlementService.InvalidateForUser(userId);
            if (!string.Equals(statusBefore, normalizedStatus, StringComparison.Ordinal)
                || accessBefore.KeepsPaidAccess != accessAfter.KeepsPaidAccess)
            {
                _logger.LogInformation(
                    "Stripe subscription policy state changed. UserId={UserId} StripeMode={StripeMode} StatusBefore={StatusBefore} StatusAfter={StatusAfter} PaidAccessBefore={PaidAccessBefore} PaidAccessAfter={PaidAccessAfter} PolicyBefore={PolicyBefore} PolicyAfter={PolicyAfter} CancelAtPeriodEnd={CancelAtPeriodEnd} CurrentPeriodEndUtc={CurrentPeriodEndUtc}",
                    userId,
                    activeMode,
                    statusBefore,
                    normalizedStatus,
                    accessBefore.KeepsPaidAccess,
                    accessAfter.KeepsPaidAccess,
                    accessBefore.PolicyCode,
                    accessAfter.PolicyCode,
                    cancelAtPeriodEnd,
                    nextCurrentPeriodEndUtc);
            }

            _logger.LogInformation(
                "Stripe entitlement sync applied. UserId={UserId} StripeMode={StripeMode} PlanBefore={PlanBefore} PlanAfter={PlanAfter} Status={Status} Policy={Policy} PaidAccess={PaidAccess} StripeCustomerId={StripeCustomerId} StripeSubscriptionId={StripeSubscriptionId} StripePriceId={StripePriceId}",
                userId,
                activeMode,
                planBefore,
                entitlement.PlanKey ?? string.Empty,
                normalizedStatus,
                accessAfter.PolicyCode,
                accessAfter.KeepsPaidAccess,
                resolvedCustomerId ?? string.Empty,
                subscriptionId ?? string.Empty,
                stripePriceId ?? string.Empty);
            return entitlement;
        }

        private string ResolvePlanKey(UserEntitlement entitlement, string? stripePriceId, string normalizedStatus)
        {
            if (string.Equals(normalizedStatus, "canceled", StringComparison.OrdinalIgnoreCase))
            {
                return UserEntitlementDefaults.FreePlanKey;
            }

            if (!string.IsNullOrWhiteSpace(stripePriceId))
            {
                string? resolvedPlanKey = _stripePriceResolver.ResolvePlanKey(stripePriceId);
                if (!string.IsNullOrWhiteSpace(resolvedPlanKey))
                {
                    return UserEntitlementDefaults.NormalizePlanKey(resolvedPlanKey);
                }
            }

            string preservedPlanKey = UserEntitlementDefaults.NormalizePlanKey(entitlement.PlanKey);
            BillingSubscriptionPolicyDecision policyDecision = BillingSubscriptionPolicy.Evaluate(normalizedStatus);
            bool preserveExistingPaidPlan =
                _stripeOptions.IsLiveMode
                && policyDecision.KeepsPaidAccess
                && IsPaidPlan(preservedPlanKey);

            if (preserveExistingPaidPlan)
            {
                _logger.LogError(
                    "Stripe entitlement sync found unmapped live price id and preserved prior paid plan. UserId={UserId} PriceId={PriceId} Status={Status} PreservedPlanKey={PreservedPlanKey} StripeSubscriptionId={StripeSubscriptionId}",
                    entitlement.UserId,
                    stripePriceId ?? string.Empty,
                    normalizedStatus,
                    preservedPlanKey,
                    entitlement.StripeSubscriptionId ?? string.Empty);
                return preservedPlanKey;
            }

            _logger.LogError(
                "Stripe entitlement sync found unmapped Stripe price id. UserId={UserId} StripeMode={StripeMode} PriceId={PriceId} Status={Status} ExistingPlanKey={ExistingPlanKey} FallbackPlanKey={FallbackPlanKey}",
                entitlement.UserId,
                StripeBillingEnvironment.Normalize(_stripeOptions.Mode),
                stripePriceId ?? string.Empty,
                normalizedStatus,
                preservedPlanKey,
                UserEntitlementDefaults.FreePlanKey);
            return UserEntitlementDefaults.FreePlanKey;
        }

        private static bool IsPaidPlan(string planKey)
        {
            return string.Equals(planKey, UserEntitlementDefaults.StandardPlanKey, StringComparison.Ordinal)
                || string.Equals(planKey, UserEntitlementDefaults.ProfessionalPlanKey, StringComparison.Ordinal);
        }

        private static int ResolveBudget(string planKey)
        {
            string normalizedPlan = UserEntitlementDefaults.NormalizePlanKey(planKey);
            return normalizedPlan switch
            {
                UserEntitlementDefaults.StandardPlanKey => UserEntitlementDefaults.StandardMonthlyTokenBudget,
                UserEntitlementDefaults.ProfessionalPlanKey => UserEntitlementDefaults.ProfessionalMonthlyTokenBudget,
                _ => UserEntitlementDefaults.FreeMonthlyTokenBudget
            };
        }

        private static string NormalizeStripeStatus(string? rawStatus)
        {
            return BillingSubscriptionPolicy.NormalizeStatus(rawStatus);
        }

        private static string? ReadString(JsonElement element, params object[] path)
        {
            if (!TryTraverse(element, path, out JsonElement target))
            {
                return null;
            }

            if (target.ValueKind == JsonValueKind.String)
            {
                return target.GetString();
            }

            if (target.ValueKind == JsonValueKind.Number)
            {
                return target.ToString();
            }

            return null;
        }

        private static bool? ReadBool(JsonElement element, params object[] path)
        {
            if (!TryTraverse(element, path, out JsonElement target))
            {
                return null;
            }

            return target.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static DateTimeOffset? ReadUnixTimestamp(JsonElement element, params object[] path)
        {
            if (!TryTraverse(element, path, out JsonElement target))
            {
                return null;
            }

            if (target.ValueKind == JsonValueKind.Number && target.TryGetInt64(out long value))
            {
                return DateTimeOffset.FromUnixTimeSeconds(value);
            }

            if (target.ValueKind == JsonValueKind.String
                && long.TryParse(target.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            {
                return DateTimeOffset.FromUnixTimeSeconds(parsed);
            }

            return null;
        }

        private static bool TryTraverse(JsonElement element, object[] path, out JsonElement target)
        {
            target = element;
            foreach (object segment in path)
            {
                if (segment is string propertyName)
                {
                    if (target.ValueKind != JsonValueKind.Object
                        || !target.TryGetProperty(propertyName, out JsonElement child))
                    {
                        target = default;
                        return false;
                    }

                    target = child;
                    continue;
                }

                if (segment is int index)
                {
                    if (target.ValueKind != JsonValueKind.Array
                        || index < 0
                        || index >= target.GetArrayLength())
                    {
                        target = default;
                        return false;
                    }

                    target = target[index];
                    continue;
                }

                target = default;
                return false;
            }

            return true;
        }
    }
}
