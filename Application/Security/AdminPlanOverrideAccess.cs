using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using WriterApp.Data;

namespace WriterApp.Application.Security
{
    public static class AdminPlanOverrideAccess
    {
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
            // Legacy role-only check used by older code paths. Role admin remains
            // the standard production access model.
            return AdminAccessEvaluation.HasLegacyRoleAdminClaim(user);
        }

        public static bool IsAuthorized(ClaimsPrincipal user, IConfiguration configuration)
        {
            if (AdminAccessEvaluation.HasLegacyRoleAdminClaim(user))
            {
                return true;
            }

            if (user.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            bool bootstrapEnabled = string.Equals(
                configuration["BOOTSTRAP_ADMIN_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (!bootstrapEnabled)
            {
                return false;
            }

            string? bootstrapUserId = configuration["BOOTSTRAP_ADMIN_USER_ID"];
            if (!string.IsNullOrWhiteSpace(bootstrapUserId))
            {
                string? userId = ExternalIdentityClaims.ResolveStableUserId(user.Claims);
                return !string.IsNullOrWhiteSpace(userId)
                       && string.Equals(IdNorm.Norm(bootstrapUserId), IdNorm.Norm(userId), StringComparison.Ordinal);
            }

            string? bootstrapOid = configuration["BOOTSTRAP_ADMIN_OID"];
            string? userOid = ExternalIdentityClaims.ResolveOid(user.Claims);
            return !string.IsNullOrWhiteSpace(bootstrapOid)
                   && !string.IsNullOrWhiteSpace(userOid)
                   && string.Equals(IdNorm.Norm(bootstrapOid), IdNorm.Norm(userOid), StringComparison.Ordinal);
        }
    }
}
