using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace WriterApp.Application.Security
{
    public static class AdminPlanOverrideAccess
    {
        public static bool IsEnabled(IConfiguration configuration)
        {
            return configuration.GetValue<bool?>("Admin:EnablePlanOverride") ?? false;
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
    }
}
