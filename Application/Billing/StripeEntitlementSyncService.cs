using System;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Application.Billing
{
    public sealed class StripeEntitlementSyncService
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly IEntitlementService _entitlementService;
        private readonly IStripePriceResolver _stripePriceResolver;
        private readonly ILogger<StripeEntitlementSyncService> _logger;

        public StripeEntitlementSyncService(
            AppDbContext dbContext,
            IUserEntitlementStore userEntitlementStore,
            IEntitlementService entitlementService,
            IStripePriceResolver stripePriceResolver,
            ILogger<StripeEntitlementSyncService> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _stripePriceResolver = stripePriceResolver ?? throw new ArgumentNullException(nameof(stripePriceResolver));
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
            string? subscriptionId = ReadString(subscription, "id");
            string? resolvedCustomerId = stripeCustomerId;
            if (string.IsNullOrWhiteSpace(resolvedCustomerId))
            {
                resolvedCustomerId = ReadString(subscription, "customer");
            }

            string? stripePriceId = ReadString(subscription, "items", "data", 0, "price", "id");
            string normalizedStatus = NormalizeStripeStatus(ReadString(subscription, "status"));
            DateTimeOffset? nextPeriodStartUtc = ReadUnixTimestamp(subscription, "current_period_start");
            DateTimeOffset? nextCurrentPeriodEndUtc = ReadUnixTimestamp(subscription, "current_period_end");
            bool cancelAtPeriodEnd = ReadBool(subscription, "cancel_at_period_end") ?? false;

            string nextPlanKey = ResolvePlanKey(stripePriceId, normalizedStatus);
            int nextBudget = ResolveBudget(nextPlanKey);

            if (nextPeriodStartUtc.HasValue && entitlement.PeriodStartUtc != nextPeriodStartUtc.Value)
            {
                entitlement.AiTokensUsedThisPeriod = 0;
            }

            entitlement.PlanKey = nextPlanKey;
            entitlement.SubscriptionStatus = normalizedStatus;
            entitlement.AiMonthlyTokenBudget = nextBudget;
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
            _logger.LogInformation(
                "Stripe entitlement sync applied. UserId={UserId} PlanBefore={PlanBefore} PlanAfter={PlanAfter} StripeCustomerId={StripeCustomerId} StripeSubscriptionId={StripeSubscriptionId} StripePriceId={StripePriceId}",
                userId,
                planBefore,
                entitlement.PlanKey ?? string.Empty,
                resolvedCustomerId ?? string.Empty,
                subscriptionId ?? string.Empty,
                stripePriceId ?? string.Empty);
            return entitlement;
        }

        private string ResolvePlanKey(string? stripePriceId, string normalizedStatus)
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

            _logger.LogWarning(
                "Stripe entitlement sync found unknown price id. PriceId={PriceId} Status={Status}. Defaulting to free plan.",
                stripePriceId,
                normalizedStatus);
            return UserEntitlementDefaults.FreePlanKey;
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
            if (string.IsNullOrWhiteSpace(rawStatus))
            {
                return "active";
            }

            string status = rawStatus.Trim().ToLowerInvariant();
            return status switch
            {
                "trialing" => "active",
                "active" => "active",
                "past_due" => "past_due",
                "unpaid" => "unpaid",
                "incomplete" => "incomplete",
                "canceled" => "canceled",
                _ => status
            };
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
