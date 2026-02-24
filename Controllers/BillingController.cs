using System;
using System.Net;
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
        private readonly StripeApiClient _stripeApiClient;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            AppDbContext dbContext,
            IUserIdResolver userIdResolver,
            IUserEntitlementStore userEntitlementStore,
            StripeOptions stripeOptions,
            StripeApiClient stripeApiClient,
            ILogger<BillingController> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _userIdResolver = userIdResolver ?? throw new ArgumentNullException(nameof(userIdResolver));
            _userEntitlementStore = userEntitlementStore ?? throw new ArgumentNullException(nameof(userEntitlementStore));
            _stripeOptions = stripeOptions ?? throw new ArgumentNullException(nameof(stripeOptions));
            _stripeApiClient = stripeApiClient ?? throw new ArgumentNullException(nameof(stripeApiClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [HttpPost("checkout")]
        public async Task<ActionResult<BillingUrlResponse>> CreateCheckoutSession(
            [FromBody] CreateBillingCheckoutRequest request,
            CancellationToken ct)
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

            if (!TryResolvePlan(request.PlanKey, out string normalizedPlanKey, out string priceId))
            {
                return BadRequest(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Invalid plan",
                    Detail = "planKey must be either 'standard' or 'pro'."
                });
            }

            string userId = _userIdResolver.ResolveUserId(User);
            UserEntitlement entitlement = await _userEntitlementStore.GetOrCreateAsync(userId, ct);
            string customerId = await EnsureStripeCustomerIdAsync(entitlement, userId, allowCreate: true, ct);

            string successUrl = BuildAbsoluteUrl(_stripeOptions.SuccessUrl, StripeOptions.DefaultSuccessUrl);
            string cancelCandidate = string.IsNullOrWhiteSpace(request.ReturnUrl)
                ? _stripeOptions.CancelUrl
                : ReturnUrlSafety.NormalizeOrFallback(request.ReturnUrl, StripeOptions.DefaultCancelUrl);
            string cancelUrl = BuildAbsoluteUrl(cancelCandidate, StripeOptions.DefaultCancelUrl);

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
                _logger.LogWarning(
                    ex,
                    "Stripe checkout session creation failed. UserId={UserId} PlanKey={PlanKey} StatusCode={StatusCode}",
                    userId,
                    normalizedPlanKey,
                    (int)ex.StatusCode);

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
                customerId = await EnsureStripeCustomerIdAsync(entitlement, userId, allowCreate: false, ct);
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

        private async Task<string> EnsureStripeCustomerIdAsync(
            UserEntitlement entitlement,
            string userId,
            bool allowCreate,
            CancellationToken ct)
        {
            string? customerId = entitlement.StripeCustomerId;
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                bool exists = await _stripeApiClient.CustomerExistsAsync(_stripeOptions.SecretKey, customerId, ct);
                if (exists)
                {
                    return customerId;
                }
            }

            string? found = await _stripeApiClient.FindCustomerByUserIdAsync(_stripeOptions.SecretKey, userId, ct);
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

            string created = await _stripeApiClient.CreateCustomerAsync(_stripeOptions.SecretKey, userId, ct);
            entitlement.StripeCustomerId = created;
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return created;
        }

        private bool TryResolvePlan(string? input, out string normalizedPlanKey, out string priceId)
        {
            normalizedPlanKey = string.Empty;
            priceId = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            string candidate = input.Trim().ToLowerInvariant();
            if (candidate == "standard")
            {
                normalizedPlanKey = "standard";
                priceId = _stripeOptions.PriceStandard;
                return !string.IsNullOrWhiteSpace(priceId);
            }

            if (candidate == "pro")
            {
                normalizedPlanKey = "pro";
                priceId = _stripeOptions.PricePro;
                return !string.IsNullOrWhiteSpace(priceId);
            }

            return false;
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

        public sealed record CreateBillingCheckoutRequest(string PlanKey, string? ReturnUrl);
        public sealed record BillingUrlResponse(string Url);
    }
}
