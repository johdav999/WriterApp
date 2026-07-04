using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using WriterApp.Application.Billing;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared;
using WriterApp.Shared.Billing;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("api/billing")]
    [Authorize]
    public sealed class BillingController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IUserIdResolver _userIdResolver;
        private readonly IUserEntitlementStore _userEntitlementStore;
        private readonly StripeOptions _stripeOptions;
        private readonly IStripePriceResolver _stripePriceResolver;
        private readonly IStripeClientFacade _stripeClientFacade;
        private readonly StripeEntitlementSyncService _stripeEntitlementSyncService;
        private readonly StripeApiClient _stripeApiClient;
        private readonly StripeRedirectUrlBuilder _stripeRedirectUrlBuilder;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IUserEntitlementStore userEntitlementStore,
            StripeOptions stripeOptions,
            IStripePriceResolver stripePriceResolver,
            IStripeClientFacade stripeClientFacade,
            StripeEntitlementSyncService stripeEntitlementSyncService,
            StripeApiClient stripeApiClient,
            StripeRedirectUrlBuilder stripeRedirectUrlBuilder,
            ILogger<BillingController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _stripeOptions = stripeOptions ?? throw new ArgumentNullException(nameof(stripeOptions));
            _stripePriceResolver = stripePriceResolver ?? throw new ArgumentNullException(nameof(stripePriceResolver));
            _stripeClientFacade = stripeClientFacade ?? throw new ArgumentNullException(nameof(stripeClientFacade));
            _stripeEntitlementSyncService = stripeEntitlementSyncService ?? throw new ArgumentNullException(nameof(stripeEntitlementSyncService));
            _stripeApiClient = stripeApiClient ?? throw new ArgumentNullException(nameof(stripeApiClient));
            _stripeRedirectUrlBuilder = stripeRedirectUrlBuilder ?? throw new ArgumentNullException(nameof(stripeRedirectUrlBuilder));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("status")]
        public ActionResult<BillingStatusResponse> GetBillingStatus()
        {
            bool hasSecretKey = !string.IsNullOrWhiteSpace(_stripeOptions.SecretKey);
            bool hasWebhookSecret = !string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret);
            bool hasStandardPrice =
                !string.IsNullOrWhiteSpace(_stripeOptions.Prices.Standard.TestPriceId)
                || !string.IsNullOrWhiteSpace(_stripeOptions.Prices.Standard.LivePriceId);
            bool hasProPrice =
                !string.IsNullOrWhiteSpace(_stripeOptions.Prices.Pro.TestPriceId)
                || !string.IsNullOrWhiteSpace(_stripeOptions.Prices.Pro.LivePriceId);

            return Ok(new BillingStatusResponse(
                _stripeOptions.Mode,
                hasSecretKey,
                hasSecretKey && hasWebhookSecret,
                hasStandardPrice,
                hasProPrice));
        }

        [HttpPost("checkout-session")]
        [HttpPost("checkout")]
        public async Task<ActionResult<BillingUrlResponse>> CreateCheckoutSession(
            [FromBody] CreateBillingCheckoutRequest request,
            CancellationToken ct)
        {
            if (!_stripeOptions.Enabled || string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                _logger.LogWarning(
                    "Stripe checkout session request blocked because Stripe is not configured. Enabled={Enabled} Mode={Mode} SecretKeyPresent={SecretKeyPresent} WebhookSecretPresent={WebhookSecretPresent} CurrentStandardPriceConfigured={CurrentStandardPriceConfigured} CurrentProPriceConfigured={CurrentProPriceConfigured} LegacyBillingConfigFallbackUsed={LegacyBillingConfigFallbackUsed}",
                    _stripeOptions.Enabled,
                    _stripeOptions.Mode,
                    !string.IsNullOrWhiteSpace(_stripeOptions.SecretKey),
                    !string.IsNullOrWhiteSpace(_stripeOptions.WebhookSecret),
                    !string.IsNullOrWhiteSpace(_stripeOptions.CurrentStandardPriceId),
                    !string.IsNullOrWhiteSpace(_stripeOptions.CurrentProPriceId),
                    _stripeOptions.LegacyBillingConfigFallbackUsed);

                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Billing unavailable",
                    Detail = "Stripe checkout is disabled because Stripe is not configured."
                });
            }

            string normalizedPlanKey;
            string priceId;
            try
            {
                priceId = _stripePriceResolver.ResolvePriceId(request.PlanKey, out normalizedPlanKey);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid billing request",
                    Detail = ex.Message
                });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            string customerId = await EnsureStripeCustomerIdAsync(entitlement, userId, allowCreate: true, _stripeOptions.SecretKey, ct);

            StripeBaseUrlResolution baseUrlResolution = _stripeRedirectUrlBuilder.ResolveBaseUrlContext(Request);
            string baseUrl = baseUrlResolution.BaseUrl;
            string successUrl = AppendQueryParameter(
                BuildCheckoutUrl(baseUrl, _stripeOptions.Checkout.SuccessPath),
                "plan",
                normalizedPlanKey);
            string cancelUrl = BuildCheckoutUrl(baseUrl, _stripeOptions.Checkout.CancelPath);

            _logger.LogInformation(
                "Stripe checkout session request prepared. UserId={UserId} PlanKey={PlanKey} PriceId={PriceId} StripeMode={StripeMode} StripeCustomerId={StripeCustomerId} SuccessUrl={SuccessUrl} CancelUrl={CancelUrl} BaseUrl={BaseUrl} BaseUrlSource={BaseUrlSource} RequestHost={RequestHost} RequestScheme={RequestScheme} RequestPathBase={RequestPathBase} RequestHostLooksLikeAzureAppService={RequestHostLooksLikeAzureAppService}",
                userId,
                normalizedPlanKey,
                priceId,
                GetActiveStripeMode(),
                customerId,
                successUrl,
                cancelUrl,
                baseUrlResolution.BaseUrl,
                baseUrlResolution.Source,
                baseUrlResolution.RequestHost,
                baseUrlResolution.RequestScheme,
                baseUrlResolution.RequestPathBase,
                baseUrlResolution.RequestHostLooksLikeAzureAppService);

            string checkoutUrl;
            try
            {
                checkoutUrl = await _stripeApiClient.CreateCheckoutSessionAsync(
                    _stripeOptions.SecretKey,
                    customerId,
                    userId,
                    normalizedPlanKey,
                    priceId,
                    successUrl,
                    cancelUrl,
                    ct);
            }
            catch (StripeApiException ex)
            {
                if (ex.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Stripe returns 400 (often with error.param=success_url/cancel_url) when redirect URLs are invalid.
                    _logger.LogWarning(
                        ex,
                        "Stripe rejected checkout session request. UserId={UserId} PlanKey={PlanKey} ErrorParam={ErrorParam} RawBody={RawBody}",
                        userId,
                        normalizedPlanKey,
                        ex.ErrorParam ?? string.Empty,
                        ex.RawBody ?? string.Empty);

                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Billing error",
                        Detail = "Stripe checkout request rejected. Check redirect URL configuration."
                    });
                }

                _logger.LogWarning(
                    ex,
                    "Stripe checkout session creation failed. UserId={UserId} PlanKey={PlanKey} StatusCode={StatusCode} ErrorParam={ErrorParam} RawBody={RawBody}",
                    userId,
                    normalizedPlanKey,
                    (int)ex.StatusCode,
                    ex.ErrorParam ?? string.Empty,
                    ex.RawBody ?? string.Empty);

                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Status = StatusCodes.Status502BadGateway,
                    Title = "Billing error",
                    Detail = ex.Message
                });
            }

            entitlement.StripePriceId = priceId;
            entitlement.StripeMode = GetActiveStripeMode();
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return Ok(new BillingUrlResponse(checkoutUrl));
        }

        [HttpPost("portal")]
        public async Task<ActionResult<BillingUrlResponse>> CreatePortalSession(CancellationToken ct)
        {
            if (!_stripeOptions.Enabled)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Billing unavailable",
                    Detail = "Stripe billing is not configured for this environment."
                });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            string customerId;
            try
            {
                customerId = await EnsureStripeCustomerIdAsync(entitlement, userId, allowCreate: false, _stripeOptions.SecretKey, ct);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Billing portal unavailable",
                    Detail = ex.Message
                });
            }

            string portalReturnUrl = _stripeRedirectUrlBuilder.BuildAbsoluteUrl(Request, _stripeOptions.BillingPortalReturnUrl, "/app/account");
            _logger.LogInformation(
                "Stripe billing portal request prepared. UserId={UserId} StripeCustomerId={StripeCustomerId} StripeMode={StripeMode} PortalReturnUrl={PortalReturnUrl}",
                userId,
                customerId,
                GetActiveStripeMode(),
                portalReturnUrl);

            string portalUrl;
            try
            {
                portalUrl = await _stripeApiClient.CreateBillingPortalSessionAsync(
                    _stripeOptions.SecretKey,
                    customerId,
                    portalReturnUrl,
                    ct);
            }
            catch (StripeApiException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Stripe billing portal session creation failed. UserId={UserId} StatusCode={StatusCode}",
                    userId,
                    (int)ex.StatusCode);

                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Status = StatusCodes.Status502BadGateway,
                    Title = "Billing error",
                    Detail = ex.Message
                });
            }

            return Ok(new BillingUrlResponse(portalUrl));
        }

        // First-class cancel-to-free path. This schedules Stripe cancellation at period end,
        // keeps paid access active until the paid term ends, and relies on Stripe webhooks
        // for the final downgrade when the subscription actually ends.
        [HttpPost("cancel-subscription")]
        public async Task<ActionResult<SyncEntitlementsResponse>> CancelSubscription(CancellationToken ct)
        {
            if (!_stripeOptions.Enabled || string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Billing unavailable",
                    Detail = "Stripe billing is not configured for this environment."
                });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);

            try
            {
                (string? customerId, JsonDocument currentSubscription) = await ResolveManagedSubscriptionAsync(entitlement, userId, ct);
                using (currentSubscription)
                {
                    string normalizedStatus = NormalizeSubscriptionStatus(ReadString(currentSubscription.RootElement, "status"));
                    bool alreadyCanceling = ReadBool(currentSubscription.RootElement, "cancel_at_period_end") ?? false;
                    if (normalizedStatus is "canceled" or "incomplete_expired")
                    {
                        UserEntitlement syncedCanceled = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                            userId,
                            customerId,
                            currentSubscription.RootElement,
                            ct);
                        return Ok(BuildSyncEntitlementsResponse(syncedCanceled));
                    }

                    if (alreadyCanceling)
                    {
                        UserEntitlement syncedExisting = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                            userId,
                            customerId,
                            currentSubscription.RootElement,
                            ct);
                        _logger.LogInformation(
                            "Stripe subscription cancel-at-period-end already scheduled. UserId={UserId} StripeSubscriptionId={StripeSubscriptionId}",
                            userId,
                            syncedExisting.StripeSubscriptionId ?? string.Empty);
                        return Ok(BuildSyncEntitlementsResponse(syncedExisting));
                    }
                }

                string subscriptionId = entitlement.StripeSubscriptionId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(subscriptionId))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "No Stripe subscription",
                        Detail = "No Stripe subscription is linked to this account."
                    });
                }

                using JsonDocument updatedSubscription = await _stripeApiClient.UpdateSubscriptionCancelAtPeriodEndAsync(
                    _stripeOptions.SecretKey,
                    subscriptionId,
                    cancelAtPeriodEnd: true,
                    ct);

                UserEntitlement updated = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                    userId,
                    entitlement.StripeCustomerId,
                    updatedSubscription.RootElement,
                    ct);

                _logger.LogInformation(
                    "Stripe subscription scheduled to cancel at period end. UserId={UserId} StripeSubscriptionId={StripeSubscriptionId} CurrentPeriodEndUtc={CurrentPeriodEndUtc}",
                    userId,
                    updated.StripeSubscriptionId ?? string.Empty,
                    updated.CurrentPeriodEndUtc);

                return Ok(BuildSyncEntitlementsResponse(updated));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Cancellation unavailable",
                    Detail = ex.Message
                });
            }
            catch (StripeApiException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Stripe cancel-at-period-end request failed. UserId={UserId} StatusCode={StatusCode}",
                    userId,
                    (int)ex.StatusCode);

                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Status = StatusCodes.Status502BadGateway,
                    Title = "Billing error",
                    Detail = ex.Message
                });
            }
        }

        [HttpGet("upgrade-url")]
        public async Task<ActionResult<BillingUrlResponse>> GetUpgradeUrl(
            [FromQuery] string? plan,
            [FromQuery] string? feature,
            CancellationToken ct)
        {
            if (!TryNormalizeUpgradePlan(plan, out string normalizedPlanKey))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid plan",
                    Detail = "plan must be either 'standard' or 'pro'."
                });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            string correlationId = Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? HttpContext.TraceIdentifier;
            bool hasSubscription = !string.IsNullOrWhiteSpace(entitlement.StripeSubscriptionId)
                && !StripeBillingEnvironment.IsModeMismatch(ResolveStoredStripeMode(entitlement), GetActiveStripeMode());

            if (hasSubscription)
            {
                if (!_stripeOptions.Enabled)
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                    {
                        Status = StatusCodes.Status503ServiceUnavailable,
                        Title = "Billing unavailable",
                        Detail = "Stripe billing is not configured for this environment."
                    });
                }

                string customerId;
                try
                {
                    customerId = await EnsureStripeCustomerIdAsync(entitlement, userId, allowCreate: false, _stripeOptions.SecretKey, ct);
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Billing portal unavailable",
                        Detail = ex.Message
                    });
                }

                string portalReturnUrl = _stripeRedirectUrlBuilder.BuildAbsoluteUrl(Request, _stripeOptions.BillingPortalReturnUrl, "/app/account/billing");
                string portalUrl;
                try
                {
                    portalUrl = await _stripeApiClient.CreateBillingPortalSessionAsync(
                        _stripeOptions.SecretKey,
                        customerId,
                        portalReturnUrl,
                        ct);
                }
                catch (StripeApiException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Upgrade URL portal creation failed. UserId={UserId} PlanKey={PlanKey} CorrelationId={CorrelationId} Feature={Feature} StatusCode={StatusCode}",
                        userId,
                        normalizedPlanKey,
                        correlationId,
                        feature ?? string.Empty,
                        (int)ex.StatusCode);

                    return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                    {
                        Status = StatusCodes.Status502BadGateway,
                        Title = "Billing error",
                        Detail = ex.Message
                    });
                }

                _logger.LogInformation(
                    "Upgrade URL created. UserId={UserId} PlanKey={PlanKey} Route=portal CorrelationId={CorrelationId} Feature={Feature} PortalReturnUrl={PortalReturnUrl}",
                    userId,
                    normalizedPlanKey,
                    correlationId,
                    feature ?? string.Empty,
                    portalReturnUrl);

                return Ok(new BillingUrlResponse(portalUrl));
            }

            if (!_stripeOptions.Enabled || string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Billing unavailable",
                    Detail = "Stripe checkout is disabled because Stripe is not configured."
                });
            }

            string priceId;
            try
            {
                priceId = _stripePriceResolver.ResolvePriceId(normalizedPlanKey, out normalizedPlanKey);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid billing request",
                    Detail = ex.Message
                });
            }
            string stripeCustomerId = await EnsureStripeCustomerIdAsync(entitlement, userId, allowCreate: true, _stripeOptions.SecretKey, ct);
            StripeBaseUrlResolution baseUrlResolution = _stripeRedirectUrlBuilder.ResolveBaseUrlContext(Request);
            string baseUrl = baseUrlResolution.BaseUrl;
            string successUrl = AppendQueryParameter(
                BuildCheckoutUrl(baseUrl, _stripeOptions.Checkout.SuccessPath),
                "plan",
                normalizedPlanKey);
            string cancelUrl = BuildCheckoutUrl(baseUrl, _stripeOptions.Checkout.CancelPath);
            _logger.LogInformation(
                "Upgrade checkout redirect URLs prepared. CorrelationId={CorrelationId} UserId={UserId} PlanKey={PlanKey} Feature={Feature} PriceId={PriceId} StripeMode={StripeMode} BaseUrl={BaseUrl} BaseUrlSource={BaseUrlSource} RequestHost={RequestHost} RequestScheme={RequestScheme} RequestPathBase={RequestPathBase} RequestHostLooksLikeAzureAppService={RequestHostLooksLikeAzureAppService} SuccessUrl={SuccessUrl} CancelUrl={CancelUrl}",
                correlationId,
                userId,
                normalizedPlanKey,
                feature ?? string.Empty,
                priceId,
                GetActiveStripeMode(),
                baseUrlResolution.BaseUrl,
                baseUrlResolution.Source,
                baseUrlResolution.RequestHost,
                baseUrlResolution.RequestScheme,
                baseUrlResolution.RequestPathBase,
                baseUrlResolution.RequestHostLooksLikeAzureAppService,
                successUrl,
                cancelUrl);

            if (!TryValidateStripeRedirectUrl(successUrl, out string successReason))
            {
                _logger.LogWarning(
                    "Invalid Stripe success_url configuration. CorrelationId={CorrelationId} PlanKey={PlanKey} Feature={Feature} Reason={Reason}",
                    correlationId,
                    normalizedPlanKey,
                    feature ?? string.Empty,
                    successReason);
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid billing configuration",
                    Detail = "Invalid Stripe checkout redirect URL configuration. Failed field: success_url."
                });
            }

            if (!TryValidateStripeRedirectUrl(cancelUrl, out string cancelReason))
            {
                _logger.LogWarning(
                    "Invalid Stripe cancel_url configuration. CorrelationId={CorrelationId} PlanKey={PlanKey} Feature={Feature} Reason={Reason}",
                    correlationId,
                    normalizedPlanKey,
                    feature ?? string.Empty,
                    cancelReason);
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid billing configuration",
                    Detail = "Invalid Stripe checkout redirect URL configuration. Failed field: cancel_url."
                });
            }

            string checkoutUrl;
            try
            {
                checkoutUrl = await _stripeApiClient.CreateCheckoutSessionAsync(
                    _stripeOptions.SecretKey,
                    stripeCustomerId,
                    userId,
                    normalizedPlanKey,
                    priceId,
                    successUrl,
                    cancelUrl,
                    ct);
            }
            catch (StripeApiException ex)
            {
                if (ex.StatusCode == HttpStatusCode.BadRequest)
                {
                    // Stripe returns 400 (often with error.param=success_url/cancel_url) when redirect URLs are invalid.
                    _logger.LogWarning(
                        ex,
                        "Stripe rejected upgrade checkout request. UserId={UserId} PlanKey={PlanKey} CorrelationId={CorrelationId} Feature={Feature} ErrorParam={ErrorParam} RawBody={RawBody}",
                        userId,
                        normalizedPlanKey,
                        correlationId,
                        feature ?? string.Empty,
                        ex.ErrorParam ?? string.Empty,
                        ex.RawBody ?? string.Empty);
                    return BadRequest(new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Billing error",
                        Detail = "Stripe checkout request rejected. Check redirect URL configuration."
                    });
                }

                _logger.LogWarning(
                    ex,
                    "Upgrade URL checkout creation failed. UserId={UserId} PlanKey={PlanKey} CorrelationId={CorrelationId} Feature={Feature} StatusCode={StatusCode} ErrorParam={ErrorParam} RawBody={RawBody}",
                    userId,
                    normalizedPlanKey,
                    correlationId,
                    feature ?? string.Empty,
                    (int)ex.StatusCode,
                    ex.ErrorParam ?? string.Empty,
                    ex.RawBody ?? string.Empty);

                return StatusCode(StatusCodes.Status502BadGateway, new ProblemDetails
                {
                    Status = StatusCodes.Status502BadGateway,
                    Title = "Billing error",
                    Detail = ex.Message
                });
            }

            entitlement.StripePriceId = priceId;
            entitlement.StripeMode = GetActiveStripeMode();
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Upgrade URL created. UserId={UserId} PlanKey={PlanKey} Route=checkout CorrelationId={CorrelationId} Feature={Feature}",
                userId,
                normalizedPlanKey,
                correlationId,
                feature ?? string.Empty);

            return Ok(new BillingUrlResponse(checkoutUrl));
        }

        // Legacy recovery endpoint. Normal plan activation should come from Stripe webhook processing.
        [HttpGet("checkout-status")]
        public async Task<ActionResult<CheckoutStatusDto>> GetCheckoutStatus(
            [FromQuery] string? sessionId,
            [FromQuery(Name = "session_id")] string? sessionIdSnakeCase,
            CancellationToken ct)
        {
            if (!_stripeOptions.Enabled || string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateApiError("stripe_not_configured", "Stripe is not configured."));
            }

            string? incomingSessionId = string.IsNullOrWhiteSpace(sessionId)
                ? sessionIdSnakeCase
                : sessionId;

            if (string.IsNullOrWhiteSpace(incomingSessionId) || !incomingSessionId.Trim().StartsWith("cs_", StringComparison.Ordinal))
            {
                return BadRequest(CreateApiError("invalid_session_id", "sessionId/session_id is required and must start with 'cs_'."));
            }

            string normalizedSessionId = incomingSessionId.Trim();
            string userId = _userIdResolver.ResolveUserId(User);
            _logger.LogInformation("Checkout status requested. UserId={UserId} SessionSuffix={SessionSuffix}", userId, Last6(normalizedSessionId));

            try
            {
                using JsonDocument session = await _stripeClientFacade.GetCheckoutSessionAsync(_stripeOptions.SecretKey, normalizedSessionId, ct);
                if (!IsSessionOwnedByUser(session.RootElement, userId))
                {
                    _logger.LogWarning("Checkout status forbidden due to ownership mismatch. UserId={UserId} SessionSuffix={SessionSuffix}", userId, Last6(normalizedSessionId));
                    return StatusCode(StatusCodes.Status403Forbidden, CreateApiError("forbidden", "Checkout session does not belong to the current user."));
                }

                CheckoutStatusDto dto = BuildCheckoutStatusDto(session.RootElement);
                if (string.Equals(dto.State, "paid", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(dto.SubscriptionId))
                {
                    string? customerId = ReadString(session.RootElement, "customer");
                    using JsonDocument subscription = await _stripeClientFacade.GetSubscriptionAsync(
                        _stripeOptions.SecretKey,
                        dto.SubscriptionId,
                        ct);
                    await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(
                        userId,
                        customerId,
                        subscription.RootElement,
                        ct);
                }

                return Ok(dto);
            }
            catch (StripeApiException ex)
            {
                _logger.LogWarning(ex, "Checkout status lookup failed. UserId={UserId} SessionSuffix={SessionSuffix}", userId, Last6(normalizedSessionId));
                return StatusCode(StatusCodes.Status502BadGateway, CreateApiError("stripe_error", ex.Message));
            }
        }

        // Recovery endpoint for delayed webhook settlement. This is not the normal checkout completion path.
        [HttpPost("sync-entitlements")]
        public async Task<ActionResult<SyncEntitlementsResponse>> SyncEntitlements(
            [FromBody] SyncEntitlementsRequest request,
            CancellationToken ct)
        {
            if (!_stripeOptions.Enabled || string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, CreateApiError("stripe_not_configured", "Stripe is not configured."));
            }

            if (request is null || (string.IsNullOrWhiteSpace(request.SessionId) && string.IsNullOrWhiteSpace(request.SubscriptionId)))
            {
                return BadRequest(CreateApiError("invalid_request", "sessionId or subscriptionId is required."));
            }

            string userId = _userIdResolver.ResolveUserId(User);
            string? subscriptionId = request.SubscriptionId?.Trim();
            string? customerId = null;

            if (!string.IsNullOrWhiteSpace(request.SessionId))
            {
                string sessionId = request.SessionId.Trim();
                if (!sessionId.StartsWith("cs_", StringComparison.Ordinal))
                {
                    return BadRequest(CreateApiError("invalid_session_id", "sessionId must start with 'cs_'."));
                }

                using JsonDocument session = await _stripeClientFacade.GetCheckoutSessionAsync(_stripeOptions.SecretKey, sessionId, ct);
                if (!IsSessionOwnedByUser(session.RootElement, userId))
                {
                    _logger.LogWarning("Sync entitlements forbidden due to ownership mismatch. UserId={UserId} SessionSuffix={SessionSuffix}", userId, Last6(sessionId));
                    return StatusCode(StatusCodes.Status403Forbidden, CreateApiError("forbidden", "Checkout session does not belong to the current user."));
                }

                subscriptionId = ReadString(session.RootElement, "subscription");
                customerId = ReadString(session.RootElement, "customer");
            }

            if (string.IsNullOrWhiteSpace(subscriptionId))
            {
                return BadRequest(CreateApiError("invalid_subscription_id", "subscriptionId could not be resolved."));
            }

            _logger.LogInformation(
                "Sync entitlements invoked. UserId={UserId} SubscriptionId={SubscriptionId} StripeCustomerId={StripeCustomerId} SessionIdPresent={SessionIdPresent} StripeMode={StripeMode}",
                userId,
                subscriptionId,
                customerId ?? string.Empty,
                !string.IsNullOrWhiteSpace(request.SessionId),
                GetActiveStripeMode());

            using JsonDocument subscription = await _stripeClientFacade.GetSubscriptionAsync(_stripeOptions.SecretKey, subscriptionId, ct);
            UserEntitlement updated = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(userId, customerId, subscription.RootElement, ct);
            _logger.LogInformation(
                "Sync entitlements completed. UserId={UserId} PlanKey={PlanKey} SubscriptionStatus={SubscriptionStatus} StripeCustomerId={StripeCustomerId} StripeSubscriptionId={StripeSubscriptionId} StripePriceId={StripePriceId}",
                updated.UserId,
                updated.PlanKey ?? string.Empty,
                updated.SubscriptionStatus ?? string.Empty,
                updated.StripeCustomerId ?? string.Empty,
                updated.StripeSubscriptionId ?? string.Empty,
                updated.StripePriceId ?? string.Empty);
            return Ok(BuildSyncEntitlementsResponse(updated));
        }

        private async Task<string> EnsureStripeCustomerIdAsync(
            UserEntitlement entitlement,
            string userId,
            bool allowCreate,
            string secretKey,
            CancellationToken ct)
        {
            string activeMode = GetActiveStripeMode();
            string storedMode = ResolveStoredStripeMode(entitlement);
            bool hasModeMismatch = StripeBillingEnvironment.IsModeMismatch(storedMode, activeMode);
            string? customerId = entitlement.StripeCustomerId;
            if (hasModeMismatch)
            {
                LogStripeModeMismatch("customer lookup", entitlement, storedMode);
                customerId = null;
            }

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                bool exists = await _stripeApiClient.CustomerExistsAsync(secretKey, customerId, ct);
                if (exists)
                {
                    await StampStripeModeIfNeededAsync(entitlement, activeMode, ct);
                    return customerId;
                }
            }

            string? found = await _stripeApiClient.FindCustomerByUserIdAsync(secretKey, userId, ct);
            if (!string.IsNullOrWhiteSpace(found))
            {
                if (hasModeMismatch)
                {
                    ClearStoredSubscriptionState(entitlement);
                }

                entitlement.StripeMode = activeMode;
                entitlement.StripeCustomerId = found;
                entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
                return found;
            }

            if (!allowCreate)
            {
                throw new InvalidOperationException("No Stripe customer is linked to this account yet.");
            }

            string created = await _stripeApiClient.CreateCustomerAsync(secretKey, userId, ct);
            if (hasModeMismatch)
            {
                ClearStoredSubscriptionState(entitlement);
            }

            entitlement.StripeMode = activeMode;
            entitlement.StripeCustomerId = created;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return created;
        }

        private async Task<(string? CustomerId, JsonDocument Subscription)> ResolveManagedSubscriptionAsync(
            UserEntitlement entitlement,
            string userId,
            CancellationToken ct)
        {
            string activeMode = GetActiveStripeMode();
            string storedMode = ResolveStoredStripeMode(entitlement);
            string? customerId = entitlement.StripeCustomerId;
            string? subscriptionId = entitlement.StripeSubscriptionId;
            if (StripeBillingEnvironment.IsModeMismatch(storedMode, activeMode))
            {
                LogStripeModeMismatch("managed subscription lookup", entitlement, storedMode);
                customerId = null;
                subscriptionId = null;
            }

            if (string.IsNullOrWhiteSpace(customerId) && !string.IsNullOrWhiteSpace(subscriptionId))
            {
                JsonDocument subscriptionById = await _stripeApiClient.GetSubscriptionAsync(_stripeOptions.SecretKey, subscriptionId, ct);
                string? resolvedCustomerId = ReadString(subscriptionById.RootElement, "customer");
                if (!string.IsNullOrWhiteSpace(resolvedCustomerId))
                {
                    entitlement.StripeMode = activeMode;
                    entitlement.StripeCustomerId = resolvedCustomerId;
                    entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
                    await _dbContext.SaveChangesAsync(ct);
                }

                return (resolvedCustomerId, subscriptionById);
            }

            if (string.IsNullOrWhiteSpace(customerId))
            {
                customerId = await _stripeApiClient.FindCustomerByUserIdAsync(_stripeOptions.SecretKey, userId, ct);
                if (!string.IsNullOrWhiteSpace(customerId))
                {
                    entitlement.StripeMode = activeMode;
                    entitlement.StripeCustomerId = customerId;
                    entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
                    await _dbContext.SaveChangesAsync(ct);
                }
            }

            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                JsonDocument subscriptionById = await _stripeApiClient.GetSubscriptionAsync(_stripeOptions.SecretKey, subscriptionId, ct);
                return (customerId, subscriptionById);
            }

            if (string.IsNullOrWhiteSpace(customerId))
            {
                throw new InvalidOperationException("No Stripe customer is linked to this account yet.");
            }

            JsonDocument listDoc = await _stripeApiClient.ListSubscriptionsByCustomerAsync(_stripeOptions.SecretKey, customerId, ct);
            if (!listDoc.RootElement.TryGetProperty("data", out JsonElement data)
                || data.ValueKind != JsonValueKind.Array
                || data.GetArrayLength() == 0)
            {
                listDoc.Dispose();
                throw new InvalidOperationException("No Stripe subscription found for this account.");
            }

            JsonElement selected = data[0];
            string? selectedId = ReadString(selected, "id");
            if (string.IsNullOrWhiteSpace(selectedId))
            {
                listDoc.Dispose();
                throw new InvalidOperationException("Stripe returned a subscription without an id.");
            }

            entitlement.StripeMode = activeMode;
            entitlement.StripeCustomerId = customerId;
            entitlement.StripeSubscriptionId = selectedId;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            JsonDocument subscription = JsonDocument.Parse(selected.GetRawText());
            listDoc.Dispose();
            return (customerId, subscription);
        }

        private string GetActiveStripeMode()
        {
            return StripeBillingEnvironment.Normalize(_stripeOptions.Mode);
        }

        private string ResolveStoredStripeMode(UserEntitlement entitlement)
        {
            return StripeBillingEnvironment.ResolveStoredMode(entitlement, _stripeOptions);
        }

        private async Task StampStripeModeIfNeededAsync(UserEntitlement entitlement, string activeMode, CancellationToken ct)
        {
            if (string.Equals(StripeBillingEnvironment.Normalize(entitlement.StripeMode), activeMode, StringComparison.Ordinal))
            {
                return;
            }

            entitlement.StripeMode = activeMode;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }

        private static void ClearStoredSubscriptionState(UserEntitlement entitlement)
        {
            entitlement.StripeSubscriptionId = null;
            entitlement.StripePriceId = null;
            entitlement.CurrentPeriodEndUtc = null;
            entitlement.CancelAtPeriodEnd = false;
        }

        private void LogStripeModeMismatch(string operation, UserEntitlement entitlement, string storedMode)
        {
            _logger.LogWarning(
                "Stripe linkage mode mismatch detected. Operation={Operation} UserId={UserId} StoredStripeMode={StoredStripeMode} ActiveStripeMode={ActiveStripeMode} StoredCustomerId={StoredCustomerId} StoredSubscriptionId={StoredSubscriptionId}",
                operation,
                entitlement.UserId,
                storedMode,
                GetActiveStripeMode(),
                entitlement.StripeCustomerId ?? string.Empty,
                entitlement.StripeSubscriptionId ?? string.Empty);
        }

        private SyncEntitlementsResponse BuildSyncEntitlementsResponse(UserEntitlement entitlement)
        {
            string? planKey = _stripePriceResolver.ResolvePlanKey(entitlement.StripePriceId ?? string.Empty) ?? entitlement.PlanKey;
            return new SyncEntitlementsResponse(
                entitlement.UserId,
                planKey,
                entitlement.SubscriptionStatus,
                entitlement.StripeCustomerId,
                entitlement.StripeSubscriptionId,
                entitlement.StripePriceId,
                entitlement.CurrentPeriodEndUtc,
                entitlement.CancelAtPeriodEnd);
        }

        private static string BuildCheckoutUrl(string baseUrl, string? path)
        {
            string normalizedPath = string.IsNullOrWhiteSpace(path)
                ? "/app/account/billing"
                : path.Trim();
            if (Uri.TryCreate(normalizedPath, UriKind.Absolute, out Uri? absolute)
                && (string.Equals(absolute.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(absolute.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return absolute.ToString();
            }

            string prefixed = normalizedPath.StartsWith("/", StringComparison.Ordinal) ? normalizedPath : "/" + normalizedPath;
            return $"{baseUrl}{prefixed}";
        }

        private static string AppendQueryParameter(string url, string key, string value)
        {
            if (string.IsNullOrWhiteSpace(url)
                || string.IsNullOrWhiteSpace(key)
                || string.IsNullOrWhiteSpace(value))
            {
                return url;
            }

            string separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
            return $"{url}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
        }

        private static CheckoutStatusDto BuildCheckoutStatusDto(JsonElement session)
        {
            string? sessionStatus = ReadString(session, "status");
            string? paymentStatus = ReadString(session, "payment_status");
            string state = BillingCheckoutStateMapper.MapState(sessionStatus, paymentStatus);
            string? customerId = ReadString(session, "customer");
            string? subscriptionId = ReadString(session, "subscription");
            string? planKey = ReadString(session, "metadata", "planKey");

            string message = state switch
            {
                "paid" => "Payment confirmed.",
                "open" => "Checkout is still open.",
                "expired" => "Checkout session expired.",
                "incomplete" => "Checkout completed but payment not confirmed yet.",
                _ => "Checkout status is unknown."
            };

            return new CheckoutStatusDto(state, subscriptionId, customerId, planKey, message);
        }

        private static bool IsSessionOwnedByUser(JsonElement session, string userId)
        {
            string? clientReferenceId = ReadString(session, "client_reference_id");
            string? metadataUserId = ReadString(session, "metadata", "userId");
            return string.Equals(clientReferenceId, userId, StringComparison.Ordinal)
                || string.Equals(metadataUserId, userId, StringComparison.Ordinal);
        }

        private static string? ReadString(JsonElement root, params object[] path)
        {
            JsonElement current = root;
            foreach (object segment in path)
            {
                if (segment is string property)
                {
                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(property, out JsonElement child))
                    {
                        return null;
                    }

                    current = child;
                    continue;
                }

                return null;
            }

            return current.ValueKind == JsonValueKind.String
                ? current.GetString()
                : current.ValueKind == JsonValueKind.Number
                    ? current.ToString()
                    : current.ValueKind == JsonValueKind.Object && current.TryGetProperty("id", out JsonElement idProp) && idProp.ValueKind == JsonValueKind.String
                        ? idProp.GetString()
                        : null;
        }

        private static bool? ReadBool(JsonElement root, params object[] path)
        {
            JsonElement current = root;
            foreach (object segment in path)
            {
                if (segment is string property)
                {
                    if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(property, out JsonElement child))
                    {
                        return null;
                    }

                    current = child;
                    continue;
                }

                return null;
            }

            return current.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        private static string NormalizeSubscriptionStatus(string? subscriptionStatus)
        {
            return BillingSubscriptionPolicy.NormalizeStatus(subscriptionStatus);
        }

        private static object CreateApiError(string code, string message, string? upgradePath = null)
        {
            return new
            {
                code,
                message,
                upgradePath
            };
        }

        private static string Last6(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return value.Length <= 6 ? value : value[^6..];
        }

        private static bool TryNormalizeUpgradePlan(string? value, out string planKey)
        {
            planKey = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string candidate = value.Trim().ToLowerInvariant();
            if (candidate is "standard" or "pro")
            {
                planKey = candidate;
                return true;
            }

            return false;
        }

        private static bool TryValidateStripeRedirectUrl(string? value, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                reason = "empty";
                return false;
            }

            string candidate = value.Trim();
            if (candidate.Any(char.IsWhiteSpace))
            {
                reason = "contains whitespace";
                return false;
            }

            if (candidate.IndexOfAny(new[] { '\r', '\n', '\t' }) >= 0)
            {
                reason = "contains control characters";
                return false;
            }

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
            {
                reason = "not absolute";
                return false;
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                reason = "invalid scheme";
                return false;
            }

            return true;
        }

        public sealed record CreateBillingCheckoutRequest(string PlanKey);
        public sealed record BillingUrlResponse(string Url);
        public sealed record BillingStatusResponse(
            string Mode,
            bool Enabled,
            bool KeysPresent,
            bool StandardPriceConfigured,
            bool ProPriceConfigured);
        public sealed record SyncEntitlementsRequest(string? SessionId, string? SubscriptionId);
        public sealed record SyncEntitlementsResponse(
            string UserId,
            string? PlanKey,
            string SubscriptionStatus,
            string? StripeCustomerId,
            string? StripeSubscriptionId,
            string? StripePriceId,
            DateTimeOffset? CurrentPeriodEndUtc,
            bool CancelAtPeriodEnd);
    }
}
