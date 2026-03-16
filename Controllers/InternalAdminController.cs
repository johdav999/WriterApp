using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WriterApp.Data;

namespace WriterApp.Controllers
{
    [ApiController]
    [Route("internal/admin")]
    [Authorize(Policy = "AdminOnly")]
    public sealed class InternalAdminController : ControllerBase
    {
        private const string ResetStripeLinkConfirmation = "unlink-stripe";
        private readonly AppDbContext _db;

        public InternalAdminController(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        // Break-glass endpoint only. This intentionally does not cancel Stripe billing and should
        // only be used when the Stripe linkage in app state is already wrong and a safer resync
        // path is not possible.
        [HttpPost("reset-stripe-link")]
        public async Task<IActionResult> ResetStripeLink(
            [FromQuery] Guid userId,
            [FromQuery] string? confirm,
            [FromServices] IConfiguration config)
        {
            if (!bool.TryParse(config["InternalAdmin:EnableStripeLinkReset"], out bool resetEnabled) || !resetEnabled)
            {
                return NotFound();
            }

            string? expected = config["INTERNAL_ADMIN_KEY"];
            string provided = Request.Headers["X-Admin-Key"].ToString();

            if (string.IsNullOrWhiteSpace(expected) || !string.Equals(provided, expected, StringComparison.Ordinal))
            {
                return Unauthorized();
            }

            if (!string.Equals(confirm, ResetStripeLinkConfirmation, StringComparison.Ordinal))
            {
                return BadRequest($"confirm must be '{ResetStripeLinkConfirmation}'.");
            }

            string targetUserId = userId.ToString();
            Data.Subscriptions.UserEntitlement? entitlement = await _db.UserEntitlements
                .FirstOrDefaultAsync(x => x.UserId == targetUserId);

            if (entitlement is null)
            {
                return NotFound("UserEntitlement not found.");
            }

            if (HasManagedStripeSubscription(entitlement))
            {
                return Conflict("Stripe linkage reset is blocked while the account still has a managed Stripe subscription state. Use the normal billing cancel flow or admin Stripe sync first.");
            }

            entitlement.StripeCustomerId = null;
            entitlement.StripeSubscriptionId = null;
            entitlement.StripePriceId = null;
            entitlement.CurrentPeriodEndUtc = null;
            entitlement.CancelAtPeriodEnd = false;
            entitlement.SubscriptionStatus = "None";
            entitlement.UpdatedUtc = DateTimeOffset.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Stripe linkage reset",
                entitlement.UserId,
                entitlement.StripeCustomerId,
                entitlement.StripeSubscriptionId
            });
        }

        private static bool HasManagedStripeSubscription(Data.Subscriptions.UserEntitlement entitlement)
        {
            if (entitlement is null || string.IsNullOrWhiteSpace(entitlement.StripeSubscriptionId))
            {
                return false;
            }

            string status = NormalizeBillingStatus(entitlement.SubscriptionStatus);
            return status is "active" or "trialing" or "past_due" or "incomplete" or "unpaid";
        }

        private static string NormalizeBillingStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                ? string.Empty
                : status.Trim().ToLowerInvariant();
        }
    }
}
