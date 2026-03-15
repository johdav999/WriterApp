using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using WriterApp.Data;

namespace WriterApp.Application.Security
{
    // Effective admin access is intentionally split into:
    // 1. Role admin: the standard production access model, backed by app-managed assignments
    // 2. Bootstrap admin: an emergency fallback for initial setup / recovery
    // A legacy external Admin claim is still honored for compatibility for the
    // current signed-in principal, but app-managed role assignments are the
    // canonical source of truth that the admin UI can grant and revoke.
    public enum AdminAccessSource
    {
        None = 0,
        Role = 1,
        Bootstrap = 2
    }

    public enum AdminAccessReason
    {
        None = 0,
        GrantedRole = 1,
        GrantedBootstrap = 2,
        NotAuthenticated = 3,
        BootstrapDisabled = 4,
        BootstrapOidMissing = 5,
        BootstrapOidMismatch = 6,
        UserIdMismatch = 7
    }

    public readonly record struct AdminAccessResolution(bool IsAdminAccess, AdminAccessSource Source, AdminAccessReason Reason)
    {
        public static AdminAccessResolution None { get; } = new(false, AdminAccessSource.None, AdminAccessReason.None);
    }

    public readonly record struct AdminAccessDiagnosticInfo(
        AdminAccessResolution Resolution,
        bool IsRoleAdmin,
        bool HasPersistedRoleAdmin,
        bool HasLegacyRoleAdminClaim,
        bool BootstrapEnabled,
        bool BootstrapOidConfigured,
        bool UserOidPresent,
        bool BootstrapMatched);

    public interface IAdminAccessResolver
    {
        // Resolve effective admin access for the current authenticated principal.
        AdminAccessResolution Resolve(ClaimsPrincipal user);
        AdminAccessDiagnosticInfo Describe(ClaimsPrincipal user);
        // Resolve the effective admin-access state for a listed user id.
        AdminAccessResolution ResolveForUserId(string? userId);
        IReadOnlyDictionary<string, AdminAccessResolution> ResolveForUserIds(IEnumerable<string> userIds, ClaimsPrincipal? currentPrincipal = null);
        // Resolve admin-access display state for a listed user, preserving any
        // current-session legacy role claim for the matching signed-in principal.
        AdminAccessResolution ResolveForUserId(string? userId, ClaimsPrincipal? currentPrincipal);
        bool HasPersistedRoleAdmin(string? userId);
    }

    public sealed class AdminAccessResolver : IAdminAccessResolver
    {
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _dbContext;

        public AdminAccessResolver(IConfiguration configuration, AppDbContext dbContext)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        }

        public AdminAccessResolution Resolve(ClaimsPrincipal user)
            => AdminAccessEvaluation.Resolve(user, _configuration, _dbContext);

        public AdminAccessDiagnosticInfo Describe(ClaimsPrincipal user)
            => AdminAccessEvaluation.Describe(user, _configuration, _dbContext);

        public AdminAccessResolution ResolveForUserId(string? userId)
            => AdminAccessEvaluation.ResolveForUserId(userId, _configuration, _dbContext, currentPrincipal: null);

        public IReadOnlyDictionary<string, AdminAccessResolution> ResolveForUserIds(IEnumerable<string> userIds, ClaimsPrincipal? currentPrincipal = null)
            => AdminAccessEvaluation.ResolveForUserIds(userIds, currentPrincipal, _configuration, _dbContext);

        public AdminAccessResolution ResolveForUserId(string? userId, ClaimsPrincipal? currentPrincipal)
            => AdminAccessEvaluation.ResolveForUserId(userId, _configuration, _dbContext, currentPrincipal);

