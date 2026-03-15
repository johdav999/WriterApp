using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WriterApp.Application.Security
{
    public static class AdminPolicyDiagnostics
    {
        private static ILogger? _logger;

        public static void Configure(ILoggerFactory loggerFactory)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _logger = loggerFactory.CreateLogger("AdminPolicy");
        }

        public static void LogBootstrapConfiguration(IConfiguration configuration)
        {
            ILogger? logger = _logger;
            if (logger is null)
            {
                return;
            }

            // Bootstrap admin is an emergency fallback path. Keep its runtime state explicit.
            BootstrapAdminConfigurationState state = GetBootstrapConfigurationState(configuration);
            logger.LogInformation(
                "Bootstrap admin configuration. Enabled={Enabled} OidConfigured={OidConfigured} BootstrapOid={BootstrapOid}",
                state.Enabled,
                state.OidConfigured,
                state.MaskedOid);

            if (state.Enabled && !state.OidConfigured)
            {
                logger.LogWarning("Bootstrap admin is enabled but BOOTSTRAP_ADMIN_OID is missing or blank. Bootstrap access will be unavailable until configuration is fixed.");
            }
        }

        public static void LogDecision(
            bool isRoleAdmin,
            bool bootstrapEnabled,
            bool bootstrapOidPresent,
            bool userOidPresent,
            bool decision,
            string? bootstrapOid,
            string? userOid)
        {
            ILogger? logger = _logger;
            if (logger is null)
            {
                return;
            }

            logger.LogInformation(
                "AdminOnly policy: isRoleAdmin={IsRoleAdmin} bootstrapEnabled={BootstrapEnabled} bootstrapOidPresent={BootstrapOidPresent} userOidPresent={UserOidPresent} decision={Decision} bootstrapOid={BootstrapOid} userOid={UserOid}",
                isRoleAdmin,
                bootstrapEnabled,
                bootstrapOidPresent,
                userOidPresent,
                decision,
                MaskOid(bootstrapOid),
                MaskOid(userOid));
        }

        public static void LogBootstrapAccessGranted(object? resource, string? userIdentifier)
        {
            ILogger? logger = _logger;
            if (logger is null)
            {
                return;
            }

            string path = "unknown";
            string traceIdentifier = string.Empty;

            switch (resource)
            {
                case HttpContext httpContext:
                    path = httpContext.Request.Path.HasValue ? httpContext.Request.Path.Value! : "unknown";
                    traceIdentifier = httpContext.TraceIdentifier ?? string.Empty;
                    break;
            }

            logger.LogWarning(
                "Bootstrap admin access granted. Path={Path} User={User} CorrelationId={CorrelationId}",
                path,
                MaskOid(userIdentifier),
                traceIdentifier);
        }

        public static void LogAdminApiAccessDecision(HttpContext context, AdminAccessDiagnosticInfo diagnostic, string? reasonCodeOverride = null)
        {
            ILogger? logger = _logger;
            if (logger is null)
            {
                return;
            }

            string path = context.Request.Path.HasValue ? context.Request.Path.Value! : "unknown";
            string method = context.Request.Method ?? "UNKNOWN";
            string correlationId = context.TraceIdentifier ?? string.Empty;
            string? userIdentifier = ExternalIdentityClaims.ResolveStableUserId(context.User.Claims)
                ?? context.User.FindFirst("oid")?.Value
                ?? context.User.Identity?.Name;

            logger.LogInformation(
                "Admin API access decision. Method={Method} Path={Path} User={User} IsRoleAdmin={IsRoleAdmin} BootstrapMatched={BootstrapMatched} BootstrapEnabled={BootstrapEnabled} BootstrapOidConfigured={BootstrapOidConfigured} UserOidPresent={UserOidPresent} AccessResult={AccessResult} AccessSource={AccessSource} Reason={Reason} CorrelationId={CorrelationId}",
                method,
                path,
                MaskOid(userIdentifier),
                diagnostic.IsRoleAdmin,
                diagnostic.BootstrapMatched,
                diagnostic.BootstrapEnabled,
                diagnostic.BootstrapOidConfigured,
                diagnostic.UserOidPresent,
                diagnostic.Resolution.IsAdminAccess ? "Granted" : "Denied",
                diagnostic.Resolution.Source.ToString(),
                reasonCodeOverride ?? ToReasonCode(diagnostic.Resolution.Reason),
                correlationId);
        }

        public static BootstrapAdminConfigurationState GetBootstrapConfigurationState(IConfiguration configuration)
        {
            if (configuration is null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            string? bootstrapOid = configuration["BOOTSTRAP_ADMIN_OID"];
            bool enabled = string.Equals(
                configuration["BOOTSTRAP_ADMIN_ENABLED"],
                "true",
                StringComparison.OrdinalIgnoreCase);

            return new BootstrapAdminConfigurationState(
                enabled,
                !string.IsNullOrWhiteSpace(bootstrapOid),
                MaskOid(bootstrapOid));
        }

        private static string MaskOid(string? oid)
        {
            if (string.IsNullOrWhiteSpace(oid))
            {
                return string.Empty;
            }

            string trimmed = oid.Trim();
            int length = Math.Min(6, trimmed.Length);
            return $"***{trimmed.Substring(trimmed.Length - length, length)}";
        }

        public static string ToReasonCode(AdminAccessReason reason)
        {
            return reason switch
            {
                AdminAccessReason.GrantedRole => "granted_role",
                AdminAccessReason.GrantedBootstrap => "granted_bootstrap",
                AdminAccessReason.NotAuthenticated => "not_authenticated",
                AdminAccessReason.BootstrapDisabled => "bootstrap_disabled",
                AdminAccessReason.BootstrapOidMissing => "bootstrap_oid_missing",
                AdminAccessReason.BootstrapOidMismatch => "bootstrap_oid_mismatch",
                AdminAccessReason.UserIdMismatch => "user_id_mismatch",
                _ => "not_admin"
            };
        }

        public readonly record struct BootstrapAdminConfigurationState(
            bool Enabled,
            bool OidConfigured,
            string MaskedOid);
    }
}
