using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Billing;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/stripe")]
    [AllowAnonymous]
    public sealed class StripeWebhookController : ControllerBase
    {
        private const string StatusProcessed = "Processed";
        private const string StatusSkipped = "Skipped";
        private const string StatusError = "Error";
        private const string StatusProcessing = "Processing";
        private static readonly TimeSpan SignatureTolerance = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _dbContext;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly StripeOptions _stripeOptions;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            AppDbContext dbContext,
            IUserEntitlementStore userEntitlementStore,
            StripeOptions stripeOptions,
            ILogger<StripeWebhookController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _stripeOptions = stripeOptions ?? throw new ArgumentNullException(nameof(stripeOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> HandleWebhook(CancellationToken ct)
        {
            if (!_stripeOptions.Enabled || string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Stripe webhook is not configured.");
            }

            string payload;
            using (StreamReader reader = new(Request.Body, Encoding.UTF8))
            {
                payload = await reader.ReadToEndAsync(ct);
            }

            string signatureHeader = Request.Headers["Stripe-Signature"].ToString();
            if (!IsStripeSignatureValid(signatureHeader, payload, _stripeOptions.WebhookSecret))
            {
                _logger.LogWarning("Stripe webhook signature verification failed.");
                return BadRequest("Invalid Stripe signature.");
            }

            JsonDocument eventDoc;
            try
            {
                eventDoc = JsonDocument.Parse(payload);
            }
            catch (JsonException)
            {
                return BadRequest("Invalid JSON payload.");
            }

            using (eventDoc)
            {
                JsonElement root = eventDoc.RootElement;
                string? eventId = ReadString(root, "id");
                string? eventType = ReadString(root, "type");
                JsonElement dataObject = ReadDataObject(root);

                if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
                {
                    return BadRequest("Stripe event missing id or type.");
                }

                StripeEventLog eventLog = await GetOrCreateEventLogAsync(eventId, eventType, ct);
                if (string.Equals(eventLog.Status, StatusProcessed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventLog.Status, StatusSkipped, StringComparison.OrdinalIgnoreCase))
                {
                    return Ok();
                }

                if (string.Equals(eventLog.Status, StatusProcessing, StringComparison.OrdinalIgnoreCase)
                    && eventLog.ProcessedUtc is null
                    && eventLog.ReceivedUtc >= DateTimeOffset.UtcNow - ProcessingLease)
                {
                    return Ok();
                }

                eventLog.Type = eventType;
                eventLog.ReceivedUtc = DateTimeOffset.UtcNow;
                eventLog.ProcessedUtc = null;
                eventLog.Status = StatusProcessing;
                eventLog.Error = null;
                await _dbContext.SaveChangesAsync(ct);

                try
                {
                    WebhookHandleResult handleResult = await DispatchAsync(eventType, dataObject, ct);

                    eventLog.UserId = handleResult.UserId ?? eventLog.UserId;
                    eventLog.Status = handleResult.Status;
                    eventLog.ProcessedUtc = DateTimeOffset.UtcNow;
                    eventLog.Error = null;
                    await _dbContext.SaveChangesAsync(ct);
                    return Ok();
                }
                catch (Exception ex)
                {
                    eventLog.Status = StatusError;
                    eventLog.ProcessedUtc = DateTimeOffset.UtcNow;
                    eventLog.Error = Truncate(ex.Message, 2000);
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogError(ex, "Stripe webhook processing failed. EventId={EventId} Type={Type}", eventId, eventType);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Webhook processing failed.");
                }
            }
        }

        private async Task<StripeEventLog> GetOrCreateEventLogAsync(string eventId, string eventType, CancellationToken ct)
        {
            StripeEventLog? existing = await _dbContext.StripeEventLogs
                .FirstOrDefaultAsync(item => item.StripeEventId == eventId, ct);
            if (existing is not null)
            {
                return existing;
            }

            StripeEventLog created = new()
            {
                StripeEventId = eventId,
                Type = eventType,
                ReceivedUtc = DateTimeOffset.UtcNow,
                Status = StatusError,
                Error = "Accepted for processing."
            };

            _dbContext.StripeEventLogs.Add(created);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                return created;
            }
            catch (DbUpdateException)
            {
                StripeEventLog? raced = await _dbContext.StripeEventLogs
                    .FirstOrDefaultAsync(item => item.StripeEventId == eventId, ct);
                if (raced is not null)
                {
                    return raced;
                }

                throw;
            }
        }

        private async Task<WebhookHandleResult> DispatchAsync(string eventType, JsonElement dataObject, CancellationToken ct)
        {
            switch (eventType)
            {
                case "checkout.session.completed":
                    return await HandleCheckoutSessionCompletedAsync(dataObject, ct);
                case "customer.subscription.created":
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                    return await HandleSubscriptionChangedAsync(dataObject, ct);
                case "invoice.paid":
                    return await HandleInvoicePaidAsync(dataObject, ct);
                case "invoice.payment_failed":
                    return await HandleInvoicePaymentFailedAsync(dataObject, ct);
                default:
                    return WebhookHandleResult.Skipped();
            }
        }

        private async Task<WebhookHandleResult> HandleCheckoutSessionCompletedAsync(JsonElement session, CancellationToken ct)
        {
            string? userId = ReadString(session, "client_reference_id")
                ?? ReadString(session, "metadata", "userId");
            if (string.IsNullOrWhiteSpace(userId))
            {
                return WebhookHandleResult.Skipped();
            }

            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            string? customerId = ReadString(session, "customer");
            string? subscriptionId = ReadString(session, "subscription");
            string? planKey = ReadString(session, "metadata", "planKey");

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                entitlement.StripeCustomerId = customerId;
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                entitlement.StripeSubscriptionId = subscriptionId;
            }

            if (!string.IsNullOrWhiteSpace(planKey))
            {
                entitlement.StripePriceId = NormalizePlanToPriceId(planKey);
            }

            entitlement.SubscriptionStatus = UserEntitlementDefaults.ActiveSubscriptionStatus;
            entitlement.CancelAtPeriodEnd = false;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return WebhookHandleResult.Processed(userId);
        }

        private async Task<WebhookHandleResult> HandleSubscriptionChangedAsync(JsonElement subscription, CancellationToken ct)
        {
            string? subscriptionId = ReadString(subscription, "id");
            string? customerId = ReadString(subscription, "customer");
            string? userId = ReadString(subscription, "metadata", "userId");

            UserEntitlement? entitlement = null;
            if (!string.IsNullOrWhiteSpace(userId))
            {
                entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            }
            else
            {
                entitlement = await ResolveEntitlementAsync(customerId, subscriptionId, ct);
                userId = entitlement?.UserId;
            }

            if (entitlement is null)
            {
                return WebhookHandleResult.Skipped();
            }

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                entitlement.StripeCustomerId = customerId;
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                entitlement.StripeSubscriptionId = subscriptionId;
            }

            string? priceId = ReadString(subscription, "items", "data", 0, "price", "id");
            if (!string.IsNullOrWhiteSpace(priceId))
            {
                entitlement.StripePriceId = priceId;
            }

            entitlement.CancelAtPeriodEnd = ReadBool(subscription, "cancel_at_period_end") ?? false;
            entitlement.CurrentPeriodEndUtc = ReadUnixTimestamp(subscription, "current_period_end");
            entitlement.SubscriptionStatus = NormalizeStripeStatus(ReadString(subscription, "status"));
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            return WebhookHandleResult.Processed(userId);
        }

        private async Task<WebhookHandleResult> HandleInvoicePaidAsync(JsonElement invoice, CancellationToken ct)
        {
            string? customerId = ReadString(invoice, "customer");
            string? subscriptionId = ReadString(invoice, "subscription");
            UserEntitlement? entitlement = await ResolveEntitlementAsync(customerId, subscriptionId, ct);
            if (entitlement is null)
            {
                return WebhookHandleResult.Skipped();
            }

            entitlement.SubscriptionStatus = UserEntitlementDefaults.ActiveSubscriptionStatus;
            entitlement.CancelAtPeriodEnd = false;
            entitlement.CurrentPeriodEndUtc =
                ReadUnixTimestamp(invoice, "lines", "data", 0, "period", "end")
                ?? entitlement.CurrentPeriodEndUtc;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return WebhookHandleResult.Processed(entitlement.UserId);
        }

        private async Task<WebhookHandleResult> HandleInvoicePaymentFailedAsync(JsonElement invoice, CancellationToken ct)
        {
            string? customerId = ReadString(invoice, "customer");
            string? subscriptionId = ReadString(invoice, "subscription");
            UserEntitlement? entitlement = await ResolveEntitlementAsync(customerId, subscriptionId, ct);
            if (entitlement is null)
            {
                return WebhookHandleResult.Skipped();
            }

            entitlement.SubscriptionStatus = "PaymentFailed";
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return WebhookHandleResult.Processed(entitlement.UserId);
        }

        private async Task<UserEntitlement?> ResolveEntitlementAsync(string? customerId, string? subscriptionId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                UserEntitlement? bySubscription = await _dbContext.UserEntitlements
                    .FirstOrDefaultAsync(item => item.StripeSubscriptionId == subscriptionId, ct);
                if (bySubscription is not null)
                {
                    return bySubscription;
                }
            }

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                return await _dbContext.UserEntitlements
                    .FirstOrDefaultAsync(item => item.StripeCustomerId == customerId, ct);
            }

            return null;
        }

        private static bool IsStripeSignatureValid(string header, string payload, string webhookSecret)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return false;
            }

            Dictionary<string, List<string>> parts = ParseSignatureHeader(header);
            if (!parts.TryGetValue("t", out List<string>? timestampValues)
                || timestampValues.Count == 0
                || !long.TryParse(timestampValues[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestampUnix))
            {
                return false;
            }

            if (!parts.TryGetValue("v1", out List<string>? signatures) || signatures.Count == 0)
            {
                return false;
            }

            DateTimeOffset timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampUnix);
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (timestamp < now - SignatureTolerance || timestamp > now + SignatureTolerance)
            {
                return false;
            }

            string signedPayload = $"{timestampUnix}.{payload}";
            byte[] keyBytes = Encoding.UTF8.GetBytes(webhookSecret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(signedPayload);
            using HMACSHA256 hmac = new(keyBytes);
            byte[] hash = hmac.ComputeHash(payloadBytes);
            string computedHex = ConvertToHex(hash);

            foreach (string signature in signatures)
            {
                if (FixedTimeEqualsHex(computedHex, signature))
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<string, List<string>> ParseSignatureHeader(string header)
        {
            Dictionary<string, List<string>> parts = new(StringComparer.OrdinalIgnoreCase);
            foreach (string rawPart in header.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string part = rawPart.Trim();
                int separator = part.IndexOf('=');
                if (separator <= 0 || separator >= part.Length - 1)
                {
                    continue;
                }

                string key = part[..separator].Trim();
                string value = part[(separator + 1)..].Trim();
                if (!parts.TryGetValue(key, out List<string>? values))
                {
                    values = new List<string>();
                    parts[key] = values;
                }

                values.Add(value);
            }

            return parts;
        }

        private static string ConvertToHex(byte[] bytes)
        {
            StringBuilder builder = new(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static bool FixedTimeEqualsHex(string leftHex, string rightHex)
        {
            if (leftHex.Length != rightHex.Length)
            {
                return false;
            }

            byte[] left = Encoding.ASCII.GetBytes(leftHex.ToLowerInvariant());
            byte[] right = Encoding.ASCII.GetBytes(rightHex.ToLowerInvariant());
            return CryptographicOperations.FixedTimeEquals(left, right);
        }

        private static JsonElement ReadDataObject(JsonElement root)
        {
            if (!root.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Object
                || !data.TryGetProperty("object", out JsonElement obj))
            {
                return default;
            }

            return obj;
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

            if (target.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (target.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            return null;
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

        private string? NormalizePlanToPriceId(string planKey)
        {
            if (string.Equals(planKey, "standard", StringComparison.OrdinalIgnoreCase))
            {
                return _stripeOptions.PriceStandard;
            }

            if (string.Equals(planKey, "pro", StringComparison.OrdinalIgnoreCase)
                || string.Equals(planKey, "professional", StringComparison.OrdinalIgnoreCase))
            {
                return _stripeOptions.PricePro;
            }

            return null;
        }

        private static string NormalizeStripeStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return UserEntitlementDefaults.ActiveSubscriptionStatus;
            }

            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                return UserEntitlementDefaults.ActiveSubscriptionStatus;
            }

            if (string.Equals(status, "past_due", StringComparison.OrdinalIgnoreCase))
            {
                return "PastDue";
            }

            if (string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase))
            {
                return "Canceled";
            }

            if (string.Equals(status, "unpaid", StringComparison.OrdinalIgnoreCase))
            {
                return "Unpaid";
            }

            return status.Trim();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }

        private sealed record WebhookHandleResult(string Status, string? UserId)
        {
            public static WebhookHandleResult Processed(string? userId) => new(StatusProcessed, userId);
            public static WebhookHandleResult Skipped() => new(StatusSkipped, null);
        }
    }
}