        public bool HasPersistedRoleAdmin(string? userId)
            => AdminAccessEvaluation.HasPersistedRoleAdmin(userId, _dbContext);
    }

    internal static class AdminAccessEvaluation
    {
        public static AdminAccessResolution Resolve(ClaimsPrincipal user, IConfiguration configuration, AppDbContext dbContext)
        {
            return Describe(user, configuration, dbContext).Resolution;
        }

        public static AdminAccessDiagnosticInfo Describe(ClaimsPrincipal user, IConfiguration configuration, AppDbContext dbContext)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return new AdminAccessDiagnosticInfo(
                    new AdminAccessResolution(false, AdminAccessSource.None, AdminAccessReason.NotAuthenticated),
                    IsRoleAdmin: false,
                    HasPersistedRoleAdmin: false,
                    HasLegacyRoleAdminClaim: false,
                    BootstrapEnabled: false,
                    BootstrapOidConfigured: false,
                    UserOidPresent: false,
                    BootstrapMatched: false);
            }

            string? userId = ExternalIdentityClaims.ResolveStableUserId(user.Claims);
            bool hasPersistedRoleAdmin = HasPersistedRoleAdmin(userId, dbContext);
            bool hasLegacyRoleAdminClaim = HasLegacyRoleAdminClaim(user);
            bool isRoleAdmin = hasPersistedRoleAdmin || hasLegacyRoleAdminClaim;
            BootstrapResolutionState bootstrapState = GetBootstrapState(configuration);
            string? userOid = ExternalIdentityClaims.ResolveOid(user.Claims);
            bool userOidPresent = !string.IsNullOrWhiteSpace(userOid);
            bool bootstrapMatched =
                bootstrapState.Enabled
                && bootstrapState.OidConfigured
                && userOidPresent
                && string.Equals(IdNorm.Norm(bootstrapState.BootstrapOid), IdNorm.Norm(userOid), StringComparison.Ordinal);

            AdminAccessResolution resolution;
            // App-managed role admin is the normal operating model. A matching
            // legacy Admin claim is still treated as role access for compatibility,
            // and role access always takes precedence over bootstrap fallback.
            if (isRoleAdmin)
            {
                resolution = new AdminAccessResolution(true, AdminAccessSource.Role, AdminAccessReason.GrantedRole);
            }
            else
            {
                // Bootstrap admin is evaluated only as an emergency fallback.
                resolution = ResolveBootstrap(user, configuration);
            }

            return new AdminAccessDiagnosticInfo(
                resolution,
                isRoleAdmin,
                hasPersistedRoleAdmin,
                hasLegacyRoleAdminClaim,
                bootstrapState.Enabled,
                bootstrapState.OidConfigured,
                userOidPresent,
                bootstrapMatched);
        }

        public static IReadOnlyDictionary<string, AdminAccessResolution> ResolveForUserIds(
            IEnumerable<string> userIds,
            ClaimsPrincipal? currentPrincipal,
            IConfiguration configuration,
            AppDbContext dbContext)
        {
            string[] normalizedUserIds = userIds
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(IdNorm.Norm)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            HashSet<string> persistedRoleAdmins = dbContext.AdminRoleAssignments
                .AsNoTracking()
                .Where(item => normalizedUserIds.Contains(item.UserId))
                .Select(item => item.UserId)
                .ToHashSet(StringComparer.Ordinal);

            BootstrapResolutionState bootstrapState = GetBootstrapState(configuration);
            bool currentPrincipalHasLegacyRole = currentPrincipal is not null && HasLegacyRoleAdminClaim(currentPrincipal);
            string? currentPrincipalUserId = currentPrincipal is null
                ? null
                : ExternalIdentityClaims.ResolveStableUserId(currentPrincipal.Claims);

            Dictionary<string, AdminAccessResolution> result = new(StringComparer.Ordinal);
            foreach (string normalizedUserId in normalizedUserIds)
            {
                if (persistedRoleAdmins.Contains(normalizedUserId))
                {
                    result[normalizedUserId] = new AdminAccessResolution(true, AdminAccessSource.Role, AdminAccessReason.GrantedRole);
                    continue;
                }

                if (currentPrincipalHasLegacyRole
                    && string.Equals(IdNorm.Norm(currentPrincipalUserId), normalizedUserId, StringComparison.Ordinal))
                {
                    result[normalizedUserId] = new AdminAccessResolution(true, AdminAccessSource.Role, AdminAccessReason.GrantedRole);
                    continue;
                }

                result[normalizedUserId] = ResolveBootstrapForUserId(normalizedUserId, bootstrapState);
            }

            return result;
        }

        public static AdminAccessResolution ResolveForUserId(
            string? userId,
            IConfiguration configuration,
            AppDbContext dbContext,
            ClaimsPrincipal? currentPrincipal)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new AdminAccessResolution(false, AdminAccessSource.None, AdminAccessReason.UserIdMismatch);
            }

            string normalizedUserId = IdNorm.Norm(userId);
            if (HasPersistedRoleAdmin(normalizedUserId, dbContext))
            {
                return new AdminAccessResolution(true, AdminAccessSource.Role, AdminAccessReason.GrantedRole);
            }

            if (currentPrincipal?.Identity?.IsAuthenticated == true
                && HasLegacyRoleAdminClaim(currentPrincipal)
                && string.Equals(
                    IdNorm.Norm(ExternalIdentityClaims.ResolveStableUserId(currentPrincipal.Claims)),
                    normalizedUserId,
                    StringComparison.Ordinal))
            {
                return new AdminAccessResolution(true, AdminAccessSource.Role, AdminAccessReason.GrantedRole);
            }

            return ResolveBootstrapForUserId(normalizedUserId, GetBootstrapState(configuration));
        }

        public static bool HasPersistedRoleAdmin(string? userId, AppDbContext dbContext)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            string normalizedUserId = IdNorm.Norm(userId);
            return dbContext.AdminRoleAssignments
                .AsNoTracking()
                .Any(item => item.UserId == normalizedUserId);
        }

        internal static bool HasLegacyRoleAdminClaim(ClaimsPrincipal user)
        {
            if (user.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            // Legacy external Admin claim compatibility path.
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

        private static AdminAccessResolution ResolveBootstrap(ClaimsPrincipal user, IConfiguration configuration)
        {
            string? userOid = ExternalIdentityClaims.ResolveOid(user.Claims);
            return ResolveBootstrapForUserId(userOid, GetBootstrapState(configuration));
        }

        private static AdminAccessResolution ResolveBootstrapForUserId(string? userId, BootstrapResolutionState bootstrapState)
        {
            if (!bootstrapState.Enabled)
            {
                return new AdminAccessResolution(false, AdminAccessSource.None, AdminAccessReason.BootstrapDisabled);
            }

            if (!bootstrapState.OidConfigured)
            {
                return new AdminAccessResolution(false, AdminAccessSource.None, AdminAccessReason.BootstrapOidMissing);
            }

            return !string.IsNullOrWhiteSpace(userId)
                && string.Equals(IdNorm.Norm(bootstrapState.BootstrapOid), IdNorm.Norm(userId), StringComparison.Ordinal)
                    ? new AdminAccessResolution(true, AdminAccessSource.Bootstrap, AdminAccessReason.GrantedBootstrap)
                    : new AdminAccessResolution(false, AdminAccessSource.None, AdminAccessReason.BootstrapOidMismatch);
        }

        private static BootstrapResolutionState GetBootstrapState(IConfiguration configuration)
        {
            string? bootstrapOid = configuration["BOOTSTRAP_ADMIN_OID"];
            string? bootstrapEnabledValue = configuration["BOOTSTRAP_ADMIN_ENABLED"];
            bool bootstrapEnabled = string.Equals(bootstrapEnabledValue, "true", StringComparison.OrdinalIgnoreCase);

            return new BootstrapResolutionState(
                bootstrapEnabled,
                !string.IsNullOrWhiteSpace(bootstrapOid),
                bootstrapOid);
        }

        private readonly record struct BootstrapResolutionState(
            bool Enabled,
            bool OidConfigured,
            string? BootstrapOid);
    }
}
