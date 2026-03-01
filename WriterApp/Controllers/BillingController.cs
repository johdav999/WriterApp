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
using Microsoft.Extensions.Options;
using WriterApp.Application.Billing;
using WriterApp.Application.Security;
using WriterApp.Application.Subscriptions;
using WriterApp.Data;
using WriterApp.Data.Subscriptions;
using WriterApp.Shared;

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
        private readonly StripeBillingOptions _stripeBillingOptions;
        private readonly IStripePriceResolver _stripePriceResolver;
        private readonly IStripeClientFacade _stripeClientFacade;
        private readonly StripeEntitlementSyncService _stripeEntitlementSyncService;
        private readonly StripeApiClient _stripeApiClient;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IUserEntitlementStore userEntitlementStore,
            StripeOptions stripeOptions,
            IOptions<StripeBillingOptions> stripeBillingOptions,
            IStripePriceResolver stripePriceResolver,
            IStripeClientFacade stripeClientFacade,
            StripeEntitlementSyncService stripeEntitlementSyncService,
            StripeApiClient stripeApiClient,
            ILogger<BillingController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _stripeOptions = stripeOptions ?? throw new ArgumentNullException(nameof(stripeOptions));
            _stripeBillingOptions = stripeBillingOptions?.Value ?? throw new ArgumentNullException(nameof(stripeBillingOptions));
            _stripePriceResolver = stripePriceResolver ?? throw new ArgumentNullException(nameof(stripePriceResolver));
            _stripeClientFacade = stripeClientFacade ?? throw new ArgumentNullException(nameof(stripeClientFacade));
            _stripeEntitlementSyncService = stripeEntitlementSyncService ?? throw new ArgumentNullException(nameof(stripeEntitlementSyncService));
            _stripeApiClient = stripeApiClient ?? throw new ArgumentNullException(nameof(stripeApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpGet("status")]
        public ActionResult<BillingStatusResponse> GetBillingStatus()
        {
            bool hasSecretKey = !string.IsNullOrWhiteSpace(_stripeBillingOptions.ApiKey);
            bool hasWebhookSecret = !string.IsNullOrWhiteSpace(_stripeBillingOptions.WebhookSecret);
            bool hasStandardPrice =
                !string.IsNullOrWhiteSpace(_stripeBillingOptions.Prices.Standard.TestPriceId)
                || !string.IsNullOrWhiteSpace(_stripeBillingOptions.Prices.Standard.LivePriceId);
            bool hasProPrice =
                !string.IsNullOrWhiteSpace(_stripeBillingOptions.Prices.Pro.TestPriceId)
                || !string.IsNullOrWhiteSpace(_stripeBillingOptions.Prices.Pro.LivePriceId);

            return Ok(new BillingStatusResponse(
                _stripeBillingOptions.Mode,
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
            if (string.IsNullOrWhiteSpace(_stripeBillingOptions.ApiKey))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Billing unavailable",
                    Detail = "Stripe checkout is disabled because Stripe:Billing:ApiKey is not configured."
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
            string customerId = await EnsureStripeCustomerIdAsync(entitlement, userId, allowCreate: true, _stripeBillingOptions.ApiKey, ct);

            string baseUrl = ResolveCheckoutBaseUrl();
            string successUrl = BuildCheckoutUrl(baseUrl, _stripeBillingOptions.Checkout.SuccessPath);
            string cancelUrl = BuildCheckoutUrl(baseUrl, _stripeBillingOptions.Checkout.CancelPath);

            string checkoutUrl;
            try
            {
                checkoutUrl = await _stripeApiClient.CreateCheckoutSessionAsync(
                    _stripeBillingOptions.ApiKey,
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

            string portalReturnUrl = BuildAbsoluteUrl(_stripeOptions.BillingPortalReturnUrl, "/app/account");

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
            bool hasSubscription = !string.IsNullOrWhiteSpace(entitlement.StripeSubscriptionId);

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

                string portalReturnUrl = BuildAbsoluteUrl(_stripeOptions.BillingPortalReturnUrl, "/app/account/billing");
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
                    "Upgrade URL created. UserId={UserId} PlanKey={PlanKey} Route=portal CorrelationId={CorrelationId} Feature={Feature}",
                    userId,
                    normalizedPlanKey,
                    correlationId,
                    feature ?? string.Empty);

                return Ok(new BillingUrlResponse(portalUrl));
            }

            if (string.IsNullOrWhiteSpace(_stripeBillingOptions.ApiKey))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
                {
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Title = "Billing unavailable",
                    Detail = "Stripe checkout is disabled because Stripe:Billing:ApiKey is not configured."
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
            string stripeCustomerId = await EnsureStripeCustomerIdAsync(entitlement, userId, allowCreate: true, _stripeBillingOptions.ApiKey, ct);
            string baseUrl = ResolveCheckoutBaseUrl();
            string successUrl = BuildCheckoutUrl(baseUrl, _stripeBillingOptions.Checkout.SuccessPath);
            string cancelUrl = BuildCheckoutUrl(baseUrl, _stripeBillingOptions.Checkout.CancelPath);
            _logger.LogInformation(
                "Upgrade checkout redirect URLs prepared. CorrelationId={CorrelationId} PlanKey={PlanKey} Feature={Feature} BaseUrl={BaseUrl} SuccessUrl={SuccessUrl} CancelUrl={CancelUrl}",
                correlationId,
                normalizedPlanKey,
                feature ?? string.Empty,
                baseUrl,
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
                    _stripeBillingOptions.ApiKey,
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

        [HttpGet("checkout-status")]
        public async Task<ActionResult<CheckoutStatusDto>> GetCheckoutStatus(
            [FromQuery] string? sessionId,
            [FromQuery(Name = "session_id")] string? sessionIdSnakeCase,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_stripeBillingOptions.ApiKey))
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
                using JsonDocument session = await _stripeClientFacade.GetCheckoutSessionAsync(_stripeBillingOptions.ApiKey, normalizedSessionId, ct);
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
                        _stripeBillingOptions.ApiKey,
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

        [HttpPost("sync-entitlements")]
        public async Task<ActionResult<SyncEntitlementsResponse>> SyncEntitlements(
            [FromBody] SyncEntitlementsRequest request,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_stripeBillingOptions.ApiKey))
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

                using JsonDocument session = await _stripeClientFacade.GetCheckoutSessionAsync(_stripeBillingOptions.ApiKey, sessionId, ct);
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

            _logger.LogInformation("Sync entitlements invoked. UserId={UserId} SubscriptionId={SubscriptionId}", userId, subscriptionId);

            using JsonDocument subscription = await _stripeClientFacade.GetSubscriptionAsync(_stripeBillingOptions.ApiKey, subscriptionId, ct);
            UserEntitlement updated = await _stripeEntitlementSyncService.SyncFromSubscriptionAsync(userId, customerId, subscription.RootElement, ct);
            string? planKey = _stripePriceResolver.ResolvePlanKey(updated.StripePriceId ?? string.Empty) ?? updated.PlanKey;

            return Ok(new SyncEntitlementsResponse(
                updated.UserId,
                planKey,
                updated.SubscriptionStatus,
                updated.StripeCustomerId,
                updated.StripeSubscriptionId,
                updated.StripePriceId,
                updated.CurrentPeriodEndUtc,
                updated.CancelAtPeriodEnd));
        }

        private async Task<string> EnsureStripeCustomerIdAsync(
            UserEntitlement entitlement,
            string userId,
            bool allowCreate,
            string secretKey,
            CancellationToken ct)
        {
            string? customerId = entitlement.StripeCustomerId;
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                bool exists = await _stripeApiClient.CustomerExistsAsync(secretKey, customerId, ct);
                if (exists)
                {
                    return customerId;
                }
            }

            string? found = await _stripeApiClient.FindCustomerByUserIdAsync(secretKey, userId, ct);
            if (!string.IsNullOrWhiteSpace(found))
            {
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
            entitlement.StripeCustomerId = created;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return created;
        }

        private string BuildAbsoluteUrl(string? configuredUrl, string fallbackRelativePath)
        {
            string candidate = string.IsNullOrWhiteSpace(configuredUrl)
                ? fallbackRelativePath
                : configuredUrl.Trim();

            if (Uri.TryCreate(candidate, UriKind.Absolute, out Uri? absolute))
            {
                return absolute.ToString();
            }

            string relative = ReturnUrlSafety.NormalizeOrFallback(candidate, fallbackRelativePath);
            return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{relative}";
        }

        private string ResolveCheckoutBaseUrl()
        {
            if (!string.IsNullOrWhiteSpace(_stripeBillingOptions.Checkout.BaseUrl))
            {
                return _stripeBillingOptions.Checkout.BaseUrl.Trim().TrimEnd('/');
            }

            return $"{Request.Scheme}://{Request.Host}{Request.PathBase}".TrimEnd('/');
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
