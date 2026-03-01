using System;
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
    }
}
