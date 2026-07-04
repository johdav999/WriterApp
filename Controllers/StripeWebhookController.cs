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
using WriterApp.Shared.Billing;

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
        private const string StatusNoUser = "NoUser";
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
            if (!_stripeOptions.Enabled || !_stripeOptions.WebhookHandlingEnabled || string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Stripe webhook is not configured.");
            }

            string activeMode = StripeBillingEnvironment.Normalize(_stripeOptions.Mode);

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
                    "Stripe webhook received. EventId={EventId} EventType={EventType} StripeMode={StripeMode} StripeRequestId={StripeRequestId} CustomerId={CustomerId} SubscriptionId={SubscriptionId} CheckoutSessionId={CheckoutSessionId} PaymentIntentId={PaymentIntentId} UserId={UserId}",
                    eventId,
                    eventType,
                    activeMode,
                    stripeRequestId,
                    customerId ?? string.Empty,
                    subscriptionId ?? string.Empty,
                    ReadString(dataObject, "id") ?? string.Empty,
                    ReadString(dataObject, "payment_intent") ?? string.Empty,
                    userIdFromPayload ?? string.Empty);

                StripeEventLog eventLog = await GetOrCreateEventLogAsync(eventId, eventType, activeMode, ct);
                if (StripeBillingEnvironment.IsModeMismatch(eventLog.StripeMode, activeMode))
                {
                    _logger.LogWarning(
                        "Stripe webhook ignored due to stored event mode mismatch. EventId={EventId} EventType={EventType} StoredStripeMode={StoredStripeMode} ActiveStripeMode={ActiveStripeMode}",
                        eventId,
                        eventType,
                        eventLog.StripeMode,
                        activeMode);
                    return Ok();
                }

                if (string.Equals(eventLog.Status, StatusProcessed, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventLog.Status, StatusSkipped, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(eventLog.Status, StatusNoUser, StringComparison.OrdinalIgnoreCase))
                {
                    return Ok();
                }

                if (string.Equals(eventLog.Status, StatusProcessing, StringComparison.OrdinalIgnoreCase)
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

        private async Task<StripeEventLog> GetOrCreateEventLogAsync(string eventId, string eventType, string activeMode, CancellationToken ct)
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
                StripeMode = activeMode,
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
                    return WebhookHandleResult.Skipped();
            }
        }

        private async Task<WebhookHandleResult> HandleCheckoutSessionCompletedAsync(JsonElement session, CancellationToken ct)
        {
            string? userId = ReadString(session, "client_reference_id")
                ?? ReadString(session, "metadata", "userId");
            string? customerId = ReadString(session, "customer");
            string? subscriptionId = ReadString(session, "subscription");
            string? sessionId = ReadString(session, "id");
            if (string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(subscriptionId))
            {
                (userId, _) = await ResolveSubscriptionUserIdAsync(
                    sessionId ?? "checkout.session.completed",
                    default,
                    customerId,
                    subscriptionId,
                    ct);
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning(
                    "Stripe checkout.session.completed missing durable user mapping. CheckoutSessionId={CheckoutSessionId} CustomerId={CustomerId} SubscriptionId={SubscriptionId}",
                    sessionId ?? string.Empty,
                    customerId ?? string.Empty,
                    subscriptionId ?? string.Empty);
                return WebhookHandleResult.NoUser("checkout.session.completed did not include a resolvable user id.");
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                using JsonDocument subscription = await _stripeApiClient.GetSubscriptionAsync(
                    _stripeOptions.SecretKey,
                    subscriptionId,
                    ct);
                UserEntitlement updated = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(userId, customerId, subscription.RootElement, ct);
                _logger.LogInformation(
                    "Stripe checkout.session.completed synced entitlement. CheckoutSessionId={CheckoutSessionId} UserId={UserId} PlanKey={PlanKey} SubscriptionStatus={SubscriptionStatus} StripeCustomerId={StripeCustomerId} StripeSubscriptionId={StripeSubscriptionId} StripePriceId={StripePriceId}",
                    sessionId ?? string.Empty,
                    updated.UserId,
                    updated.PlanKey ?? string.Empty,
                    updated.SubscriptionStatus ?? string.Empty,
                    updated.StripeCustomerId ?? string.Empty,
                    updated.StripeSubscriptionId ?? string.Empty,
                    updated.StripePriceId ?? string.Empty);
                return WebhookHandleResult.Processed(userId);
            }

            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            entitlement.StripeMode = StripeBillingEnvironment.Normalize(_stripeOptions.Mode);
            entitlement.StripeCustomerId = customerId ?? entitlement.StripeCustomerId;
            entitlement.StripeSubscriptionId = subscriptionId ?? entitlement.StripeSubscriptionId;
            entitlement.SubscriptionStatus = "active";
            entitlement.CancelAtPeriodEnd = false;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Stripe checkout.session.completed applied fallback entitlement update. CheckoutSessionId={CheckoutSessionId} UserId={UserId} StripeCustomerId={StripeCustomerId}",
                sessionId ?? string.Empty,
                entitlement.UserId,
                entitlement.StripeCustomerId ?? string.Empty);
            return WebhookHandleResult.Processed(userId);
        }

        private async Task<WebhookHandleResult> HandleSubscriptionChangedAsync(string eventId, JsonElement subscription, CancellationToken ct)
        {
            string? subscriptionId = ReadString(subscription, "id");
            string? customerId = ReadString(subscription, "customer");
            (string? userId, string resolutionSource) = await ResolveSubscriptionUserIdAsync(
                eventId,
                subscription,
                customerId,
                subscriptionId,
                ct);

            _logger.LogInformation(
                "Webhook user resolution: EventId={EventId} CustomerId={CustomerId} SubscriptionId={SubscriptionId} ResolvedUserId={ResolvedUserId} Source={Source}",
                eventId,
                customerId ?? string.Empty,
                subscriptionId ?? string.Empty,
                userId ?? string.Empty,
                resolutionSource);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _logger.LogWarning(
                    "Stripe webhook could not resolve user for subscription event. EventId={EventId} CustomerId={CustomerId} SubscriptionId={SubscriptionId}",
                    eventId,
                    customerId ?? string.Empty,
                    subscriptionId ?? string.Empty);
                return WebhookHandleResult.NoUser(
                    $"No user mapping found for subscription event. customerId={customerId ?? string.Empty} subscriptionId={subscriptionId ?? string.Empty}");
            }

            UserEntitlement updated = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(userId!, customerId, subscription, ct);
            _logger.LogInformation(
                "Stripe subscription webhook synced entitlement. EventId={EventId} UserId={UserId} PlanKey={PlanKey} SubscriptionStatus={SubscriptionStatus} StripeCustomerId={StripeCustomerId} StripeSubscriptionId={StripeSubscriptionId} StripePriceId={StripePriceId}",
                eventId,
                updated.UserId,
                updated.PlanKey ?? string.Empty,
                updated.SubscriptionStatus ?? string.Empty,
                updated.StripeCustomerId ?? string.Empty,
                updated.StripeSubscriptionId ?? string.Empty,
                updated.StripePriceId ?? string.Empty);
            return WebhookHandleResult.Processed(userId);
        }

        private async Task<(string? UserId, string Source)> ResolveSubscriptionUserIdAsync(
            string eventId,
            JsonElement subscription,
            string? customerId,
            string? subscriptionId,
            CancellationToken ct)
        {
            string? userId = ReadString(subscription, "metadata", "userId")
                ?? ReadString(subscription, "client_reference_id");
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return (userId, "Metadata");
            }

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                try
                {
                    using JsonDocument customer = await _stripeApiClient.GetCustomerAsync(
                        _stripeOptions.SecretKey,
                        customerId,
                        ct);
                    userId = ReadString(customer.RootElement, "metadata", "userId");
                    if (!string.IsNullOrWhiteSpace(userId))
                    {
                        return (userId, "CustomerMetadata");
                    }
                }
                catch (StripeApiException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Stripe webhook customer metadata lookup failed. EventId={EventId} CustomerId={CustomerId}",
                        eventId,
                        customerId);
                }
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                UserEntitlement? bySubscription = await _dbContext.UserEntitlements
                    .AsNoTracking()
                    .Where(item => item.StripeSubscriptionId == subscriptionId)
                    .FirstOrDefaultAsync(ct);
                if (TryGetModeCompatibleUserId(bySubscription, "subscription", subscriptionId, out string? bySubscriptionUserId))
                {
                    return (bySubscriptionUserId, "DbSubscription");
                }
            }

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                UserEntitlement? byCustomer = await _dbContext.UserEntitlements
                    .AsNoTracking()
                    .Where(item => item.StripeCustomerId == customerId)
                    .FirstOrDefaultAsync(ct);
                if (TryGetModeCompatibleUserId(byCustomer, "customer", customerId, out string? byCustomerUserId))
                {
                    return (byCustomerUserId, "DbCustomer");
                }
            }

            return (null, "None");
        }

        private async Task<WebhookHandleResult> HandleInvoicePaidAsync(JsonElement invoice, CancellationToken ct)
        {
            string? customerId = ReadString(invoice, "customer");
            string? subscriptionId = ReadString(invoice, "subscription");
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                using JsonDocument subscription = await _stripeApiClient.GetSubscriptionAsync(
                    _stripeOptions.SecretKey,
                    subscriptionId,
                    ct);
                (string? resolvedUserId, string resolutionSource) = await ResolveSubscriptionUserIdAsync(
                    "invoice.paid",
                    subscription.RootElement,
                    customerId,
                    subscriptionId,
                    ct);
                if (!string.IsNullOrWhiteSpace(resolvedUserId))
                {
                    UserEntitlement updated = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                        resolvedUserId!,
                        customerId,
                        subscription.RootElement,
                        ct);
                    _logger.LogInformation(
                        "Stripe invoice.paid synced entitlement from subscription. UserId={UserId} ResolutionSource={ResolutionSource} PlanKey={PlanKey} SubscriptionStatus={SubscriptionStatus} StripeCustomerId={StripeCustomerId} StripeSubscriptionId={StripeSubscriptionId} StripePriceId={StripePriceId}",
                        updated.UserId,
                        resolutionSource,
                        updated.PlanKey ?? string.Empty,
                        updated.SubscriptionStatus ?? string.Empty,
                        updated.StripeCustomerId ?? string.Empty,
                        updated.StripeSubscriptionId ?? string.Empty,
                        updated.StripePriceId ?? string.Empty);
                    return WebhookHandleResult.Processed(updated.UserId);
                }
            }

            UserEntitlement? entitlement = await ResolveEntitlementAsync(customerId, subscriptionId, ct);
            if (entitlement is null)
            {
                _logger.LogWarning(
                    "Stripe invoice.paid could not resolve entitlement. CustomerId={CustomerId} SubscriptionId={SubscriptionId}",
                    customerId ?? string.Empty,
                    subscriptionId ?? string.Empty);
                return WebhookHandleResult.NoUser(
                    $"invoice.paid could not resolve entitlement. customerId={customerId ?? string.Empty} subscriptionId={subscriptionId ?? string.Empty}");
            }

            entitlement.SubscriptionStatus = "active";
            entitlement.StripeMode = StripeBillingEnvironment.Normalize(_stripeOptions.Mode);
            entitlement.CancelAtPeriodEnd = false;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Stripe invoice.paid fallback applied entitlement status. UserId={UserId} StripeMode={StripeMode} Status={Status} PaidAccessPolicy={PolicyCode}",
                entitlement.UserId,
                StripeBillingEnvironment.Normalize(_stripeOptions.Mode),
                entitlement.SubscriptionStatus,
                BillingSubscriptionPolicy.Evaluate(entitlement.SubscriptionStatus).PolicyCode);
            return WebhookHandleResult.Processed(entitlement.UserId);
        }

        private async Task<WebhookHandleResult> HandleInvoicePaymentFailedAsync(JsonElement invoice, CancellationToken ct)
        {
            string? customerId = ReadString(invoice, "customer");
            string? subscriptionId = ReadString(invoice, "subscription");
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                using JsonDocument subscription = await _stripeApiClient.GetSubscriptionAsync(
                    _stripeOptions.SecretKey,
                    subscriptionId,
                    ct);
                (string? resolvedUserId, string resolutionSource) = await ResolveSubscriptionUserIdAsync(
                    "invoice.payment_failed",
                    subscription.RootElement,
                    customerId,
                    subscriptionId,
                    ct);
                if (!string.IsNullOrWhiteSpace(resolvedUserId))
                {
                    UserEntitlement updated = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                        resolvedUserId!,
                        customerId,
                        subscription.RootElement,
                        ct);
                    _logger.LogInformation(
                        "Stripe invoice.payment_failed synced entitlement from subscription. UserId={UserId} ResolutionSource={ResolutionSource} PlanKey={PlanKey} SubscriptionStatus={SubscriptionStatus} StripeCustomerId={StripeCustomerId} StripeSubscriptionId={StripeSubscriptionId} StripePriceId={StripePriceId}",
                        updated.UserId,
                        resolutionSource,
                        updated.PlanKey ?? string.Empty,
                        updated.SubscriptionStatus ?? string.Empty,
                        updated.StripeCustomerId ?? string.Empty,
                        updated.StripeSubscriptionId ?? string.Empty,
                        updated.StripePriceId ?? string.Empty);
                    return WebhookHandleResult.Processed(updated.UserId);
                }
            }

            UserEntitlement? entitlement = await ResolveEntitlementAsync(customerId, subscriptionId, ct);
            if (entitlement is null)
            {
                _logger.LogWarning(
                    "Stripe invoice.payment_failed could not resolve entitlement. CustomerId={CustomerId} SubscriptionId={SubscriptionId}",
                    customerId ?? string.Empty,
                    subscriptionId ?? string.Empty);
                return WebhookHandleResult.NoUser(
                    $"invoice.payment_failed could not resolve entitlement. customerId={customerId ?? string.Empty} subscriptionId={subscriptionId ?? string.Empty}");
            }

            entitlement.SubscriptionStatus = "past_due";
            entitlement.StripeMode = StripeBillingEnvironment.Normalize(_stripeOptions.Mode);
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Stripe invoice.payment_failed fallback applied entitlement status. UserId={UserId} StripeMode={StripeMode} Status={Status} PaidAccessPolicy={PolicyCode}",
                entitlement.UserId,
                StripeBillingEnvironment.Normalize(_stripeOptions.Mode),
                entitlement.SubscriptionStatus,
                BillingSubscriptionPolicy.Evaluate(entitlement.SubscriptionStatus).PolicyCode);
            return WebhookHandleResult.Processed(entitlement.UserId);
        }

        private async Task<UserEntitlement?> ResolveEntitlementAsync(string? customerId, string? subscriptionId, CancellationToken ct)
        {
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                UserEntitlement? bySubscription = await _dbContext.UserEntitlements
                    .FirstOrDefaultAsync(item => item.StripeSubscriptionId == subscriptionId, ct);
                if (TryGetModeCompatibleEntitlement(bySubscription, "subscription", subscriptionId, out UserEntitlement? matchedBySubscription))
                {
                    return matchedBySubscription;
                }
            }

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                UserEntitlement? byCustomer = await _dbContext.UserEntitlements
                    .FirstOrDefaultAsync(item => item.StripeCustomerId == customerId, ct);
                if (TryGetModeCompatibleEntitlement(byCustomer, "customer", customerId, out UserEntitlement? matchedByCustomer))
                {
                    return matchedByCustomer;
                }
            }

            return null;
        }

        private bool TryGetModeCompatibleUserId(
            UserEntitlement? entitlement,
            string identifierKind,
            string identifierValue,
            out string? userId)
        {
            userId = null;
            if (!TryGetModeCompatibleEntitlement(entitlement, identifierKind, identifierValue, out UserEntitlement? matchedEntitlement))
            {
                return false;
            }

            userId = matchedEntitlement!.UserId;
            return true;
        }

        private bool TryGetModeCompatibleEntitlement(
            UserEntitlement? entitlement,
            string identifierKind,
            string identifierValue,
            out UserEntitlement? matchedEntitlement)
        {
            matchedEntitlement = null;
            if (entitlement is null)
            {
                return false;
            }

            string activeMode = StripeBillingEnvironment.Normalize(_stripeOptions.Mode);
            string storedMode = StripeBillingEnvironment.ResolveStoredMode(entitlement, _stripeOptions);
            if (StripeBillingEnvironment.IsModeMismatch(storedMode, activeMode))
            {
                _logger.LogWarning(
                    "Stripe webhook ignored opposite-mode entitlement linkage. IdentifierKind={IdentifierKind} IdentifierValue={IdentifierValue} UserId={UserId} StoredStripeMode={StoredStripeMode} ActiveStripeMode={ActiveStripeMode}",
                    identifierKind,
                    identifierValue,
                    entitlement.UserId,
                    storedMode,
                    activeMode);
                return false;
            }

            matchedEntitlement = entitlement;
            return true;
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

        private sealed record WebhookHandleResult(string Status, string? UserId, string? Error)
        {
            public static WebhookHandleResult Processed(string? userId) => new(StatusProcessed, userId, null);
            public static WebhookHandleResult Skipped() => new(StatusSkipped, null, null);
            public static WebhookHandleResult NoUser(string? error) => new(StatusNoUser, null, error);
        }
    }
}
