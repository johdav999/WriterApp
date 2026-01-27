using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WriterApp.Client.Diagnostics
{
    public static class SectionReorderDiagnostics
    {
        private const string FlagKey = "WriterApp:Diagnostics:SectionReorder:Enabled";

        public static bool IsEnabled(IConfiguration configuration)
        {
            if (configuration is null)
            {
                return false;
            }

            return configuration.GetValue<bool?>(FlagKey) ?? false;
        }

        public static void LogDebug(
            ILogger logger,
            IConfiguration configuration,
            string message,
            params object?[] args)
        {
            if (logger is null || !IsEnabled(configuration))
            {
                return;
            }

            logger.LogDebug("[SectionReorder] " + message, args);
        }

        public static void LogWarning(
            ILogger logger,
            IConfiguration configuration,
            string message,
            params object?[] args)
        {
            if (logger is null || !IsEnabled(configuration))
            {
                return;
            }

            logger.LogWarning("[SectionReorder] " + message, args);
        }
    }
}
