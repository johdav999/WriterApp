// TEMPORARY ADMIN ENDPOINT - REMOVE AFTER STRIPE SANDBOX RECOVERY
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
    public sealed class InternalAdminController : ControllerBase
    {
        private readonly AppDbContext _db;

        public InternalAdminController(AppDbContext db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        [AllowAnonymous]
        [HttpPost("reset-stripe-link")]
        public async Task<IActionResult> ResetStripeLink(
            [FromQuery] Guid userId,
            [FromServices] IConfiguration config)
        {
            string? expected = config["INTERNAL_ADMIN_KEY"];
            string provided = Request.Headers["X-Admin-Key"].ToString();

            if (string.IsNullOrWhiteSpace(expected) || !string.Equals(provided, expected, StringComparison.Ordinal))
            {
                return Unauthorized();
            }

            string targetUserId = userId.ToString();
            Data.Subscriptions.UserEntitlement? entitlement = await _db.UserEntitlements
                .FirstOrDefaultAsync(x => x.UserId == targetUserId);

            if (entitlement is null)
            {
                return NotFound("UserEntitlement not found.");
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
    }
}
