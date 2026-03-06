using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace WriterApp.Application.Security
{
    public static class AdminPlanOverrideAccess
    {
        private const string OidClaimType = "http://schemas.microsoft.com/identity/claims/objectidentifier";

        public static bool IsEnabled(IConfiguration configuration)
        {
            return configuration.GetValue<bool?>("Admin:EnablePlanOverride") ?? false;
        }

        public static bool IsAdminApiEnabled(IConfiguration configuration)
        {
            return configuration.GetValue<bool?>("Admin:EnableAdminApi") ?? false;
        }

        public static bool IsAuthorized(ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            if (user.IsInRole("Admin"))
            {
                return true;
            }

            return user.Claims.Any(claim =>
                (string.Equals(claim.Type, "roles", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(claim.Type, "appRole", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
                && string.Equals(claim.Value, "Admin", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsAuthorized(ClaimsPrincipal user, IConfiguration configuration)
        {
            if (IsAuthorized(user))
            {
                return true;
            }

            string? bootstrapEnabledValue = configuration["BOOTSTRAP_ADMIN_ENABLED"];
            bool bootstrapEnabled = string.Equals(bootstrapEnabledValue, "true", StringComparison.OrdinalIgnoreCase);
            if (!bootstrapEnabled)
            {
                return false;
            }

            string? bootstrapOid = configuration["BOOTSTRAP_ADMIN_OID"];
            if (string.IsNullOrWhiteSpace(bootstrapOid))
            {
                return false;
            }

            string? userOid = user.FindFirstValue(OidClaimType)
                ?? user.FindFirstValue("oid");

            return !string.IsNullOrWhiteSpace(userOid)
                && string.Equals(bootstrapOid, userOid, StringComparison.OrdinalIgnoreCase);
        }
    }
}
