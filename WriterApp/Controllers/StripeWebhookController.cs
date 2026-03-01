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

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/stripe")]
    [AllowAnonymous]
    public sealed class StripeWebhookController : ControllerBase
    {
        private const string StatusProcessedSuccessfully = "ProcessedSuccessfully";
        private const string StatusProcessedLegacy = "Processed";
        private const string StatusSkipped = "Skipped";
        private const string StatusError = "Error";
        private const string StatusProcessing = "Processing";
        private static readonly TimeSpan SignatureTolerance = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan ProcessingLease = TimeSpan.FromMinutes(10);

        private readonly AppDbContext _dbContext;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly StripeEntitlementSyncService _stripeEntitlementSyncService;
        private readonly StripeApiClient _stripeApiClient;
        private readonly StripeOptions _stripeOptions;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            AppDbContext dbContext,
            IUserEntitlementStore userEntitlementStore,
            StripeEntitlementSyncService stripeEntitlementSyncService,
            StripeApiClient stripeApiClient,
            StripeOptions stripeOptions,
            ILogger<StripeWebhookController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _stripeEntitlementSyncService = stripeEntitlementSyncService ?? throw new ArgumentNullException(nameof(stripeEntitlementSyncService));
            _stripeApiClient = stripeApiClient ?? throw new ArgumentNullException(nameof(stripeApiClient));
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
            string stripeRequestId = GetStripeRequestId();
            if (!IsStripeSignatureValid(signatureHeader, payload, _stripeOptions.WebhookSecret))
            {
                _logger.LogWarning(
                    "Stripe webhook signature verification failed. TraceId={TraceId} StripeRequestId={StripeRequestId}",
                    HttpContext.TraceIdentifier,
                    stripeRequestId);
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
                string? customerId = ReadString(dataObject, "customer");
                string? subscriptionId = ReadString(dataObject, "subscription")
                    ?? ReadString(dataObject, "id");
                string? userIdFromPayload = ReadString(dataObject, "client_reference_id")
                    ?? ReadString(dataObject, "metadata", "userId");

                if (string.IsNullOrWhiteSpace(eventId) || string.IsNullOrWhiteSpace(eventType))
                {
                    return BadRequest("Stripe event missing id or type.");
                }

                _logger.LogInformation(
                    "Stripe webhook received. EventId={EventId} EventType={EventType} StripeRequestId={StripeRequestId} CustomerId={CustomerId} SubscriptionId={SubscriptionId} UserId={UserId}",
                    eventId,
                    eventType,
                    stripeRequestId,
                    customerId ?? string.Empty,
                    subscriptionId ?? string.Empty,
                    userIdFromPayload ?? string.Empty);

                StripeEventLog eventLog = await GetOrCreateEventLogAsync(eventId, eventType, ct);
                bool force = ParseTruthy(Request.Query["force"].FirstOrDefault());
                if (!force
                    && (string.Equals(eventLog.Status, StatusProcessedSuccessfully, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(eventLog.Status, StatusProcessedLegacy, StringComparison.OrdinalIgnoreCase)))
                {
                    return Ok();
                }

                if (!force
                    && string.Equals(eventLog.Status, StatusProcessing, StringComparison.OrdinalIgnoreCase)
                    && eventLog.ProcessedUtc is null
                    && eventLog.ReceivedUtc >= DateTime.UtcNow - ProcessingLease)
                {
                    return Ok();
                }

                eventLog.Type = eventType;
                eventLog.ReceivedUtc = DateTime.UtcNow;
                eventLog.ProcessedUtc = null;
                eventLog.Status = StatusProcessing;
                eventLog.Error = null;
                await _dbContext.SaveChangesAsync(ct);

                try
                {
                    WebhookHandleResult handleResult = await DispatchAsync(eventId, eventType, dataObject, ct);

                    eventLog.UserId = handleResult.UserId ?? eventLog.UserId;
                    eventLog.Status = handleResult.Status;
                    eventLog.ProcessedUtc = DateTime.UtcNow;
                    eventLog.Error = handleResult.Error;
                    await _dbContext.SaveChangesAsync(ct);
                    _logger.LogInformation(
                        "Stripe webhook processed. EventId={EventId} EventType={EventType} Status={Status} StripeRequestId={StripeRequestId} UserId={UserId}",
                        eventId,
                        eventType,
                        handleResult.Status,
                        stripeRequestId,
                        handleResult.UserId ?? string.Empty);
                    return Ok();
                }
                catch (Exception ex)
                {
                    eventLog.Status = StatusError;
                    eventLog.ProcessedUtc = DateTime.UtcNow;
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
                ReceivedUtc = DateTime.UtcNow,
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

        private async Task<WebhookHandleResult> DispatchAsync(string eventId, string eventType, JsonElement dataObject, CancellationToken ct)
        {
            switch (eventType)
            {
                case "checkout.session.completed":
                    return await HandleCheckoutSessionCompletedAsync(dataObject, ct);
                case "customer.subscription.created":
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                    return await HandleSubscriptionChangedAsync(eventId, dataObject, ct);
                case "invoice.paid":
                    return await HandleInvoicePaidAsync(dataObject, ct);
                case "invoice.payment_failed":
                    return await HandleInvoicePaymentFailedAsync(dataObject, ct);
                default:
                    return WebhookHandleResult.Skipped($"Unsupported event type '{eventType}'.");
            }
        }

        private async Task<WebhookHandleResult> HandleCheckoutSessionCompletedAsync(JsonElement session, CancellationToken ct)
        {
            string? userId = ReadString(session, "client_reference_id")
                ?? ReadString(session, "metadata", "userId");
            if (string.IsNullOrWhiteSpace(userId))
            {
                return WebhookHandleResult.Skipped("Checkout session did not contain a user id.");
            }

            string? customerId = ReadString(session, "customer");
            string? subscriptionId = ReadString(session, "subscription");
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                using JsonDocument subscription = await _stripeApiClient.GetSubscriptionAsync(
                    _stripeOptions.SecretKey,
                    subscriptionId,
                    ct);
                await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(userId, customerId, subscription.RootElement, ct);
                return WebhookHandleResult.ProcessedSuccessfully(userId);
            }

            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            entitlement.StripeCustomerId = customerId ?? entitlement.StripeCustomerId;
            entitlement.StripeSubscriptionId = subscriptionId ?? entitlement.StripeSubscriptionId;
            entitlement.SubscriptionStatus = "active";
            entitlement.CancelAtPeriodEnd = false;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return WebhookHandleResult.ProcessedSuccessfully(userId);
        }

        private async Task<WebhookHandleResult> HandleSubscriptionChangedAsync(string eventId, JsonElement subscriptionEventObject, CancellationToken ct)
        {
            string? subscriptionId = ReadString(subscriptionEventObject, "id");
            string? customerId = ReadString(subscriptionEventObject, "customer");
            (string? userId, string source) = await ResolveSubscriptionUserIdAsync(
                subscriptionEventObject,
                customerId,
                subscriptionId,
                ct);

            _logger.LogInformation(
                "Webhook user resolution: EventId={EventId} CustomerId={CustomerId} SubscriptionId={SubscriptionId} ResolvedUserId={ResolvedUserId} Source={Source}",
                eventId,
                customerId ?? string.Empty,
                subscriptionId ?? string.Empty,
                userId ?? string.Empty,
                source);

            if (string.IsNullOrWhiteSpace(userId))
            {
                string reason = $"User unresolved for subscription event. CustomerId={customerId ?? string.Empty}, SubscriptionId={subscriptionId ?? string.Empty}.";
                _logger.LogWarning(
                    "Stripe webhook skipped because user could not be resolved. EventId={EventId} CustomerId={CustomerId} SubscriptionId={SubscriptionId}",
                    eventId,
                    customerId ?? string.Empty,
                    subscriptionId ?? string.Empty);
                return WebhookHandleResult.Skipped(reason);
            }

            JsonElement subscriptionForSync = subscriptionEventObject;
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                try
                {
                    using JsonDocument latestSubscription = await _stripeApiClient.GetSubscriptionAsync(
                        _stripeOptions.SecretKey,
                        subscriptionId,
                        ct);
                    subscriptionForSync = latestSubscription.RootElement.Clone();
                }
                catch (StripeApiException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Stripe subscription fetch failed for webhook; falling back to event payload. EventId={EventId} SubscriptionId={SubscriptionId}",
                        eventId,
                        subscriptionId);
                }
            }

            await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(userId, customerId, subscriptionForSync, ct);
            return WebhookHandleResult.ProcessedSuccessfully(userId);
        }

        private async Task<(string? UserId, string Source)> ResolveSubscriptionUserIdAsync(
            JsonElement subscriptionEventObject,
            string? customerId,
            string? subscriptionId,
            CancellationToken ct)
        {
            string? userIdFromMetadata = ReadString(subscriptionEventObject, "metadata", "userId");
            if (!string.IsNullOrWhiteSpace(userIdFromMetadata))
            {
                return (userIdFromMetadata, "Metadata");
            }

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                try
                {
                    using JsonDocument customer = await _stripeApiClient.GetCustomerAsync(_stripeOptions.SecretKey, customerId, ct);
                    string? userIdFromCustomerMetadata = ReadString(customer.RootElement, "metadata", "userId");
                    if (!string.IsNullOrWhiteSpace(userIdFromCustomerMetadata))
                    {
                        return (userIdFromCustomerMetadata, "CustomerMetadata");
                    }
                }
                catch (StripeApiException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Stripe customer metadata lookup failed during webhook user resolution. CustomerId={CustomerId}",
                        customerId);
                }
            }

            string? userIdFromCheckoutPayload = ReadString(subscriptionEventObject, "client_reference_id")
                ?? ReadString(subscriptionEventObject, "checkout_session", "client_reference_id")
                ?? ReadString(subscriptionEventObject, "checkout_session", "metadata", "userId");
            if (!string.IsNullOrWhiteSpace(userIdFromCheckoutPayload))
            {
                return (userIdFromCheckoutPayload, "CheckoutPayload");
            }

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                string? userIdFromCustomer = await _dbContext.UserEntitlements
                    .AsNoTracking()
                    .Where(item => item.StripeCustomerId == customerId)
                    .Select(item => item.UserId)
                    .FirstOrDefaultAsync(ct);
                if (!string.IsNullOrWhiteSpace(userIdFromCustomer))
                {
                    return (userIdFromCustomer, "DbCustomer");
                }
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                string? userIdFromSubscription = await _dbContext.UserEntitlements
                    .AsNoTracking()
                    .Where(item => item.StripeSubscriptionId == subscriptionId)
                    .Select(item => item.UserId)
                    .FirstOrDefaultAsync(ct);
                if (!string.IsNullOrWhiteSpace(userIdFromSubscription))
                {
                    return (userIdFromSubscription, "DbSubscription");
                }
            }

            return (null, "None");
        }

        private async Task<WebhookHandleResult> HandleInvoicePaidAsync(JsonElement invoice, CancellationToken ct)
        {
            string? customerId = ReadString(invoice, "customer");
            string? subscriptionId = ReadString(invoice, "subscription");
            UserEntitlement? entitlement = await ResolveEntitlementAsync(customerId, subscriptionId, ct);
            if (entitlement is null)
            {
                return WebhookHandleResult.Skipped("Invoice paid event could not be mapped to a user entitlement.");
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                using JsonDocument subscription = await _stripeApiClient.GetSubscriptionAsync(
                    _stripeOptions.SecretKey,
                    subscriptionId,
                    ct);
                await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                    entitlement.UserId,
                    customerId,
                    subscription.RootElement,
                    ct);
                return WebhookHandleResult.ProcessedSuccessfully(entitlement.UserId);
            }

            entitlement.SubscriptionStatus = "active";
            entitlement.CancelAtPeriodEnd = false;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return WebhookHandleResult.ProcessedSuccessfully(entitlement.UserId);
        }

        private async Task<WebhookHandleResult> HandleInvoicePaymentFailedAsync(JsonElement invoice, CancellationToken ct)
        {
            string? customerId = ReadString(invoice, "customer");
            string? subscriptionId = ReadString(invoice, "subscription");
            UserEntitlement? entitlement = await ResolveEntitlementAsync(customerId, subscriptionId, ct);
            if (entitlement is null)
            {
                return WebhookHandleResult.Skipped("Invoice payment failed event could not be mapped to a user entitlement.");
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                using JsonDocument subscription = await _stripeApiClient.GetSubscriptionAsync(
                    _stripeOptions.SecretKey,
                    subscriptionId,
                    ct);
                await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                    entitlement.UserId,
                    customerId,
                    subscription.RootElement,
                    ct);
                return WebhookHandleResult.ProcessedSuccessfully(entitlement.UserId);
            }

            entitlement.SubscriptionStatus = "past_due";
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return WebhookHandleResult.ProcessedSuccessfully(entitlement.UserId);
        }

        [HttpPost("admin/sync-entitlements")]
        [Authorize]
        public async Task<IActionResult> AdminSyncEntitlements([FromQuery] string? userId, CancellationToken ct)
        {
            string enabled = Environment.GetEnvironmentVariable("ENABLE_STRIPE_ADMIN_SYNC") ?? string.Empty;
            if (!string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return BadRequest("userId is required.");
            }

            if (!_stripeOptions.Enabled || string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Stripe is not configured.");
            }

            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId.Trim(), ct);
            if (string.IsNullOrWhiteSpace(entitlement.StripeCustomerId))
            {
                return NotFound("No StripeCustomerId is linked to the specified user.");
            }

            using JsonDocument subscriptions = await _stripeApiClient.ListSubscriptionsByCustomerAsync(
                _stripeOptions.SecretKey,
                entitlement.StripeCustomerId,
                ct);

            if (!subscriptions.RootElement.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() == 0)
            {
                return NotFound("No Stripe subscriptions found for the user customer id.");
            }

            JsonElement subscription = data[0];
            if (data.GetArrayLength() > 1)
            {
                JsonElement? active = data.EnumerateArray().FirstOrDefault(item =>
                    string.Equals(ReadString(item, "status"), "active", StringComparison.OrdinalIgnoreCase));
                if (active.HasValue)
                {
                    subscription = active.Value;
                }
            }

            UserEntitlement updated = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                userId.Trim(),
                entitlement.StripeCustomerId,
                subscription,
                ct);

            return Ok(new
            {
                updated.UserId,
                updated.PlanKey,
                updated.SubscriptionStatus,
                updated.StripeCustomerId,
                updated.StripeSubscriptionId,
                updated.StripePriceId,
                updated.CurrentPeriodEndUtc,
                updated.CancelAtPeriodEnd,
                updated.UpdatedUtc
            });
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

        private string GetStripeRequestId()
        {
            string fromStripeHeader = Request.Headers["Stripe-Request-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(fromStripeHeader))
            {
                return fromStripeHeader;
            }

            string fromRequestIdHeader = Request.Headers["Request-Id"].ToString();
            if (!string.IsNullOrWhiteSpace(fromRequestIdHeader))
            {
                return fromRequestIdHeader;
            }

            return HttpContext.TraceIdentifier;
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

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }

        private static bool ParseTruthy(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string value = raw.Trim();
            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private sealed record WebhookHandleResult(string Status, string? UserId, string? Error)
        {
            public static WebhookHandleResult ProcessedSuccessfully(string? userId) => new(StatusProcessedSuccessfully, userId, null);
            public static WebhookHandleResult Skipped(string reason) => new(StatusSkipped, null, Truncate(reason, 2000));
        }
    }
}
